using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HKVision.Wpf.Model;
using MyAsset.Wpf.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace HKVision.Wpf.ViewModels
{
    public partial class VisionMainViewModel : ObservableObject
    {
        private readonly HikVisionHelper _hikVisionHelper;
        private readonly DispatcherTimer _runStateTimer;
        private bool _isRefreshingLoadedState;

        [ObservableProperty]
        private string solutionPath;

        [ObservableProperty]
        private string selectedProcessName;

        [ObservableProperty]
        private string selectedModuleName;

        [ObservableProperty]
        private bool isSingleRunActive = true;

        [ObservableProperty]
        private bool isContinuousRunActive;

        [ObservableProperty]
        private bool isFovScanning;

        [ObservableProperty]
        private string fovScanButtonText = "影像扫描";

        public ObservableCollection<string> Processes { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Modules { get; } = new ObservableCollection<string>();

        public HikVisionHelper HikVisionHelper => _hikVisionHelper;

        public Func<Task> StartFovFunc { get; set; }

        public Action StopFovFunc { get; set; }

        public VisionMainViewModel(HikVisionHelper hikVisionHelper)
        {
            _hikVisionHelper = hikVisionHelper;
            _runStateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _runStateTimer.Tick += (_, _) => RefreshRunState();
        }

        public void RefreshFromVmManager()
        {
            string currentModuleName = _hikVisionHelper.VmManager.CurrentModule?.Name;

            _isRefreshingLoadedState = true;
            try
            {
                RefreshCollection(Processes, _hikVisionHelper.VmManager.ProcessNameList);
                RefreshCollection(Modules, _hikVisionHelper.VmManager.ModuleNameList);

                SelectedProcessName = Processes.Count > 0 ? Processes[0] : null;

                SelectedModuleName = !string.IsNullOrEmpty(currentModuleName) && Modules.Contains(currentModuleName)
                    ? currentModuleName
                    : Modules.Count > 0 ? Modules[0] : null;
            }
            finally
            {
                _isRefreshingLoadedState = false;
            }

            RefreshRunState();
        }

        partial void OnSelectedProcessNameChanged(string value)
        {
            if (_isRefreshingLoadedState)
                return;

            if (string.IsNullOrEmpty(value))
                return;

            int index = Processes.IndexOf(value);
            if (index < 0)
                return;

            var result = _hikVisionHelper.VmManager.LoadVmProcess(index);
            if (result.IsFailure)
            {
                PublishVisionError(result.Message, "视觉流程切换失败");
                return;
            }

            Modules.Clear();
            foreach (var item in _hikVisionHelper.VmManager.ModuleNameList)
                Modules.Add(item);

            RefreshRunState();
            PublishVisionInfo($"视觉流程已切换：{value}", "视觉流程");
        }

        partial void OnSelectedModuleNameChanged(string value)
        {
            if (_isRefreshingLoadedState)
                return;

            if (string.IsNullOrEmpty(value))
                return;

            int index = Modules.IndexOf(value);
            if (index < 0)
                return;

            var result = _hikVisionHelper.VmManager.LoadVmModule(index);
            if (result.IsFailure)
            {
                PublishVisionError(result.Message, "视觉模块切换失败");
                return;
            }

            PublishVisionInfo($"视觉模块已切换：{value}", "视觉模块");
        }

        [RelayCommand]
        private void SelectSolution()
        {
            string selectedPath = _hikVisionHelper.VmManager.SelectProject();
            if (string.IsNullOrEmpty(selectedPath))
            {
                PublishVisionInfo("已取消选择视觉方案。", "视觉方案");
                return;
            }

            SolutionPath = selectedPath;

            var result = _hikVisionHelper.VmManager.LoadProject(SolutionPath);
            if (result.IsFailure)
            {
                PublishVisionError("加载失败--- " + result.Message, "视觉方案加载失败");
                return;
            }

            Processes.Clear();
            Modules.Clear();

            foreach (var item in _hikVisionHelper.VmManager.ProcessNameList)
                Processes.Add(item);

            RefreshRunState();
            PublishVisionSuccess($"视觉方案已加载：{SolutionPath}", "视觉方案");
        }

        [RelayCommand]
        private void Save()
        {
            var result = _hikVisionHelper.VmManager.Save();
            if (result.IsFailure)
            {
                PublishVisionError(result.Message, "视觉方案保存失败");
                return;
            }

            PublishVisionSuccess("视觉方案已保存。", "视觉方案");
        }

        [RelayCommand]
        private void ConfigureParam()
        {
            var result = _hikVisionHelper.VmManager.SetVmModuleParam();
            if (result.IsFailure)
            {
                PublishVisionError("加载参数失败--- " + result.Message, "视觉参数配置失败");
                return;
            }

            PublishVisionSuccess("视觉参数已配置并保存。", "视觉参数");
        }

        [RelayCommand]
        private void SingleRun()
        {
            try
            {
                _hikVisionHelper.VmManager.SingleRun();
                RefreshRunState();
                PublishVisionInfo("已执行单次拍照。", "视觉执行");
            }
            catch (Exception ex)
            {
                PublishVisionError("单次拍照失败--- " + ex.Message, "视觉执行失败");
            }
        }

        [RelayCommand]
        private void ContinueRun()
        {
            try
            {
                _hikVisionHelper.VmManager.ContinueRun();
                RefreshRunState();
                PublishVisionSuccess("已开始连续拍照。", "视觉执行");
            }
            catch (Exception ex)
            {
                PublishVisionError("连续拍照启动失败--- " + ex.Message, "视觉执行失败");
            }
        }

        [RelayCommand]
        private Task StartFovAsync()
        {
            if (IsFovScanning)
                return Task.CompletedTask;

            return RunFovScanAsync();
        }

        [RelayCommand]
        private void ToggleFovScan()
        {
            if (IsFovScanning)
            {
                StopFov();
                return;
            }

            _ = StartFovAsync();
        }

        [RelayCommand]
        private void StopFov()
        {
            if (StopFovFunc == null)
            {
                PublishVisionWarning("影像扫描停止接口未绑定。", "影像扫描");
                return;
            }

            try
            {
                StopFovFunc.Invoke();
                PublishVisionInfo("已请求停止影像扫描。", "影像扫描");
            }
            catch (Exception ex)
            {
                PublishVisionError("停止影像扫描失败--- " + ex.Message, "影像扫描失败");
            }
        }

        [RelayCommand]
        private async Task MoveNearestPointAsync()
        {
            Result loadMatchResult = _hikVisionHelper.VmManager.LoadMatchModule(RoiPosEnum.Center);
            if (loadMatchResult.IsFailure)
            {
                PublishVisionError(loadMatchResult.Message, "视觉匹配失败");
                return;
            }

            Point2D? centerPoint = null;
            try
            {
                centerPoint = await _hikVisionHelper.ReadServerCenterDataAsync();
            }
            catch (Exception ex)
            {
                PublishVisionError("读取最近点失败--- " + ex.Message, "视觉匹配失败");
                return;
            }
            finally
            {
                Result loadDefaultResult = _hikVisionHelper.VmManager.LoadDefaultModule();
                if (loadDefaultResult.IsFailure)
                    PublishVisionWarning(loadDefaultResult.Message, "视觉模块恢复失败");
            }

            if (centerPoint == null)
            {
                PublishVisionWarning("未获取到可移动的最近点。", "视觉匹配");
                return;
            }

            var offsetValue = _hikVisionHelper.DataModel.Transformer.TransformOffset((Point2D)centerPoint);
            if (_hikVisionHelper.MoveSync == null)
            {
                PublishVisionError("同步移动接口未绑定，无法移动到最近点", "视觉移动失败");
                return;
            }

            try
            {
                short moveResult = await _hikVisionHelper.MoveSync(offsetValue.X, offsetValue.Y);
                if (moveResult != 0)
                {
                    PublishVisionError($"移动到最近点失败，错误码:{moveResult}", "视觉移动失败");
                    return;
                }

                PublishVisionSuccess($"已移动到最近点，偏移 X:{offsetValue.X:F4}, Y:{offsetValue.Y:F4}", "视觉移动");
            }
            catch (Exception ex)
            {
                PublishVisionError("移动到最近点异常--- " + ex.Message, "视觉移动失败");
            }
        }

        private void RefreshRunState()
        {
            if (!CanRefreshRunState())
            {
                IsContinuousRunActive = false;
                IsSingleRunActive = false;
                if (_runStateTimer.IsEnabled)
                    _runStateTimer.Stop();
                return;
            }

            if (!_runStateTimer.IsEnabled)
                _runStateTimer.Start();

            IsContinuousRunActive = _hikVisionHelper.VmManager.IsContinuousRun();
            IsSingleRunActive = !IsContinuousRunActive;
        }

        private bool CanRefreshRunState()
        {
            return _hikVisionHelper.VmManager.IsConnect && _hikVisionHelper.VmManager.CurrentProcess != null;
        }

        private static void RefreshCollection(ObservableCollection<string> target, System.Collections.Generic.IEnumerable<string> source)
        {
            target.Clear();
            if (source == null)
                return;

            foreach (var item in source)
                target.Add(item);
        }

        partial void OnIsFovScanningChanged(bool value)
        {
            FovScanButtonText = value ? "停止扫描" : "影像扫描";
        }

        private async Task RunFovScanAsync()
        {
            if (StartFovFunc == null)
            {
                PublishVisionWarning("影像扫描启动接口未绑定。", "影像扫描");
                return;
            }

            IsFovScanning = true;
            _hikVisionHelper.OnLogMessage("开始影像扫描。");

            try
            {
                await StartFovFunc();
                PublishVisionSuccess("影像扫描已完成。", "影像扫描");
            }
            catch (Exception ex)
            {
                PublishVisionError("影像扫描失败--- " + ex.Message, "影像扫描失败");
            }
            finally
            {
                IsFovScanning = false;
            }
        }

        private void PublishVisionInfo(string message, string title)
        {
            PublishVisionMessage(VisionNotificationKind.Info, message, title);
        }

        private void PublishVisionSuccess(string message, string title)
        {
            PublishVisionMessage(VisionNotificationKind.Success, message, title);
        }

        private void PublishVisionWarning(string message, string title)
        {
            PublishVisionMessage(VisionNotificationKind.Warning, message, title);
        }

        private void PublishVisionError(string message, string title)
        {
            PublishVisionMessage(VisionNotificationKind.Error, message, title);
        }

        private void PublishVisionMessage(VisionNotificationKind kind, string message, string title)
        {
            _hikVisionHelper.OnLogMessage(message);
            _hikVisionHelper.RequestNotification(kind, message, title);
        }
    }
}
