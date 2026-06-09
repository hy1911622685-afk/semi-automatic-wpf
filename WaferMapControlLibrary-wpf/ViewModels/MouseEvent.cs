using MyAsset.Wpf.Infrastructure;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WaferMap.Wpf.Model;

namespace WaferMap.Wpf.ViewModels
{
    public partial class PlotHelper
    {
        private WaferMapOperationalEnum _waferMapOperationalMode = WaferMapOperationalEnum.Move;
        private bool _catchMouseIsDown;
        private Point2D _mouseDownPlace;
        private Point2D _mouseNowPlace;
        private long _nextSelectionRefreshTicks;
        private const int SelectionRefreshIntervalMs = 16;

        /// <summary>
        /// 获取或设置晶圆图当前鼠标交互模式。
        /// </summary>
        public WaferMapOperationalEnum WaferMapOperationalMode
        {
            get => _waferMapOperationalMode;
            set
            {
                if (_waferMapOperationalMode == value)
                    return;

                _waferMapOperationalMode = value;
                _catchMouseIsDown = false;
                _mouseDownPlace = default;
                _mouseNowPlace = default;
                MyRenderer?.ResetOperationRegionVisual();
                MyRenderer?.ConfigureInteractionMode(_waferMapOperationalMode);
                OperationalModeChanged?.Invoke(_waferMapOperationalMode);
            }
        }

        /// <summary>
        /// 判断当前地图场景是否支持拖拽框选操作。
        /// </summary>
        private bool SupportsOperationRegionMode()
        {
            return WaferMapOperationalMode == WaferMapOperationalEnum.SelectorDeselect ||
                   WaferMapOperationalMode == WaferMapOperationalEnum.AddorRemove;
        }

        /// <summary>
        /// 将所有启用 Die（包含边缘 Die）标记为待测。
        /// </summary>
        internal void SelectAllForTest()
        {
            ClearTestQueue();
            using (DataModel.BeginBatchUpdate())
            {
                foreach (var item in DataModel.AllDies.Where(o => o.IsEnabled))
                {
                    item.SelectForTest();
                    DataModel.UpdateDieState(item);
                }
            }

            RecalculateDieIndexes();
        }

        /// <summary>
        /// 清除全部待测状态，并恢复为跳过状态。
        /// </summary>
        public void ClearTestQueue()
        {
            using (DataModel.BeginBatchUpdate())
            {
                foreach (DieModel item in MyNavigator.TestQueueDiesInMoveOrder.ToList())
                {
                    item.SkipTest();
                    DataModel.UpdateDieState(item);
                }
            }

            RecalculateDieIndexes();
        }

        /// <summary>
        /// 将所有非边缘启用 Die 标记为待测。
        /// </summary>
        public void SelectNonEdgeForTest()
        {
            ClearTestQueue();
            using (DataModel.BeginBatchUpdate())
            {
                foreach (DieModel item in DataModel.AllDies.Where(o => o.IsEnabled && !o.IsEdge))
                {
                    item.SelectForTest();
                    DataModel.UpdateDieState(item);
                }
            }

            RecalculateDieIndexes();
        }
        /// <summary>
        /// Die的点击事件
        /// </summary>
        /// <param name="e"></param>
        /// <param name="screenPoint"></param>
        /// <returns></returns>
        public async Task HandlePlotMouseClickAsync(MouseButtonEventArgs e, Point screenPoint)
        {
            if (e.ChangedButton != MouseButton.Left || WaferMapOperationalMode != WaferMapOperationalEnum.Move)
                return;
            var dataPoint = MyRenderer.ToMapPoint(screenPoint);

            DieModel item = MyNavigator.HitTestDie(dataPoint);
            if (item == null || !item.IsEnabled)
                return;

            DataModel.SelectedDie = item;
            MyRenderer.UpdateSelectedDieDisplay();

            if (DataModel.IsSync)
            {
                // Move 模式下点击 Die 是绝对定位：目标坐标基于 Home Die 和物理 Home 坐标计算。
                if (DataModel.HomeDie == null)
                {
                    OnLogMessage("请先设置并同步Home点后再移动Die");
                    return;
                }

                var position = MyNavigator.CalculateAbsolutePosition(DataModel.HomeDie, item);
                if (position is null)
                    return;
                if (SyncAbsMoveFunc != null)
                    await SyncAbsMoveFunc(position.Value.X, position.Value.Y);
            }

            DataModel.UpdateDieState(item);
        }

        /// <summary>
        /// 在当前模式支持时开始拖拽框选。
        /// </summary>
        public void HandlePlotMouseDown(MouseButtonEventArgs e, Point screenPoint)
        {
            if (e.ChangedButton != MouseButton.Left || !SupportsOperationRegionMode())
                return;

            bool isCtrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (isCtrlPressed)
                return;
            var dataPoint = MyRenderer.ToMapPoint(screenPoint);
            _mouseDownPlace = new Point2D(dataPoint.X, dataPoint.Y);
            MyRenderer?.HideOperationRegion();
            _catchMouseIsDown = true;
        }

