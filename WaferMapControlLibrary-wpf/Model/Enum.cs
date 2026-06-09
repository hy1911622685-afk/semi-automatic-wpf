using System;
using System.ComponentModel;

namespace WaferMap.Wpf.Model
{
    /// <summary>
    /// 晶圆图鼠标操作模式。
    /// </summary>
    public enum WaferMapOperationalEnum
    {
        /// <summary>
        /// 普通移动/点选模式。
        /// </summary>
        Move,

        /// <summary>
        /// 待测/跳过选择模式。
        /// </summary>
        SelectorDeselect,

        /// <summary>
        /// 手动添加/删除 Die 模式。
        /// </summary>
        AddorRemove,
    }

    /// <summary>
    /// 晶圆图当前场景类型。
    /// </summary>
    public enum WaferMapSource
    {
        /// <summary>
        /// 普通晶圆布局。
        /// </summary>
        LayoutGenerated,

        /// <summary>
        /// 通过影像视野动态采集 Die 的盲扫模式。
        /// </summary>
        VisionScanned,
    }

    /// <summary>
    /// 逻辑轴方向配置，主要用于界面和移动趋势语义。
    /// </summary>
    public enum AxisOrientationEnum
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>
    /// 待测队列的移动排序策略。
    /// </summary>
    public enum MoveTrendEnum
    {
        /// <summary>
        /// 按行从上到下蛇形移动。
        /// </summary>
        SnakeTopToBottom,

        /// <summary>
        /// 按列从左到右蛇形移动。
        /// </summary>
        SnakeLeftToRight,

        /// <summary>
        /// 按列从左到右移动。
        /// </summary>
        LeftToRight,

        /// <summary>
        /// 按行从上到下移动。
        /// </summary>
        UpToDown
    }

    /// <summary>
    /// Die 的形态属性。待测状态和测试结果不放在这里。
    /// </summary>
    [Flags]
    public enum DieAttributes : byte
    {
        /// <summary>
        /// Die 是否启用。
        /// </summary>
        IsEnabled = 1 << 0,

        /// <summary>
        /// Die 是否位于晶圆边缘。
        /// </summary>
        IsEdge = 1 << 1,
    }

    /// <summary>
    /// 手动添加 Die 时，网格相对晶圆边界的包含关系。
    /// </summary>
    public enum CellContainment
    {
        Outside = 0,
        Partial = 1,
        Inside = 2
    }

    public enum MapType : byte
    {
        [Description("缺口")]
        Notch = 0,
        [Description("平边")]
        Flat = 1
    }

    public enum GapDirection : int
    {
        Right = 0,
        Top = 90,
        Left = 180,
        Bottom = 270
    }
}
