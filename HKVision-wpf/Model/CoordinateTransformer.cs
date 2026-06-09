using MyAsset.Wpf.Infrastructure;
using System;
using System.Collections.Generic;

namespace HKVision.Wpf.Model
{
    public class CoordinateTransformer
    {
        public double Tx { get; set; }
        public double Ty { get; set; }
        public double A11 { get; set; } = 211.6723;
        public double A22 { get; set; } = 222.1908;

        private int _centerX;
        private int _centerY;

        public CoordinateTransformer(int cameraX, int cameraY)
        {
            UpdateCameraSize(cameraX, cameraY);
        }

        public void UpdateCameraSize(int cameraX, int cameraY)
        {
            if (cameraX <= 0)
                throw new ArgumentOutOfRangeException(nameof(cameraX), "相机宽度必须大于 0。");
            if (cameraY <= 0)
                throw new ArgumentOutOfRangeException(nameof(cameraY), "相机高度必须大于 0。");

            _centerX = cameraX / 2;
            _centerY = cameraY / 2;
        }

        public Point2D Transform(Point2D pixelDie)
        {
            var physicalOffset = TransformOffset(pixelDie);
            return new Point2D
            {
                X = (float)(physicalOffset.X + Tx),
                Y = (float)(physicalOffset.Y + Ty)
            };
        }

        public Point2D TransformOffset(Point2D pixelDie)
        {
            double diffX = pixelDie.X - _centerX;
            double diffY = pixelDie.Y - _centerY;

            return new Point2D
            {
                X = (float)(-diffX / A11),
                Y = (float)(-diffY / A22)
            };
        }

        public double GetDeviationDistance(Point2D pixelDie)
        {
            var physicalOffset = TransformOffset(pixelDie);
            return Math.Sqrt(physicalOffset.X * physicalOffset.X + physicalOffset.Y * physicalOffset.Y);
        }

        public List<Point2D> TransformList(List<Point2D> pixelDies)
        {
            var physicalDies = new List<Point2D>(pixelDies.Count);
            foreach (var die in pixelDies)
                physicalDies.Add(Transform(die));
            return physicalDies;
        }

        public void CalculateRotationCenter(
            double x1,
            double y1,
            double x2,
            double y2,
            double x3,
            double y3,
            out double cx,
            out double cy,
            out double radius)
        {
            double a = 2 * (x1 * (y2 - y3) + x2 * (y3 - y1) + x3 * (y1 - y2));

            if (Math.Abs(a) < 1e-6)
            {
                cx = 0;
                cy = 0;
                radius = 0;
                return;
            }

            double s1 = x1 * x1 + y1 * y1;
            double s2 = x2 * x2 + y2 * y2;
            double s3 = x3 * x3 + y3 * y3;

            cx = Math.Round((s1 * (y2 - y3) + s2 * (y3 - y1) + s3 * (y1 - y2)) / a, 3);
            cy = Math.Round((s1 * (x3 - x2) + s2 * (x1 - x3) + s3 * (x2 - x1)) / a, 3);
            radius = Math.Sqrt((cx - x1) * (cx - x1) + (cy - y1) * (cy - y1));
        }
    }
}
