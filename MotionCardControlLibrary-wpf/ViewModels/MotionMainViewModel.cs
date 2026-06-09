using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotionCard.Model;
using MotionCard.Wpf.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using MyAsset.Wpf;

namespace MotionCard.Wpf.ViewModels
{
    public partial class MotionMainViewModel : ObservableObject
    {
        private (double X, double Y)? _levelPoint1;
        private (double X, double Y)? _levelPoint2;

        [ObservableProperty]
        private string distance = "1";

        [ObservableProperty]
        private string height = "1";

        [ObservableProperty]
        private string angle = "1";

        [ObservableProperty]
        private SpeedType selectedSpeed = SpeedType.Fast;

        [ObservableProperty]
        private SpeedType continuousSpeed = SpeedType.Fast;

        [ObservableProperty]
        private SpeedType fixedDistanceSpeed = SpeedType.Fast;

        [ObservableProperty]
        private string statusText = "未连接";

        [ObservableProperty]
        private string logText;

        [ObservableProperty]
        private bool isInitializing;

        [ObservableProperty]
        private string initializingMessage;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProbeModeButtonText))]
        private bool isProbeMode;

        public string ProbeModeButtonText => IsProbeMode ? "关闭扎针状态" : "开启扎针状态";

        public ObservableCollection<SpeedType> SpeedModes { get; } = new ObservableCollection<SpeedType>
        {
            SpeedType.Fast,
            SpeedType.Normal,
            SpeedType.Slow,
        };

        public Func<short> ConnectAction;
        public Action DisconnectAction;
        public Action ManualPlanarMoveStartedAction;

        partial void OnSelectedSpeedChanged(SpeedType value)
        {
            MyLTDMC.CurrentSpeed = value;
            WriteLog("运动控制", $"速度模式切换为 {MotionCard.Wpf.Infrastructure.EnumHelper.GetEnumDescription(value)}");
        }

        public MotionMainViewModel()
        {
            MyLTDMC.SpeedChangeAction += RefreshSpeedMode;
        }

        [RelayCommand]
        private void Connect()
        {
            bool isServiceConnected = ConnectAction != null;
            short connectResult = ConnectAction?.Invoke() ?? MyLTDMC.Connect();
            UpdateConnectionStatus(connectResult == MotionResult.Success
                ? "运动控制卡已连接"
                : GetConnectFailedMessage(connectResult),
                !isServiceConnected);
        }

        [RelayCommand]
        private void Disconnect()
        {
            if (DisconnectAction != null)
            {
                DisconnectAction.Invoke();
                return;
            }

            MyLTDMC.DisConnect();
            UpdateConnectionStatus("运动控制卡已断开", true);
        }

        [RelayCommand]
        private async Task ContactAsync()
        {
            await MyLTDMC.AxisZMove(ZAxisHeightEnum.Contact);
        }

        [RelayCommand]
        private async Task SeparationAsync()
        {
            await MyLTDMC.AxisZMove(ZAxisHeightEnum.Separation);
        }

        [RelayCommand]
        private async Task InitCenterAsync()
        {
            OnManualPlanarMoveStarted();
            await RunInitializationAsync("中心初始化", MyLTDMC.InitCenter);
        }

        [RelayCommand]
        private async Task InitFrontAsync()
        {
            OnManualPlanarMoveStarted();
            await RunInitializationAsync("前侧初始化", MyLTDMC.InitFront);
        }

        [RelayCommand]
        private void StopAll()
        {
            MyLTDMC.StopAllAxes("用户点击停止按钮");
        }

        [RelayCommand]
        private async Task ToggleProbeModeAsync()
        {
            if (IsProbeMode)
            {
                IsProbeMode = false;
                short result = await MyLTDMC.AxisZMove(ZAxisHeightEnum.Separation);
                if (result == MotionResult.Success)
                {
                    WriteLog("运动控制", "已关闭扎针状态，Z轴已移动到分离高度");
                }
                else
                {
                    WriteLog("运动控制", $"关闭扎针状态失败，Z轴移动到分离高度失败，错误码{result}");
                }

                return;
            }

            string message =
                "即将开启扎针状态。\r\n\r\n" +
                "1. 扎针状态下禁止所有连续运动。\r\n" +
                "2. 定距XY移动时，系统会先将Z轴移动到分离高度，再移动XY，最后回到接触高度。\r\n" +
                "3. 请确认探针、晶圆和平台状态安全。";

            if (MyMessageBox.ShowQuery(message, "扎针状态提示") != MyDialogResult.OK)
                return;

            IsProbeMode = true;
            WriteLog("运动控制", "已开启扎针状态");
        }

        [RelayCommand]
        private async Task MoveFrontAsync()
        {
            if (TryGetDistance(out double value))
            {
                OnManualPlanarMoveStarted();
                await MoveFixedPlanarAsync(AxisDirType.Back, value);
            }
        }

        [RelayCommand]
        private async Task MoveBackAsync()
        {
            if (TryGetDistance(out double value))
            {
                OnManualPlanarMoveStarted();
                await MoveFixedPlanarAsync(AxisDirType.Front, value);
            }
        }

        [RelayCommand]
        private async Task MoveLeftAsync()
        {
            if (TryGetDistance(out double value))
            {
                OnManualPlanarMoveStarted();
                await MoveFixedPlanarAsync(AxisDirType.Left, value);
            }
        }

        [RelayCommand]
        private async Task MoveRightAsync()
        {
            if (TryGetDistance(out double value))
            {
                OnManualPlanarMoveStarted();
                await MoveFixedPlanarAsync(AxisDirType.Right, value);
            }
        }

        [RelayCommand]
        private async Task MoveUpAsync()
        {
            if (TryGetHeight(out double value))
                await MyLTDMC.dmc_pmove(AxisDirType.Up, value, FixedDistanceSpeed);
        }

        [RelayCommand]
        private async Task MoveDownAsync()
        {
            if (TryGetHeight(out double value))
                await MyLTDMC.dmc_pmove(AxisDirType.Down, value, FixedDistanceSpeed);
        }

        [RelayCommand]
        private async Task RotateRedoAsync()
        {
            if (TryGetAngle(out double value))
                await MyLTDMC.dmc_pmove(AxisDirType.Redo, value % 360, FixedDistanceSpeed, PosiMode.RelativeMotion);
        }

        [RelayCommand]
        private async Task RotateUndoAsync()
        {
            if (TryGetAngle(out double value))
                await MyLTDMC.dmc_pmove(AxisDirType.Undo, value % 360, FixedDistanceSpeed, PosiMode.RelativeMotion);
        }

        [RelayCommand]
        private async Task MoveFixedUpAsync()
        {
            if (TryGetDistance(out double value))
                await MyLTDMC.dmc_pmove(AxisDirType.Up, value, FixedDistanceSpeed);
        }

        [RelayCommand]
        private async Task MoveFixedDownAsync()
        {
            if (TryGetDistance(out double value))
                await MyLTDMC.dmc_pmove(AxisDirType.Down, value, FixedDistanceSpeed);
        }

        [RelayCommand]
        private async Task RotateFixedRedoAsync()
        {
            if (TryGetDistance(out double value))
                await MyLTDMC.dmc_pmove(AxisDirType.Redo, value % 360, FixedDistanceSpeed, PosiMode.RelativeMotion);
        }

        [RelayCommand]
        private async Task RotateFixedUndoAsync()
        {
            if (TryGetDistance(out double value))
                await MyLTDMC.dmc_pmove(AxisDirType.Undo, value % 360, FixedDistanceSpeed, PosiMode.RelativeMotion);
        }

        [RelayCommand]
        private void CaptureLevelPoint1()
        {
            _levelPoint1 = MyLTDMC.ReadPhysicalPos();
            WriteLog("运动控制", $"已记录点1: X={_levelPoint1.Value.X:F3}, Y={_levelPoint1.Value.Y:F3}");
        }

        [RelayCommand]
        private void CaptureLevelPoint2()
        {
            _levelPoint2 = MyLTDMC.ReadPhysicalPos();
            WriteLog("运动控制", $"已记录点2: X={_levelPoint2.Value.X:F3}, Y={_levelPoint2.Value.Y:F3}");
        }

        [RelayCommand]
        private async Task LevelingAsync()
        {
            if (_levelPoint1 == null || _levelPoint2 == null)
            {
                WriteLog("运动控制", "请先设置两个找平点");
                return;
            }

            double angleValue = Math.Round(Math.Atan2(
                _levelPoint1.Value.Y - _levelPoint2.Value.Y,
                _levelPoint1.Value.X - _levelPoint2.Value.X) * (180 / Math.PI), 3);

            angleValue %= 90;
            if (angleValue > 45 && angleValue < 90)
                angleValue -= 90;
            else if (angleValue < -45 && angleValue > -90)
                angleValue += 90;

            if (_levelPoint1.Value.Y > _levelPoint2.Value.Y)
                angleValue = _levelPoint1.Value.X < _levelPoint2.Value.X ? Math.Abs(angleValue) : -Math.Abs(angleValue);
            else
                angleValue = _levelPoint1.Value.X < _levelPoint2.Value.X ? -Math.Abs(angleValue) : Math.Abs(angleValue);

            if (Math.Abs(angleValue) > 15)
            {
                WriteLog("运动控制", "旋转角度较大，请先手动调整");
                return;
            }

            await MyLTDMC.dmc_pmove(AxisType.AxisT, angleValue, SpeedType.Normal);
            _levelPoint1 = null;
            _levelPoint2 = null;
        }

        private void RefreshSpeedMode()
        {
            SelectedSpeed = MyLTDMC.CurrentSpeed;
        }


        private bool TryGetDistance(out double value)
        {
            return TryGetPositiveNumber(Distance, "运动距离", out value);
        }

        public void NotifyManualPlanarMoveStarted()
        {
            OnManualPlanarMoveStarted();
        }

        public void NotifyContinuousMoveBlockedInProbeMode()
        {
            string message = "扎针状态下禁止连续运动，请先关闭扎针状态。";
            WriteLog("运动控制", message);
            MyMessageBox.ShowWarn(message, "扎针状态提示");
        }

        private void OnManualPlanarMoveStarted()
        {
            ManualPlanarMoveStartedAction?.Invoke();
        }

        private async Task MoveFixedPlanarAsync(AxisDirType direction, double distance)
        {
            if (IsProbeMode)
                await MyLTDMC.MoveFixedPlanarWithProbeAsync(direction, distance, FixedDistanceSpeed);
            else
                await MyLTDMC.dmc_pmove(direction, distance, FixedDistanceSpeed);
        }

        private bool TryGetHeight(out double value)
        {
            return TryGetPositiveNumber(Height, "Z轴高度", out value);
        }

        private bool TryGetAngle(out double value)
        {
            return TryGetPositiveNumber(Angle, "旋转角度", out value);
        }

        private bool TryGetPositiveNumber(string text, string label, out double value)
        {
            if (double.TryParse(text, out value) && value >= 0)
                return true;

            value = 0;
            return false;
        }

        public void UpdateConnectionStatus(string statusText, bool writeLog = false)
        {
            StatusText = statusText;
            if (writeLog)
                WriteLog("运动控制", statusText);
        }

        private static string GetConnectFailedMessage(short connectResult)
        {
            return connectResult == MotionResult.ErrConnectFailed
                ? "运动控制卡连接失败：初始化或链路异常"
                : $"运动控制卡连接失败：错误码{connectResult}";
        }

        public void AppendLog(string module, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            string content = string.IsNullOrWhiteSpace(module)
                ? message
                : $"[{module}] {message}";
            LogText += $"[{DateTime.Now:HH:mm:ss}] {content}{Environment.NewLine}";
        }

        private void WriteLog(string module, string message)
        {
            AppendLog(module, message);
        }

        private async Task RunInitializationAsync(string message, Func<Task> initializationAction)
        {
            if (IsInitializing || initializationAction == null)
                return;

            IsInitializing = true;
            InitializingMessage = message;

            try
            {
                await initializationAction();
            }
            finally
            {
                IsInitializing = false;
                InitializingMessage = null;
            }
        }
    }
}
