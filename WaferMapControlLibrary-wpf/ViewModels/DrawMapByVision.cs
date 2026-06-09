using ScottPlot.WPF;
using System;
using System.Collections.Generic;
using WaferMap.Wpf.Model;

namespace WaferMap.Wpf.ViewModels
{
    public partial class WaferRenderer
    {
        /// <summary>
        /// 创建盲扫模式下使用的空白晶圆图。
        /// </summary>
        public void BuildBlindScanPlot()
        {
            BuildBlindScanPlot(false);
        }

        /// <summary>
        /// 创建或重建盲扫显示图层；重建时保留当前已采集到的 Die。
        /// </summary>
        private void BuildBlindScanPlot(bool isRebuildFromData)
        {
            if (!HasPlotView)
                return;

            _useBlindScanVisualStyle = true;
            PreparePlot(isRebuildFromData);

            if (isRebuildFromData)
                DrawDieFromData();

            BindPlotAndRefresh();
        }

        /// <summary>
        /// 将本次视野扫描得到的新 Die 和视野范围同步到动态地图。
        /// </summary>
        public void UpdateDynamicMap(List<DieModel> newDies, double fovLeft, double fovRight, double fovBottom, double fovTop)
        {
            if (_myPlotView == null)
                return;

            lock (_myPlotView.Plot.Sync)
            {
                // 首个视野直接设置显示范围；后续视野只扩展范围，避免用户失去整体采集轨迹。
                EnsureAxisRange(fovLeft, fovRight, fovBottom, fovTop, _useBlindScanVisualStyle && _dataModel.AllDies.Count == 0);
                ShowOperationRegion(
                    Math.Min(fovLeft, fovRight),
                    Math.Max(fovLeft, fovRight),
                    Math.Min(fovBottom, fovTop),
                    Math.Max(fovBottom, fovTop));

                if (newDies != null && newDies.Count > 0)
                {
                    foreach (var dieModel in newDies)
                    {
                        // 动态新增 Die 先不分配序号，统一由 RecalculateDieIndexes 按移动顺序重排。
                        dieModel.Index = -1;
                        _dataModel.AllDies.Add(dieModel);
                        PaintDieToGrid(dieModel);
                    }
                }
            }

            _dataModel.RefreshPlot();
        }

        /// <summary>
        /// 根据扫描视野扩展绘图范围，确保动态采集过程中新视野始终可见。
        /// </summary>
        private void EnsureAxisRange(double fovLeft, double fovRight, double fovBottom, double fovTop, bool resetToFov = false)
        {
            if (_myPlotView == null)
                return;

            const double padding = 2.0;
            var limits = _myPlotView.Plot.Axes.GetLimits();

            if (resetToFov)
            {
                _myPlotView.Plot.Axes.SetLimits(
                    Math.Min(fovLeft, fovRight) - padding,
                    Math.Max(fovLeft, fovRight) + padding,
                    Math.Min(fovBottom, fovTop) - padding,
                    Math.Max(fovBottom, fovTop) + padding);
                return;
            }

            var minX = Math.Min(Math.Min(fovLeft, fovRight) - padding, limits.Left);
            var maxX = Math.Max(Math.Max(fovLeft, fovRight) + padding, limits.Right);
            var minY = Math.Min(Math.Min(fovBottom, fovTop) - padding, limits.Bottom);
            var maxY = Math.Max(Math.Max(fovBottom, fovTop) + padding, limits.Top);

            _myPlotView.Plot.Axes.SetLimits(minX, maxX, minY, maxY);
        }
    }
}
