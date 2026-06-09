using MyAsset.Wpf.Infrastructure;
using ScottPlot;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using WaferMap.Wpf.Model;

namespace WaferMap.Wpf.ViewModels
{
    /// <summary>
    /// ScottPlot 自定义绘图对象，负责一次性绘制晶圆边界、Die、框选区域、选中十字和 Home 标记。
    /// </summary>
    internal sealed class WaferMapPlottable : IPlottable
    {
        private const double DieFillScale = 0.8d;
        private const float MinimumScaledDiePixels = 3f;

        private readonly WaferDataModel _dataModel;
        private readonly Func<IReadOnlyList<Point2D>> _getBoundary;
        private readonly Func<bool> _useBlindScanVisualStyle;

        public bool IsVisible { get; set; } = true;
        public IAxes Axes { get; set; }
        public IEnumerable<LegendItem> LegendItems => LegendItem.None;

        public WaferMapPlottable(
            WaferDataModel dataModel,
            Func<IReadOnlyList<Point2D>> getBoundary,
            Func<bool> useBlindScanVisualStyle)
        {
            _dataModel = dataModel;
            _getBoundary = getBoundary;
            _useBlindScanVisualStyle = useBlindScanVisualStyle;
        }

        public AxisLimits GetAxisLimits()
        {
            int mapLength = Math.Max(2, _dataModel.MapHalfLength);
            return new AxisLimits(-mapLength, mapLength, -mapLength, mapLength);
        }

        public void Render(RenderPack rp)
        {
            if (Axes == null)
                return;

            // 绘制顺序很重要：Die 在边界之后，操作覆盖层和 Home 标记在最上层。
            using var boundaryPath = CreateBoundaryPath();
            DrawBoundary(rp, boundaryPath);
            DrawDies(rp, boundaryPath);
            DrawOperationRegion(rp);
            DrawSelectedDieCrosshair(rp);
            DrawHomeMarker(rp);
        }

        private void DrawBoundary(RenderPack rp, SKPath boundaryPath)
        {
            if (boundaryPath == null)
                return;

            using var strokePaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                Color = ToSkColor(_dataModel.ColorManager.OutlineColor)
            };
            rp.Canvas.DrawPath(boundaryPath, strokePaint);
        }

        private void DrawDies(RenderPack rp, SKPath boundaryPath)
        {
            using var fillPaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill
            };

