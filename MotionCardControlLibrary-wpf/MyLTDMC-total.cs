using csLTDMC;
using MyAsset;
using MyAsset.Wpf.Infrastructure;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MotionCard
{
    public sealed class PlanarMoveSafetyRequest
    {
        public ushort TargetAxis { get; set; }
        public double CurrentZHeight { get; set; }
        public double ContactHeight { get; set; }
        public double SafetyHeight { get; set; }
    }

    public static class MyLTDMC
    {
        private static object _lockObject = new object();
        private static readonly AsyncLocal<bool> _isMotionSequenceContext = new AsyncLocal<bool>();
        private static readonly HashSet<ushort> _activeAxes = new HashSet<ushort>();
        private static readonly HashSet<ushort> _stoppedAxes = new HashSet<ushort>();
        private static Dictionary<int, string> _stopReasonMap;
        private static CancellationTokenSource _motionSequenceCts;
        private static string _currentMotionSequenceName = string.Empty;
        private static bool _isMotionSequenceRunning = false;
        public static Action<string, string> LogAction;
        public static Action<string, string> StopReasonNotificationAction;
        public static Action SpeedChangeAction;//速度切换同步更新UI
        public static Action CloseAction;
        public static Func<PlanarMoveSafetyRequest, Task<bool>> PlanarMoveSafetyConfirmationAction;
        public static Action<bool, string> MotionWaitStateAction;
        public static Func<int> ReadAxisTSingleAction;
        //电机运动相关参数约定
        //X、Y、Z三个电机的分辨率都设置为1微米，按丝杠的导程三个驱动器的每圈细分设置为4000或者5000步，
        //目前X向丝杠的导程为4mm，Y向丝杠的导程为5mm，Z向丝杠的导程为1mm，
        //对应的X，Y，Z的驱动器细分设置为4000,5000,1000.
        //T轴同步带轮的变比为1:7.2，目前设置T轴驱动器的细分数为每圈4000步，则CHUCK每圈为4000*7.2=28800步，
        //即每步旋转角度为360/28800=0.0125°，80步/1°，800步/10°。
        //T轴同步带轮的变比为1:7.2，当设置T轴驱动器的细分数为每圈8000步，则CHUCK每圈为8000*7.2=57600步，
        //即每步旋转角度为360/57600=0.00625°，160步/1°，1600步/10°。

        //T轴大轮216 小轮15  传动比14.4
        public static double Multiple = 2500d; // 5千个脉冲移动1mm
        public static double MultipleZ = 10000d; // 5千个脉冲移动1mm
        //public static double MultipleRad = 0.00625d;
        //public static double MultipleRad = 0.00620689d;
        public static double MultipleRad = 400;// 400个脉冲移动1°

        // ✅ 在类顶部定义常量或可配置属性
        public static double XyNormalSpeed = 1d;
        public static double XySlowSpeed = 0.03d;
        public static double XyFastSpeed = 6d;
        public static double XyZeroSpeed = 8d;

        public static double ZFastSpeed = 4d;
        public static double ZNormalSpeed = 2d;
        public static double ZSlowSpeed = 0.5d;

        public static double TFastSpeed = 0.5d;
        public static double TNormalSpeed = 0.5d;
        public static double TSlowSpeed = 0.1d;
        private const ushort StopModeParam = 1;
        private const int EmergencyStopTimeoutMs = 2000;
        private const int EmergencyStopRetryDelayMs = 50;
        private const double PositionTolerance = 0.001d;



        public static Point2D CCDPoint = new Point2D(100, -100); // CCD点坐标，单位mm，机械轴以此为原点进行运动
        public static Point2D ProbeCenter = new Point2D(80, 50); // 扎针相机坐标，单位mm，机械轴以此为原点进行运动


        private static SpeedType _currentSpeed = SpeedType.Fast;
        /// <summary>
        /// 速度模式
        /// </summary>
        public static SpeedType CurrentSpeed
        {
            get { return _currentSpeed; }
            set { _currentSpeed = value; }
        }

        public static bool IsMotionSequenceRunning
        {
            get
            {
                lock (_lockObject)
                {
                    return _isMotionSequenceRunning;
                }
            }
        }

        public static string CurrentMotionSequenceName
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentMotionSequenceName;
                }
            }
        }
        /// <summary>
        /// 释放控制卡封装层持有的委托引用，并断开当前控制卡连接。
        /// </summary>
        public static void Dispose()
        {
            DisConnect();

            LogAction = null;
            StopReasonNotificationAction = null;
            SpeedChangeAction = null;
            CloseAction = null;
            PlanarMoveSafetyConfirmationAction = null;
            MotionWaitStateAction = null;
            ReadAxisTSingleAction = null;


        }

        /// <summary>
        /// 输出机械轴模块日志。
        /// </summary>
        /// <param name="message">要记录的日志内容。</param>
        private static void OnLogMessage(string message)
        {
            LogAction?.Invoke("机械轴模组", message);
        }

        /// <summary>
        /// 读取轴停止原因。原因码为 0 表示正常停止，不做提示；非 0 时写日志并触发弹窗委托。
        /// </summary>
        private static void ReportStopReasonIfNeeded(ushort axis)
        {
            int stopReason = 0;
            short result = LTDMC.dmc_get_stop_reason(AxisInfo.CardNo, axis, ref stopReason);
            if (result != 0 || stopReason == 0)
                return;

            string reasonText = GetStopReasonText(stopReason);
            string message = $"轴{axis}异常停止，原因码：{stopReason}，原因：{reasonText}";
            OnLogMessage(message);
            StopReasonNotificationAction?.Invoke("机械轴停止原因", message);
            //清除停止原因
            LTDMC.dmc_clear_stop_reason(AxisInfo.CardNo, axis);
        }

        private static string GetStopReasonText(int stopReason)
        {
            Dictionary<int, string> reasonMap = GetStopReasonMap();
            return reasonMap.TryGetValue(stopReason, out string reasonText)
                ? reasonText
                : "未知停止原因";
        }

        private static Dictionary<int, string> GetStopReasonMap()
        {
            lock (_lockObject)
            {
                if (_stopReasonMap != null)
                    return _stopReasonMap;

                _stopReasonMap = LoadStopReasonMap();
                return _stopReasonMap;
            }
        }

        private static Dictionary<int, string> LoadStopReasonMap()
        {
            var reasonMap = new Dictionary<int, string>();
            string reasonFilePath = Path.Combine(AppContext.BaseDirectory, "轴错误停止原因.txt");
            if (!File.Exists(reasonFilePath))
            {
                OnLogMessage($"未找到轴停止原因文件：{reasonFilePath}");
                return reasonMap;
            }

            try
            {
                foreach (string rawLine in File.ReadAllLines(reasonFilePath, Encoding.UTF8))
                {
                    string line = rawLine?.Trim();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    int separatorIndex = line.IndexOf('：');
                    if (separatorIndex < 0)
                        separatorIndex = line.IndexOf(':');
                    if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
                        continue;

                    string codeText = line.Substring(0, separatorIndex).Trim();
                    string reasonText = line.Substring(separatorIndex + 1).Trim();
                    if (int.TryParse(codeText, out int reasonCode) && !string.IsNullOrWhiteSpace(reasonText))
                    {
                        reasonMap[reasonCode] = reasonText;
                    }
                }
            }
            catch (Exception ex)
            {
                OnLogMessage($"读取轴停止原因文件失败：{ex.Message}");
            }

            return reasonMap;
        }

        /// <summary>
        /// 统一设置错误信息并返回软件层错误码，避免同类分支重复写日志。
        /// </summary>
        private static short SetErrorAndReturn(string message, short errorCode = MotionResult.ErrStateInvalid)
        {
            OnLogMessage(message);
            return errorCode;
        }

        /// <summary>
        /// 当前线程如果处于统一运动流程中，则返回该流程的取消令牌；
        /// 普通单步操作则返回 None，不额外引入取消语义。
        /// </summary>
        private static CancellationToken GetCurrentMotionToken()
        {
            lock (_lockObject)
            {
                return _motionSequenceCts != null ? _motionSequenceCts.Token : CancellationToken.None;
            }
        }

        /// <summary>
        /// 所有可等待的运动步骤都走这里判断中断。
        /// 不直接依赖雷赛返回值，是为了区分“指令已发出”和“流程仍然有效”。
        /// </summary>
        private static short CheckMotionInterrupted(CancellationToken token)
        {
            if (!AxisInfo.IsConnect || AxisInfo.IsEmgStop)
                return MotionResult.ErrStateInvalid;
            if (token.CanBeCanceled && token.IsCancellationRequested)
                return MotionResult.ErrCanceled;
            return MotionResult.Success;
        }

        /// <summary>
        /// XY/T 轴运动前检查真实 Z 高度；如果已经处于扎针危险高度，则取消本次 XY/T 运动。
        /// </summary>
        private static async Task<short> EnsureZBeforePlanarMoveAsync(ushort targetAxis, CancellationToken token)
        {
            if (targetAxis == AxisType.AxisZ)
                return MotionResult.Success;

            short interruptResult = CheckMotionInterrupted(token);
            if (interruptResult != 0)
                return interruptResult;

            double contactHeight = ZAxisHeightConfig.GetHeight(ZAxisHeightEnum.Contact);
            double safeHeight = ZAxisHeightConfig.GetHeight(ZAxisHeightEnum.Safety);
            double currentHeight = dmc_get_position(AxisType.AxisZ);
            if (!IsAtOrBeyondContactHeight(currentHeight, contactHeight, safeHeight))
                return MotionResult.Success;

            string message = $"轴{targetAxis}运动前检测到Z轴在扎针危险高度，已取消本次XY/T移动。当前Z={currentHeight:F3}，扎针位={contactHeight:F3}，安全高度={safeHeight:F3}";
            OnLogMessage(message);

            var request = new PlanarMoveSafetyRequest
            {
                TargetAxis = targetAxis,
                CurrentZHeight = currentHeight,
                ContactHeight = contactHeight,
                SafetyHeight = safeHeight
            };

            bool confirmed = await ConfirmPlanarMoveSafetyInterlockAsync(request);
            if (confirmed)
            {
                short zMoveResult;
                MotionWaitStateAction?.Invoke(true, "正在移动Z轴到安全高度...");
                try
                {
                    OnLogMessage("将轴Z移动到安全高度");
                    zMoveResult = await dmc_pmoveCore(AxisType.AxisZ, safeHeight, SpeedType.Fast, PosiMode.AbsoluteMotion, token);
                }
                finally
                {
                    MotionWaitStateAction?.Invoke(false, null);
                }

                if (zMoveResult != MotionResult.Success)
                    return zMoveResult;
            }

            return MotionResult.ErrCanceled;
        }

        private static bool IsAtOrBeyondContactHeight(double currentHeight, double contactHeight, double safetyHeight)
        {
            if (contactHeight >= safetyHeight)
                return currentHeight >= contactHeight - PositionTolerance;

            return currentHeight <= contactHeight + PositionTolerance;
        }

        private static async Task<bool> ConfirmPlanarMoveSafetyInterlockAsync(PlanarMoveSafetyRequest request)
        {
            var handler = PlanarMoveSafetyConfirmationAction;
            if (handler == null)
                return false;

            try
            {
                return await handler(request);
            }
            catch (Exception ex)
            {
                OnLogMessage($"处理XY/T安全联锁失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 运动启动前的统一准备：
        /// 1. 先检查流程是否已取消
        /// 2. 再设置速度参数
        /// 3. 速度设置成功后再做一次中断检查
        /// 这样可以避免“参数已写入，但流程其实已经无效”的边界情况。
        /// </summary>
        private static short PrepareMotionCommand(ushort axis, SpeedType speedType, CancellationToken token, string actionName)
        {
            short interruptResult = CheckMotionInterrupted(token);
            if (interruptResult != 0)
                return interruptResult;

            short res = dmc_set_profile(axis, speedType);
            if (res != 0)
            {
                MarkAxisStopped(axis);
                OnLogMessage($"轴{axis}{actionName}前设置速度失败,错误码{res}");
                return res;
            }

            interruptResult = CheckMotionInterrupted(token);
            if (interruptResult != 0)
            {
                MarkAxisStopped(axis);
                return interruptResult;
            }

            return MotionResult.Success;
        }

        /// <summary>
        /// 真正下发运动命令的统一收口。
        /// 只有命令成功发出并且流程仍有效，才把该轴登记为 Active。
        /// </summary>
        private static short FinalizeMotionCommandStart(ushort axis, Func<short> startCommand, string startMessage, string failMessage, CancellationToken token)
        {
            short interruptResult = CheckMotionInterrupted(token);
            if (interruptResult != 0)
            {
                MarkAxisStopped(axis);
                return interruptResult;
            }

            lock (_lockObject)
            {
                _stoppedAxes.Remove(axis);
            }

            short res = startCommand();
            if (res != 0)
            {
                MarkAxisStopped(axis);
                OnLogMessage($"{failMessage},错误码{res}");
                return res;
            }

            interruptResult = CheckMotionInterrupted(token);
            if (interruptResult != 0)
            {
                MarkAxisStopped(axis);
                return interruptResult;
            }

            MarkAxisStarted(axis);
            OnLogMessage(startMessage);
            return MotionResult.Success;
        }

        /// <summary>
        /// 轴进入运动态后统一登记，后续停止/取消都依赖这个集合找出当前活跃轴。
        /// </summary>
        private static void MarkAxisStarted(ushort axis)
        {
            lock (_lockObject)
            {
                _stoppedAxes.Remove(axis);
                _activeAxes.Add(axis);
                AxisInfo.IsRun = true;
            }
        }

        /// <summary>
        /// 轴退出运动态后统一摘除。
        /// AxisInfo.IsRun 仍保留给旧代码使用，但实际以 _activeAxes 为准。
        /// </summary>
        private static void MarkAxisStopped(ushort axis)
        {
            lock (_lockObject)
            {
                _activeAxes.Remove(axis);
                AxisInfo.IsRun = _activeAxes.Count > 0;
            }
        }

        /// <summary>
        /// 判断指定轴当前是否登记为活动轴。
        /// </summary>
        /// <param name="axis">轴号。</param>
        /// <returns>活动中返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
        private static bool IsAxisActive(ushort axis)
        {
            lock (_lockObject)
            {
                return _activeAxes.Contains(axis);
            }
        }

        /// <summary>
        /// 记录“人为停止该轴”的请求。
        /// 因为部分雷赛接口在 stop 后仍会返回 0，所以需要额外的软件标记辅助判定。
        /// </summary>
        private static void MarkAxisStopRequested(ushort axis)
        {
            lock (_lockObject)
            {
                _stoppedAxes.Add(axis);
            }
        }

        /// <summary>
        /// 消费一次停止请求，确保同一个 stop 只把当前这次运动判成取消，不污染后续动作。
        /// </summary>
        private static bool ConsumeAxisStopRequest(ushort axis)
        {
            lock (_lockObject)
            {
                return _stoppedAxes.Remove(axis);
            }
        }

        /// <summary>
        /// 重置软件层维护的运动状态缓存。
        /// </summary>
        private static void ResetMotionState()
        {
            lock (_lockObject)
            {
                _activeAxes.Clear();
                _stoppedAxes.Clear();
                AxisInfo.IsRun = false;
            }
        }

        private static List<ushort> GetAllAxes()
        {
            var axes = new List<ushort>();
            for (ushort axis = 0; axis < AxisInfo.TotalAxis; axis++)
                axes.Add(axis);
            return axes;
        }

        /// <summary>
        /// 以统一的可取消流程上下文执行一段多步骤运动逻辑。
        /// </summary>
        /// <param name="sequenceName">流程名称，用于日志和状态提示。</param>
        /// <param name="motionAction">实际执行的运动逻辑委托。</param>
        /// <returns>返回流程最终结果码。</returns>
        public static async Task<short> RunMotionSequenceAsync(string sequenceName, Func<CancellationToken, Task<short>> motionAction)
        {
            if (motionAction == null)
                return MotionResult.ErrStateInvalid;

            // 嵌套流程直接复用外层 token，避免“流程里再开流程”把取消语义切断。
            if (_isMotionSequenceContext.Value)
            {
                short nestedResult = await motionAction(GetCurrentMotionToken());
                CancellationToken nestedToken = GetCurrentMotionToken();
                if (nestedResult == MotionResult.Success && nestedToken.CanBeCanceled && nestedToken.IsCancellationRequested)
                    return MotionResult.ErrCanceled;
                return nestedResult;
            }

            CancellationToken token;
            lock (_lockObject)
            {
                if (!AxisInfo.IsConnect)
                    return SetErrorAndReturn("运动前请先连接运动控制卡");
                if (AxisInfo.IsEmgStop)
                    return SetErrorAndReturn("运动前请关闭急停");
                if (_isMotionSequenceRunning)
                    return SetErrorAndReturn($"当前正在执行运动流程：{_currentMotionSequenceName}");
                if (AxisInfo.IsRun)
                    return SetErrorAndReturn("检测到有机械轴正在运动");

                _motionSequenceCts?.Dispose();
                _motionSequenceCts = new CancellationTokenSource();
                _currentMotionSequenceName = string.IsNullOrWhiteSpace(sequenceName) ? "未命名流程" : sequenceName;
                _isMotionSequenceRunning = true;
                token = _motionSequenceCts.Token;
            }

            try
            {
                _isMotionSequenceContext.Value = true;
                short result = await motionAction(token);
                if (result == MotionResult.Success && token.IsCancellationRequested)
                    return MotionResult.ErrCanceled;
                return result;
            }
            catch (OperationCanceledException)
            {
                return MotionResult.ErrCanceled;
            }
            catch (Exception ex)
            {
                OnLogMessage($"运动流程[{_currentMotionSequenceName}]执行异常：{ex.Message}");
                return MotionResult.ErrStateInvalid;
            }
            finally
            {
                _isMotionSequenceContext.Value = false;
                lock (_lockObject)
                {
                    _motionSequenceCts?.Dispose();
                    _motionSequenceCts = null;
                    _currentMotionSequenceName = string.Empty;
                    _isMotionSequenceRunning = false;
                    AxisInfo.IsRun = _activeAxes.Count > 0;
                }
            }
        }

        /// <summary>
        /// 只取消当前运动流程的令牌并记录原因，不直接下发停轴命令。
        /// 需要停轴时由 dmc_stop、StopAllAxes 或 dmc_check_done 明确负责。
        /// </summary>
        private static void CancelMotionSequenceCore(string reason = null)
        {
            string sequenceName;

            lock (_lockObject)
            {
                if (!_isMotionSequenceRunning || _motionSequenceCts == null)
                    return;

                if (!_motionSequenceCts.IsCancellationRequested)
                {
                    _motionSequenceCts.Cancel();
                }

                sequenceName = _currentMotionSequenceName;
            }

            string logMessage = string.IsNullOrWhiteSpace(reason)
                ? $"运动流程[{sequenceName}]收到停止指令"
                : $"运动流程[{sequenceName}]已取消：{reason}";
            OnLogMessage(logMessage);
        }

        /// <summary>
        /// 用户级停止入口：取消当前运动流程，并向所有轴下发停止指令。
        /// 用于界面停止按钮、信号卡停止按钮等“停止一切轴运动”的场景。
        /// </summary>
        public static void StopAllAxes(string reason = null)
        {
            if (!AxisInfo.IsConnect)
            {
                ResetMotionState();
                OnLogMessage("运动控制卡未连接，已重置运动状态");
                return;
            }

            CancelMotionSequenceCore(string.IsNullOrWhiteSpace(reason) ? "停止所有轴运动" : reason);
            foreach (ushort axis in GetAllAxes())
                dmc_stop(axis);
            OnLogMessage("已下发停止所有轴运动指令");
        }

        /// <summary>
        /// 按 Fast 速度执行 X、Y 轴相对联动。
        /// </summary>
        /// <param name="posX">X 轴移动量，单位 mm。</param>
        /// <param name="posY">Y 轴移动量，单位 mm。</param>
        public static Task<short> MoveSync(double posX, double posY)
        {
            return MoveSyncCore(posX, posY, PosiMode.RelativeMotion, nameof(MoveSync));
        }

        /// <summary>
        /// 按 Fast 速度执行 X、Y 轴绝对联动。
        /// </summary>
        /// <param name="posX">X 轴目标位置，单位 mm。</param>
        /// <param name="posY">Y 轴目标位置，单位 mm。</param>
        public static Task<short> AbsMoveSync(double posX, double posY)
        {
            return MoveSyncCore(posX, posY, PosiMode.AbsoluteMotion, nameof(AbsMoveSync));
        }

        private static async Task<short> MoveSyncCore(double posX, double posY, ushort posiMode, string sequenceName)
        {
            return await RunMotionSequenceAsync(sequenceName, async token =>
            {
                short safeRes = await EnsureZBeforePlanarMoveAsync(AxisType.AxisX, token);
                if (safeRes != MotionResult.Success)
                    return safeRes;

                short rlt = await dmc_pmoveCore(AxisType.AxisX, posX, SpeedType.Fast, posiMode, token);
                if (rlt == MotionResult.Success)
                    rlt = await dmc_pmoveCore(AxisType.AxisY, posY, SpeedType.Fast, posiMode, token);
                return rlt;
            });
        }

        /// <summary>
        /// 扎针状态下执行单次 XY 定距移动：先到分离高度，再移动 XY，最后回到接触高度。
        /// </summary>
        public static async Task<short> MoveFixedPlanarWithProbeAsync(AxisDirType axisDirType, double dist, SpeedType speedType)
        {
            ConvertDirType(axisDirType, out ushort axis, out ushort dir);
            if (axis != AxisType.AxisX && axis != AxisType.AxisY)
                return SetErrorAndReturn("扎针状态下仅允许XY定距移动");

            double signedDistance = (dir == 0 ? -1 : 1) * Math.Abs(dist);

            return await RunMotionSequenceAsync(nameof(MoveFixedPlanarWithProbeAsync), async token =>
            {
                short result = await MoveZToConfiguredHeightCoreAsync(ZAxisHeightEnum.Separation, token);
                if (result != MotionResult.Success)
                    return result;

                result = await dmc_pmoveCore(axis, signedDistance, speedType, PosiMode.RelativeMotion, token);
                if (result != MotionResult.Success)
                    return result;

                return await MoveZToConfiguredHeightCoreAsync(ZAxisHeightEnum.Contact, token);
            });
        }

        /// <summary>
        /// 读取当前 X、Y 轴位置。
        /// </summary>
        /// <returns>返回 X、Y 轴当前位置，单位为 mm。</returns>
        public static (double X, double Y) ReadPhysicalPos()
        {
            return (dmc_get_position(AxisType.AxisX), dmc_get_position(AxisType.AxisY));
        }

        /// <summary>
        /// 初始化并连接雷赛运动控制卡。
        /// </summary>
        /// <returns>连接成功返回 <see cref="MotionResult.Success"/>，否则返回错误码。</returns>
        public static short Connect()
        {
            if (AxisInfo.IsConnect)
            {
                DisConnect();
            }
            try
            {
                //初始化轴卡
                short res = LTDMC.dmc_board_init();
                if (res <= 0 || res > 8)
                {
                    OnLogMessage("初始化错误。链接失败");
                    AxisInfo.IsConnect = false;
                    return MotionResult.ErrConnectFailed;
                }

                //获取轴卡硬件ID号和固件版本
                LTDMC.dmc_get_CardInfList(ref AxisInfo.CardNum, AxisInfo.CardTypeList, AxisInfo.CardIdList);

                AxisInfo.CardNo = AxisInfo.CardIdList[1];
                //超时
                LTDMC.dmc_set_timeout(AxisInfo.CardNo, 10000);

                //获取卡的轴数
                //LTDMC.dmc_get_total_axes(AxisInfo.CardNo, ref AxisInfo.TotalAxis);

                //总线型读取轴数
                LTDMC.nmc_get_total_axes(AxisInfo.CardNo, ref AxisInfo.TotalAxis);
                if (AxisInfo.TotalAxis > 4)
                    AxisInfo.TotalAxis = 4;

                for (ushort i = 0; i < AxisInfo.TotalAxis; i++)
                {
                    LTDMC.nmc_set_axis_enable(AxisInfo.CardNo, i);
                    //设置S段运动 使机械轴移动平滑
                    //s_mode 保留参数固定值0 
                    //s_para S段时间 取值范围0~0.5秒 数值越大运动越平滑 
                    LTDMC.dmc_set_s_profile(AxisInfo.CardNo, i, 0, AxisInfo.Dec);

                    //减速停止时间设置  设置了S段运动就不需要了
                    //LTDMC.dmc_set_dec_stop_time(AxisInfo.CardNo, i, AxisInfo.Dec);

                    //设置指定轴的脉冲当量
                    LTDMC.dmc_set_equiv(AxisInfo.CardNo, i, GetMultiple(i));

                    //res = LTDMC.dmc_set_backlash_unit_extern(AxisInfo.CardNo, i, 0.003, 4, 1, 0.5, 0, 10);
                    //清除总线轴错误码
                    LTDMC.nmc_clear_axis_errcode(AxisInfo.CardNo, i);
                }
                ////T轴  旋转角度转换不正确 暂不使用脉冲当量 (26弃用)
                //LTDMC.dmc_set_equiv(AxisInfo.CardNo, AxisType.AxisT, MultipleRad);


                //设置指定轴的反向间隙值  3um
                LTDMC.dmc_set_backlash_unit(AxisInfo.CardNo, AxisType.AxisX, 0.003);
                LTDMC.dmc_set_backlash_unit(AxisInfo.CardNo, AxisType.AxisY, 0.003);

                //设置辅助编码器输入方式
                LTDMC.dmc_set_extra_encoder_mode(AxisInfo.CardNo, 0, 1, 1);
                //设置辅助编码器计数值
                LTDMC.dmc_set_encoder_unit(AxisInfo.CardNo, 0, 0d);



                //res = LTDMC.dmc_set_softlimit(AxisInfo.CardNo, AxisType.AxisX, 1, 1, 0, 0, 0);
                //LTDMC.dmc_set_softlimit_unit(AxisInfo.CardNo, AxisType.AxisY, 1, 1, 0, -10, 10);



                //取消软限位
                //MyLTDMC.dmc_set_softlimit(AxisType.AxisX, 0, 0);
                //MyLTDMC.dmc_set_softlimit(AxisType.AxisY, 0, 0);
                //MyLTDMC.dmc_set_softlimit(AxisType.AxisZ, 0, 0);

                //软限位
                //MyLTDMC.dmc_set_softlimit(AxisType.AxisX, -205, -5);
                //MyLTDMC.dmc_set_softlimit(AxisType.AxisY, -205, -5);
                //MyLTDMC.dmc_set_softlimit(AxisType.AxisZ, 12, 1);

                //LTDMC.dmc_set_home_pin_logic(AxisInfo.CardNo, AxisType.AxisY, 1, 0);
                //设置限位电平
                //LTDMC.dmc_set_el_mode(AxisInfo.CardNo, AxisType.AxisT, 1, 1, 0);

                //反向间隙
                LTDMC.dmc_set_counter_inmode(AxisInfo.CardNo, AxisType.AxisX, 3);
                LTDMC.dmc_set_encoder_dir(AxisInfo.CardNo, AxisType.AxisX, 0);
                LTDMC.dmc_set_encoder(AxisInfo.CardNo, AxisType.AxisX, 0);


                //设置误差带
                LTDMC.dmc_set_factor_error(AxisInfo.CardNo, AxisType.AxisX, 1, 1);
                OnLogMessage("链接成功");

                AxisInfo.IsConnect = true;
                return MotionResult.Success;
            }
            catch (Exception ex)
            {
                OnLogMessage($"链接失败。错误原因 ：{ex.Message}");
                AxisInfo.IsConnect = false;
                return MotionResult.ErrConnectFailed;
            }
        }

        /// <summary>
        /// 断开当前运动控制卡连接，并清理运动状态。
        /// </summary>
        public static void DisConnect()
        {
            if (!AxisInfo.IsConnect)
            {
                ResetMotionState();
                return;
            }

            CancelMotionSequenceCore("控制卡断开连接");
            for (ushort i = 0; i < AxisInfo.TotalAxis; i++)
            {
                //断开使能
                LTDMC.nmc_set_axis_disable(AxisInfo.CardNo, i);
            }

            var res = LTDMC.dmc_board_close();
            if (res == 0)
            {
                OnLogMessage("已断开链接");
                AxisInfo.IsConnect = false;
                ResetMotionState();
            }
            else
            {
                OnLogMessage("断开链接失败");
            }

        }
        #region 监听IO口输入端

        /// <summary>
        /// 监听IO口输入端     
        /// </summary>
        /// <summary>
        /// 后台监听控制卡链路状态，检测掉线后执行急停与断开处理。
        /// </summary>
        public static void ListenIOInPort()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    ushort linkState = 0;
                    while (AxisInfo.IsConnect)
                    {
                        //监听是否上电  0:链接 1：断开
                        LTDMC.dmc_LinkState(AxisInfo.CardNo, ref linkState);
                        //如果断开
                        if (linkState == 1)
                        {
                            await dmc_emg_stop();
                            DisConnect();
                            CloseAction?.Invoke();
                            break; // ✅ 使用 break 退出循环，结束 Task
                        }
                        await Task.Delay(50); // ✅ 替换 Thread.Sleep
                    }
                }
                catch (Exception ex)
                {
                    OnLogMessage("IO监听发生异常：" + ex.Message);
                }
            });
        }


        /// <summary>
        /// 切换速度挡位
        /// </summary>
        /// <summary>
        /// 在快速、常速、慢速三种档位之间循环切换当前速度模式。
        /// </summary>
        public static void SetSpeedLevel()
        {
            if (_currentSpeed == SpeedType.Fast)
            {
                _currentSpeed = SpeedType.Normal;
            }
            else if (_currentSpeed == SpeedType.Normal)
            {
                _currentSpeed = SpeedType.Slow;
            }
            else
            {
                _currentSpeed = SpeedType.Fast;
            }
            SpeedChangeAction?.Invoke();
        }


        #endregion

        /// <summary>
        /// 移动在测试位置
        /// </summary>
        public static async Task<short> InitCenter()
        {
            return await RunMotionSequenceAsync(nameof(InitCenter), async token =>
            {
                short res = await EnsureZBeforePlanarMoveAsync(AxisType.AxisX, token);
                if (res != 0)
                    return res;

                res = await dmc_pmoveCore(AxisType.AxisX, CCDPoint.X, SpeedType.Fast, PosiMode.AbsoluteMotion, token);
                if (res != 0)
                    return res;

                return await dmc_pmoveCore(AxisType.AxisY, CCDPoint.Y, SpeedType.Fast, PosiMode.AbsoluteMotion, token);
            });
        }

        /// <summary>
        /// 判断 XY 轴是否位于测试位。
        /// </summary>
        /// <returns>位于测试位返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
        public static bool IsInitCenter()
        {
            double positionY = dmc_get_position(AxisType.AxisY);
            double positionX = dmc_get_position(AxisType.AxisX);
            return Math.Abs(positionX - (CCDPoint.X)) < 0.1 && Math.Abs(positionY - (CCDPoint.Y)) < 0.1;
        }

        /// <summary>
        /// 判断 XY 轴是否位于装片位。
        /// </summary>
        /// <returns>位于装片位返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
        public static bool IsInitFront()
        {
            double positionY = dmc_get_position(AxisType.AxisY);
            double positionX = dmc_get_position(AxisType.AxisX);
            return Math.Abs(positionX) < 0.1 && Math.Abs(positionY) < 0.1;
        }


        /// <summary>
        /// 移动在装片位置
        /// </summary>
        public static async Task<short> InitFront()
        {
            return await RunMotionSequenceAsync(nameof(InitFront), async token =>
              {
                  short rlt = 0;
                  rlt = await dmc_home_move(AxisType.AxisZ);
                  //if (rlt == 0)
                  //    rlt = await dmc_home_move(AxisType.AxisX);
                  //if (rlt == 0)
                  //    rlt = await dmc_home_move(AxisType.AxisY);
                  //if (rlt == 0)
                  //    rlt = await dmc_home_move(AxisType.AxisT);
                  return rlt;
              });
        }

        //Z轴的各种运动位置及方式
        //Mode=0，运动到分离高度，即测试高度
        //Mode=1，运动到接触高度
        //Mode=2，运动到对准高度
        //Mode=3，运动到接片高度
        //Mode=4，运动到接片加抬升高度
        //Mode=5，空
        //Mode=6，运动到电气零位高度
        //Mode=7，Z轴微动增加一个微步1微米，测试高度变化
        //Mode=8，Z轴微动减少一个微步1微米，测试高度变化
        public static async Task<short> AxisZMove(ZAxisHeightEnum mode)
        {
            if (mode == ZAxisHeightEnum.Contact)
            {
                OnLogMessage("将轴Z移动到接触高度");
            }
            else if ((mode == ZAxisHeightEnum.Separation))
            {
                OnLogMessage("移动轴Z到分离高度");
            }
            else if (mode == ZAxisHeightEnum.Safety)
            {
                OnLogMessage("将轴Z移动到安全高度");
            }
            return await dmc_pmove(AxisType.AxisZ, ZAxisHeightConfig.GetHeight(mode), SpeedType.Fast, PosiMode.AbsoluteMotion);
        }

        private static async Task<short> MoveZToConfiguredHeightCoreAsync(ZAxisHeightEnum mode, CancellationToken token)
        {
            if (mode == ZAxisHeightEnum.Contact)
            {
                OnLogMessage("将轴Z移动到接触高度");
            }
            else if (mode == ZAxisHeightEnum.Separation)
            {
                OnLogMessage("移动轴Z到分离高度");
            }
            else if (mode == ZAxisHeightEnum.Safety)
            {
                OnLogMessage("将轴Z移动到安全高度");
            }

            return await dmc_pmoveCore(AxisType.AxisZ, ZAxisHeightConfig.GetHeight(mode), SpeedType.Fast, PosiMode.AbsoluteMotion, token);
        }


        #region 运动

        /// <summary>
        /// 以指定速度启动某个方向的连续运动，启动前会检查 XY/T 运动的 Z 轴安全高度。
        /// </summary>
        /// <param name="axisDirType">运动方向。</param>
        /// <param name="spdType">速度档位。</param>
        public static async Task<short> dmc_vmove(AxisDirType axisDirType, SpeedType spdType)
        {
            short res = await RunMotionSequenceAsync($"{nameof(dmc_vmove)}-{axisDirType}", async token =>
            {
                ConvertDirType(axisDirType, out ushort axis, out _);
                short safeRes = await EnsureZBeforePlanarMoveAsync(axis, token);
                if (safeRes != 0)
                    return safeRes;

                return dmc_vmoveCore(axisDirType, spdType, token);
            });

            if (res != 0 && res > 0)
            {
                OnLogMessage($"持续移动错误,错误代码 ： {res}");
            }

            return res;
        }

        /// <summary>
        /// 连续运动的统一启动入口。
        /// </summary>
        private static short dmc_vmoveCore(AxisDirType axisDirType, SpeedType spdType, CancellationToken token)
        {
            ConvertDirType(axisDirType, out ushort axis, out ushort dir);
            int DriveDir = dir == 1 ? 1 : -1; //调整方向符号以适配限位检查逻辑
            short limitResult = ValidateAxisLimit(axis, DriveDir);
            if (limitResult != MotionResult.Success)
                return limitResult;

            short res = PrepareMotionCommand(axis, spdType, token, "持续运动");
            if (res != 0)
            {
                return res;
            }

            res = FinalizeMotionCommandStart(
                axis,
                () => LTDMC.dmc_vmove(AxisInfo.CardNo, axis, dir),
                $"轴{axis}开始持续移动",
                $"轴{axis}持续移动失败",
                token);
            if (res != 0 && res > 0)
            {
                OnLogMessage($"持续移动错误,错误代码 ： {res}");
            }

            return res;
        }


       

        private static int GetDirectionSignFromDistance(double distance)
        {
            if (distance > PositionTolerance)
                return 1;
            if (distance < -PositionTolerance)
                return -1;
            return 0;
        }

        private static short ValidateAxisLimit(ushort axis, int directionSign)
        {
            if (directionSign == 0)
                return MotionResult.Success;

            lock (_lockObject)
            {
                if (axis == AxisType.AxisX)
                {
                    if (directionSign > 0 && AxisInfo.IsRightLimit)
                    {
                        OnLogMessage("X轴位于正限位，只允许负向运动");
                        return MotionResult.ErrStateInvalid;
                    }
                    if (directionSign < 0 && AxisInfo.IsLeftLimit)
                    {
                        OnLogMessage("X轴位于负限位，只允许正向运动");
                        return MotionResult.ErrStateInvalid;
                    }
                }
                else if (axis == AxisType.AxisY)
                {
                    if (directionSign > 0 && AxisInfo.IsFrontLimit)
                    {
                        OnLogMessage("Y轴位于正限位，只允许负向运动");
                        return MotionResult.ErrStateInvalid;
                    }
                    if (directionSign < 0 && AxisInfo.IsBackLimit)
                    {
                        OnLogMessage("Y轴位于负限位，只允许正向运动");
                        return MotionResult.ErrStateInvalid;
                    }
                }
                else if (axis == AxisType.AxisZ)
                {
                    if (directionSign > 0 && AxisInfo.IsTopLimit)
                    {
                        OnLogMessage("Z轴位于正限位，只允许负向运动");
                        return MotionResult.ErrStateInvalid;
                    }
                    if (directionSign < 0 && AxisInfo.IsBottomLimit)
                    {
                        OnLogMessage("Z轴位于负限位，只允许正向运动");
                        return MotionResult.ErrStateInvalid;
                    }
                }

                return MotionResult.Success;
            }
        }


        /// <summary>
        /// 将逻辑方向枚举转换为控制卡轴号与方向参数。
        /// </summary>
        /// <param name="axisDirType">逻辑方向。</param>
        /// <param name="axis">输出对应的轴号。</param>
        /// <param name="dir">输出对应的驱动方向参数，0 为反向，1 为正向。</param>
        public static void ConvertDirType(AxisDirType axisDirType, out ushort axis, out ushort dir)
        {
            if (axisDirType is AxisDirType.Down || axisDirType is AxisDirType.Up)
            {
                axis = AxisType.AxisZ;
                dir = axisDirType is AxisDirType.Up ? (ushort)1 : (ushort)0;
            }
            else if (axisDirType is AxisDirType.Front || axisDirType is AxisDirType.Back)
            {
                axis = AxisType.AxisY;
                dir = axisDirType is AxisDirType.Front ? (ushort)0 : (ushort)1;
            }
            else if (axisDirType is AxisDirType.Left || axisDirType is AxisDirType.Right)
            {
                axis = AxisType.AxisX;
                dir = axisDirType is AxisDirType.Right ? (ushort)1 : (ushort)0;
            }
            else
            {
                axis = AxisType.AxisT;
                dir = axisDirType is AxisDirType.Redo ? (ushort)1 : (ushort)0;
            }
        }

        /// <summary>
        /// 移动指定距离 单位mm
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="dist">距离 正数-反向 负数-正向 靠近电机的方向为反向</param>
        /// <param name="posMode">0 -相对运动  1-绝对运动</param>
        /// <returns></returns>
        public static async Task<short> dmc_pmove(ushort axis, double dist, ushort posMode = PosiMode.RelativeMotion)
        {
            return await dmc_pmove(axis, dist, _currentSpeed, posMode);
        }
        /// <summary>
        /// 移动指定距离 单位mm  系统指定配速
        /// </summary>
        /// <param name="axisDirType">方向</param>
        /// <param name="dist">距离mm 正数-反向 负数-正向 靠近电机的方向为反向</param>
        /// <param name="posMode">0 -相对运动  1-绝对运动</param>
        /// <returns></returns>
        public static async Task<short> dmc_pmove(AxisDirType axisDirType, double dist, ushort posMode = PosiMode.RelativeMotion)
        {
            ConvertDirType(axisDirType, out ushort axis, out ushort dir);
            int dirParam = dir == 0 ? -1 : 1;
            return await dmc_pmove(axis, dirParam * Math.Abs(dist), _currentSpeed, posMode);
        }

        /// <summary>
        /// 移动指定距离 单位mm  系统指定配速
        /// </summary>
        /// <param name="axisDirType">方向</param>
        /// <param name="dist">距离mm 正数-反向 负数-正向 靠近电机的方向为反向</param>
        /// <param name="speedType">速度模式</param>
        /// <param name="posMode">0 -相对运动  1-绝对运动</param>
        /// <returns></returns>
        public static async Task<short> dmc_pmove(AxisDirType axisDirType, double dist, SpeedType speedType, ushort posMode = PosiMode.RelativeMotion)
        {
            ConvertDirType(axisDirType, out ushort axis, out ushort dir);
            if (posMode == PosiMode.RelativeMotion)
            {
                int intDir = dir == 0 ? -1 : dir;
                return await dmc_pmove(axis, intDir * Math.Abs(dist), speedType, posMode);
            }
            else
            {
                return await dmc_pmove(axis, dist, speedType, posMode);
            }
        }
        /// <summary>
        /// 移动指定距离 单位mm  系统指定配速
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="dist">距离mm 正数-远离电机运动 负数-朝电机运动</param>
        /// <param name="speedType">速度等级</param>
        /// <param name="posMode">0 -相对运动  1-绝对运动</param>
        /// <returns></returns>
        public static async Task<short> dmc_pmove(ushort axis, double dist, SpeedType speedType, ushort posMode = PosiMode.RelativeMotion)
        {
            return await RunMotionSequenceAsync($"{nameof(dmc_pmove)}-{axis}", async token =>
            {
                short safeRes = await EnsureZBeforePlanarMoveAsync(axis, token);
                if (safeRes != 0)
                    return safeRes;

                return await dmc_pmoveCore(axis, dist, speedType, posMode, token);
            });
        }

        /// <summary>
        /// 定位运动的底层实现。对外统一入口会在这里之前完成安全位切换。
        /// </summary>
        private static async Task<short> dmc_pmoveCore(ushort axis, double dist, SpeedType speedType, ushort posMode, CancellationToken token)
        {
            if (dist == 0 && posMode == PosiMode.RelativeMotion)
                return MotionResult.Success;

            short interruptResult = CheckMotionInterrupted(token);
            if (interruptResult != 0)
                return interruptResult;

            double moveDistance = posMode == PosiMode.AbsoluteMotion
                ? dist - dmc_get_position(axis)
                : dist;
            short limitResult = ValidateAxisLimit(axis, GetDirectionSignFromDistance(moveDistance));
            if (limitResult != MotionResult.Success)
                return limitResult;

            // 先校验启动链路，再等待运动完成。不要把雷赛原始返回 0 直接当成“动作已完成”。
            short res = PrepareMotionCommand(axis, speedType, token, "定位运动");
            if (res != 0)
                return res;

            string mvModeStr = posMode == PosiMode.RelativeMotion ? "相对运动" : "绝对运动";
            string unitStr = axis == AxisType.AxisT ? " °" : "mm";
            string startMessage = $"轴{axis}开始{mvModeStr}--指定位置：{Math.Round(dist, 3)}{unitStr}";
            string failMessage = $"轴{axis}{mvModeStr}启动失败";
            res = FinalizeMotionCommandStart(
                axis,
                () => LTDMC.dmc_pmove_unit(AxisInfo.CardNo, axis, dist, posMode),
                startMessage,
                failMessage,
                token);
            if (res != 0)
                return res;

            // await 内部已经实现异步的检测方法，不需要 Task.Run
            res = await dmc_check_done(axis);
            return res;
        }
        #endregion


        /// <summary>
        /// 回零运动 会等待任务完成
        /// </summary>
        /// <param name="axis"></param>
        /// <returns></returns>
        public static async Task<short> dmc_home_move(ushort axis)
        {
            //17-负限位回零  18-正方向回零  27-原点回零
            ushort homeMode = 17;
            short res = 0;
            if (axis == AxisType.AxisZ)
            {
                homeMode = 17;
                res = LTDMC.nmc_set_home_profile(AxisInfo.CardNo, axis, homeMode, ZNormalSpeed, ZNormalSpeed, AxisInfo.Acc, AxisInfo.Acc, 0);
            }
            else if (axis == AxisType.AxisT)
            {
                homeMode = 27;
                res = LTDMC.nmc_set_home_profile(AxisInfo.CardNo, axis, homeMode, TNormalSpeed, TNormalSpeed, AxisInfo.Acc, AxisInfo.Acc, 0);
            }
            else if (axis == AxisType.AxisX)
            {
                res = LTDMC.nmc_set_home_profile(AxisInfo.CardNo, axis, homeMode, XyZeroSpeed, XyZeroSpeed, AxisInfo.Acc, AxisInfo.Acc, 5);
            }
            else
            {
                homeMode = 18;
                res = LTDMC.nmc_set_home_profile(AxisInfo.CardNo, axis, homeMode, XyZeroSpeed, XyZeroSpeed, AxisInfo.Acc, AxisInfo.Acc, 5);
            }
            if (res != 0)
                return res;

            res = LTDMC.dmc_home_move(AxisInfo.CardNo, axis);
            if (res != 0)
                return res;

            MarkAxisStarted(axis);
            res = await dmc_check_done(axis);
            if (res != 0)
                return res;

            //设置外部编码器当前位置为0
            //LTDMC.dmc_set_encoder_unit(AxisInfo.CardNo, axis, 0);
            if (axis == AxisType.AxisX || axis == AxisType.AxisY)
            {
                //设置编码器为0
                res = LTDMC.dmc_set_extra_encoder(AxisInfo.CardNo, axis, 0);
                if (res != 0)
                    return res;
            }
            //设置指令位置为0
            res = LTDMC.dmc_set_position(AxisInfo.CardNo, axis, 0);
            // ✅ 将真正的结果返回给调用者
            return res;
        }


        /// <summary>
        /// 获取当前位置 单位mm
        /// </summary>
        /// <param name="axis"></param>
        /// <returns></returns>
        public static double dmc_get_position(ushort axis)
        {
            double multiple = GetMultiple(axis);

            double position = 0;

            //：读取指定轴的当前指令位置计数器值
            LTDMC.dmc_get_position_unit(AxisInfo.CardNo, axis, ref position);
            return Math.Round(position, 3);

        }

        /// <summary>
        /// 获取指定轴的位置换算倍率。
        /// </summary>
        /// <param name="axis">轴号。</param>
        /// <returns>线轴返回脉冲与 mm 的倍率，T 轴返回脉冲与角度的倍率。</returns>
        private static double GetMultiple(ushort axis)
        {
            if (axis == AxisType.AxisZ)
            {
                return MultipleZ;
            }
            else if (axis == AxisType.AxisT)
            {
                return MultipleRad;
            }
            return Multiple;

        }



        /// <summary>
        /// 设置梯形运动速度
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="minVel"></param>
        /// <param name="maxVel"></param>
        /// <returns></returns>
        public static short dmc_set_profile(ushort axis, double minVel, double maxVel)
        {
            minVel = Math.Min(minVel, maxVel);
            maxVel = Math.Max(minVel, maxVel);
            double multiple = GetMultiple(axis);
            return LTDMC.dmc_set_profile(AxisInfo.CardNo, axis, minVel * multiple, maxVel * multiple, AxisInfo.Acc, AxisInfo.Dec, maxVel * multiple);
        }

        /// <summary>
        /// 设置指定轴的运动速度 
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="speedType"></param>
        /// <returns></returns>
        public static short dmc_set_profile(ushort axis, SpeedType speedType)
        {
            double minVel, maxVel;
            if (axis == AxisType.AxisT)
            {
                //单位mm/s
                switch (speedType)
                {
                    case SpeedType.Fast:
                        minVel = TFastSpeed;
                        maxVel = TFastSpeed;
                        break;
                    case SpeedType.Slow:
                        minVel = TSlowSpeed;
                        maxVel = TSlowSpeed;
                        break;
                    default:
                        minVel = TNormalSpeed;
                        maxVel = TNormalSpeed;
                        break;
                }
            }
            else if (axis == AxisType.AxisZ)
            {
                //单位mm/s
                switch (speedType)
                {
                    case SpeedType.Fast:
                        minVel = ZFastSpeed;
                        maxVel = ZFastSpeed;
                        break;
                    case SpeedType.Slow:
                        minVel = ZSlowSpeed;
                        maxVel = ZSlowSpeed;
                        break;
                    default:
                        minVel = ZNormalSpeed;
                        maxVel = ZNormalSpeed;
                        break;
                }
            }
            else
            {
                //单位mm/s
                switch (speedType)
                {
                    case SpeedType.Fast:
                        minVel = XyFastSpeed;
                        maxVel = XyFastSpeed;
                        break;
                    case SpeedType.Slow:
                        minVel = XySlowSpeed;
                        maxVel = XySlowSpeed;
                        break;
                    case SpeedType.Zero:
                        minVel = XyZeroSpeed;
                        maxVel = XyZeroSpeed;
                        break;
                    default:
                        minVel = XyNormalSpeed;
                        maxVel = XyNormalSpeed;
                        break;
                }
            }
            minVel = Math.Min(minVel, maxVel);
            maxVel = Math.Max(minVel, maxVel);

            return LTDMC.dmc_set_profile_unit(AxisInfo.CardNo, axis, minVel, maxVel, AxisInfo.Acc, AxisInfo.Dec, maxVel);

        }



        /// <summary>
        /// 获取当前轴速度
        /// </summary>
        /// <param name="axis"></param>
        /// <returns></returns>
        public static double dmc_read_current_speed(ushort axis)
        {
            return Math.Round(LTDMC.dmc_read_current_speed(AxisInfo.CardNo, axis) * GetMultiple(axis), 3);
        }
        /// <summary>
        /// 停止指定轴：只负责登记该轴停止请求、向板卡下发单轴停止指令，并清理该轴软件运行态。
        /// 不取消运动流程；流程取消由 StopAllAxes、dmc_emg_stop 或等待中的 dmc_check_done 处理。
        /// </summary>
        public static void dmc_stop(ushort axis)
        {
            MarkAxisStopRequested(axis);

            try
            {
                if (!AxisInfo.IsConnect)
                    return;

                short done = LTDMC.dmc_check_done(AxisInfo.CardNo, axis);
                if (done != 0)
                    return;

                short result = LTDMC.dmc_stop(AxisInfo.CardNo, axis, StopModeParam);
                if (result != 0)
                    OnLogMessage($"指定轴{axis}停止失败,错误码{result}");
            }
            finally
            {
                MarkAxisStopped(axis);
            }
        }

        /// <summary>
        /// 急停
        /// </summary>
        /// <returns></returns>
        public static async Task<short> dmc_emg_stop()
        {
            if (!AxisInfo.IsConnect)
                return SetErrorAndReturn("急停前请先连接运动控制卡");

            CancelMotionSequenceCore("触发急停");
            short lastResult = MotionResult.ErrStateInvalid;
            DateTime deadline = DateTime.Now.AddMilliseconds(EmergencyStopTimeoutMs);

            while (DateTime.Now < deadline)
            {
                lastResult = LTDMC.dmc_emg_stop(AxisInfo.CardNo);
                if (lastResult == MotionResult.Success)
                {
                    AxisInfo.IsEmgStop = true;
                    ResetMotionState();
                    return lastResult;
                }

                await Task.Delay(EmergencyStopRetryDelayMs);
            }

            return SetErrorAndReturn($"触发急停失败,错误码{lastResult}");
        }

        /// <summary>
        /// 取消急停
        /// </summary>
        /// <returns></returns>
        public static void dmc_emg_exitStop(bool isWarn = false)
        {
            AxisInfo.IsEmgStop = false;
            ResetMotionState();
            if (isWarn)
                OnLogMessage("解除急停");
        }

        /// <summary>
        /// 等待指定轴运动完成，并返回本次运动是正常完成、被取消，还是因状态异常中断。
        /// 它不主动启动运动；运行态由 MarkAxisStarted 登记，完成/取消/异常时由本函数清理。
        /// </summary>
        public static async Task<short> dmc_check_done(ushort axis)
        {
            CancellationToken token = GetCurrentMotionToken();

            while (LTDMC.dmc_check_done(AxisInfo.CardNo, axis) == 0) //0：指定轴正在运行，1：指定轴已停止
            {
                // 即使驱动 stop 后最终返回 0，这里也会优先把本次动作判定成“被取消”。
                if (ConsumeAxisStopRequest(axis))
                {
                    MarkAxisStopped(axis);
                    return MotionResult.ErrCanceled;
                }

                short interruptResult = CheckMotionInterrupted(token);
                if (interruptResult == MotionResult.ErrCanceled)
                {
                    dmc_stop(axis);
                    return interruptResult;
                }
                if (interruptResult != 0)
                {
                    MarkAxisStopped(axis);
                    return interruptResult;
                }

                await Task.Delay(50); // 异步等待，不占用后台线程
            }

            if (ConsumeAxisStopRequest(axis))
            {
                MarkAxisStopped(axis);
                return MotionResult.ErrCanceled;
            }
            //ReportStopReasonIfNeeded(axis);
            MarkAxisStopped(axis);
            return MotionResult.Success;
        }

        public static uint dmc_read_inport(UInt16 portNo)
        {
            if (!AxisInfo.IsConnect)
            {
                OnLogMessage("请先连接运动控制卡");
                return 0;
            }
            return LTDMC.dmc_read_inport(AxisInfo.CardNo, portNo);
        }
        /// <summary>
        /// 读取指定输入端口的状态
        /// </summary>
        /// <param name="portNo"></param>
        /// <returns></returns>
        public static short dmc_read_inbit(UInt16 portNo)
        {
            return LTDMC.dmc_read_inbit(AxisInfo.CardNo, portNo);
        }
        /// <summary>
        /// 软限位
        /// </summary>
        /// <param name="axis">轴</param>
        /// <param name="nLimit">负限位位置</param>
        /// <param name="pLimit">正限位位置</param>
        /// <returns></returns>
        public static short dmc_set_softlimit(ushort axis, double nLimit, double pLimit)
        {
            ///// <param name="enable">使能状态 0-禁止 1-允许</param>
            ///// <param name="source_sel">计数器类型 0-指令位置计数器 1-编码器计数器</param>
            ///// <param name="SL_action">停止方式 0-立即 1-减速</param>
            //23.5° 指逆时针旋转灯灭的极限位置
            ushort enable = 1, source_sel = 0, slAction = 1;
            if (nLimit == 0 && pLimit == 0)
            {
                //自己试验发现：将所有参数都设为0 可以初始化
                enable = 0; slAction = 0;
            }

            double multiple = GetMultiple(axis);
            short res = LTDMC.dmc_set_softlimit(AxisInfo.CardNo, axis, enable, source_sel, slAction,
                 Convert.ToInt32(nLimit * multiple), Convert.ToInt32(pLimit * multiple));
            return res;
        }


    }
}
