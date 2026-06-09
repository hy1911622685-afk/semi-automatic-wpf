using HKVision.Wpf;
using MotionCard;
using MotionCard.Model;
using MyAsset.Wpf.Infrastructure;
using WaferMap.Wpf.ViewModels;
using WaferSystem.Wpf.Models;

namespace WaferSystem.Wpf.Services
{
    public static class SystemSettingsMapper
    {
        public static SystemSettings Capture(HikVisionHelper hikVisionHelper, WaferMainViewModel waferMap)
        {
            var settings = new SystemSettings();

            settings.Motion.CcdX = MyLTDMC.CCDPoint.X;
            settings.Motion.CcdY = MyLTDMC.CCDPoint.Y;
            settings.Motion.XyFastSpeed = MyLTDMC.XyFastSpeed;
            settings.Motion.XyNormalSpeed = MyLTDMC.XyNormalSpeed;
            settings.Motion.XyZeroSpeed = MyLTDMC.XyZeroSpeed;
            settings.Motion.XySlowSpeed = MyLTDMC.XySlowSpeed;
            settings.Motion.ProbeCenterX = MyLTDMC.ProbeCenter.X;
            settings.Motion.ProbeCenterY = MyLTDMC.ProbeCenter.Y;
            settings.Motion.SafetyHeight = ZAxisHeightConfig.GetHeight(ZAxisHeightEnum.Safety);
            settings.Motion.SeparationHeight = ZAxisHeightConfig.GetHeight(ZAxisHeightEnum.Separation);
            settings.Motion.ContactHeight = ZAxisHeightConfig.GetHeight(ZAxisHeightEnum.Contact);

            if (hikVisionHelper?.DataModel != null)
            {
                settings.Vision.TransformerA11 = hikVisionHelper.DataModel.Transformer.A11;
                settings.Vision.TransformerA22 = hikVisionHelper.DataModel.Transformer.A22;
                settings.Vision.MatchScore = hikVisionHelper.DataModel.MatchScore;
                settings.Vision.AoiMatchScore = hikVisionHelper.DataModel.AoiMatchScore;
                settings.Vision.AllowableErrorNumber = hikVisionHelper.DataModel.AllowableErrorNumber;
                settings.Vision.ScanStepX = hikVisionHelper.DataModel.AOIScanStepX;
                settings.Vision.ScanStepY = hikVisionHelper.DataModel.AOIScanStepY;
                settings.Vision.DieDimensionScanStep = hikVisionHelper.DataModel.DieDimensionScanStep;
            }

            if (waferMap?.DataModel != null)
            {
                settings.WaferMap.ToleranceFactor = waferMap.DataModel.ToleranceFactor;
            }

            return settings;
        }

        public static void Apply(SystemSettings settings, HikVisionHelper hikVisionHelper, WaferMainViewModel waferMap)
        {
            settings = SystemSettingsStore.Normalize(settings);
            ApplyMotion(settings.Motion);
            ApplyVision(settings.Vision, hikVisionHelper);
            ApplyWaferMap(settings, waferMap);
        }

        private static void ApplyMotion(MotionSettings settings)
        {
            if (settings == null)
                return;

            MyLTDMC.CCDPoint = new Point2D(settings.CcdX, settings.CcdY);
            MyLTDMC.XyFastSpeed = settings.XyFastSpeed;
            MyLTDMC.XyNormalSpeed = settings.XyNormalSpeed;
            MyLTDMC.XyZeroSpeed = settings.XyZeroSpeed;
            MyLTDMC.XySlowSpeed = settings.XySlowSpeed;
            MyLTDMC.ProbeCenter = new Point2D(settings.ProbeCenterX, settings.ProbeCenterY);
            ZAxisHeightConfig.Update(ZAxisHeightEnum.Safety, settings.SafetyHeight);
            ZAxisHeightConfig.Update(ZAxisHeightEnum.Separation, settings.SeparationHeight);
            ZAxisHeightConfig.Update(ZAxisHeightEnum.Contact, settings.ContactHeight);
        }

        private static void ApplyVision(VisionSettings settings, HikVisionHelper hikVisionHelper)
        {
            if (settings == null || hikVisionHelper?.DataModel == null)
                return;

            hikVisionHelper.DataModel.Transformer.A11 = settings.TransformerA11;
            hikVisionHelper.DataModel.Transformer.A22 = settings.TransformerA22;
            hikVisionHelper.DataModel.MatchScore = settings.MatchScore;
            hikVisionHelper.DataModel.AoiMatchScore = settings.AoiMatchScore;
            hikVisionHelper.DataModel.AllowableErrorNumber = settings.AllowableErrorNumber;
            hikVisionHelper.DataModel.AOIScanStepX = settings.ScanStepX;
            hikVisionHelper.DataModel.AOIScanStepY = settings.ScanStepY;
            hikVisionHelper.DataModel.DieDimensionScanStep = settings.DieDimensionScanStep;
        }

        private static void ApplyWaferMap(SystemSettings settings, WaferMainViewModel waferMap)
        {
            if (waferMap?.DataModel == null)
                return;

            waferMap.DataModel.PhysicalReferencePosition = (settings.Motion.ProbeCenterX, settings.Motion.ProbeCenterY);
            waferMap.DataModel.ToleranceFactor = settings.WaferMap.ToleranceFactor;
            waferMap.DataModel.EnsureMapCoordinateTransformer();
        }
    }
}
