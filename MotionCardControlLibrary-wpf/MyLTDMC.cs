using csLTDMC;
using MotionCard.Wpf.Infrastructure;
using MyAsset.Wpf.Infrastructure;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MotionCard
{
    public static class MyLTDMC
    {
        private static object _lockObject = new object();
        private static readonly AsyncLocal<bool> _isMotionSequenceContext = new AsyncLocal<bool>();
        private static readonly HashSet<ushort> _activeAxes = new HashSet<ushort>();
        private static readonly HashSet<ushort> _stoppedAxes = new HashSet<ushort>();
        private static CancellationTokenSource _motionSequenceCts;
        private static string _currentMotionSequenceName = string.Empty;
        private static bool _isMotionSequenceRunning = false;
        public static Action<string, string> LogAction;
        public static Action SpeedChangeAction;//速度切换同步更新UI
        public static Action CloseAction;
        public static Func<int> ReadAxisTSingleAction;
        public static string ErrorInfo = string.Empty;
        //电机运动相关参数约定
        //X、Y、Z三个电机的分辨率都设置为1微米，按丝杠的导程三个驱动器的每圈细分设置为4000或者5000步，
        //目前X向丝杠的导程为4mm，Y向丝杠的导程为5mm，Z向丝杠的导程为1mm，
        //对应的X，Y，Z的驱动器细分设置为4000,5000,1000.
        //T轴同步带轮的变比为1:7.2，目前设置T轴驱动器的细分数为每圈4000步，则CHUCK每圈为4000*7.2=28800步，
        //即每步旋转角度为360/28800=0.0125°，80步/1°，800步/10°。
        //T轴同步带轮的变比为1:7.2，当设置T轴驱动器的细分数为每圈8000步，则CHUCK每圈为8000*7.2=57600步，
        //即每步旋转角度为360/57600=0.00625°，160步/1°，1600步/10°。
        public static double Multiple = 1000d; // 1千个脉冲移动1mm
        //public static double MultipleRad = 0.00625d;
        //public static double MultipleRad = 0.00620689d;
        public static double MultipleRad = 400;// 400个脉冲移动1°

        // ? 在类顶部定义常量或可配置属性
        public static double XyNormalSpeed = 1d;
        public static double XySlowSpeed = 0.02d;
        public static double XyFastSpeed = 10d;

        //public static double XyFastSpeed = 15d;
        //public static double XyNormalSpeed = 3d;
        //public static double XySlowSpeed = 0.02d;
        public static double XyTestSpeed = 20d;

        public static double ZFastSpeed = 4d;
        public static double ZNormalSpeed = 2d;
        public static double ZSlowSpeed = 0.02d;
        public static double ZTestSpeed = 15d;

        public static double TFastSpeed = 12d;
        public static double TNormalSpeed = 3d;
        public static double TSlowSpeed = 0.02d;
        public static double TTestSpeed = 10d;
        public static Point2D CCDPoint = new Point2D(-100, -100); // CCD点坐标，单位mm，机械轴以此为原点进行运动
        public static Point2D ProbeCenter = new Point2D(-80, -50); // CCD点坐标，单位mm，机械轴以此为原点进行运动


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
            SpeedChangeAction = null;
            CloseAction = null;
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
        /// 统一设置错误信息并返回软件层错误码，避免同类分支重复写日志。
        /// </summary>
        private static short SetErrorAndReturn(string message, short errorCode = MotionResult.ErrStateInvalid)
        {
            ErrorInfo = message;
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

        /// <summary>
        /// 仅负责向驱动发送停轴，不负责决定“流程是否继续”。
        /// 流程级跳过由 CancelCurrentMotionSequence 统一处理。
        /// </summary>
        private static void StopAxisCore(ushort axis, bool markStopRequested, bool logMessage)
        {
            if (markStopRequested)
            {
                MarkAxisStopRequested(axis);
            }

            if (!AxisInfo.IsConnect)
            {
                MarkAxisStopped(axis);
                return;
            }

            var data = LTDMC.dmc_check_done(AxisInfo.CardNo, axis);
            if (data == 0)
            {
                var res = LTDMC.dmc_stop(AxisInfo.CardNo, axis, 1);
                if (res == 0 && logMessage)
                {
                    OnLogMessage($"指定轴{axis}停止运动");
                }
            }

            MarkAxisStopped(axis);
        }

        /// <summary>
        /// 多步骤运动统一入口。
        /// 以后只要流程里包含多个 await 的运动步骤，都建议包在这里，
        /// 这样中途 stop/cancel 后，后续步骤会自然短路。
        /// </summary>
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
                ErrorInfo = ex.Message;
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
        /// 流程级取消。
        /// 先取消 token，再停止当前所有活跃轴，这样等待中的步骤会返回 ErrCanceled。
        /// </summary>
        /// <summary>
        /// 取消当前正在执行的统一运动流程，并停止所有已登记的活动轴。
        /// </summary>
        /// <param name="reason">取消原因，会记录到日志中。</param>
        public static void CancelCurrentMotionSequence(string reason = null)
        {
            List<ushort> axesToStop;
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
                axesToStop = new List<ushort>(_activeAxes);
            }

            string logMessage = string.IsNullOrWhiteSpace(reason)
                ? $"运动流程[{sequenceName}]收到停止指令"
                : $"运动流程[{sequenceName}]已取消：{reason}";
            OnLogMessage(logMessage);

            foreach (ushort axis in axesToStop)
            {
                StopAxisCore(axis, true, false);
            }
        }

        /// <summary>
        /// 机械轴根据Die的位置同步移动  相对运动
        /// </summary>
        /// <param name="posX">离原点的距离X 单位mm</param>
        /// <param name="posY">离原点的距离Y 单位mm</param>
        public static async Task<short> MoveSync(double posX, double posY)
        {
            return await RunMotionSequenceAsync(nameof(MoveSync), async token =>
            {
                short rlt = await dmc_pmove(AxisType.AxisX, posX, SpeedType.Normal);
                if (rlt == 0)
                    rlt = await dmc_pmove(AxisType.AxisY, posY, SpeedType.Normal);
                return rlt;
            });
        }
        /// <summary>
        /// 机械轴根据Die的位置同步移动 绝对运动
        /// </summary>
        /// <param name="posX">离原点的距离X 单位mm</param>
        /// <param name="posY">离原点的距离Y 单位mm</param>
        public static async Task<short> AbsMoveSync(double posX, double posY)
        {
            return await RunMotionSequenceAsync(nameof(AbsMoveSync), async token =>
            {
                short rlt = await dmc_pmove(AxisType.AxisX, posX, SpeedType.Fast, PosiMode.AbsoluteMotion);
                if (rlt == 0)
                    rlt = await dmc_pmove(AxisType.AxisY, posY, SpeedType.Fast, PosiMode.AbsoluteMotion);
                return rlt;
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
        /// <returns>连接成功返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
        public static bool Connect()
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
                    ErrorInfo = "初始化错误。链接失败";
                    OnLogMessage(ErrorInfo);
                    return false;
                }

                //获取轴卡硬件ID号和固件版本
                LTDMC.dmc_get_CardInfList(ref AxisInfo.CardNum, AxisInfo.CardTypeList, AxisInfo.CardIdList);

                AxisInfo.CardNo = AxisInfo.CardIdList[1];
                //超时
                LTDMC.dmc_set_timeout(AxisInfo.CardNo, 10000);

                //获取卡的轴数
                LTDMC.dmc_get_total_axes(AxisInfo.CardNo, ref AxisInfo.TotalAxis);

                for (ushort i = 0; i < AxisInfo.TotalAxis; i++)
                {
                    //设置S段运动 使机械轴移动平滑
                    //s_mode 保留参数固定值0 
                    //s_para S段时间 取值范围0~0.5秒 数值越大运动越平滑 
                    LTDMC.dmc_set_s_profile(AxisInfo.CardNo, i, 0, AxisInfo.Dec);

                    //减速停止时间设置  设置了S段运动就不需要了
                    //LTDMC.dmc_set_dec_stop_time(AxisInfo.CardNo, i, AxisInfo.Dec);

                    //设置指定轴的脉冲当量
                    LTDMC.dmc_set_equiv(AxisInfo.CardNo, i, 1000);

                    //res = LTDMC.dmc_set_backlash_unit_extern(AxisInfo.CardNo, i, 0.003, 4, 1, 0.5, 0, 10);

                }
                //T轴  旋转角度转换不正确 暂不使用脉冲当量 
                LTDMC.dmc_set_equiv(AxisInfo.CardNo, AxisType.AxisT, 1);


                //设置指定轴的反向间隙值  3um
                LTDMC.dmc_set_backlash_unit(AxisInfo.CardNo, AxisType.AxisX, 0.003);
                LTDMC.dmc_set_backlash_unit(AxisInfo.CardNo, AxisType.AxisY, 0.003);


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

                LTDMC.dmc_set_counter_inmode(AxisInfo.CardNo, AxisType.AxisX, 3);
                LTDMC.dmc_set_encoder_dir(AxisInfo.CardNo, AxisType.AxisX, 0);
                LTDMC.dmc_set_encoder(AxisInfo.CardNo, AxisType.AxisX, 0);


                //设置误差带
                LTDMC.dmc_set_factor_error(AxisInfo.CardNo, AxisType.AxisX, 1, 1);
                OnLogMessage("链接成功");

                return AxisInfo.IsConnect = true;
            }
            catch (Exception ex)
            {
                ErrorInfo = ex.Message;
                OnLogMessage($"链接失败。错误原因 ：{ErrorInfo}");
                return AxisInfo.IsConnect = false;
            }
        }

        /// <summary>
        /// 断开当前运动控制卡连接，并清理运动状态。
        /// </summary>
        public static void DisConnect()
        {
            if (!AxisInfo.IsConnect)
                return;
            CancelCurrentMotionSequence("控制卡断开连接");
            var res = LTDMC.dmc_board_close();
            if (res == 0)
            {
                OnLogMessage("已断开链接");
                AxisInfo.IsConnect = false;
                ResetMotionState();
            }
            else
            {
                ErrorInfo = "断开链接失败";
                OnLogMessage(ErrorInfo);
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
            Task.Run(async () =>
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
                            break; // ? 使用 break 退出循环，结束 Task
                        }
                        await Task.Delay(50); // ? 替换 Thread.Sleep
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
                short res = 0;
                if (AxisInfo.IsEmgStop)
                {
                    OnLogMessage("请取消急停");
                    return MotionResult.ErrStateInvalid;
                }
                if (ZAxisHeightConfig.CurrentType == ZAxisHeightEnum.Contact)  //如果当前Z在接触高度，则XY台运动前下降到分离高度
                {
                    res = await AxisZMove(ZAxisHeightEnum.Safety);          //Z轴运动到安全高度 （测试高度）
                }
                if (res != 0)
                    return res;
                res = await dmc_pmove(AxisType.AxisX, CCDPoint.X, SpeedType.Fast, PosiMode.AbsoluteMotion);
                if (res != 0)
                    return res;
                return await dmc_pmove(AxisType.AxisY, CCDPoint.Y, SpeedType.Fast, PosiMode.AbsoluteMotion);
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
                short rlt = await dmc_home_move(AxisType.AxisZ);
                if (rlt == 0)
                    rlt = await dmc_home_move(AxisType.AxisX);
                if (rlt == 0)
                    rlt = await dmc_home_move(AxisType.AxisY);
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
            return await dmc_pmove(AxisType.AxisZ, ZAxisHeightConfig.GetHeight(mode), SpeedType.Test, PosiMode.AbsoluteMotion);
        }


        #region 运动

        /// <summary>
        /// 按顺序执行 X、Y 轴定位运动。
        /// </summary>
        /// <param name="x">X 轴目标绝对位置，单位 mm。</param>
        /// <param name="y">Y 轴目标绝对位置，单位 mm。</param>
        /// <param name="speed">运动速度档位。</param>
        /// <param name="posiMode">运动模式。</param>
        /// <returns>返回运动结果码。</returns>
        public static async Task<short> MoveXAndY(double x, double y, SpeedType speed = SpeedType.Normal, ushort posiMode = PosiMode.RelativeMotion)
        {
            return await RunMotionSequenceAsync(nameof(MoveXAndY), async token =>
            {
                var res = await dmc_pmove(AxisType.AxisX, x, speed, posiMode);
                if (res != 0) return res;
                return await dmc_pmove(AxisType.AxisY, y, speed, posiMode);
            });
        }


        /// <summary>
        /// 往指定方向持续运动
        /// </summary>
        /// <param name="axisDirType"></param>
        /// <param name="spdType"></param>
        /// <param name="isCheck"></param>
        /// <returns></returns>
        /// <summary>
        /// 以指定速度启动某个方向的连续运动。
        /// </summary>
        /// <param name="axisDirType">运动方向。</param>
        /// <param name="spdType">速度档位。</param>
        /// <param name="isCheck">是否执行方向相关的限位检查。</param>
        public static void dmc_vmove(AxisDirType axisDirType, SpeedType spdType, bool isCheck)
        {
            StartContinuousMove(axisDirType, spdType, isCheck, GetCurrentMotionToken());
        }

        /// <summary>
        /// 连续运动的统一启动入口。
        /// isCheck=false 只跳过方向限位判断，不跳过连接/急停/占用这些基础状态检查。
        /// </summary>
        private static short StartContinuousMove(AxisDirType axisDirType, SpeedType spdType, bool isCheck, CancellationToken token)
        {
            ConvertDirType(axisDirType, out ushort axis, out ushort dir);
            bool canStart = isCheck ? OnMove(axis, dir) : OnMove();
            if (!canStart)
                return MotionResult.ErrStateInvalid;

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
                ErrorInfo = $"持续移动错误,错误代码 ： {res}";
            }

            return res;
        }


        /// <summary>
        /// 移动前的状态检测
        /// </summary>
        private static bool OnMove()
        {
            // ? 加上锁，防止多线程并发抢占电机
            lock (_lockObject)
            {
                if (!AxisInfo.IsConnect)
                {
                    ErrorInfo = "运动前请先连接运动控制卡";
                    OnLogMessage(ErrorInfo);
                    return false;
                }
                else if (AxisInfo.IsEmgStop)
                {
                    ErrorInfo = "运动前请关闭急停";
                    OnLogMessage(ErrorInfo);
                    return false;
                }
                else if (AxisInfo.IsRun)
                {
                    ErrorInfo = "检测到有机械轴正在运动";
                    OnLogMessage(ErrorInfo);
                    return false;
                }
                else if (_isMotionSequenceRunning && !_isMotionSequenceContext.Value)
                {
                    ErrorInfo = $"当前正在执行运动流程：{_currentMotionSequenceName}";
                    OnLogMessage(ErrorInfo);
                    return false;
                }

                // 如果通过了检查，就立刻把状态设为 true，防止其他线程在缝隙中进入
                AxisInfo.IsRun = true;
                return true;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="dist">移动距离或者移动方向  移动方向为0代表负方向运动</param>
        /// <returns></returns>
        private static bool OnMove(ushort axis, double dist)
        {

            if (!OnMove())
                return false;
            lock (_lockObject)
            {
                bool rlt = true;

                if (axis == AxisType.AxisX)
                {
                    if (dist > 0 && AxisInfo.IsRightLimit)
                    {
                        OnLogMessage("X轴到达正限位");
                        rlt = false;
                    }
                    else if (dist <= 0 && AxisInfo.IsLeftLimit)
                    {
                        OnLogMessage("X轴到达负限位");
                        rlt = false;
                    }
                }
                else if (axis == AxisType.AxisY)
                {
                    if (dist > 0 && AxisInfo.IsFrontLimit)
                    {
                        OnLogMessage("Y轴到达正限位");
                        rlt = false;
                    }
                    else if (dist <= 0 && AxisInfo.IsBackLimit)
                    {
                        OnLogMessage("Y轴到达负限位");
                        rlt = false;
                    }
                }
                else if (axis == AxisType.AxisZ)
                {
                    if (dist > 0 && AxisInfo.IsTopLimit)
                    {
                        OnLogMessage("Z轴到达正限位");
                        rlt = false;
                    }
                    else if (dist <= 0 && AxisInfo.IsBottomLimit)
                    {
                        OnLogMessage("Z轴到达负限位");
                        rlt = false;
                    }
                }
                AxisInfo.IsRun = rlt;
                return rlt;
            }
        }




        /// <summary>
        /// 以当前全局速度档位启动某个方向的连续运动。
        /// </summary>
        /// <param name="axisDirType">运动方向。</param>
        /// <param name="isCheck">是否执行方向相关的限位检查。</param>
        public static void dmc_vmove(AxisDirType axisDirType, bool isCheck = true)
        {
            dmc_vmove(axisDirType, _currentSpeed, isCheck);
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
                dir = axisDirType is AxisDirType.Front ? (ushort)1 : (ushort)0;
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
            if (dist == 0 && posMode == PosiMode.RelativeMotion)
                return MotionResult.Success;
            CancellationToken token = GetCurrentMotionToken();
            short interruptResult = CheckMotionInterrupted(token);
            if (interruptResult != 0)
                return interruptResult;

            if (!OnMove(axis, dist))
                return MotionResult.ErrStateInvalid;

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
                () => axis == AxisType.AxisT
                    ? LTDMC.dmc_pmove(AxisInfo.CardNo, axis, Convert.ToInt32(dist * GetMultiple(axis)), posMode)
                    : LTDMC.dmc_pmove_unit(AxisInfo.CardNo, axis, dist, posMode),
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
            SpeedType speed = SpeedType.Fast;
            short finalResult; // 用于捕获子线程的真实执行结果
            CancellationToken token = GetCurrentMotionToken();
            short interruptResult = CheckMotionInterrupted(token);
            if (interruptResult != 0)
                return interruptResult;

            if (axis != AxisType.AxisT)
            {
                finalResult = await Task.Run(async () =>
                {
                    short currentResult = CheckMotionInterrupted(token);
                    if (currentResult != 0)
                        return currentResult;

                    // XYZ 回零本质上是先持续朝 home 方向运动，停下后再回拉到软限位并重置坐标。
                    if (axis == AxisType.AxisX)
                        currentResult = StartContinuousMove(AxisDirType.Right, speed, false, token);
                    else if (axis == AxisType.AxisY)
                        currentResult = StartContinuousMove(AxisDirType.Front, speed, false, token);
                    else if (axis == AxisType.AxisZ)
                        currentResult = StartContinuousMove(AxisDirType.Down, speed, false, token);
                    else
                        return MotionResult.ErrStateInvalid;

                    if (currentResult != 0)
                        return currentResult;

                    if (IsAxisActive(axis))
                    {
                        short waitResult = await dmc_check_done(axis);
                        if (waitResult != 0)
                            return waitResult;

                        currentResult = CheckMotionInterrupted(token);
                        if (currentResult != 0)
                            return currentResult;

                        // 回调到软限位处并重设零点
                        short res = await MoveToSoftLimit(axis);
                        if (res == 0)
                        {
                            OnLogMessage($"轴{axis}回零完成");
                        }
                        return res; // ? 正确返回内部执行结果
                    }
                    return MotionResult.ErrMotionStartFailed; // 启动运动失败
                });
            }
            else
            {
                // 1、设置运动速度
                short profileRes = PrepareMotionCommand(AxisType.AxisT, SpeedType.Normal, token, "回零");
                if (profileRes != 0)
                    return profileRes;

                finalResult = await Task.Run(async () =>
                {
                    bool isFirst = true;
                    int lightFirstState = 0, lightSecondState = 1;
                    AxisDirType homeAxisDir = AxisDirType.Undo;

                    int? singleData = ReadAxisTSingleAction?.Invoke();
                    if (singleData == 1)
                    {
                        lightFirstState = 1;
                        lightSecondState = 0;
                        homeAxisDir = AxisDirType.Redo;
                    }

                    // ? 增加超时保护机制 (例如：30秒未找到零点则报错退出)
                    DateTime startTime = DateTime.Now;

                    while (AxisInfo.IsConnect && !AxisInfo.IsEmgStop)
                    {
                        short currentResult = CheckMotionInterrupted(token);
                        if (currentResult != 0)
                            return currentResult;

                        if ((DateTime.Now - startTime).TotalSeconds > 30)
                        {
                            StopAxisCore(AxisType.AxisT, false, true);
                            OnLogMessage($"轴{AxisType.AxisT}回零超时，请检查传感器！");
                            return MotionResult.ErrStateInvalid;
                        }

                        singleData = ReadAxisTSingleAction?.Invoke();

                        // 如果一开始就是灯亮的状态 顺时针持续移动
                        if (singleData == lightFirstState && isFirst)
                        {
                            short startResult = StartContinuousMove(homeAxisDir, SpeedType.Normal, true, token);
                            if (startResult != 0)
                                return startResult;

                            if (IsAxisActive(AxisType.AxisT))
                                isFirst = false;
                            else
                                return MotionResult.ErrMotionStartFailed; // 运动指令发送失败
                        }

                        // 如果灯灭 停止运动并设为零点
                        if (singleData == lightSecondState)
                        {
                            StopAxisCore(AxisType.AxisT, false, false);
                            LTDMC.dmc_set_position(AxisInfo.CardNo, AxisType.AxisT, 0);
                            break;
                        }

                        await Task.Delay(10);
                    }

                    // 如果是因为急停或断开连接退出的循环，直接返回错误
                    if (!AxisInfo.IsConnect || AxisInfo.IsEmgStop)
                        return MotionResult.ErrStateInvalid;

                    // 必须要 给机械缓冲时间
                    await Task.Delay(300);

                    short interruptAfterDelay = CheckMotionInterrupted(token);
                    if (interruptAfterDelay != 0)
                        return interruptAfterDelay;

                    // 回调5度
                    short res = await dmc_pmove(AxisDirType.Undo, -5, SpeedType.Fast, PosiMode.AbsoluteMotion);
                    if (res == 0)
                    {
                        OnLogMessage($"轴{AxisType.AxisT}回零完成");
                    }
                    return res;
                });
            }

            // ? 将真正的结果返回给调用者
            return finalResult;
        }
        ///// <summary>
        ///// T轴专用回零
        ///// </summary>
        ///// <param name="data">卡0 idi09  0-低 1-高 </param>
        ///// <returns></returns>
        //public static async Task AixsT_home_move(int data)
        //{
        //    if (!AxisInfo.IsConnect)
        //    {
        //        LogAction?.Invoke(_moduleStr, "请先连接运动控制卡");
        //        return;
        //    }
        //    if (AxisInfo.IsEmgStop)
        //    {
        //        LogAction?.Invoke(_moduleStr, "请关闭急停");
        //        return;
        //    }
        //    //1、设置运动速度
        //    dmc_set_profile(AxisType.AxisT, SpeedType.Normal);

        //}



        /// <summary>
        /// 回零后移动到软限位处并重设零点 仅使用于x y z轴
        /// </summary>
        /// <param name="axis"></param>
        private static async Task<short> MoveToSoftLimit(ushort axis)
        {
            short interruptResult = CheckMotionInterrupted(GetCurrentMotionToken());
            if (interruptResult != 0)
                return interruptResult;

            double dist = -5d;  // X、Y的软限位位置
            if (axis == AxisType.AxisZ)
                dist = 1;
            //回调到软限位的位置
            var res = await dmc_pmove(axis, dist, SpeedType.Fast);
            if (res != 0)
                return res;
            //4、设置轴的指令脉冲计数器绝对位置为0
            return LTDMC.dmc_set_position(AxisInfo.CardNo, axis, 0);
        }



        /// <summary>
        /// 获取当前位置 单位mm
        /// </summary>
        /// <param name="axis"></param>
        /// <returns></returns>
        public static double dmc_get_position(ushort axis)
        {
            double multiple = GetMultiple(axis);
            return Math.Round(LTDMC.dmc_get_position(AxisInfo.CardNo, axis) / multiple, 3);
        }

        /// <summary>
        /// 获取指定轴的位置换算倍率。
        /// </summary>
        /// <param name="axis">轴号。</param>
        /// <returns>线轴返回脉冲与 mm 的倍率，T 轴返回脉冲与角度的倍率。</returns>
        private static double GetMultiple(ushort axis)
        {
            return axis == AxisType.AxisT ? MultipleRad : Multiple;
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
            short res;
            if (axis == AxisType.AxisT)
            {
                //单位mm/s
                switch (speedType)
                {
                    case SpeedType.Test:
                        minVel = TTestSpeed;
                        maxVel = TTestSpeed;
                        break;
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
                    case SpeedType.Test:
                        minVel = ZTestSpeed;
                        maxVel = ZTestSpeed;
                        break;
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
                    case SpeedType.Test:
                        minVel = XyTestSpeed;
                        maxVel = XyTestSpeed;
                        break;
                    case SpeedType.Fast:
                        minVel = XyFastSpeed;
                        maxVel = XyFastSpeed;
                        break;
                    case SpeedType.Slow:
                        minVel = XySlowSpeed;
                        maxVel = XySlowSpeed;
                        break;
                    default:
                        minVel = XyNormalSpeed;
                        maxVel = XyNormalSpeed;
                        break;
                }
            }
            minVel = Math.Min(minVel, maxVel);
            maxVel = Math.Max(minVel, maxVel);


            if (axis == AxisType.AxisT)
            {
                double multiple = GetMultiple(axis);
                res = LTDMC.dmc_set_profile(AxisInfo.CardNo, axis, minVel * multiple, maxVel * multiple, AxisInfo.Acc, AxisInfo.Dec, maxVel);
            }
            else
                res = LTDMC.dmc_set_profile_unit(AxisInfo.CardNo, axis, minVel, maxVel, AxisInfo.Acc, AxisInfo.Dec, maxVel);
            if (res != 0)
                return res;

            return LTDMC.dmc_set_s_profile(AxisInfo.CardNo, axis, 0, 0.005);
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
        /// 指定轴停止
        /// </summary>
        /// <param name="axis"></param>
        /// <returns></returns>
        public static void dmc_stop(ushort axis)
        {
            // 如果当前处于多步骤流程中，stop 的语义不是“只停当前轴”，而是“取消当前流程”。
            if (IsMotionSequenceRunning)
            {
                CancelCurrentMotionSequence($"轴{axis}收到停止指令");
                return;
            }

            StopAxisCore(axis, true, true);
        }

        /// <summary>
        /// 急停
        /// </summary>
        /// <returns></returns>
        public static async Task<short> dmc_emg_stop()
        {
            CancelCurrentMotionSequence("触发急停");
            DateTime startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalSeconds < 2)
            {
                var res = LTDMC.dmc_emg_stop(AxisInfo.CardNo);
                if (res == 0)
                {
                    AxisInfo.IsEmgStop = true;
                    ResetMotionState();
                    return res; // ? 成功后直接返回，原代码这里有逻辑嵌套问题
                }
                await Task.Delay(50); // ? 替换 Thread.Sleep
            }
            return MotionResult.ErrStateInvalid; // 超时返回错误码
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
        /// 等待轴运动完成  AxisInfo.IsRun状态更改
        /// </summary>
        /// <param name="axis"></param>
        public static async Task<short> dmc_check_done(ushort axis)
        {
            lock (_lockObject)
            {
                AxisInfo.IsRun = true;
            }

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
                    StopAxisCore(axis, true, false);
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
