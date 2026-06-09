using MyAsset.Wpf.Infrastructure;
using ScottPlot.WPF;
using System;
using System.Collections.Generic;
using WaferMap.Wpf.Model;

namespace WaferMap.Wpf.ViewModels
{
    public partial class WaferRenderer
    {
        //限制最小缩放级别，防止过度缩放导致性能问题或显示异常
        private const double MinimumViewportSpanFactor = 8d;
        //限制最大缩放级别，防止过度缩放导致性能问题或显示异常
        private const double MaximumViewportSpanFactor = 180d;

        private readonly WaferDataModel _dataModel;
        private WpfPlot _myPlotView;
        private bool _useBlindScanVisualStyle;
        private List<Point2D> _boundary = new List<Point2D>();
        //绘图对象
        private WaferMapPlottable _waferPlottable;

        public WaferRenderer(WaferDataModel dataModel)
        {
            _dataModel = dataModel;

            _dataModel.OnSingleDieChanged += HandleSingleDieChanged;
            _dataModel.OnBatchUpdateCompleted += HandleBatchCompleted;
            _dataModel.RefreshRequested += RefreshPlot;
        }

        public bool HasPlotView => _myPlotView != null;

        internal void AttachPlotView(WpfPlot plotView)
        {
            _myPlotView = plotView;
        }

        internal bool BuildPlot(bool isRebuildFromData)
        {
            if (!HasPlotView)
                return false;

            _useBlindScanVisualStyle = false;
            PreparePlot(isRebuildFromData);
            DrawBoundaryAndDies(_dataModel.Radius, isRebuildFromData);
            BindPlotAndRefresh();
            return true;
        }

        private void PreparePlot(bool isRebuildFromData)
        {
            ResetMap(isRebuildFromData);
            ConfigurePlot();
            CreateWaferPlottable();
            InitializeOverlayRegions();
        }

        private void ConfigurePlot()
        {
            var plot = _myPlotView.Plot;
            plot.Clear();
            plot.FigureBackground.Color = ScottPlot.Colors.Transparent;
            plot.DataBackground.Color = ScottPlot.Colors.Transparent;
            plot.HideAxesAndGrid();
            int radius = _dataModel.Radius;
            plot.Axes.SetLimits(-radius, radius, -radius, radius);
        }

        private void CreateWaferPlottable()
        {
            var plot = _myPlotView.Plot;
            _waferPlottable = new WaferMapPlottable(
                _dataModel,
                () => _boundary,
                () => _useBlindScanVisualStyle);

            _waferPlottable.Axes = new ScottPlot.Axes();
            plot.PlottableList.Add(_waferPlottable);
        }

        private void BindPlotAndRefresh()
        {
            ApplyDefaultViewport();
            RefreshPlot();
        }

        private void ApplyDefaultViewport()
        {
            if (_myPlotView == null)
                return;

            var axes = _myPlotView.Plot.Axes;
            axes.AutoScale();
            axes.SquareUnits(false);
            axes.SquareUnits(true);
        }

        private void RefreshPlot()
        {
            _myPlotView?.Refresh();
        }

        private void DrawBoundaryAndDies(int radius, bool isRebuildFromData)
        {
            // 普通建图时会根据晶圆形态重新生成 Die；从数据重建时只复用已有 Die 数据。
            if (_dataModel.MapType == MapType.Notch)
            {
                BuildNotchBoundary(radius, (double)_dataModel.GapAngle, _dataModel.GapDepth);
                DrawNotchPlotDie(isRebuildFromData);
            }
            else
            {
                BuildFlatBoundary(radius, _dataModel.GapAngle, _dataModel.GapDepth);
                DrawFlatPlotDie(isRebuildFromData);
            }
        }

