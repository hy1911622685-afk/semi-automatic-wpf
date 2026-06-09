using System;

namespace MotionCard.Model
{
    public class WaferCompensation
    {
        public double OffsetX { get; private set; }
        public double OffsetY { get; private set; }
        public bool IsCalibrated { get; private set; }
        public double ReferenceX { get; private set; }
        public double ReferenceY { get; private set; }
        public double ReferenceRadius { get; private set; }

        public void Calibrate(
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

        public void SetReference(double x, double y, double radius)
        {
            ReferenceX = x;
            ReferenceY = y;
            ReferenceRadius = radius;
        }

        public (double X, double Y) CalculateProbePosition(int ringNumber, int dieIndex, double cumulativeAngle)
        {
            if (!IsCalibrated)
                throw new InvalidOperationException("请先执行校准");

            double targetRadius = ringNumber * 10.415;
            double deltaAngleRad = dieIndex * 6.0 * Math.PI / 180.0;
            double cumulativeAngleRad = cumulativeAngle * Math.PI / 180.0;

            double dieX = targetRadius * Math.Cos(deltaAngleRad);
            double dieY = targetRadius * Math.Sin(deltaAngleRad);

            double cosTheta = Math.Cos(cumulativeAngleRad);
            double sinTheta = Math.Sin(cumulativeAngleRad);

            double rotatedX = dieX * cosTheta - dieY * sinTheta;
            double rotatedY = dieX * sinTheta + dieY * cosTheta;

            double actualX = rotatedX + OffsetX * cosTheta - OffsetY * sinTheta;
            double actualY = rotatedY + OffsetX * sinTheta + OffsetY * cosTheta;

            double probeX = ReferenceX + (actualX - ReferenceRadius);
            double probeY = ReferenceY + actualY;

            return (probeX, probeY);
        }

        public double GetEccentricity()
        {
            return Math.Sqrt(OffsetX * OffsetX + OffsetY * OffsetY);
        }

        public double VerifyCompensation(
            int ringNumber,
            int dieIndex,
            double cumulativeAngle,
            double measuredX,
            double measuredY)
        {
            var (expectedX, expectedY) = CalculateProbePosition(ringNumber, dieIndex, cumulativeAngle);
            double errorX = measuredX - expectedX;
            double errorY = measuredY - expectedY;
            return Math.Sqrt(errorX * errorX + errorY * errorY);
        }
    }
}
