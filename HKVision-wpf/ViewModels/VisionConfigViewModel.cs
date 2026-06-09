using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HKVision.Wpf.Model;
using MyAsset.Wpf.Messaging;
using System;
using System.Threading.Tasks;

namespace HKVision.Wpf.ViewModels
{
    public partial class VisionConfigViewModel : ObservableObject
    {
        private readonly HikVisionHelper _hikVisionHelper;
        private readonly IMessageBoxService _messageBoxService;

        private Tuple<double, double, double, double> _calibratePoint1;
        private Tuple<double, double, double, double> _calibratePoint2;
        private (double X, double Y)? _levelingPoint1;
        private (double X, double Y)? _levelingPoint2;

        [ObservableProperty]
        private bool calibrationEnabled;

        [ObservableProperty]
        private bool levelingEnabled;

        [ObservableProperty]
        private string calibrateRatioX;

        [ObservableProperty]
        private string calibrateRatioY;

        [ObservableProperty]
        private string scannedDieWidth;

        [ObservableProperty]
        private string scannedDieHeight;

        [ObservableProperty]
        private bool hasCalibratePoint1;

        [ObservableProperty]
        private bool hasCalibratePoint2;

        [ObservableProperty]
        private bool hasLevelingPoint1;

        [ObservableProperty]
        private bool hasLevelingPoint2;

        public Func<(double X, double Y)> ReadAxisPositionFunc;
        public Func<double, Task> MoveAxisTFunc;
        public Action<bool> CalibrationAction;
        public Action<bool> LevelingAction;

        public VisionConfigViewModel(HikVisionHelper hikVisionHelper, IMessageBoxService messageBoxService = null)
        {
            _hikVisionHelper = hikVisionHelper;
            _messageBoxService = messageBoxService ?? new MessageBoxService("HKVision-wpf", "视觉对话框", "视觉错误", "视觉警告");
            CalibrateRatioX = _hikVisionHelper.DataModel.Transformer.A11.ToString();
            CalibrateRatioY = _hikVisionHelper.DataModel.Transformer.A22.ToString();
        }

        partial void OnCalibrationEnabledChanged(bool value)
        {
            if (value)
            {
                LevelingEnabled = false;
            }
            else
            {
                ResetCalibrationButtons();
                var result = _hikVisionHelper.VmManager.LoadDefaultModule();
                if (result.IsFailure)
                    _hikVisionHelper.OnLogMessage(result.Message);
            }

            CalibrationAction?.Invoke(value);
        }

        partial void OnLevelingEnabledChanged(bool value)
        {
            if (value)
                CalibrationEnabled = false;
            else
                ResetLevelingButtons();

            LevelingAction?.Invoke(value);
        }

        [RelayCommand]
        private void ConfigureImageSource()
        {
            var result = _hikVisionHelper.VmManager.LoadDefaultModule();
            if (result.IsFailure)
            {
                _hikVisionHelper.OnLogMessage(result.Message);
                return;
            }
        }

        [RelayCommand]
        private void ConfigureMatchModule()
        {
            var result = _hikVisionHelper.VmManager.LoadMatchModule(RoiPosEnum.None);
            if (result.IsFailure)
            {
                _hikVisionHelper.OnLogMessage(result.Message);
                return;
            }
        }

        [RelayCommand]
        private void CalibratePoint1()
        {
            CaptureCalibratePoint1(null);
        }

        [RelayCommand]
        private void CalibratePoint2()
        {
            CaptureCalibratePoint2(null);
        }

        [RelayCommand]
        private void Calibrate()
        {
            if (_calibratePoint1 == null)
            {
                _hikVisionHelper.OnLogMessage("请先设置第一个特殊点");
                return;
            }

            if (_calibratePoint2 == null)
            {
                _hikVisionHelper.OnLogMessage("请先设置第二个特殊点");
                return;
            }

            double xData = Math.Round((_calibratePoint2.Item3 - _calibratePoint1.Item3) / (_calibratePoint2.Item1 - _calibratePoint1.Item1), 4);
            double yData = Math.Round((_calibratePoint2.Item4 - _calibratePoint1.Item4) / (_calibratePoint2.Item2 - _calibratePoint1.Item2), 4);

            _hikVisionHelper.DataModel.Transformer.A11 = xData;
            _hikVisionHelper.DataModel.Transformer.A22 = yData;
            CalibrateRatioX = xData.ToString();
            CalibrateRatioY = yData.ToString();

            _calibratePoint1 = null;
            _calibratePoint2 = null;
            NotifyPointStateChanged();

            var result = _hikVisionHelper.VmManager.LoadDefaultModule();
            if (result.IsFailure)
                _hikVisionHelper.OnLogMessage(result.Message);
        }

        [RelayCommand]
        private void LevelingPoint1()
        {
            CaptureLevelingPoint1(null);
        }

