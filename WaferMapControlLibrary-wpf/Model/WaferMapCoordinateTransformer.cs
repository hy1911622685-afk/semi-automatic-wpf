using Newtonsoft.Json;
using System;

namespace WaferMap.Wpf.Model
{
    public sealed class WaferMapCoordinateTransformer
    {
        /// <summary>
        /// Map 坐标 X 递增方向与轴 X 递增方向的关系；同向为 1，反向为 -1。
        /// </summary>
        [JsonIgnore]
        public double MapToAxisDirectionX { get; set; } = -1d;

        /// <summary>
        /// Map 坐标 Y 递增方向与轴 Y 递增方向的关系；同向为 1，反向为 -1。
        /// </summary>
        [JsonIgnore]
        public double MapToAxisDirectionY { get; set; } = 1d;

        /// <summary>
        /// 将两颗 Die 之间的 Map 逻辑位移转换为 XY 轴相对移动量。
        /// </summary>
        public (double X, double Y)? DieToAxisMoveOffset(
            DieModel source,
            DieModel target,
            double pitchX,
            double pitchY)
        {
            if (source == null || target == null)
                return null;

            double deltaMapX = (target.GridX - source.GridX) * pitchX;
            double deltaMapY = (target.GridY - source.GridY) * pitchY;

            return (
                deltaMapX * NormalizeDirection(MapToAxisDirectionX),
                deltaMapY * NormalizeDirection(MapToAxisDirectionY));
        }

        /// <summary>
        /// 以已同步的 Home Die 为基准，计算目标 Die 的 XY 轴绝对坐标。
        /// </summary>
        public (double X, double Y)? DieToPhysicalPosition(
            DieModel homeDie,
            DieModel target,
            double pitchX,
            double pitchY,
            (double X, double Y) physicalHomePosition)
        {
            var offset = DieToAxisMoveOffset(homeDie, target, pitchX, pitchY);
            if (offset == null)
                return null;

            return (
                physicalHomePosition.X + offset.Value.X,
                physicalHomePosition.Y + offset.Value.Y);
        }

        /// <summary>
        /// 将 XY 轴物理坐标转换为 Map 显示坐标。
        /// </summary>
        public (double X, double Y) PhysicalToMapDisplayPoint(
            double physicalX,
            double physicalY,
            (double X, double Y) physicalHomePosition)
        {
            return (
                NormalizeDirection(MapToAxisDirectionX) * (physicalX - physicalHomePosition.X),
                NormalizeDirection(MapToAxisDirectionY) * (physicalY - physicalHomePosition.Y));
        }

        /// <summary>
        /// 将轴坐标系下的视野矩形转换为 Map 显示范围。
        /// </summary>
        public (double MinimumX, double MaximumX, double MinimumY, double MaximumY) PhysicalToMapDisplayBounds(
            double physicalLeft,
            double physicalRight,
            double physicalBottom,
            double physicalTop,
            (double X, double Y) physicalHomePosition)
        {
            var first = PhysicalToMapDisplayPoint(
                physicalLeft,
                physicalBottom,
                physicalHomePosition);

            var second = PhysicalToMapDisplayPoint(
                physicalRight,
                physicalTop,
                physicalHomePosition);

            return (
                Math.Min(first.X, second.X),
                Math.Max(first.X, second.X),
                Math.Min(first.Y, second.Y),
                Math.Max(first.Y, second.Y));
        }

        private static double NormalizeDirection(double direction)
        {
            return direction < 0 ? -1d : 1d;
        }

    }
}
