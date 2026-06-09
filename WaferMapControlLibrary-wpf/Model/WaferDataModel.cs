using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;

namespace WaferMap.Wpf.Model
{
    /// <summary>
    /// 晶圆图的核心数据模型，保存 Die 列表、几何参数、显示颜色和坐标转换配置。
    /// </summary>
    [Serializable]
    public class WaferDataModel
    {
        /// <summary>
        /// 当前晶圆图中的全部 Die。这里包含启用、禁用、边缘、待测和已有结果的 Die。
        /// </summary>
        public BindingList<DieModel> AllDies { get; set; } = new BindingList<DieModel>();

        /// <summary>
        /// 负责根据测试结果、待测状态和形态计算最终显示颜色。
        /// </summary>
        public ColorManager ColorManager { get; set; } = new ColorManager();

        [field: NonSerialized]
        public event Action RefreshRequested;

        [JsonIgnore]
        public readonly double RectRadius = 0.5d;

        [JsonIgnore]
        public readonly System.Windows.Point CenterPoint = new System.Windows.Point(0, 0);

        public int OffsetNum { get; set; } = 0;
        public int SkipNum { get; set; } = 2;
        public double DieWidth { get; set; } = 1d;
        public double DieHeight { get; set; } = 1d;
        public double StreetWidth { get; set; } = 0d;
        public double StreetHeight { get; set; } = 0d;

        /// <summary>
        /// 逻辑 Home Die，用于建立 Map 坐标与实际轴坐标之间的基准关系。
        /// </summary>
        public DieModel HomeDie { get; set; }

        /// <summary>
        /// Home Die 对应的实际 XY 轴坐标。
        /// </summary>
        public (double X, double Y) PhysicalReferencePosition { get; set; }
        /// <summary>
        /// wafer尺寸，单位英寸；仅用于计算地图显示范围和坐标转换，不直接限制 Die 的分布范围。
        /// </summary>
        public int WaferSize { get; set; } = 4;
        /// <summary>
        /// 缺口深度，单位 Die 行列数；仅用于计算地图显示范围和坐标转换，不直接限制 Die 的分布范围。
        /// </summary>
        public int GapDepth { get; set; } = 2;
        /// <summary>
        /// 位置容差因子，0-1 之间；用于计算地图显示范围和坐标转换时在边界上增加额外的容差空间，避免 Die 紧贴边界导致显示和交互问题。
        /// </summary>
        public double ToleranceFactor { get; set; } = 0.3d;
        /// <summary>
        /// 缺口方向，决定了地图显示时缺口的位置和坐标转换的基准；不同缺口方向的地图在同一坐标系下可能具有不同的 Die 分布范围和显示效果。
        /// </summary>
        public GapDirection GapAngle { get; set; } = GapDirection.Bottom;
        /// <summary>
        /// wafer 图的类型，决定了显示样式和坐标转换方式；不同类型的地图在同一坐标系下可能具有不同的 Die 分布范围和显示效果。
        /// </summary>
        public MapType MapType { get; set; } = MapType.Flat;
        /// <summary>
        /// wafer 图的坐标来源，决定了坐标转换的基准和方式；不同来源的地图在同一坐标系下可能具有不同的 Die 分布范围和显示效果。
        /// </summary>
        public WaferMapSource MapSource { get; set; } = WaferMapSource.LayoutGenerated;
        /// <summary>
        /// 坐标轴的朝向，决定了地图显示时坐标轴的位置和坐标转换的基准；不同朝向的地图在同一坐标系下可能具有不同的 Die 分布范围和显示效果。
        /// </summary>
        public AxisOrientationEnum AxisOrientation { get; set; } = AxisOrientationEnum.TopRight;
        /// <summary>
        /// 测试时 Die 的移动顺序，决定了批量测试时 Die 状态更新和显示刷新的顺序；不同移动顺序的地图在同一坐标系下可能具有不同的 Die 分布范围和显示效果。
        /// </summary>
        public MoveTrendEnum MoveTrendMode { get; set; } = MoveTrendEnum.SnakeTopToBottom;

        /// <summary>
        /// Map 坐标与轴坐标之间的转换器，Die 间移动必须统一经过这里。
        /// </summary>
        public WaferMapCoordinateTransformer MapCoordinateTransformer { get; set; } = new WaferMapCoordinateTransformer();
        /// <summary>
        /// 是否已经将 Home Die 与物理轴坐标同步。
        /// </summary>
        [JsonIgnore]
        public bool IsSync { get; set; }

        /// <summary>
        /// 当前界面选中的 Die，仅用于运行时交互，不写入地图文件。
        /// </summary>
        [JsonIgnore]
        public DieModel SelectedDie { get; internal set; }