        private void DrawNotchPlotDie(bool isRebuildFromData)
        {
            if (isRebuildFromData)
            {
                DrawDieFromData();
                return;
            }

            int index = 0;
            var bounds = GetLogicalGridBounds();

            for (int gridY = bounds.maxGridY; gridY >= bounds.minGridY; gridY--)
            {
                for (int gridX = bounds.minGridX; gridX <= bounds.maxGridX; gridX++)
                {
                    var corners = _dataModel.Geometry.GetGridCorners(gridX, gridY);
                    int insideCount = 0;

                    // 四个角都在晶圆内是完整 Die；只有部分角在晶圆内则作为边缘 Die。
                    foreach (var corner in corners)
                    {
                        if (IsPointInPolygon(new Point2D(corner.X, corner.Y), _boundary))
                            insideCount++;
                    }

                    if (insideCount == 4)
                    {
                        AddCalculatedDie(gridX, gridY, index++, false);
                    }
                    else if (insideCount > 0)
                    {
                        AddCalculatedDie(gridX, gridY, index++, true);
                    }
                }
            }
        }

        public void BuildNotchBoundary(double radius, double notchAngle, double notchRadius)
        {
            _boundary = GenerateAdaptiveWaferPolygon(radius, notchAngle, notchRadius);
        }

        /// <summary>
        /// 生成带缺口的晶圆边界多边形；缺口使用额外采样点保证边界显示平滑。
        /// </summary>
        public static List<Point2D> GenerateAdaptiveWaferPolygon(double radius, double notchAngle = 0.0, double notchRadius = 4.0)
        {
            var points = new List<Point2D>();
            double notchAngleRad = notchAngle * Math.PI / 180.0;

            double cosThetaMax = 1.0 - (notchRadius * notchRadius) / (2.0 * radius * radius);
            double thetaMax = Math.Acos(cosThetaMax);

            double startAngle = notchAngleRad + thetaMax;
            double endAngle = notchAngleRad + 2 * Math.PI - thetaMax;

            double sweep = endAngle - startAngle;
            int mainArcSteps = (int)Math.Ceiling(sweep / (Math.PI / 180.0));
            double exactMainStepRad = sweep / mainArcSteps;

            for (int i = 0; i <= mainArcSteps; i++)
            {
                double a = startAngle + i * exactMainStepRad;
                points.Add(new Point2D(radius * Math.Cos(a), radius * Math.Sin(a)));
            }

            int notchSteps = 200;
            double notchSweep = 2 * thetaMax;
            double notchStepRad = notchSweep / notchSteps;

            for (int i = 1; i < notchSteps; i++)
            {
                double relativeAngle = -thetaMax + i * notchStepRad;

                double rSin = radius * Math.Sin(relativeAngle);
                double innerTerm = notchRadius * notchRadius - rSin * rSin;
                innerTerm = innerTerm < 0 ? 0 : innerTerm;

                double r = radius * Math.Cos(relativeAngle) - Math.Sqrt(innerTerm);
                double absoluteAngle = notchAngleRad + relativeAngle;

                points.Add(new Point2D(r * Math.Cos(absoluteAngle), r * Math.Sin(absoluteAngle)));
            }

            return points;
        }

