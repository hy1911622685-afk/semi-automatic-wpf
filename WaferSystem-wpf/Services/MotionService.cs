using MotionCard;
using MotionCard.Wpf.ViewModels;
using MyAsset.Wpf.Messaging;
using System;
using System.Threading.Tasks;
using WaferMap.Wpf.ViewModels;

namespace WaferSystem.Wpf.Services
{
    public sealed class MotionService : IDisposable
    {
        private readonly WaferMainViewModel _waferViewModel;
        private readonly MotionMainViewModel _motionViewModel;
        private readonly SubscriptionGroup _waferSubscriptions = new SubscriptionGroup();
        private readonly SubscriptionGroup _hardwareSubscriptions = new SubscriptionGroup();
        private readonly SubscriptionGroup _motionViewModelSubscriptions = new SubscriptionGroup();
        private bool _initialized;
        private bool _hardwareBound;
        private bool _disposed;

        public event Action<string, string> LogPublished;
        public event Action<MotionNotificationKind, string, string> NotificationRequested;
        public event Action<bool, string> MotionWaitStateChanged;
        public Func<PlanarMoveSafetyRequest, Task<bool>> PlanarMoveSafetyConfirmationRequested { get; set; }

        public MotionService(WaferMainViewModel waferViewModel, MotionMainViewModel motionViewModel = null)
        {
            _waferViewModel = waferViewModel;
            _motionViewModel = motionViewModel;
            BindHardwareEvents();
            BindWaferMapIntegration();
            BindMotionPanelIntegration();
        }

        public short Initialize()
        {
            if (_initialized)
                return MotionResult.Success;

            short connectResult = MyLTDMC.Connect();
            string statusText = connectResult == MotionResult.Success
                ? "运动控制卡已连接"
                : GetConnectFailedMessage(connectResult);
            UpdateConnectionStatus(statusText, true);

            if (connectResult != MotionResult.Success)
            {
                RequestNotification(MotionNotificationKind.Error, statusText, "运动控制卡连接失败");
                return connectResult;
            }

            _initialized = true;
            return MotionResult.Success;
        }

        public void Disconnect()
        {
            MyLTDMC.DisConnect();
            _initialized = false;
            UpdateConnectionStatus("运动控制卡已断开", true);
        }

        public async Task HandleDieMoveAsync(AxisDirType axisDirType)
        {
            var navigator = _waferViewModel.PlotHelper.MyNavigator;
            var currentDie = _waferViewModel.PlotHelper.DataModel.SelectedDie;
            if (currentDie is null || navigator is null)
                return;

            (double X, double Y)? offset = axisDirType == AxisDirType.Left || axisDirType == AxisDirType.Back
                ? navigator.PreviousDiePos(out _)
                : navigator.NextDiePos(out _);

            if (offset != null)
                await MyLTDMC.MoveSync(offset.Value.X, offset.Value.Y);
        }

        public Task<short> SyncMoveAsync(double posX, double posY)
        {
            return MyLTDMC.MoveSync(posX, posY);
        }

        private void HandleDeviceDisconnected()
        {
            _initialized = false;
            UpdateConnectionStatus("运动控制卡已断开", true);
            RequestNotification(MotionNotificationKind.Warning, "运动控制卡已断开，请检查控制器和连接线。", "运动控制卡已断开");
        }

        private void BindHardwareEvents()
        {
            if (_hardwareBound)
                return;

            _hardwareBound = true;
            _hardwareSubscriptions.Bind(() => MyLTDMC.LogAction += HandleHardwareLog, () => MyLTDMC.LogAction -= HandleHardwareLog);
            _hardwareSubscriptions.Bind(() => MyLTDMC.StopReasonNotificationAction += HandleStopReasonNotification, () => MyLTDMC.StopReasonNotificationAction -= HandleStopReasonNotification);
            _hardwareSubscriptions.Bind(() => MyLTDMC.CloseAction += HandleDeviceDisconnected, () => MyLTDMC.CloseAction -= HandleDeviceDisconnected);
            _hardwareSubscriptions.Bind(() => MyLTDMC.PlanarMoveSafetyConfirmationAction = HandlePlanarMoveSafetyConfirmation, () => MyLTDMC.PlanarMoveSafetyConfirmationAction = null);
            _hardwareSubscriptions.Bind(() => MyLTDMC.MotionWaitStateAction += HandleMotionWaitState, () => MyLTDMC.MotionWaitStateAction -= HandleMotionWaitState);
        }

        private void BindWaferMapIntegration()
        {
            if (_waferViewModel == null)
                return;

            BindWaferMapMotionActions();
            BindWaferMapPositionReader();
        }