        [RelayCommand]
        private void LevelingPoint2()
        {
            CaptureLevelingPoint2(null);
        }

        [RelayCommand]
        private async Task LevelingAsync()
        {
            if (_levelingPoint1 == null)
            {
                _messageBoxService.ShowError("请先设置第一个特殊点");
                return;
            }

            if (_levelingPoint2 == null)
            {
                _messageBoxService.ShowError("请先设置第二个特殊点");
                return;
            }

            double angle = Math.Round(
                Math.Atan2(_levelingPoint1.Value.Y - _levelingPoint2.Value.Y, _levelingPoint1.Value.X - _levelingPoint2.Value.X) * (180 / Math.PI),
                3);

            angle %= 90;
            if (angle > 45 && angle < 90)
                angle -= 90;
            else if (angle < -45 && angle > -90)
                angle += 90;

            if (_levelingPoint1.Value.Y > _levelingPoint2.Value.Y)
                angle = _levelingPoint1.Value.X < _levelingPoint2.Value.X ? Math.Abs(angle) : -Math.Abs(angle);
            else
                angle = _levelingPoint1.Value.X < _levelingPoint2.Value.X ? -Math.Abs(angle) : Math.Abs(angle);

            if (Math.Abs(angle) > 15)
            {
                _messageBoxService.ShowWarning("旋转角度较大，请先手动调整");
                return;
            }
            
            if (MoveAxisTFunc != null)
                await MoveAxisTFunc.Invoke(angle);

            ResetLevelingButtons();
        }

        [RelayCommand]
        private async Task ScanDieWidthAsync()
        {
            ScannedDieWidth = await ScanDieDimensionTextAsync(VisionMoveDir.Right);
        }

        [RelayCommand]
        private async Task ScanDieHeightAsync()
        {
            ScannedDieHeight = await ScanDieDimensionTextAsync(VisionMoveDir.Front);
        }

        private async Task<string> ScanDieDimensionTextAsync(VisionMoveDir moveDir)
        {
            double dieDimension = await _hikVisionHelper.StartScanningDieDimensionAsync(moveDir);
            return dieDimension > 0 ? dieDimension.ToString("F4") : string.Empty;
        }

        public void AnalysisPositionData((double, double) pos, string info)
        {
            if (CalibrationEnabled)
            {
                if (info.ToLowerInvariant() == "right")
                    CaptureCalibratePoint2(pos);
                else if (info.ToLowerInvariant() == "left")
                    CaptureCalibratePoint1(pos);
                else
                    CalibrationEnabled = false;
            }
            else
            {
                if (info.ToLowerInvariant() == "right")
                    CaptureLevelingPoint2(pos);
                else if (info.ToLowerInvariant() == "left")
                    CaptureLevelingPoint1(pos);
                else
                    LevelingEnabled = false;
            }
        }

        private async void CaptureCalibratePoint1((double X, double Y)? pos)
        {
            var point = pos ?? ReadAxisPositionFunc?.Invoke();
            if (point == null)
                return;

            var data = await _hikVisionHelper.ReadServerCenterDataAsync();
            if (data == null)
                return;

            _calibratePoint1 = new Tuple<double, double, double, double>(point.Value.X, point.Value.Y, data.Value.X, data.Value.Y);
            HasCalibratePoint1 = true;
        }

        private async void CaptureCalibratePoint2((double X, double Y)? pos)
        {
            var point = pos ?? ReadAxisPositionFunc?.Invoke();
            if (point == null)
                return;

            var data = await _hikVisionHelper.ReadServerCenterDataAsync();
            if (data == null)
                return;

            _calibratePoint2 = new Tuple<double, double, double, double>(point.Value.X, point.Value.Y, data.Value.X, data.Value.Y);
            HasCalibratePoint2 = true;
        }

        private void CaptureLevelingPoint1((double X, double Y)? pos)
        {
            _levelingPoint1 = pos ?? ReadAxisPositionFunc?.Invoke();
            HasLevelingPoint1 = _levelingPoint1 != null;
        }

        private void CaptureLevelingPoint2((double X, double Y)? pos)
        {
            _levelingPoint2 = pos ?? ReadAxisPositionFunc?.Invoke();
            HasLevelingPoint2 = _levelingPoint2 != null;
        }

        private void ResetCalibrationButtons()
        {
            _calibratePoint1 = null;
            _calibratePoint2 = null;
            NotifyPointStateChanged();
        }

        private void ResetLevelingButtons()
        {
            _levelingPoint1 = null;
            _levelingPoint2 = null;
            NotifyPointStateChanged();
        }

        private void NotifyPointStateChanged()
        {
            HasCalibratePoint1 = _calibratePoint1 != null;
            HasCalibratePoint2 = _calibratePoint2 != null;
            HasLevelingPoint1 = _levelingPoint1 != null;
            HasLevelingPoint2 = _levelingPoint2 != null;
        }
    }
}