        [JsonIgnore]
        [field: NonSerialized]
        public WaferMapGeometry Geometry { get; private set; }

        [JsonIgnore]
        public bool UsePhysicalPositionForDisplay { get; set; }

        #region  用于计算地图显示范围和坐标转换的衍生属性；不直接参与序列化存储。

        [JsonIgnore]
        public int Radius => WaferSize * 25 / 2;

        [JsonIgnore]
        public double PitchX => DieWidth + StreetWidth;

        [JsonIgnore]
        public double PitchY => DieHeight + StreetHeight;

        /// <summary>
        /// 防止异常参数导致除零；只用于显示和命中测试的兜底计算。
        /// </summary>
        [JsonIgnore]
        public double SafePitchX => PitchX > 0 ? PitchX : 1d;

        /// <summary>
        /// 防止异常参数导致除零；只用于显示和命中测试的兜底计算。
        /// </summary>
        [JsonIgnore]
        public double SafePitchY => PitchY > 0 ? PitchY : 1d;

        [JsonIgnore]
        public double UiRadiusX => Radius / SafePitchX;

        [JsonIgnore]
        public double UiRadiusY => Radius / SafePitchY;

        [JsonIgnore]
        public int MapHalfLength => (int)Math.Ceiling(Radius + Math.Max(SafePitchX, SafePitchY) * 2d);

        [JsonIgnore]
        public double DieHalfWidth => DieWidth > 0 ? DieWidth / 2d : SafePitchX / 2d;

        [JsonIgnore]
        public double DieHalfHeight => DieHeight > 0 ? DieHeight / 2d : SafePitchY / 2d;

        #endregion


        /// <summary>
        /// 确保旧文件或外部构造的数据模型也拥有坐标转换器。
        /// </summary>
        public WaferMapCoordinateTransformer EnsureMapCoordinateTransformer()
        {
            MapCoordinateTransformer ??= new WaferMapCoordinateTransformer();
            return MapCoordinateTransformer;
        }

        public WaferDataModel()
        {
            InitializeRuntimeMembers();
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            InitializeRuntimeMembers();
        }

        private void InitializeRuntimeMembers()
        {
            Geometry = new WaferMapGeometry(this);
        }

        /// <summary>
        /// 请求刷新整张图。高频单 Die 更新应优先使用 UpdateDieState。
        /// </summary>
        public void RefreshPlot()
        {
            RefreshRequested?.Invoke();
        }


        #region 界面刷新节流机制：批量更新时只触发一次全局刷新，单颗 Die 更新时限制刷新频率，避免 UI 卡顿。

        [NonSerialized]
        private int _batchUpdateDepth = 0;

        [NonSerialized]
        private bool _needsGlobalRefresh = false;

        [NonSerialized]
        private long _nextSingleDieRefreshTicks = 0;

        private const int SingleDieRefreshIntervalMs = 50;

        [field: NonSerialized]
        public event Action<DieModel> OnSingleDieChanged;

        [field: NonSerialized]
        public event Action OnBatchUpdateCompleted;

        /// <summary>
        /// 批量更新的作用域，进入时增加深度，退出时减少深度并根据需要触发全局刷新。使用时建议配合 using 语句确保正确结束作用域。
        /// </summary>
        /// <returns></returns>
        public IDisposable BeginBatchUpdate()
        {
            _batchUpdateDepth++;
            return new BatchUpdateScope(() =>
            {
                _batchUpdateDepth--;
                if (_batchUpdateDepth == 0 && _needsGlobalRefresh)
                {
                    _needsGlobalRefresh = false;
                    OnBatchUpdateCompleted?.Invoke();
                }
            });
        }

        /// <summary>
        /// 通知单颗 Die 状态变化。批量更新中只记录需要刷新，避免大量 UI 刷新。
        /// </summary>
        public void UpdateDieState(DieModel die)
        {
            if (die == null)
                return;

            if (_batchUpdateDepth > 0)
            {
                _needsGlobalRefresh = true;
            }
            else
            {
                //50ms 刷新一次，避免频繁更新导致 UI 卡顿；批量更新时不触发单 Die 刷新，等批量结束后统一刷新。
                long now = Environment.TickCount64;
                if (now < _nextSingleDieRefreshTicks)
                    return;

                _nextSingleDieRefreshTicks = now + SingleDieRefreshIntervalMs;
                OnSingleDieChanged?.Invoke(die);
            }
        }

        #endregion

    }

    public sealed class BatchUpdateScope : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;

        public BatchUpdateScope(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _onDispose?.Invoke();
        }
    }
}