        /// <summary>
        /// 拖拽过程中更新当前框选矩形。
        /// </summary>
        public void HandlePlotMouseMove(Point screenPoint)
        {
            if (!_catchMouseIsDown)
                return;
            var dataPoint = MyRenderer.ToMapPoint(screenPoint);
            _mouseNowPlace = new Point2D(dataPoint.X, dataPoint.Y);

            MyRenderer?.ShowOperationRegion(
                Math.Min(_mouseDownPlace.X, _mouseNowPlace.X),
                Math.Max(_mouseDownPlace.X, _mouseNowPlace.X),
                Math.Min(_mouseDownPlace.Y, _mouseNowPlace.Y),
                Math.Max(_mouseDownPlace.Y, _mouseNowPlace.Y));

            RefreshPlotForDrag();
        }

        /// <summary>
        /// 完成拖拽框选，并应用选中/取消选中或添加/移除操作。
        /// </summary>
        public void HandlePlotMouseUp(Point screenPoint)
        {
            if (!_catchMouseIsDown)
                return;

            _catchMouseIsDown = false;
            if (MyRenderer?.IsOperationRegionEmpty() != false)
            {
                var dataPoint = MyRenderer.ToMapPoint(screenPoint);
                _mouseNowPlace = new Point2D(dataPoint.X, dataPoint.Y);

                if (WaferMapOperationalMode == WaferMapOperationalEnum.SelectorDeselect)
                {
                    // 未形成有效框选时，保持单颗 Die 的点击反选行为。
                    DieModel item = MyNavigator.HitTestDie(dataPoint);
                    if (item != null && item.IsEnabled)
                    {
                        if (item.IsSelectedForTest)
                            item.SkipTest();
                        else
                            item.SelectForTest();
                        DataModel.UpdateDieState(item);
                        RecalculateDieIndexes();
                    }
                }
                else if (WaferMapOperationalMode == WaferMapOperationalEnum.AddorRemove)
                {
                    HandleManualCellEdit(dataPoint);
                }
            }
            else
            {
                if (WaferMapOperationalMode == WaferMapOperationalEnum.SelectorDeselect)
                {
                    // 框选时不逐颗反选，而是按参考 Die 的状态统一切换整批 Die。
                    ApplyRegionTestQueueState();
                }
                else if (WaferMapOperationalMode == WaferMapOperationalEnum.AddorRemove)
                {
                    HandleManualRectangleEdit();
                }
            }

            MyRenderer?.HideOperationRegion();
            DataModel.RefreshPlot();
        }

        /// <summary>
        /// 根据框选区域内“最上、再最左”的启用 Die 状态，统一设置整批 Die 是否待测。
        /// </summary>
        private void ApplyRegionTestQueueState()
        {
            var dies = MyNavigator.GetDiesInRegion(MyRenderer.OperationRegion)
                .Where(d => d.IsEnabled)
                .OrderByDescending(d => d.GridY)
                .ThenBy(d => d.GridX)
                .ToList();

            if (dies.Count == 0)
                return;

            // 参考 Die 已待测则整批跳过；参考 Die 未待测则整批加入测试队列。
            bool shouldSelectForTest = !dies[0].IsSelectedForTest;
            using (DataModel.BeginBatchUpdate())
            {
                foreach (DieModel item in dies)
                {
                    if (shouldSelectForTest)
                        item.SelectForTest();
                    else
                        item.SkipTest();

                    DataModel.UpdateDieState(item);
                }
            }

            RecalculateDieIndexes();
        }

        /// <summary>
        /// 限制拖拽过程中的刷新频率，避免框选时产生过多 UI 刷新。
        /// </summary>
        private void RefreshPlotForDrag()
        {
            long now = Environment.TickCount64;
            if (now < _nextSelectionRefreshTicks)
                return;

            _nextSelectionRefreshTicks = now + SelectionRefreshIntervalMs;
            DataModel.RefreshPlot();
        }

        /// <summary>
        /// 显示鼠标指针所在的晶圆图坐标。
        /// </summary>
        public void ShowCurrentDieLocation(TextBlock ctrl, Point screenPoint)
        {
            if (DataModel.HomeDie is null)
                return;
            var dataPoint = MyRenderer.ToMapPoint(screenPoint);
            if (Math.Abs(dataPoint.X) > DataModel.Radius || Math.Abs(dataPoint.Y) > DataModel.Radius)
                return;
            ctrl.Text = $@"X: {Math.Round(dataPoint.X)}, Y: {Math.Round(dataPoint.Y)}";
        }

        /// <summary>
        /// 显示鼠标指针所在 Die 的 Index。
        /// </summary>
        public void ShowCurrentDieIndex(TextBlock ctrl, Point screenPoint)
        {
            if (DataModel.HomeDie is null)
                return;
            var dataPoint = MyRenderer.ToMapPoint(screenPoint);

            DieModel item = MyNavigator.HitTestDie(dataPoint);
            ctrl.Text = item != null && item.IsInTestQueue
                ? $@"Index: {item.Index}"
                : "Index: -";
        }
    }
}