        public static bool IsPointInPolygon(Point2D p, List<Point2D> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if (((poly[i].Y > p.Y) != (poly[j].Y > p.Y)) &&
                    (p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private void DrawFlatPlotDie(bool isRebuildFromData)
        {
            if (isRebuildFromData)
            {
                DrawDieFromData();
                return;
            }

            int index = 0;
            var bounds = GetLogicalGridBounds();

            for (int gridY = bounds.maxGridY; gridY >= bounds.minGridY; gridY--)
            {
                for (int gridX = bounds.minGridX; gridX <= bounds.maxGridX; gridX++)
                {
                    var corners = _dataModel.Geometry.GetGridCorners(gridX, gridY);
                    int insideCount = 0;

                    // 平边晶圆同样使用角点数量区分完整 Die、边缘 Die 和晶圆外格子。
                    foreach (var corner in corners)
                    {
                        if (IsPhysicalPointInsideFlatWafer(corner.X, corner.Y))
                            insideCount++;
                    }

                    if (insideCount == 4)
                    {
                        AddCalculatedDie(gridX, gridY, index++, false);
                    }
                    else if (insideCount > 0)
                    {
                        AddCalculatedDie(gridX, gridY, index++, true);
                    }
                }
            }
        }

        private void BuildFlatBoundary(int radius, GapDirection gapAngle, int gapDepth)
        {
            double flatDistance = radius * Convert.ToDouble(1 - gapDepth / 100d);

            _boundary.Clear();
            var centerX = _dataModel.CenterPoint.X;
            var centerY = _dataModel.CenterPoint.Y;

            for (int i = 0; i <= 360; i++)
            {
                double angle = i * Math.PI / 180.0;
                double px = centerX + radius * Math.Cos(angle);
                double py = centerY + radius * Math.Sin(angle);

                // 根据平边方向把圆上的点裁剪到指定平边位置。
                switch (gapAngle)
                {
                    case GapDirection.Right:
                        if (px > centerX + flatDistance) px = centerX + flatDistance;
                        break;
                    case GapDirection.Left:
                        if (px < centerX - flatDistance) px = centerX - flatDistance;
                        break;
                    case GapDirection.Top:
                        if (py > centerY + flatDistance) py = centerY + flatDistance;
                        break;
                    case GapDirection.Bottom:
                        if (py < centerY - flatDistance) py = centerY - flatDistance;
                        break;
                }

                _boundary.Add(new Point2D(px, py));
            }
        }

        private void HandleSingleDieChanged(DieModel die)
        {
            RefreshPlot();
        }

        private void HandleBatchCompleted()
        {
            RefreshPlot();
        }

        private void ResetMap(bool isRebuildFromData)
        {
            if (!isRebuildFromData)
            {
                // 只有完整重新建图才清空 Die 和 Home；从已有数据刷新时必须保留业务状态。
                _dataModel.AllDies.Clear();
                _dataModel.HomeDie = null;
            }

            _boundary.Clear();
            _waferPlottable = null;
        }

        public void UpdateHomeDieDisplay(DieModel dieModel = null)
        {
            if (dieModel != null)
                _dataModel.HomeDie = dieModel;

            RefreshPlot();
        }

        public void AutoZoom(double scale = 0)
        {
            if (_myPlotView == null)
                return;

            if (scale == 0)
            {
                ApplyDefaultViewport();
            }
            else
            {
                var limits = _myPlotView.Plot.Axes.GetLimits();
                _myPlotView.Plot.Axes.SetLimits(limits.WithZoom(scale, scale));
            }

            EnforceViewportZoomLimits();
            RefreshPlot();
        }

        public bool EnforceViewportZoomLimits()
        {
            if (_myPlotView == null)
                return false;

            var limits = _myPlotView.Plot.Axes.GetLimits();
            // 缩放上下限按 Pitch 计算，避免缩到单颗 Die 以下或放大到绘制过多无效区域。
            double minSpanX = Math.Max(_dataModel.SafePitchX * MinimumViewportSpanFactor, _dataModel.DieWidth);
            double minSpanY = Math.Max(_dataModel.SafePitchY * MinimumViewportSpanFactor, _dataModel.DieHeight);
            double maxSpanX = Math.Max(minSpanX, _dataModel.SafePitchX * MaximumViewportSpanFactor);
            double maxSpanY = Math.Max(minSpanY, _dataModel.SafePitchY * MaximumViewportSpanFactor);
            double spanX = limits.Right - limits.Left;
            double spanY = limits.Top - limits.Bottom;
            double targetSpanX = Math.Min(Math.Max(spanX, minSpanX), maxSpanX);
            double targetSpanY = Math.Min(Math.Max(spanY, minSpanY), maxSpanY);

            if (Math.Abs(targetSpanX - spanX) < double.Epsilon &&
                Math.Abs(targetSpanY - spanY) < double.Epsilon)
                return false;

            double centerX = (limits.Left + limits.Right) / 2d;
            double centerY = (limits.Bottom + limits.Top) / 2d;

            _myPlotView.Plot.Axes.SetLimits(
                centerX - targetSpanX / 2d,
                centerX + targetSpanX / 2d,
                centerY - targetSpanY / 2d,
                centerY + targetSpanY / 2d);
            return true;
        }

        public void SetAxisVisible(bool isVisible)
        {
            if (_myPlotView == null)
                return;

            if (isVisible)
                _myPlotView.Plot.ShowAxesAndGrid();
            else
                _myPlotView.Plot.HideAxesAndGrid();

            RefreshPlot();
        }

        public void ConfigureInteractionMode(WaferMapOperationalEnum mode)
        {
        }

        private void DrawDieFromData()
        {
            RefreshPlot();
        }

        internal void RebuildFromCurrentData()
        {
            var viewportState = CaptureViewportState();

            BuildPlot(true);

            // 手动添加/删除后重建图层，但保持用户当前视口不跳动。
            RestoreViewportState(viewportState);
        }

        internal void RebuildBlindScanFromCurrentData()
        {
            var viewportState = CaptureViewportState();

            BuildBlindScanPlot(true);

            RestoreViewportState(viewportState);
        }

        private ScottPlot.AxisLimits? CaptureViewportState()
        {
            return _myPlotView?.Plot.Axes.GetLimits();
        }

        private void RestoreViewportState(ScottPlot.AxisLimits? viewportState)
        {
            if (_myPlotView == null || !viewportState.HasValue)
                return;

            _myPlotView.Plot.Axes.SetLimits(viewportState.Value);
            RefreshPlot();
        }

        internal bool TryGetGridCell(Point2D dataPoint, out (int GridX, int GridY) gridCell)
        {
            int gridX = (int)Math.Round(dataPoint.X / _dataModel.SafePitchX, MidpointRounding.AwayFromZero);
            int gridY = (int)Math.Round(dataPoint.Y / _dataModel.SafePitchY, MidpointRounding.AwayFromZero);
            var rect = GetGridCellRect(gridX, gridY);

            // 先四舍五入定位候选格，再用矩形范围确认鼠标确实落在该格内。
            if (dataPoint.X < rect.MinimumX || dataPoint.X > rect.MaximumX ||
                dataPoint.Y < rect.MinimumY || dataPoint.Y > rect.MaximumY)
            {
                gridCell = default;
                return false;
            }

            gridCell = (gridX, gridY);
            return true;
        }

        internal List<(int GridX, int GridY)> GetGridCellsInRegion(OperationRegion operationRegion)
        {
            var cells = new List<(int GridX, int GridY)>();
            if (operationRegion is null)
                return cells;

            int minGridX = (int)Math.Floor((operationRegion.MinimumX - _dataModel.DieHalfWidth) / _dataModel.SafePitchX);
            int maxGridX = (int)Math.Ceiling((operationRegion.MaximumX + _dataModel.DieHalfWidth) / _dataModel.SafePitchX);
            int minGridY = (int)Math.Floor((operationRegion.MinimumY - _dataModel.DieHalfHeight) / _dataModel.SafePitchY);
            int maxGridY = (int)Math.Ceiling((operationRegion.MaximumY + _dataModel.DieHalfHeight) / _dataModel.SafePitchY);

            // 框选命中按相交计算，不要求格子完全包含在框内。
            for (int gridY = minGridY; gridY <= maxGridY; gridY++)
            {
                for (int gridX = minGridX; gridX <= maxGridX; gridX++)
                {
                    var rect = GetGridCellRect(gridX, gridY);
                    if (rect.MaximumX >= operationRegion.MinimumX &&
                        rect.MinimumX <= operationRegion.MaximumX &&
                        rect.MaximumY >= operationRegion.MinimumY &&
                        rect.MinimumY <= operationRegion.MaximumY)
                    {
                        cells.Add((gridX, gridY));
                    }
                }
            }

            return cells;
        }

        [Obsolete("Use GetGridCellsInRegion instead.")]
        internal List<(int GridX, int GridY)> GetGridCellsInRectangle(OperationRegion operationRegion) =>
            GetGridCellsInRegion(operationRegion);

        internal CellContainment ClassifyManualCell(int gridX, int gridY)
        {
            EnsureBoundaryData();
            int insideCount = GetInsideCornerCount(gridX, gridY);

            // 手动补 Die 时需要区分完整格子、边缘格子和晶圆外格子。
            if (insideCount == 4)
                return CellContainment.Inside;

            if (insideCount > 0)
                return CellContainment.Partial;

            return CellContainment.Outside;
        }

        private (int minGridX, int maxGridX, int minGridY, int maxGridY) GetLogicalGridBounds()
        {
            int maxGridX = (int)Math.Ceiling(_dataModel.UiRadiusX) + 1;
            int maxGridY = (int)Math.Ceiling(_dataModel.UiRadiusY) + 1;

            return (-maxGridX, maxGridX, -maxGridY, maxGridY);
        }

        private void AddCalculatedDie(int gridX, int gridY, int index, bool isEdge)
        {
            var dieModel = new DieModel
            {
                Index = -1,
                GridX = gridX,
                GridY = gridY,
                Attrs = isEdge
                    ? DieAttributes.IsEdge | DieAttributes.IsEnabled
                    : DieAttributes.IsEnabled,
                IsSelectedForTest = !isEdge
            };

            _dataModel.AllDies.Add(dieModel);

            // 默认把逻辑原点的完整 Die 作为 Home Die。
            if (!isEdge && gridX == 0 && gridY == 0)
                UpdateHomeDieDisplay(dieModel);
        }

        private bool IsPhysicalPointInsideFlatWafer(double px, double py)
        {
            var centerX = _dataModel.CenterPoint.X;
            var centerY = _dataModel.CenterPoint.Y;
            double flatDistance = _dataModel.Radius * Convert.ToDouble(1 - _dataModel.GapDepth / 100d);

            double distance = Math.Sqrt(Math.Pow(px - centerX, 2) + Math.Pow(py - centerY, 2));
            if (distance > _dataModel.Radius)
                return false;

            switch (_dataModel.GapAngle)
            {
                case GapDirection.Right:
                    return px <= centerX + flatDistance;
                case GapDirection.Left:
                    return px >= centerX - flatDistance;
                case GapDirection.Top:
                    return py <= centerY + flatDistance;
                case GapDirection.Bottom:
                    return py >= centerY - flatDistance;
                default:
                    return true;
            }
        }

        private int GetInsideCornerCount(int gridX, int gridY)
        {
            int insideCount = 0;
            var corners = _dataModel.Geometry.GetGridCorners(gridX, gridY);

            foreach (var corner in corners)
            {
                bool isInside = _dataModel.MapType == MapType.Notch
                    ? IsPointInPolygon(new Point2D(corner.X, corner.Y), _boundary)
                    : IsPhysicalPointInsideFlatWafer(corner.X, corner.Y);

                if (isInside)
                    insideCount++;
            }

            return insideCount;
        }

        private void EnsureBoundaryData()
        {
            if (_dataModel.MapType != MapType.Notch || _boundary.Count > 0)
                return;

            _boundary = GenerateAdaptiveWaferPolygon(_dataModel.Radius, (double)_dataModel.GapAngle, _dataModel.GapDepth);
        }

        private void PaintDieToGrid(DieModel dieModel)
        {
            RefreshPlot();
        }

        internal bool TryGetDieRect(DieModel dieModel, out OperationRegion rect)
        {
            if (dieModel == null)
            {
                rect = default;
                return false;
            }

            rect = GetGridCellRect(dieModel.GridX, dieModel.GridY);
            return true;
        }

        internal OperationRegion GetGridCellRect(int gridX, int gridY)
        {
            var rect = _dataModel.Geometry.GetGridDieRect(gridX, gridY);
            return new OperationRegion(
                rect.MinimumX,
                rect.MaximumX,
                rect.MinimumY,
                rect.MaximumY);
        }

        internal Point2D ToMapPoint(System.Windows.Point screenPoint)
        {
            if (_myPlotView == null)
                return default;

            var coords = _myPlotView.Plot.GetCoordinates(
                (float)screenPoint.X,
                (float)screenPoint.Y,
                _myPlotView.Plot.Axes.Bottom,
                _myPlotView.Plot.Axes.Left);

            return new Point2D(coords.X, coords.Y);
        }
    }

    public class OperationRegion
    {
        public OperationRegion()
        {
        }

        public OperationRegion(double minimumX, double maximumX, double minimumY, double maximumY)
        {
            MinimumX = minimumX;
            MaximumX = maximumX;
            MinimumY = minimumY;
            MaximumY = maximumY;
        }

        public double MinimumX { get; set; }
        public double MaximumX { get; set; }
        public double MinimumY { get; set; }
        public double MaximumY { get; set; }
        public double StrokeThickness { get; set; }
    }

    [Obsolete("Use OperationRegion instead.")]
    public sealed class SelectionRect : OperationRegion
    {
        public SelectionRect()
        {
        }

        public SelectionRect(double minimumX, double maximumX, double minimumY, double maximumY)
            : base(minimumX, maximumX, minimumY, maximumY)
        {
        }
    }
}
