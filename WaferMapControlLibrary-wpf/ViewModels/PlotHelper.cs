using Newtonsoft.Json;
using MyAsset.Wpf.Infrastructure;
using MyAsset.Wpf.Messaging;
using ScottPlot.WPF;
using System;
using System.Linq;
using System.Threading.Tasks;
using WaferMap.Wpf.Model;

namespace WaferMap.Wpf.ViewModels
{
    public partial class PlotHelper
    {
        public event Action<WaferMapOperationalEnum> OperationalModeChanged;
        private bool _isBlindScanCollecting;

        public WaferDataModel DataModel { get; set; } = new WaferDataModel();
        public WaferRenderer MyRenderer { get; private set; }
        public WaferController MyController { get; private set; }
        public WaferNavigator MyNavigator { get; private set; }

        [JsonIgnore]
        public Func<double, double, Task<short>> SyncAbsMoveFunc;

        [JsonIgnore]
        public Func<double, double, Task<short>> SyncMoveFunc;

        [JsonIgnore]
        public Func<(double X, double Y)> ReadPhysicalPosEvent;

        [JsonIgnore]
        public Action SyncHomeChangeAction;

        public bool IsSync => DataModel.IsSync;

        /// <summary>
        /// 创建晶圆图辅助对象，并初始化渲染器、控制器和导航器。
        /// </summary>
        public PlotHelper()
        {
            InitializeCore();
        }

        /// <summary>
        /// 使用初始晶圆尺寸和 Die 尺寸创建晶圆图辅助对象。
        /// </summary>
        public PlotHelper(int waferSize, double dieHeight, double dieWidth)
        {
            InitializeCore();
            DataModel.WaferSize = waferSize;
            DataModel.DieHeight = dieHeight;
            DataModel.DieWidth = dieWidth;
        }

        /// <summary>
        /// 通过运行日志消息通道发布晶圆图日志。
        /// </summary>
        public void OnLogMessage(string msg)
        {
            RuntimeLogMessenger.Broadcast("WaferMapControlLibrary-wpf", "WaferMap", msg);
        }

        /// <summary>
        /// 基于当前数据模型重新创建核心协作对象。
        /// </summary>
        private void InitializeCore()
        {
            MyRenderer = new WaferRenderer(DataModel);
            MyController = new WaferController(DataModel);
            MyNavigator = new WaferNavigator(DataModel);
        }

        /// <summary>
        /// 将当前选择的逻辑 Home Die 绑定到物理机台坐标。
        /// </summary>
        public void SyncPhysicalHome((double X, double Y)? physicalPos = null)
        {
            if (DataModel.HomeDie == null)
            {
                OnLogMessage("请设置Home点后再同步Die");
                return;
            }

            if (physicalPos is null)
                physicalPos = ReadPhysicalPosEvent?.Invoke();
            if (physicalPos is null)
                return;

            DataModel.PhysicalReferencePosition = (physicalPos.Value.X, physicalPos.Value.Y);
            SetSyncState(true);
            OnLogMessage($"同步成功，逻辑原点（索引：{DataModel.HomeDie.Index}）绑定物理坐标：X:{physicalPos.Value.X}, Y:{physicalPos.Value.Y}");
        }

        /// <summary>
        /// 切换逻辑 Home 与物理坐标的同步状态。
        /// </summary>
        public void ToggleSyncHome()
        {
            if (DataModel.IsSync)
            {
                SetSyncState(false);
                return;
            }

            SyncPhysicalHome();
        }

        public void ExitSyncState()
        {
            SetSyncState(false);
        }

        /// <summary>
        /// 挂载渲染器使用的 WPF 绘图控件。
        /// </summary>
        public void AttachPlotView(WpfPlot plotView)
        {
            MyRenderer.AttachPlotView(plotView);
        }

        /// <summary>
        /// 创建或重建晶圆图布局，并刷新导航索引。
        /// </summary>
        public void CreateMap(bool isRebuildFromData = false)
        {
            SetSyncState(false);

            if (!MyRenderer.BuildPlot(isRebuildFromData))
                return;

            DataModel.MapSource = WaferMapSource.LayoutGenerated;
            _isBlindScanCollecting = false;
            RecalculateDieIndexes();
            ApplyOperationalMode();
        }

        /// <summary>
        /// 根据当前几何参数重新创建晶圆图。
        /// </summary>
        public void RebuildMap() => CreateMap(false);

        /// <summary>
        /// 根据现有 Die 数据刷新晶圆图显示。
        /// </summary>
        public void RefreshMapFromData()
        {
            SetSyncState(false);
            _isBlindScanCollecting = false;

            if (DataModel.MapSource == WaferMapSource.VisionScanned)
            {
                MyRenderer.RebuildBlindScanFromCurrentData();
                RecalculateDieIndexes();
                ApplyOperationalMode();
                return;
            }

            CreateMap(true);
        }

        /// <summary>
        /// 将当前交互模式应用到渲染器，并通知监听者。
        /// </summary>
        private void ApplyOperationalMode()
        {
            MyRenderer.ConfigureInteractionMode(_waferMapOperationalMode);
            OperationalModeChanged?.Invoke(_waferMapOperationalMode);
        }

        /// <summary>
        /// 通过导航索引按逻辑网格坐标查找 Die。
        /// </summary>
        private DieModel FindDieByGrid(int gridX, int gridY)
        {
            return MyNavigator.TryGetDieAtGrid(gridX, gridY, out var die) ? die : null;
        }