            var visibleLimits = GetVisibleLimits(rp);
            foreach (DieModel die in _dataModel.AllDies)
            {
                var fill = GetDieFillColor(die);
                if (fill.Alpha == 0)
                    continue;

                var dataRect = _dataModel.Geometry.GetDisplayDieRect(die);
                // 大图缩放时只绘制可见范围内的 Die，避免无意义的 Skia 绘制开销。
                if (!IntersectsVisibleLimits(dataRect, visibleLimits))
                    continue;

                fillPaint.Color = fill;
                var rect = ToScaledDiePixelRect(dataRect);
                DrawDieRect(rp, rect, fillPaint, boundaryPath, die.IsEdge);
            }
        }

        private static void DrawDieRect(RenderPack rp, SKRect rect, SKPaint paint, SKPath boundaryPath, bool clipToBoundary)
        {
            if (!clipToBoundary || boundaryPath == null)
            {
                rp.Canvas.DrawRect(rect, paint);
                return;
            }

            rp.Canvas.Save();
            try
            {
                // 边缘 Die 需要裁剪到晶圆边界内，避免显示超出晶圆轮廓。
                rp.Canvas.ClipPath(boundaryPath, SKClipOperation.Intersect, antialias: false);
                rp.Canvas.DrawRect(rect, paint);
            }
            finally
            {
                rp.Canvas.Restore();
            }
        }

        private SKPath CreateBoundaryPath()
        {
            var boundary = _getBoundary?.Invoke();
            if (boundary == null || boundary.Count < 3)
                return null;

            var path = new SKPath();
            var first = ToPixel(boundary[0].X, boundary[0].Y);
            path.MoveTo(first);

            for (int i = 1; i < boundary.Count; i++)
            {
                var pixel = ToPixel(boundary[i].X, boundary[i].Y);
                path.LineTo(pixel);
            }

            path.Close();
            return path;
        }

        private void DrawOperationRegion(RenderPack rp)
        {
            if (OperationRegionStrokeThickness <= 0)
                return;

            var rect = ToPixelRect(OperationRegionMinimumX, OperationRegionMaximumX, OperationRegionMinimumY, OperationRegionMaximumY);
            using var strokePaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = (float)OperationRegionStrokeThickness,
                Color = ToSkColor(_dataModel.ColorManager.RegionStrokeColor)
            };
            rp.Canvas.DrawRect(rect, strokePaint);
        }

        private void DrawSelectedDieCrosshair(RenderPack rp)
        {
            if (SelectedDieCrosshairStrokeThickness <= 0)
                return;

            var rect = ToPixelRect(
                SelectedDieCrosshairMinimumX,
                SelectedDieCrosshairMaximumX,
                SelectedDieCrosshairMinimumY,
                SelectedDieCrosshairMaximumY);
            float centerX = (rect.Left + rect.Right) / 2f;
            float centerY = (rect.Top + rect.Bottom) / 2f;

            using var strokePaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = (float)SelectedDieCrosshairStrokeThickness,
                Color = ToSkColor(_dataModel.ColorManager.SelectStrokeColor)
            };

            // 选中十字光标覆盖整个绘图控件，便于和影像视野/运动中心对齐。
            rp.Canvas.DrawLine(rp.DataRect.Left, centerY, rp.DataRect.Right, centerY, strokePaint);
            rp.Canvas.DrawLine(centerX, rp.DataRect.Top, centerX, rp.DataRect.Bottom, strokePaint);
        }

        private void DrawHomeMarker(RenderPack rp)
        {
            if (_dataModel.HomeDie == null)
                return;

            var displayCenter = _dataModel.Geometry.GetDisplayCenter(_dataModel.HomeDie);
            var center = ToPixel(displayCenter.X, displayCenter.Y);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Color = ToSkColor(_dataModel.ColorManager.HomeTextColor)
            };

            using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 18);
            var metrics = font.Metrics;
            float y = center.Y - (metrics.Ascent + metrics.Descent) / 2;
            rp.Canvas.DrawText("R", center.X, y, SKTextAlign.Center, font, paint);
        }

        public double OperationRegionMinimumX { get; set; }
        public double OperationRegionMaximumX { get; set; }
        public double OperationRegionMinimumY { get; set; }
        public double OperationRegionMaximumY { get; set; }
        public double OperationRegionStrokeThickness { get; set; }
        public double SelectedDieCrosshairMinimumX { get; set; }
        public double SelectedDieCrosshairMaximumX { get; set; }
        public double SelectedDieCrosshairMinimumY { get; set; }
        public double SelectedDieCrosshairMaximumY { get; set; }
        public double SelectedDieCrosshairStrokeThickness { get; set; }

        public bool IsOperationRegionEmpty =>
            OperationRegionMaximumX == 0 && OperationRegionMinimumX == 0 &&
            OperationRegionMaximumY == 0 && OperationRegionMinimumY == 0;

        private SKRect ToScaledDiePixelRect((double MinimumX, double MaximumX, double MinimumY, double MaximumY) rect)
        {
            var fullRect = ToPixelRect(rect.MinimumX, rect.MaximumX, rect.MinimumY, rect.MaximumY);
            if (fullRect.Width < MinimumScaledDiePixels || fullRect.Height < MinimumScaledDiePixels)
                return fullRect;

            // 正常缩放时留出间隙模拟街区；缩得很小时不再缩小，避免 Die 消失。
            float insetX = fullRect.Width * (float)(1d - DieFillScale) / 2f;
            float insetY = fullRect.Height * (float)(1d - DieFillScale) / 2f;
            return new SKRect(
                fullRect.Left + insetX,
                fullRect.Top + insetY,
                fullRect.Right - insetX,
                fullRect.Bottom - insetY);
        }

        private SKRect ToPixelRect(double minX, double maxX, double minY, double maxY)
        {
            var leftBottom = ToPixel(minX, minY);
            var rightTop = ToPixel(maxX, maxY);
            return new SKRect(
                Math.Min(leftBottom.X, rightTop.X),
                Math.Min(leftBottom.Y, rightTop.Y),
                Math.Max(leftBottom.X, rightTop.X),
                Math.Max(leftBottom.Y, rightTop.Y));
        }

        private SKPoint ToPixel(double x, double y)
        {
            Pixel pixel = Axes.GetPixel(new Coordinates(x, y));
            return new SKPoint(pixel.X, pixel.Y);
        }

        private SKColor GetDieFillColor(DieModel dieModel)
        {
            // 盲扫模式下，未待测且无结果的 Die 暂不显示，用于突出新采集/待测目标。
            if (_useBlindScanVisualStyle() && !dieModel.IsSelectedForTest && !dieModel.HasBin)
                return SKColors.Transparent;

            return ToSkColor(_dataModel.ColorManager.GetFinalRenderColor(
                dieModel.Attrs,
                dieModel.IsSelectedForTest,
                dieModel.BinCommand));
        }

        private static bool IntersectsVisibleLimits(
            (double MinimumX, double MaximumX, double MinimumY, double MaximumY) rect,
            AxisLimits limits)
        {
            return rect.MaximumX >= limits.Left &&
                   rect.MinimumX <= limits.Right &&
                   rect.MaximumY >= limits.Bottom &&
                   rect.MinimumY <= limits.Top;
        }

        private AxisLimits GetVisibleLimits(RenderPack rp)
        {
            double left = Axes.GetCoordinateX(rp.DataRect.Left);
            double right = Axes.GetCoordinateX(rp.DataRect.Right);
            double bottom = Axes.GetCoordinateY(rp.DataRect.Bottom);
            double top = Axes.GetCoordinateY(rp.DataRect.Top);

            return new AxisLimits(
                Math.Min(left, right),
                Math.Max(left, right),
                Math.Min(bottom, top),
                Math.Max(bottom, top));
        }

        private static SKColor ToSkColor(System.Drawing.Color color)
        {
            return new SKColor(color.R, color.G, color.B, color.A);
        }
    }
}