        private void BindWaferMapMotionActions()
        {
            _waferSubscriptions.Bind(() => _waferViewModel.SyncAbsMoveFunc = MyLTDMC.AbsMoveSync, () =>
            {
                if (_waferViewModel.SyncAbsMoveFunc == MyLTDMC.AbsMoveSync)
                    _waferViewModel.SyncAbsMoveFunc = null;
            });
            _waferSubscriptions.Bind(() => _waferViewModel.SyncMoveFunc = MyLTDMC.MoveSync, () =>
            {
                if (_waferViewModel.SyncMoveFunc == MyLTDMC.MoveSync)
                    _waferViewModel.SyncMoveFunc = null;
            });
        }

        private void BindWaferMapPositionReader()
        {
            _waferSubscriptions.Bind(() => _waferViewModel.ReadPhysicalPosEvent = MyLTDMC.ReadPhysicalPos, () =>
            {
                if (_waferViewModel.ReadPhysicalPosEvent == MyLTDMC.ReadPhysicalPos)
                    _waferViewModel.ReadPhysicalPosEvent = null;
            });
        }

        private void BindMotionPanelIntegration()
        {
            if (_motionViewModel == null)
                return;

            BindMotionPanelConnectionActions();
            BindManualPlanarMoveSyncExit();
        }

        private void BindMotionPanelConnectionActions()
        {
            _motionViewModelSubscriptions.Bind(
                () => _motionViewModel.ConnectAction = Initialize,
                () =>
                {
                    if (_motionViewModel.ConnectAction == Initialize)
                        _motionViewModel.ConnectAction = null;
                });
            _motionViewModelSubscriptions.Bind(
                () => _motionViewModel.DisconnectAction = Disconnect,
                () =>
                {
                    if (_motionViewModel.DisconnectAction == Disconnect)
                        _motionViewModel.DisconnectAction = null;
                });
        }

        private void BindManualPlanarMoveSyncExit()
        {
            _motionViewModelSubscriptions.Bind(
                () => _motionViewModel.ManualPlanarMoveStartedAction = ExitWaferSyncForManualPlanarMove,
                () =>
                {
                    if (_motionViewModel.ManualPlanarMoveStartedAction == ExitWaferSyncForManualPlanarMove)
                        _motionViewModel.ManualPlanarMoveStartedAction = null;
                });
        }

        private void ExitWaferSyncForManualPlanarMove()
        {
            _waferViewModel?.ExitSyncState();
        }

        private void HandleHardwareLog(string module, string message)
        {
            if (ShouldSuppressHardwareLog(message))
                return;

            PublishLog(module, message);
        }

        private void HandleStopReasonNotification(string title, string message)
        {
            RequestNotification(MotionNotificationKind.Error, message, title);
        }

        private Task<bool> HandlePlanarMoveSafetyConfirmation(PlanarMoveSafetyRequest request)
        {
            return PlanarMoveSafetyConfirmationRequested?.Invoke(request) ?? Task.FromResult(false);
        }

        private void HandleMotionWaitState(bool isWaiting, string message)
        {
            MotionWaitStateChanged?.Invoke(isWaiting, message);
        }

        private void UpdateConnectionStatus(string statusText, bool publishLog)
        {
            _motionViewModel?.UpdateConnectionStatus(statusText);

            if (publishLog)
                PublishLog("运动控制", statusText);
        }

        private void PublishLog(string module, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            _motionViewModel?.AppendLog(module, message);
            LogPublished?.Invoke(module, message);
            RuntimeLogMessenger.Broadcast("WaferSystem-wpf", module, message);
        }

        private void RequestNotification(MotionNotificationKind kind, string message, string title)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            NotificationRequested?.Invoke(kind, message, title);
        }

        private static bool ShouldSuppressHardwareLog(string message)
        {
            return string.Equals(message, "链接成功", StringComparison.Ordinal) ||
                   string.Equals(message, "已断开链接", StringComparison.Ordinal);
        }

        private static string GetConnectFailedMessage(short connectResult)
        {
            return connectResult == MotionResult.ErrConnectFailed
                ? "运动控制卡连接失败：初始化或链路异常"
                : $"运动控制卡连接失败：错误码{connectResult}";
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            PlanarMoveSafetyConfirmationRequested = null;
            _motionViewModelSubscriptions.Dispose();
            _waferSubscriptions.Dispose();
            _hardwareSubscriptions.Dispose();
            _hardwareBound = false;

            if (_initialized)
            {
                MyLTDMC.Dispose();
                _initialized = false;
            }
        }
    }

    public enum MotionNotificationKind
    {
        Error,
        Warning
    }
}
