using MyAsset.Wpf.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using WaferMap.Wpf.Model;

namespace WaferMap.Wpf.ViewModels
{
    public partial class PlotHelper
    {
        private bool _isBlindScanAnchorSet;
        private double _anchorPhysicalX;
        private double _anchorPhysicalY;
        private GridBasedDieRegistry _dieRegistry;
        private readonly HashSet<(int GridX, int GridY)> _logicalDieRegistry = new HashSet<(int GridX, int GridY)>();
        private readonly object _blindScanLock = new object();

        /// <summary>
        /// 重置状态，并准备进入盲扫 Die 采集模式。
        /// </summary>
        public void PrepareBlindScanMap()
        {
            lock (_blindScanLock)
            {
                _isBlindScanAnchorSet = false;
                _anchorPhysicalX = 0;
                _anchorPhysicalY = 0;
                _dieRegistry = new GridBasedDieRegistry(DataModel.PitchX, DataModel.PitchY, DataModel.ToleranceFactor);
                _logicalDieRegistry.Clear();
            }

            DataModel.MapSource = WaferMapSource.VisionScanned;
            _isBlindScanCollecting = true;
            MyRenderer.BuildBlindScanPlot();
            ApplyOperationalMode();
        }

        /// <summary>
        /// 结束盲扫采集，并恢复普通晶圆布局模式。
        /// </summary>
        public void CompleteBlindScanMap()
        {
            _isBlindScanCollecting = false;
            WaferMapOperationalMode = WaferMapOperationalEnum.Move;
        }

        /// <summary>
        /// 接收单个视野扫描到的 Die，并更新动态晶圆图。
        /// </summary>
        public void HandleFovScanned(List<Point2D> physicalDies, double fovLeft, double fovRight, double fovBottom, double fovTop)
        {
            if (!_isBlindScanCollecting)
                return;

            if (physicalDies == null || physicalDies.Count == 0)
                return;

            if (DataModel.PitchX <= 0 || DataModel.PitchY <= 0)
                return;

            List<DieModel> newGridDies;

            lock (_blindScanLock)
            {
                if (_dieRegistry == null)
                    _dieRegistry = new GridBasedDieRegistry(DataModel.PitchX, DataModel.PitchY, DataModel.ToleranceFactor);

                if (!_isBlindScanAnchorSet)
                {
                    // 第一次收到盲扫结果时，用当前轴坐标作为盲扫 Home 的物理锚点。
                    var centerPos = ReadPhysicalPosEvent?.Invoke();
                    if (centerPos == null)
                        return;

                    _anchorPhysicalX = centerPos.Value.X;
                    _anchorPhysicalY = centerPos.Value.Y;
                    DataModel.PhysicalReferencePosition = (_anchorPhysicalX, _anchorPhysicalY);
                    _isBlindScanAnchorSet = true;
                }

                // 第一层去重：根据物理坐标容差过滤同一颗 Die 的重复识别点。
                var uniqueDiesForThisFov = _dieRegistry.FilterUnique(physicalDies);
                newGridDies = new List<DieModel>(uniqueDiesForThisFov.Count);

                foreach (var item in uniqueDiesForThisFov)
                {
                    // 物理轴坐标先转换到 Map 显示坐标，再按 Pitch 归并到逻辑网格坐标。
                    var displayPoint = DataModel.Geometry.PhysicalToMapDisplayPoint(item.X, item.Y);
                    double displayX = displayPoint.X;
                    double displayY = displayPoint.Y;
                    int gridX = (int)Math.Round(displayX / DataModel.PitchX, MidpointRounding.AwayFromZero);
                    int gridY = (int)Math.Round(displayY / DataModel.PitchY, MidpointRounding.AwayFromZero);

                    // 第二层去重：避免不同物理点最终落到同一个逻辑 Die。
                    if (!_logicalDieRegistry.Add((gridX, gridY)))
                        continue;

                    newGridDies.Add(new DieModel
                    {
                        GridX = gridX,
                        GridY = gridY,
                        PhysicalPosition = (item.X, item.Y),
                        Attrs = DieAttributes.IsEnabled,
                        IsSelectedForTest = true
                    });
                }
            }

            var displayFov = DataModel.Geometry.PhysicalToMapDisplayBounds(fovLeft, fovRight, fovBottom, fovTop);

            MyRenderer.UpdateDynamicMap(newGridDies, displayFov.MinimumX, displayFov.MaximumX, displayFov.MinimumY, displayFov.MaximumY);
            RecalculateDieIndexes();

            TryInitializeBlindScanHome(newGridDies);
        }

        private void TryInitializeBlindScanHome(IReadOnlyCollection<DieModel> newGridDies)
        {
            if (DataModel.HomeDie != null || newGridDies == null || newGridDies.Count == 0)
                return;

            var homeDie = newGridDies.FirstOrDefault(d => d.GridX == 0 && d.GridY == 0)
                          ?? DataModel.AllDies.FirstOrDefault(d => d.GridX == 0 && d.GridY == 0);

            // 盲扫首次建立出逻辑原点后，立即把 Home Die 与锚点物理坐标同步。
            if (homeDie == null)
                return;

            MyRenderer.UpdateHomeDieDisplay(homeDie);
            SyncPhysicalHome((_anchorPhysicalX, _anchorPhysicalY));
        }
    }
}
