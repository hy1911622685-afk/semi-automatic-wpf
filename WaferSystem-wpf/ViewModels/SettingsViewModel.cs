using CommunityToolkit.Mvvm.ComponentModel;
using WaferSystem.Wpf.Models;

namespace WaferSystem.Wpf.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private double ccdX;

        [ObservableProperty]
        private double ccdY;

        [ObservableProperty]
        private double probeCenterX;

        [ObservableProperty]
        private double probeCenterY;

        [ObservableProperty]
        private double xyFastSpeed;

        [ObservableProperty]
        private double xyNormalSpeed;

        [ObservableProperty]
        private double xyZeroSpeed;

        [ObservableProperty]
        private double xySlowSpeed;

        [ObservableProperty]
        private double safetyHeight;

        [ObservableProperty]
        private double separationHeight;

        [ObservableProperty]
        private double contactHeight;

        [ObservableProperty]
        private double transformerA11;

        [ObservableProperty]
        private double transformerA22;

        [ObservableProperty]
        private double matchScorePercent;

        [ObservableProperty]
        private double aoiMatchScorePercent;

        [ObservableProperty]
        private int allowableErrorNumber;

        [ObservableProperty]
        private double overlapRatePercent;

        [ObservableProperty]
        private double scanStepX;

        [ObservableProperty]
        private double scanStepY;

        [ObservableProperty]
        private double dieDimensionScanStep;

        [ObservableProperty]
        private double toleranceFactorPercent;


        public static SettingsViewModel FromSettings(SystemSettings settings)
        {
            settings ??= new SystemSettings();
            return new SettingsViewModel
            {
                CcdX = settings.Motion.CcdX,
                CcdY = settings.Motion.CcdY,
                ProbeCenterX = settings.Motion.ProbeCenterX,
                ProbeCenterY = settings.Motion.ProbeCenterY,
                XyFastSpeed = settings.Motion.XyFastSpeed,
                XyNormalSpeed = settings.Motion.XyNormalSpeed,
                XyZeroSpeed = settings.Motion.XyZeroSpeed,
                XySlowSpeed = settings.Motion.XySlowSpeed,
                SafetyHeight = settings.Motion.SafetyHeight,
                SeparationHeight = settings.Motion.SeparationHeight,
                ContactHeight = settings.Motion.ContactHeight,
                TransformerA11 = settings.Vision.TransformerA11,
                TransformerA22 = settings.Vision.TransformerA22,
                MatchScorePercent = settings.Vision.MatchScore * 100d,
                AoiMatchScorePercent = settings.Vision.AoiMatchScore * 100d,
                AllowableErrorNumber = settings.Vision.AllowableErrorNumber,
                OverlapRatePercent = settings.Vision.OverlapRate * 100d,
                ScanStepX = settings.Vision.ScanStepX,
                ScanStepY = settings.Vision.ScanStepY,
                DieDimensionScanStep = settings.Vision.DieDimensionScanStep,
                ToleranceFactorPercent = settings.WaferMap.ToleranceFactor * 100d,
            };
        }

        public SystemSettings ToSettings()
        {
            return new SystemSettings
            {
                Motion =
                {
                    //CcdX = CcdX,
                    //CcdY = CcdY,
                    //ProbeCenterX = ProbeCenterX,
                    //ProbeCenterY = ProbeCenterY,
                    XyFastSpeed = XyFastSpeed,
                    XyNormalSpeed = XyNormalSpeed,
                    XyZeroSpeed = XyZeroSpeed,
                    XySlowSpeed = XySlowSpeed,
                    SafetyHeight = SafetyHeight,
                    SeparationHeight = SeparationHeight,
                    ContactHeight = ContactHeight
                },
                Vision =
                {
                    TransformerA11 = TransformerA11,
                    TransformerA22 = TransformerA22,
                    MatchScore = MatchScorePercent / 100d,
                    AoiMatchScore = AoiMatchScorePercent / 100d,
                    AllowableErrorNumber = AllowableErrorNumber,
                    OverlapRate = OverlapRatePercent / 100d,
                    ScanStepX = ScanStepX,
                    ScanStepY = ScanStepY,
                    DieDimensionScanStep = DieDimensionScanStep
                },
                WaferMap =
                {
                    ToleranceFactor = ToleranceFactorPercent / 100d,
                }
            };
        }
    }
}
