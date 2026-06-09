using System;

namespace MotionCard.Model
{
    public class WaferAlignmentCompensation
    {
        public double OffsetX { get; private set; }
        public double OffsetY { get; private set; }
        public bool IsCalibrated { get; private set; }

        public void CalibrateFromTwoPoints(double x0, double y0, double x180, double y180)
        {
            OffsetX = (x0 - x180) / 2.0;
            OffsetY = (y0 - y180) / 2.0;
            IsCalibrated = true;
        }

        public void CalibrateFromFourPoints(
            double x0,
            double y0,
            double x90,
            double y90,
            double x180,
            double y180,
            double x270,
            double y270)
        {
            double offsetX1 = (x0 - x180) / 2.0;
            double offsetY1 = (y0 - y180) / 2.0;
            double offsetX2 = (y270 - y90) / 2.0;
            double offsetY2 = (x90 - x270) / 2.0;

            OffsetX = (offsetX1 + offsetX2) / 2.0;
            OffsetY = (offsetY1 + offsetY2) / 2.0;
            IsCalibrated = true;
        }

        public (double X, double Y) CompensatePosition(double targetX, double targetY, double angleDegrees)
        {
            if (!IsCalibrated)
                throw new InvalidOperationException("请先执行校准");

            double angleRad = angleDegrees * Math.PI / 180.0;
            double cosTheta = Math.Cos(angleRad);
            double sinTheta = Math.Sin(angleRad);

            double compensatedX = targetX + OffsetX * cosTheta - OffsetY * sinTheta;
            double compensatedY = targetY + OffsetX * sinTheta + OffsetY * cosTheta;

            return (compensatedX, compensatedY);
        }

        public double GetEccentricity()
        {
            return Math.Sqrt(OffsetX * OffsetX + OffsetY * OffsetY);
        }

        public double VerifyCalibration(double angleDegrees, double measuredX, double measuredY)
        {
            var (expectedX, expectedY) = CompensatePosition(0, 0, angleDegrees);
            double errorX = measuredX - expectedX;
            double errorY = measuredY - expectedY;
            return Math.Sqrt(errorX * errorX + errorY * errorY);
        }
    }
}
