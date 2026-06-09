namespace WaferSystem.Wpf.Models
{
    public class SystemSettings
    {
        public int Version { get; set; } = 1;
        public MotionSettings Motion { get; set; } = new MotionSettings();
        public VisionSettings Vision { get; set; } = new VisionSettings();
        public WaferMapRuntimeSettings WaferMap { get; set; } = new WaferMapRuntimeSettings();
    }

    public class MotionSettings
    {
        public double CcdX { get; set; } = 100d;
        public double CcdY { get; set; } = 100d;
        public double ProbeCenterX { get; set; } = 80d;
        public double ProbeCenterY { get; set; } = 50d;
        public double XyFastSpeed { get; set; } = 8d;
        public double XyNormalSpeed { get; set; } = 1d;
        public double XyZeroSpeed { get; set; } = 8d;
        public double XySlowSpeed { get; set; } = 0.02d;
        public double SafetyHeight { get; set; } = 3d;
        public double SeparationHeight { get; set; } = 4d;
        public double ContactHeight { get; set; } = 5d;
    }

    public class VisionSettings
    {
        public double MatchScore { get; set; } = 0.7d;
        public double AoiMatchScore { get; set; } = 0.6d;
        public int AllowableErrorNumber { get; set; } = 1;
        public double OverlapRate { get; set; } = 0.1d;
        public double TransformerA11 { get; set; } = 216.2674d;
        public double TransformerA22 { get; set; } = 224.1342d;
        public double ScanStepX { get; set; } = 0d;
        public double ScanStepY { get; set; } = 0d;
        public double DieDimensionScanStep { get; set; } = 0.05d;
    }

    public class WaferMapRuntimeSettings
    {
        public double ToleranceFactor { get; set; } = 0.3d;
    }
}
