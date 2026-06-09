using Automation.BDaq;
using csLTDMC;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MotionCard.Model
{
    /// <summary>
    /// 信号控制板卡--研华
    /// </summary>
    public class SingleController
    {
        public enum SingleMode
        {
            Default,
            Leveling,
            Calibration
        }


        private CancellationTokenSource _cts;
        private const int BitNum = 8;
        private const int PortNum = 4;
        private bool _isUp;
        private bool _isInitStart = false;
        public bool _isInitFinish = false;

        #region 委托与事件
        public Action MsltAction;
        public Action DbltAction;
        public Func<Task<short>> LoadAction;
        public Func<Task<short>> VaAction;
        public Func<Task<short>> CenterAction;
        public Action ThetaAction; // 修改：原代码为 Func<Task<short>> 但调用时未 await，改为 Action 更符合实际
        public Action AdjAction;   // 修改：同上
        public Action<bool> UpAction; // 修改：原代码 Func<bool, Task<short>> 调用未 await
        public Action StartAction;
        public Action CheckAction;
        public Action AbortAction;
        public Action<AxisDirType, bool> AxisVMoveAction;
        public Action<ushort> AxisStopAction;
        public Func<Task<short>> AxisEmgStopEvent;
        public Action StopWorkerAction;
        public Action EmsAction;
        public Action IndexAction;
        public Func<(double, double)> ReadPositionEvent;
        public Action<(double, double), string> TransmitPositionAction;

        public Action<string, string> LogAction;

        #endregion

        public int WaitTime { get; set; } = 15;

        private InstantDiCtrl _diCtrl = new InstantDiCtrl();
        private InstantDiCtrl _diCtrl1 = new InstantDiCtrl();
        private InstantDoCtrl _doCtrl = new InstantDoCtrl();
        private InstantDoCtrl _doCtrl1 = new InstantDoCtrl();

        public SingleMode CurrentMode { get; private set; } = SingleMode.Default;

        /// <summary>
        /// 初始化信号控制卡封装实例。
        /// </summary>
        public SingleController()
        {
        }

        /// <summary>
        /// 切换是否进入找平模式。
        /// </summary>
        /// <param name="isOpen"><see langword="true"/> 表示启用，<see langword="false"/> 表示关闭。</param>
        public void OpenLevelingMode(bool isOpen)
        {
            CurrentMode = isOpen ? SingleMode.Leveling : SingleMode.Default;
        }

        /// <summary>
        /// 切换是否进入校准模式。
        /// </summary>
        /// <param name="isOpen"><see langword="true"/> 表示启用，<see langword="false"/> 表示关闭。</param>
        public void OpenCalibrationMode(bool isOpen)
        {
            CurrentMode = isOpen ? SingleMode.Calibration : SingleMode.Default;
        }


        /// <summary>
        /// 输出信号控制卡模块日志。
        /// </summary>
        /// <param name="msg">日志内容。</param>
        public void OnLogMessage(string msg)
        {
            LogAction?.Invoke("信号控制卡", msg);
        }

        public bool IsConnect => _diCtrl.Initialized && _diCtrl1.Initialized && _doCtrl.Initialized && _doCtrl1.Initialized;

        /// <summary>
        /// 连接研华输入输出控制卡，并初始化灯光输出状态。
        /// </summary>
        public void Connect()
        {
            try
            {
                if (IsConnect) return;

                _diCtrl.SelectedDevice = new DeviceInformation(3, "PCI-1756,BID#0", AccessMode.ModeRead, 0);
                _diCtrl1.SelectedDevice = new DeviceInformation(2, "PCI-1756,BID#1", AccessMode.ModeRead, 0);
                _doCtrl.SelectedDevice = new DeviceInformation(3, "PCI-1756,BID#0", AccessMode.ModeWrite, 0);
                _doCtrl1.SelectedDevice = new DeviceInformation(2, "PCI-1756,BID#1", AccessMode.ModeWrite, 0);

                // 如果链接成功将灯光全部熄灭（初始化）
                if (IsConnect)
                {
                    CloseAllSingle();
                    CloseAllSingle1();

                    // 进入等待初始化的状态
                    //PrepareToInitialize();
                }
            }
            catch (Exception ex)
            {
                OnLogMessage($"链接异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 断开信号控制卡监听，并关闭所有输出灯。
        /// </summary>
        public void DisConnect()
        {
            _cts?.Cancel();
            if (IsConnect)
            {
                AbortListen();
                CloseAllSingle();
                CloseAllSingle1();
            }
        }

        #region 监听循环逻辑 (全面异步化修复)

        /// <summary>
        /// 启动输入端口与限位信号的后台监听任务。
        /// </summary>
        public void ListenInPort()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token; // 捕获局部变量，避免跨线程空引用风险

            // 监听0号卡 (包含急停信号的检测)
            Task.Run(async () =>
            {
                int[,] currentInPortAry = new int[_diCtrl.Features.PortCount, BitNum];

                while (_diCtrl.Initialized && !token.IsCancellationRequested)
                {
                    try
                    {
                        for (int i = 0; i < _diCtrl.Features.PortCount && i < PortNum; i++)
                        {
                            ErrorCode err = _diCtrl.Read(i, out byte portValue);
                            if (err != ErrorCode.Success)
                            {
                                _cts?.Cancel();
                                OnLogMessage("卡0 IO端口读取异常，终止监听");
                                return;
                            }


                            for (int j = 0; j < BitNum; j++)
                            {
                                int data = (portValue >> j) & 0x1;

                                // 只有状态发生翻转时，才触发事件
                                if (currentInPortAry[i, j] == data)
                                    continue;

                                currentInPortAry[i, j] = data;

                                // 触发业务逻辑
                                InputSingleChanged(i, j, data);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // 记录偶发瞬时异常，但不终止循环
                        OnLogMessage($"卡0轮询发生异常: {ex.Message}");
                    }

                    // 将 token 传入 Delay，外部 Cancel 时可立即打断睡眠，提升停止响应速度
                    await Task.Delay(WaitTime, token);
                }
            }, token);

            ListenAxisLimit();

            // 监听1号卡 (同理优化)
            Task.Run(async () =>
            {
                int[,] currentInPortAry = new int[_diCtrl1.Features.PortCount, BitNum];

                while (_diCtrl1.Initialized && !token.IsCancellationRequested)
                {
                    try
                    {
                        for (int i = 0; i < _diCtrl1.Features.PortCount && i < PortNum; i++)
                        {
                            ErrorCode err = _diCtrl1.Read(i, out byte portData);
                            if (err != ErrorCode.Success)
                            {
                                _cts?.Cancel();
                                OnLogMessage("卡1 IO端口读取异常，终止监听");
                                return;
                            }

                            for (int j = 0; j < BitNum; j++)
                            {
                                int data = (portData >> j) & 0x1;
                                if (currentInPortAry[i, j] == data)
                                    continue;

                                currentInPortAry[i, j] = data;
                                InputSingleChanged1(i, j, data);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        OnLogMessage($"卡1轮询发生异常: {ex.Message}");
                    }
                    await Task.Delay(WaitTime, token);
                }
            }, token);
        }

        /// <summary>
        /// 后台监听限位输入信号，并同步更新轴限位状态。
        /// </summary>
        private void ListenAxisLimit()
        {
            Task.Run(async () =>
            {
                int[,] currentInPortAry = new int[_diCtrl.Features.PortCount, BitNum];
                while (_diCtrl.Initialized && _cts != null && !_cts.IsCancellationRequested)
                {
                    try
                    {
                        for (int i = 0; i < _diCtrl.Features.PortCount && i < PortNum; i++)
                        {
                            ErrorCode err = _diCtrl.Read(i, out byte portData);
                            if (err != ErrorCode.Success)
                            {
                                _cts?.Cancel();
                                OnLogMessage("卡0 IO限位端口读取异常");
                                return;
                            }
                            // 待思考：
                            //  如果此时信号就是处于负限位 ，首先我往正方向运动一定距离但仍未走出负限位，根据移动会消除反方向限位的逻辑，此时会允许我往下运动

                            for (int j = 0; j < BitNum; j++)
                            {
                                int data = (portData >> j) & 0x1;
                                if (currentInPortAry[i, j] == data)
                                    continue;
                                currentInPortAry[i, j] = data;

                                AxisLimitSingleChange(i, j, data);
                            }
                        }
                    }
                    catch { { /* 忽略瞬时读取异常 */} }

                    await Task.Delay(WaitTime);
                }
            });
        }

        #endregion

        #region 业务逻辑映射 (InputSingleChanged 等)
        /// <summary>
        /// 处理 0 号输入卡的按键与状态变化事件。
        /// </summary>
        /// <param name="portIndex">端口号。</param>
        /// <param name="bitIndex">位号。</param>
        /// <param name="data">当前位状态，0 为灭/断开，1 为亮/接通。</param>
        private async void InputSingleChanged(int portIndex, int bitIndex, int data)
        {
            // async void 必须加最外层 try-catch，防止底层异常导致上位机进程直接崩溃
            try
            {
                if (data == 0)
                {
                    if (portIndex == 0)
                    {
                        switch (bitIndex)
                        {
                            case 0:
                                CloseSingle1(0, 3);
                                break;
                            case 2:
                                CloseSingle1(0, 5);
                                break;
                            case 3:
                                CloseSingle1(0, 6);
                                break;
                            case 4:
                                CloseSingle1(0, 7);
                                break;
                            case 5:
                                CloseSingle1(1, 0);
                                break;
                            case 6:
                                CloseSingle1(1, 1);
                                break;
                            case 7:
                                CloseSingle1(0, 0);
                                break;
                        }
                    }
                    else if (portIndex == 1 && bitIndex == 0) CloseSingle1(0, 1);
                    else if (portIndex == 2)
                    {
                        switch (bitIndex)
                        {
                            case 4:
                                CloseSingle1(0, 4);
                                break;
                            case 7:
                                MyLTDMC.dmc_emg_exitStop(true);
                                // 解除急停后，如果未完成初始化，重新进入准备状态 (闪烁 Start)
                                if (!_isInitFinish)
                                    PrepareToInitialize();
                                break;

                        }
                    }
                }
                else
                {
                    if (portIndex == 0)
                    {
                        switch (bitIndex)
                        {
                            case 0:
                                OpenSingle1(0, 3);
                                if (LoadAction != null) await LoadAction();
                                break;
                            case 2:
                                OpenSingle1(0, 5);
                                if (CenterAction != null) await CenterAction();
                                break;
                            case 3:
                                OpenSingle1(0, 6);
                                ThetaAction?.Invoke();
                                break;
                            case 4:
                                OpenSingle1(0, 7);
                                if (CurrentMode == SingleMode.Leveling )
                                    TransmitPositionAction?.Invoke((0, 0), "Leveling"); // 找平 
                                else if(CurrentMode == SingleMode.Calibration)
                                    TransmitPositionAction?.Invoke((0, 0), "Calibration"); // 校准
                                else
                                    AdjAction?.Invoke();
                                break;
                            case 5:
                                OpenSingle1(1, 0);
                                UpAction?.Invoke(_isUp);
                                _isUp = !_isUp;
                                break;
                            case 6:
                                OpenSingle1(1, 1);
                                IndexAction?.Invoke();
                                break;
                            case 7:
                                OpenSingle1(0, 0);
                                if (CurrentMode == SingleMode.Leveling || CurrentMode == SingleMode.Calibration)
                                {
                                    var pos = ReadPositionEvent?.Invoke();
                                    if (pos.HasValue)
                                        TransmitPositionAction?.Invoke(pos.Value, "left"); // left - 代表点位1
                                }
                                else
                                    MsltAction?.Invoke();
                                break;
                        }
                    }
                    else if (portIndex == 1 && bitIndex == 0)
                    {
                        OpenSingle1(0, 1);
                        if (CurrentMode == SingleMode.Leveling || CurrentMode == SingleMode.Calibration)
                        {
                            var pos = ReadPositionEvent?.Invoke();
                            if (pos.HasValue)
                                TransmitPositionAction?.Invoke(pos.Value, "right"); // right - 代表点位2
                        }
                        else
                            DbltAction?.Invoke();
                    }
                    else if (portIndex == 2)
                    {
                        switch (bitIndex)
                        {
                            case 4:
                                if (_isInitFinish)
                                {
                                    StartAction?.Invoke();
                                    OpenSingle1(0, 4);
                                }
                                // 按下正在闪烁的 Start 按钮，触发真实的初始化流程
                                else if (!_isInitFinish && !_isInitStart && !AxisInfo.IsEmgStop)
                                {
                                    _ = StartInitializationAsync();
                                }

                                break;
                            case 5:
                                CheckAction?.Invoke();
                                break;
                            case 6:
                                AbortAction?.Invoke();
                                MyLTDMC.StopAllAxes("收到停止按钮指令");
                                break;
                            case 7:
                                await EmgStop();
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnLogMessage($"IO触发动作异常(Port:{portIndex}, Bit:{bitIndex}): {ex.Message}");
                // 如果是急停等关键动作抛出异常，可以考虑在这里强制触发一次安全停止
            }
        }

        /// <summary>
        /// 执行信号控制卡侧的急停联动逻辑。
        /// </summary>
        /// <returns>异步任务。</returns>
        private async Task EmgStop()
        {
            if (AxisEmgStopEvent != null)
                await AxisEmgStopEvent();
            StopWorkerAction?.Invoke();
        }

        /// <summary>
        /// 根据限位输入变化更新软件限位状态，并在触发时停止对应轴。
        /// </summary>
        /// <param name="portIndex">端口号。</param>
        /// <param name="bitIndex">位号。</param>
        /// <param name="data">当前位状态，0 为离开限位，1 为进入限位。</param>
        private void AxisLimitSingleChange(int portIndex, int bitIndex, int data)
        {
            if (data == 0)
            {
                if (portIndex == 1)
                {
                    if (bitIndex == 1)
                    {
                        OnLogMessage("X轴移出负限位");
                        AxisInfo.IsLeftLimit = false;
                    }
                    else if (bitIndex == 2)
                    {
                        OnLogMessage("Y轴移出负限位");
                        AxisInfo.IsBackLimit = false;
                    }
                }
                else if (portIndex == 2)
                {
                    if (bitIndex == 0)
                    {
                        OnLogMessage("Y轴移出正限位");
                        AxisInfo.IsFrontLimit = false;
                    }
                    else if (bitIndex == 1)
                    {
                        OnLogMessage("X轴移出正限位");
                        AxisInfo.IsRightLimit = false;
                    }
                    if (bitIndex == 2)
                    {
                        OnLogMessage("Z轴移出正限位");
                        AxisInfo.IsBottomLimit = false;
                    }
                    else if (bitIndex == 3)
                    {
                        OnLogMessage("Z轴移出负限位");
                        AxisInfo.IsTopLimit = false;
                    }
                }
            }
            else
            {
                if (portIndex == 1)
                {
                    if (bitIndex == 1)
                    {
                        AxisStopAction?.Invoke(AxisType.AxisX);
                        OnLogMessage("X轴位于负限位");
                        AxisInfo.IsLeftLimit = true;
                        AxisInfo.IsRightLimit = false;
                    }
                    else if (bitIndex == 2)
                    {
                        AxisStopAction?.Invoke(AxisType.AxisY);
                        OnLogMessage("Y轴位于负限位");
                        AxisInfo.IsBackLimit = true;
                        AxisInfo.IsFrontLimit = false;
                    }
                }
                else if (portIndex == 2)
                {
                    if (bitIndex == 0)
                    {
                        AxisStopAction?.Invoke(AxisType.AxisY);
                        OnLogMessage("Y轴位于正限位");
                        AxisInfo.IsFrontLimit = true;
                        AxisInfo.IsBackLimit = false;
                    }
                    else if (bitIndex == 1)
                    {
                        AxisStopAction?.Invoke(AxisType.AxisX);
                        OnLogMessage("X轴位于正限位");
                        AxisInfo.IsRightLimit = true;
                        AxisInfo.IsLeftLimit = false;
                    }
                    if (bitIndex == 2)
                    {
                        AxisStopAction?.Invoke(AxisType.AxisZ);
                        OnLogMessage("Z轴位于正限位");
                        AxisInfo.IsTopLimit = true;
                        AxisInfo.IsBottomLimit = false;
                    }
                    else if (bitIndex == 3)
                    {
                        AxisStopAction?.Invoke(AxisType.AxisZ);
                        OnLogMessage("Z轴位于负限位");
                        AxisInfo.IsBottomLimit = true;
                        AxisInfo.IsTopLimit = false;
                    }
                }
            }


        }

        /// <summary>
        /// 处理 1 号输入卡的按键与点动控制信号变化。
        /// </summary>
        /// <param name="portIndex">端口号。</param>
        /// <param name="bitIndex">位号。</param>
        /// <param name="data">当前位状态，0 为释放，1 为按下。</param>
        public void InputSingleChanged1(int portIndex, int bitIndex, int data)
        {
            if (data == 0)
            {
                if (portIndex == 0)
                {
                    switch (bitIndex)
                    {
                        case 1: CloseSingle1(0, 4); break;
                        case 2:
                            AxisStopAction?.Invoke(AxisType.AxisX);
                            break;
                        case 3:
                            AxisStopAction?.Invoke(AxisType.AxisX);
                            break;
                        case 7: AxisStopAction?.Invoke(AxisType.AxisY); break;
                    }
                }
                else if (portIndex == 1 && bitIndex == 0) AxisStopAction?.Invoke(AxisType.AxisY);
            }
            else
            {
                if (portIndex == 0)
                {
                    switch (bitIndex)
                    {
                        case 1:
                            OpenSingle1(0, 4);
                            if (VaAction != null) VaAction();
                            break;
                        case 2:
                            if (AxisInfo.IsRightLimit) return;
                            AxisVMoveAction?.Invoke(AxisDirType.Right, true);
                            AxisInfo.IsLeftLimit = false;
                            break;
                        case 3:
                            if (AxisInfo.IsLeftLimit) return;
                            AxisVMoveAction?.Invoke(AxisDirType.Left, true);
                            AxisInfo.IsRightLimit = false;
                            break;
                        case 4: MyLTDMC.SetSpeedLevel(); break;
                        case 7:
                            if (AxisInfo.IsFrontLimit) return;
                            AxisVMoveAction?.Invoke(AxisDirType.Front, true);
                            AxisInfo.IsBackLimit = false;
                            break;
                    }
                }
                else if (portIndex == 1 && bitIndex == 0)
                {
                    if (AxisInfo.IsBackLimit) return;
                    AxisVMoveAction?.Invoke(AxisDirType.Back, true);
                    AxisInfo.IsFrontLimit = false;
                }
            }
        }
        #endregion





        #region 底层硬件读写封装

        /// <summary>
        /// 读取 T 轴原点相关输入信号。
        /// </summary>
        /// <returns>输入位状态。</returns>
        public int ReadAxisTSingle()
        {
            _diCtrl.ReadBit(1, 1, out byte portData);
            return portData;
        }

        /// <summary>
        /// 读取 0 号输入卡指定端口的当前值。
        /// </summary>
        /// <param name="portIndex">端口号。</param>
        /// <returns>端口的字节值。</returns>
        public byte ReadInputSingle(int portIndex)
        {
            _diCtrl.Read(portIndex, out byte portData);
            return portData;
        }

        /// <summary>
        /// 读取 1 号输入卡指定端口的当前值。
        /// </summary>
        /// <param name="portIndex">端口号。</param>
        /// <returns>端口的字节值。</returns>
        public byte ReadInputSingle1(int portIndex)
        {
            _diCtrl1.Read(portIndex, out byte portData);
            return portData;
        }
        /// <summary>
        /// 读取卡0指定输出端口的状态
        /// </summary>
        public byte ReadOutputSingle(int portIndex)
        {
            _doCtrl.Read(portIndex, out byte portData);
            return portData;
        }

        /// <summary>
        /// 读取卡1指定输出端口的状态
        /// </summary>
        public byte ReadOutputSingle1(int portIndex)
        {
            _doCtrl1.Read(portIndex, out byte portData);
            return portData;
        }

        /// <summary>
        /// 打开 0 号输出卡指定端口位。
        /// </summary>
        /// <param name="portIndex">端口号。</param>
        /// <param name="bitIndex">位号。</param>
        public void OpenSingle(int portIndex, int bitIndex)
        {
            if (_doCtrl.Read(portIndex, out byte portData) != ErrorCode.Success) return;
            WriteSingle(portIndex, (byte)(portData | (1 << bitIndex)));
        }

        /// <summary>
        /// 打开 1 号输出卡指定端口位。
        /// </summary>
        /// <param name="portIndex">端口号。</param>
        /// <param name="bitIndex">位号。</param>
        public void OpenSingle1(int portIndex, int bitIndex)
        {
            if (_doCtrl1.Read(portIndex, out byte portData) != ErrorCode.Success) return;
            WriteSingle1(portIndex, (byte)(portData | (1 << bitIndex)));
        }

        /// <summary>
        /// 关闭 0 号输出卡指定端口位。
        /// </summary>
        /// <param name="portIndex">端口号。</param>
        /// <param name="bitIndex">位号。</param>
        public void CloseSingle(int portIndex, int bitIndex)
        {
            if (_doCtrl.Read(portIndex, out byte portData) != ErrorCode.Success) return;
            WriteSingle(portIndex, (byte)(portData & ~(1 << bitIndex)));
        }

        /// <summary>
        /// 关闭 1 号输出卡指定端口位。
        /// </summary>
        /// <param name="portIndex">端口号。</param>
        /// <param name="bitIndex">位号。</param>
        public void CloseSingle1(int portIndex, int bitIndex)
        {
            if (_doCtrl1.Read(portIndex, out byte portData) != ErrorCode.Success) return;
            WriteSingle1(portIndex, (byte)(portData & ~(1 << bitIndex)));
        }

        /// <summary>
        /// 向 0 号输出卡指定端口写入整字节数据。
        /// </summary>
        /// <param name="portIndex">端口号。</param>
        /// <param name="data">要写入的字节值。</param>
        public void WriteSingle(int portIndex, byte data) => _doCtrl.Write(portIndex, data);

        /// <summary>
        /// 向 1 号输出卡指定端口写入整字节数据。
        /// </summary>
        /// <param name="portIndex">端口号。</param>
        /// <param name="data">要写入的字节值。</param>
        public void WriteSingle1(int portIndex, byte data) => _doCtrl1.Write(portIndex, data);
        /// <summary>
        /// 按指定的位(Bit)写入数据 
        /// </summary>
        public void WriteSingle(int portIndex, int bitIndex, byte data)
            => _doCtrl.WriteBit(portIndex, bitIndex, data);

        /// <summary>
        /// 按指定位向 1 号输出卡写入数据。
        /// </summary>
        /// <param name="portIndex">端口号。</param>
        /// <param name="bitIndex">位号。</param>
        /// <param name="data">要写入的位值。</param>
        public void WriteSingle1(int portIndex, int bitIndex, byte data)
            => _doCtrl1.WriteBit(portIndex, bitIndex, data);

        /// <summary>
        /// 打开 0 号输出卡所有端口位。
        /// </summary>
        public void OpenAllSingle()
        {
            for (int i = 0; i < PortNum; i++) WriteSingle(i, 255);
        }

        /// <summary>
        /// 关闭 0 号输出卡所有端口位。
        /// </summary>
        public void CloseAllSingle()
        {
            for (int i = 0; i < PortNum; i++) WriteSingle(i, 0);
        }

        /// <summary>
        /// 打开 1 号输出卡所有端口位。
        /// </summary>
        public void OpenAllSingle1()
        {
            for (int i = 0; i < PortNum; i++) WriteSingle1(i, 255);
        }

        /// <summary>
        /// 关闭 1 号输出卡所有端口位。
        /// </summary>
        public void CloseAllSingle1()
        {
            for (int i = 0; i < PortNum; i++) WriteSingle1(i, 0);
        }

        /// <summary>
        /// 停止当前所有输入监听任务。
        /// </summary>
        public void AbortListen() => _cts?.Cancel();

        #endregion

        #region 初始化与状态灯控制

        private CancellationTokenSource _initFlashingCts; // 专门用于控制初始化过程中的灯光闪烁

        /// <summary>
        /// 状态1：准备初始化 (让Start按钮闪烁，等待用户按下)
        /// </summary>
        public void PrepareToInitialize()
        {
            if (_isInitFinish) return;

            _isInitStart = false;

            // 每次准备前，先取消旧的灯光任务
            _initFlashingCts?.Cancel();
            _initFlashingCts = new CancellationTokenSource();
            var token = _initFlashingCts.Token;

            // 开启 Start 按钮闪烁任务
            Task.Run(async () =>
            {
                try
                {
                    // 只有在未开始且没被取消时才循环
                    while (!_isInitStart && !token.IsCancellationRequested)
                    {
                        // 如果处于急停状态，灯灭且不闪烁；如果正常，则闪烁
                        if (!AxisInfo.IsEmgStop)
                        {
                            OpenSingle(0, 4); // Start灯亮 (ido04)
                            await Task.Delay(1000, token);
                            CloseSingle(0, 4); // Start灯灭
                            await Task.Delay(1000, token);
                        }
                        else
                        {
                            CloseSingle(0, 4);
                            await Task.Delay(1000, token);
                        }
                    }
                }
                catch (TaskCanceledException) { /* 任务被正常取消，忽略报错 */ }
            }, token);
        }

        /// <summary>
        /// 状态2：执行真正的初始化流程
        /// </summary>
        /// <summary>
        /// 执行完整的初始化流程，并驱动状态灯切换到初始化中或完成状态。
        /// </summary>
        /// <returns>异步任务。</returns>
        public async Task StartInitializationAsync()
        {
            // 如果已经初始化完成、或者正在急停中、或者已经点过开始了，则忽略
            if (_isInitFinish || AxisInfo.IsEmgStop || _isInitStart) return;

            _isInitStart = true; // 标记开始，状态1的 Start 闪烁会自动停止

            _initFlashingCts?.Cancel();
            _initFlashingCts = new CancellationTokenSource();
            var token = _initFlashingCts.Token;

            // 开启群灯闪烁任务
            _ = Task.Run(async () =>
             {
                 try
                 {
                     while (!_isInitFinish && _isInitStart && !token.IsCancellationRequested)
                     {
                         OpenAllSingle1();
                         OpenAllSingle();
                         await Task.Delay(940, token);
                         CloseAllSingle1();
                         CloseAllSingle();
                         await Task.Delay(940, token);
                     }
                 }
                 catch (TaskCanceledException) { }
             }, token);

            try
            {
                // 执行硬件回零/移动
                short res = -1;
                if (LoadAction != null)
                {
                    res = await LoadAction();
                }
                // 判断底层运动是否顺利完成，且过程中没有触发急停
                if (res == 0 && !AxisInfo.IsEmgStop)
                {
                    await Task.Delay(500);

                    if (MyLTDMC.IsInitFront())
                    {
                        _isInitFinish = true;
                        _initFlashingCts?.Cancel(); // 停止群灯闪烁

                        await Task.Delay(1000);
                        SetSpeedLight(); // 亮起常驻速度灯
                        OnLogMessage("初始化完成");
                    }
                }
                else
                {
                    if (res == MotionResult.ErrCanceled)
                        throw new OperationCanceledException("初始化流程已取消");

                    // 返回值非 0 代表运动被阻断（撞限位、或者急停）
                    throw new Exception("运动指令返回异常或急停被触发");
                }
            }
            catch (OperationCanceledException ex)
            {
                OnLogMessage($"初始化被中断: {ex.Message}");

                _isInitStart = false;
                _isInitFinish = false;
                _initFlashingCts?.Cancel();
                CloseAllSingle();
                CloseAllSingle1();

                if (!AxisInfo.IsEmgStop)
                {
                    PrepareToInitialize();
                }
            }
            catch (Exception ex)
            {
                // 初始化失败/被中断的处理逻辑
                OnLogMessage($"初始化被中断: {ex.Message}");

                _isInitStart = false;
                _isInitFinish = false;
                _initFlashingCts?.Cancel(); // 立刻停止闪烁
                CloseAllSingle();
                CloseAllSingle1();

                // 如果当前是因为其他原因（如寻零失败）失败，而不是急停，直接重置为状态1
                if (!AxisInfo.IsEmgStop)
                {
                    PrepareToInitialize();
                }
            }
        }
        /// <summary>
        /// 根据当前速度档位刷新速度指示灯状态。
        /// </summary>
        public void SetSpeedLight()
        {
            if (MyLTDMC.CurrentSpeed == SpeedType.Fast)
            {
                OpenSingle1(0, 2);
                OpenSingle1(1, 2);
                OpenSingle1(1, 3);
            }
            else if (MyLTDMC.CurrentSpeed == SpeedType.Normal)
            {
                OpenSingle1(0, 2);
                OpenSingle1(1, 2);
                CloseSingle1(1, 3);
            }
            else if (MyLTDMC.CurrentSpeed == SpeedType.Slow)
            {
                OpenSingle1(0, 2);
                CloseSingle1(1, 2);
                CloseSingle1(1, 3);
            }
        }

        #endregion
    }
}
