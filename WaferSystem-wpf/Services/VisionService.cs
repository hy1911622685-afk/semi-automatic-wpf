using HKVision.Wpf;
using HKVision.Wpf.Model;
using HKVision.Wpf.Views;
using MotionCard;
using MyAsset.Wpf.Infrastructure;
using System;
using System.Threading;
using System.Threading.Tasks;
using WaferMap.Wpf.ViewModels;

namespace WaferSystem.Wpf.Services
{
    public sealed class VisionService : IDisposable
    {
        private static readonly TimeSpan BlindScanCompletionDelay = TimeSpan.FromMilliseconds(20);

        private readonly HikVisionHelper _hikVisionHelper;
        private readonly WaferMainViewModel _waferViewModel;
        private readonly SubscriptionGroup _visionConfigSubscriptions = new SubscriptionGroup();
        private readonly SubscriptionGroup _hardwareSubscriptions = new SubscriptionGroup();
        private readonly SemaphoreSlim _initializeLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _blindScanLock = new SemaphoreSlim(1, 1);
        private VisionConfigView _visionConfig;
        private VisionMainView _visionMainView;
        private bool _hardwareBound;
        private bool _initialized;
        private bool _blindScanStopping;
        private bool _disposed;

        public VisionService(HikVisionHelper hikVisionHelper, WaferMainViewModel waferViewModel)
        {
            _hikVisionHelper = hikVisionHelper;
            _waferViewModel = waferViewModel;
            BindHardwareActions();
        }

        public async Task<bool> InitializeAsync()
        {
            if (_disposed)
                return false;

            if (_initialized)
                return true;

            await _initializeLock.WaitAsync();
            try
            {
                if (_disposed)
                    return false;

                if (_initialized)
                    return true;

                var result = _hikVisionHelper.VmManager.LookForDevice();
                if (result.IsFailure)
                {
                    if (_disposed)
                        return false;

                    PublishStartupError(result.Message, result.Message, "视觉设备连接失败");
                    return false;
                }

                result = await Task.Run(() => _hikVisionHelper.VmManager.LoadSolution());
                if (_disposed)
                    return false;

                if (result.IsFailure)
                {
                    PublishStartupError("加载视觉方案失败: " + result.Message, result.Message, "视觉方案加载失败");
                    return false;
                }

                result = _hikVisionHelper.VmManager.LoadVmProcess(0);
                if (_disposed)
                    return false;

                if (result.IsFailure)
                {
                    PublishStartupError("加载视觉流程失败: " + result.Message, result.Message, "视觉流程加载失败");
                    return false;
                }

                await Task.Delay(100);
                if (_disposed)
                    return false;

                var moduleResult = _hikVisionHelper.VmManager.LoadDefaultModule();
                if (_disposed)
                    return false;

                if (moduleResult.IsFailure)
                {
                    PublishStartupError("加载实时图像模块失败: " + moduleResult.Message, moduleResult.Message, "实时图像加载失败");
                    return false;
                }

                _hikVisionHelper.OnLogMessage("视觉设备已连接，实时图像已加载");
                _initialized = true;
                _visionMainView?.RefreshFromVmManager();
                return true;
            }
            catch (Exception ex)
            {
                if (_disposed)
                    return false;

                string message = "启动时视觉初始化异常：" + ex.Message;
                PublishStartupError(message, message, "视觉初始化异常");
                return false;
            }
            finally
            {
                _initializeLock.Release();
            }
        }

        public void BindVisionConfig(VisionConfigView visionConfig)
        {
            if (visionConfig == null)
                return;

            if (_visionConfig != null)
                UnbindVisionConfig(_visionConfig);

            _visionConfig = visionConfig;
            _visionConfigSubscriptions.Bind(
                () => visionConfig.ReadAxisPositionFunc += MyLTDMC.ReadPhysicalPos,
                () => visionConfig.ReadAxisPositionFunc -= MyLTDMC.ReadPhysicalPos);
            _visionConfigSubscriptions.Bind(
                () => visionConfig.MoveAxisTFunc += MoveAxisTAsync,
                () => visionConfig.MoveAxisTFunc -= MoveAxisTAsync);
        }

        public void UnbindVisionConfig(VisionConfigView visionConfig)
        {
            if (visionConfig == null)
                return;

            if (ReferenceEquals(_visionConfig, visionConfig))
            {
                _visionConfigSubscriptions.Clear();
                _visionConfig = null;
                return;
            }

            visionConfig.ReadAxisPositionFunc -= MyLTDMC.ReadPhysicalPos;
            visionConfig.MoveAxisTFunc -= MoveAxisTAsync;
        }

        public void BindVisionMain(VisionMainView visionMainView)
        {
            if (visionMainView == null)
                return;

            _visionMainView = visionMainView;
            visionMainView.StartFovFunc = StartBlindScanAsync;
            visionMainView.StopFovFunc = StopBlindScan;
            _visionMainView?.RefreshFromVmManager();
        }