        /// <summary>
        /// 在点击的网格单元中添加或移除单个 Die。
        /// </summary>
        private bool HandleManualCellEdit(Point2D dataPoint)
        {
            if (!MyRenderer.TryGetGridCell(dataPoint, out var gridCell))
                return false;

            var existingDie = FindDieByGrid(gridCell.GridX, gridCell.GridY);
            if (existingDie != null)
            {
                DataModel.AllDies.Remove(existingDie);
                RefreshAfterTopologyChanged();
                return true;
            }

            if (!TryAddManualDie(gridCell.GridX, gridCell.GridY))
                return false;

            RefreshAfterTopologyChanged();
            return true;
        }

        /// <summary>
        /// 批量添加或移除当前框选范围覆盖的网格单元。
        /// </summary>
        private bool HandleManualRectangleEdit()
        {
            var hitCells = MyRenderer.GetGridCellsInRegion(MyRenderer.OperationRegion)
                .OrderByDescending(c => c.GridY)
                .ThenBy(c => c.GridX)
                .ToList();

            if (hitCells.Count == 0)
                return false;

            // 框选 Add/Remove 不逐格反转；由“最上、再最左”的参考格决定整批是添加还是删除。
            bool isDeleteMode = FindDieByGrid(hitCells[0].GridX, hitCells[0].GridY) != null;
            bool changed = false;

            if (isDeleteMode)
            {
                var toDelete = hitCells
                    .Select(c => FindDieByGrid(c.GridX, c.GridY))
                    .Where(d => d != null)
                    .Distinct()
                    .ToList();

                if (toDelete.Count == 0)
                    return false;

                foreach (var die in toDelete)
                {
                    DataModel.AllDies.Remove(die);
                }

                changed = true;
            }
            else
            {
                foreach (var cell in hitCells.OrderByDescending(c => c.GridY).ThenBy(c => c.GridX))
                {
                    changed |= TryAddManualDie(cell.GridX, cell.GridY);
                }
            }

            if (changed)
            {
                RefreshAfterTopologyChanged();
            }

            return changed;
        }

        /// <summary>
        /// 当目标网格合法且为空时添加手动 Die。
        /// </summary>
        private bool TryAddManualDie(int gridX, int gridY)
        {
            if (FindDieByGrid(gridX, gridY) != null)
                return false;

            if (DataModel.MapSource == WaferMapSource.VisionScanned)
            {
                // 盲扫模式下手动补点无法反推出可靠物理坐标，因此只写逻辑网格坐标。
                DataModel.AllDies.Add(new DieModel
                {
                    GridX = gridX,
                    GridY = gridY,
                    PhysicalPosition = null,
                    Attrs = DieAttributes.IsEnabled,
                    IsSelectedForTest = true
                });
                return true;
            }

            var containment = MyRenderer.ClassifyManualCell(gridX, gridY);
            if (containment == CellContainment.Outside)
                return false;

            // 普通晶圆图中，部分落入晶圆边界的格子作为边缘 Die 处理。
            var die = new DieModel
            {
                GridX = gridX,
                GridY = gridY,
                PhysicalPosition = null,
                Attrs = containment == CellContainment.Partial
                    ? DieAttributes.IsEdge | DieAttributes.IsEnabled
                    : DieAttributes.IsEnabled
            };

            DataModel.AllDies.Add(die);
            return true;
        }

        /// <summary>
        /// 拓扑变化后恢复选择和 Home 引用，重排 Die，重建索引并刷新显示。
        /// </summary>
        private void RefreshAfterTopologyChanged()
        {
            var selectedDie = DataModel.SelectedDie != null && DataModel.AllDies.Contains(DataModel.SelectedDie)
                ? DataModel.SelectedDie
                : null;
            var homeDie = DataModel.HomeDie != null && DataModel.AllDies.Contains(DataModel.HomeDie)
                ? DataModel.HomeDie
                : null;

            if (homeDie == null && DataModel.HomeDie != null)
            {
                // Home Die 被删除后，物理同步关系已失效，必须取消同步。
                SetSyncState(false);
            }

            DataModel.SelectedDie = selectedDie;
            DataModel.HomeDie = homeDie;

            var sortedDies = DataModel.AllDies
                .OrderByDescending(d => d.GridY)
                .ThenBy(d => d.GridX)
                .ToList();

            // 先重排 AllDies，避免后续显示、保存和导航顺序因为添加/删除时间不同而漂移。
            DataModel.AllDies.Clear();
            for (int i = 0; i < sortedDies.Count; i++)
            {
                sortedDies[i].Index = -1;
                DataModel.AllDies.Add(sortedDies[i]);
            }

            if (MyRenderer.HasPlotView)
            {
                if (DataModel.MapSource == WaferMapSource.VisionScanned)
                    MyRenderer.RebuildBlindScanFromCurrentData();
                else
                    MyRenderer.RebuildFromCurrentData();

                if (DataModel.SelectedDie != null)
                {
                    MyRenderer.UpdateSelectedDieDisplay();
                }
                else
                {
                    MyRenderer.ResetSelectedDieCrosshairVisual();
                }
            }
            RecalculateDieIndexes();
        }

        /// <summary>
        /// 优先按待测 Die 导航顺序重新分配 Index，再补充其余 Die。
        /// </summary>
        public void RecalculateDieIndexes()
        {
            foreach (var die in DataModel.AllDies)
            {
                die.Index = -1;
            }

            MyNavigator.BuildSpatialIndex();

            int index = 0;
            foreach (var die in MyNavigator.TestQueueDiesInMoveOrder)
            {
                die.Index = index++;
            }

            // 未进入测试队列的 Die 保持 Index=-1，避免客户端误把它们当作可测试序列。
            DataModel.RefreshPlot();
        }
        /// <summary>
        /// 更新同步状态，并通知绑定的界面状态监听者。
        /// </summary>
        private void SetSyncState(bool isSync)
        {
            if (DataModel.IsSync == isSync)
                return;

            DataModel.IsSync = isSync;
            SyncHomeChangeAction?.Invoke();
        }
    }
}
