using System;
using System.Collections.Generic;
using MyAsset.Wpf.Infrastructure;

namespace WaferMap.Wpf.Model
{
    /// <summary>
    /// 按空间网格快速过滤重复 Die 点，主要用于盲扫过程中去除同一颗 Die 的重复识别结果。
    /// </summary>
    public class GridBasedDieRegistry
    {
        private readonly Dictionary<(int x, int y), List<Point2D>> _grid =
            new Dictionary<(int x, int y), List<Point2D>>();
        private readonly double _toleranceX;
        private readonly double _toleranceY;
        private readonly double _cellWidth;
        private readonly double _cellHeight;
        private readonly object _lock = new object();

        /// <summary>
        /// 使用 Die 尺寸和容差因子构建空间索引。容差因子必须在 (0,1) 范围内。
        /// </summary>
        public GridBasedDieRegistry(double dieWidth, double dieHeight, double toleranceFactor)
        {
            if (dieWidth <= 0)
                throw new ArgumentException("Die宽度必须大于0", nameof(dieWidth));
            if (dieHeight <= 0)
                throw new ArgumentException("Die高度必须大于0", nameof(dieHeight));
            if (toleranceFactor <= 0 || toleranceFactor >= 1)
                throw new ArgumentException("容差因子应在(0,1)范围内", nameof(toleranceFactor));

            _toleranceX = dieWidth * toleranceFactor;
            _toleranceY = dieHeight * toleranceFactor;
            _cellWidth = _toleranceX * 2.0;
            _cellHeight = _toleranceY * 2.0;
        }

        /// <summary>
        /// 尝试加入识别点；如果邻近网格中已有容差范围内的点，则认为是重复点。
        /// </summary>
        public bool TryAddAndCheckDuplicate(Point2D point)
        {
            var gridCell = CalculateGridCell(point);

            lock (_lock)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var neighborCell = (gridCell.x + dx, gridCell.y + dy);
                        if (!_grid.TryGetValue(neighborCell, out var candidates))
                            continue;

                        foreach (var existingPoint in candidates)
                        {
                            if (IsDuplicate(existingPoint, point))
                                return false;
                        }
                    }
                }

                if (!_grid.TryGetValue(gridCell, out var bucket))
                {
                    bucket = new List<Point2D>();
                    _grid[gridCell] = bucket;
                }

                bucket.Add(point);
                return true;
            }
        }

        /// <summary>
        /// 将物理坐标映射到去重索引用的粗网格。
        /// </summary>
        private (int x, int y) CalculateGridCell(Point2D point)
        {
            int gridX = (int)Math.Floor(point.X / _cellWidth);
            int gridY = (int)Math.Floor(point.Y / _cellHeight);
            return (gridX, gridY);
        }

        /// <summary>
        /// 按 X/Y 两个方向的容差判断两次识别是否属于同一颗 Die。
        /// </summary>
        private bool IsDuplicate(Point2D existingPoint, Point2D newPoint)
        {
            return Math.Abs(existingPoint.X - newPoint.X) <= _toleranceX &&
                   Math.Abs(existingPoint.Y - newPoint.Y) <= _toleranceY;
        }

        /// <summary>
        /// 从一批识别点中筛出新增的唯一点，同时会把这些点登记到历史索引中。
        /// </summary>
        public List<Point2D> FilterUnique(List<Point2D> points)
        {
            var result = new List<Point2D>(points.Count);
            foreach (var point in points)
            {
                if (TryAddAndCheckDuplicate(point))
                {
                    result.Add(point);
                }
            }
            return result;
        }
    }
}