        public async Task StartBlindScanAsync()
        {
            if (_disposed)
                return;

            if (!await _blindScanLock.WaitAsync(0))
                return;

            bool shouldCompleteBlindScan = false;
            try
            {
                _hikVisionHelper.VmManager.DiscontinueRun();
                _waferViewModel.PlotHelper.PrepareBlindScanMap();
                shouldCompleteBlindScan = true;
                _blindScanStopping = false;
                _hikVisionHelper.OnLogMessage("已初始化盲扫地图，机台开始运动...");
                await _hikVisionHelper.StartScanningAsync();
            }
            finally
            {
                try
                {
                    if (shouldCompleteBlindScan)
                    {
                        await Task.Delay(BlindScanCompletionDelay);
                        _waferViewModel.PlotHelper.CompleteBlindScanMap();
                    }
                }
                catch (Exception ex)
                {
                    _hikVisionHelper.OnLogMessage("盲扫收尾失败--- " + ex.Message);
                }
                finally
                {
                    _blindScanStopping = false;
                    _blindScanLock.Release();
                }
            }
        }

        public void StopBlindScan()
        {
            if (_disposed || _blindScanStopping)
                return;

            _blindScanStopping = true;
            _hikVisionHelper.StopScanning();
            _waferViewModel.PlotHelper.RecalculateDieIndexes();

            MyLTDMC.StopAllAxes("用户点击停止按钮");
        }

        private void BindHardwareActions()
        {
            if (_hardwareBound || _hikVisionHelper == null)
                return;

            _hardwareBound = true;
            _hardwareSubscriptions.Bind(() => _hikVisionHelper.ReadAxisPosFunc += MyLTDMC.ReadPhysicalPos, () => _hikVisionHelper.ReadAxisPosFunc -= MyLTDMC.ReadPhysicalPos);
            _hardwareSubscriptions.Bind(() => _hikVisionHelper.OnAxisMove += Scanner_OnAxisMove, () => _hikVisionHelper.OnAxisMove -= Scanner_OnAxisMove);
            _hardwareSubscriptions.Bind(() => _hikVisionHelper.MoveAbsoluteAsync += MyLTDMC.AbsMoveSync, () => _hikVisionHelper.MoveAbsoluteAsync -= MyLTDMC.AbsMoveSync);
            _hardwareSubscriptions.Bind(() => _hikVisionHelper.MoveSync += MyLTDMC.MoveSync, () => _hikVisionHelper.MoveSync -= MyLTDMC.MoveSync);
            _hardwareSubscriptions.Bind(() => _hikVisionHelper.OnFovScanned += HandleFovScanned, () => _hikVisionHelper.OnFovScanned -= HandleFovScanned);
            _hikVisionHelper.ReadToleranceFactorFunc = () => _waferViewModel.DataModel.ToleranceFactor;
        }

        private void HandleFovScanned(System.Collections.Generic.List<Point2D> physicalDies, double fovLeft, double fovRight, double fovBottom, double fovTop)
        {
            _waferViewModel.PlotHelper.HandleFovScanned(physicalDies, fovLeft, fovRight, fovBottom, fovTop);
        }

        private static async Task<short> Scanner_OnAxisMove(VisionMoveDir dir, double dist)
        {
            AxisDirType targetAxisDir = dir switch
            {
                VisionMoveDir.Right => AxisDirType.Right,
                VisionMoveDir.Front => AxisDirType.Front,
                VisionMoveDir.Left => AxisDirType.Left,
                VisionMoveDir.Back => AxisDirType.Back,
                _ => AxisDirType.Right
            };

            return await MyLTDMC.dmc_pmove(targetAxisDir, dist, SpeedType.Fast);
        }

        private static Task MoveAxisTAsync(double angle)
        {
            return MyLTDMC.dmc_pmove(AxisType.AxisT, angle, SpeedType.Normal);
        }

        private void UnbindHardwareActions()
        {
            if (!_hardwareBound || _hikVisionHelper == null)
                return;

            _hardwareSubscriptions.Clear();
            _hikVisionHelper.ReadToleranceFactorFunc = null;
            _hardwareBound = false;
        }

        private void PublishStartupError(string logMessage, string notificationMessage, string notificationTitle)
        {
            _hikVisionHelper.OnLogMessage(logMessage);
            _hikVisionHelper.RequestNotification(VisionNotificationKind.Error, notificationMessage, notificationTitle);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_visionConfig != null)
                UnbindVisionConfig(_visionConfig);

            UnbindHardwareActions();
            _visionConfigSubscriptions.Dispose();
            _hardwareSubscriptions.Dispose();
            _hikVisionHelper.Disconnect();
        }
    }
}
