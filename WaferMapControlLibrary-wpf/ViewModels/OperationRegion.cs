using WaferMap.Wpf.Model;

namespace WaferMap.Wpf.ViewModels
{
    public partial class WaferRenderer
    {
        public OperationRegion OperationRegion { get; private set; }

        // The selected die provides the center point for a full-plot crosshair.
        private OperationRegion SelectedDieCrosshairRegion { get; set; }

        private void InitializeOverlayRegions()
        {
            OperationRegion = new OperationRegion();
            SelectedDieCrosshairRegion = new OperationRegion();
            ApplyOperationRegionToPlottable();
            ApplySelectedDieCrosshairToPlottable();
        }

        public void ResetOperationRegionVisual()
        {
            HideOperationRegion();
            _dataModel.RefreshPlot();
        }

        public void ResetSelectedDieCrosshairVisual()
        {
            HideSelectedDieCrosshairRegion();
            _dataModel.RefreshPlot();
        }

        public void UpdateSelectedDieDisplay(DieModel die)
        {
            if (die is null)
                return;

            _dataModel.SelectedDie = die;
            UpdateSelectedDieDisplay();
        }

        public void UpdateSelectedDieDisplay()
        {
            if (TryGetDieRect(_dataModel.SelectedDie, out var targetRect))
            {
                SelectedDieCrosshairRegion.MinimumX = targetRect.MinimumX;
                SelectedDieCrosshairRegion.MaximumX = targetRect.MaximumX;
                SelectedDieCrosshairRegion.MinimumY = targetRect.MinimumY;
                SelectedDieCrosshairRegion.MaximumY = targetRect.MaximumY;
                SelectedDieCrosshairRegion.StrokeThickness = 2.0;
            }
            else
            {
                HideSelectedDieCrosshairRegion();
            }

            ApplySelectedDieCrosshairToPlottable();
            _dataModel.RefreshPlot();
        }

        public void ShowOperationRegion(double minX, double maxX, double minY, double maxY)
        {
            if (OperationRegion is null)
                return;

            OperationRegion.MinimumX = minX;
            OperationRegion.MaximumX = maxX;
            OperationRegion.MinimumY = minY;
            OperationRegion.MaximumY = maxY;
            OperationRegion.StrokeThickness = 3;
            ApplyOperationRegionToPlottable();
        }

        public void HideOperationRegion()
        {
            if (OperationRegion is null)
                return;

            OperationRegion.MinimumX = OperationRegion.MaximumX = 0;
            OperationRegion.MinimumY = OperationRegion.MaximumY = 0;
            OperationRegion.StrokeThickness = 0;
            ApplyOperationRegionToPlottable();
        }

        public bool IsOperationRegionEmpty()
        {
            return OperationRegion is null ||
                   (OperationRegion.MaximumX == 0 && OperationRegion.MinimumX == 0 &&
                    OperationRegion.MaximumY == 0 && OperationRegion.MinimumY == 0);
        }

        private void HideSelectedDieCrosshairRegion()
        {
            if (SelectedDieCrosshairRegion is null)
                return;

            SelectedDieCrosshairRegion.MinimumX = SelectedDieCrosshairRegion.MaximumX = 0;
            SelectedDieCrosshairRegion.MinimumY = SelectedDieCrosshairRegion.MaximumY = 0;
            SelectedDieCrosshairRegion.StrokeThickness = 0;
            ApplySelectedDieCrosshairToPlottable();
        }

        private void ApplyOperationRegionToPlottable()
        {
            if (_waferPlottable == null || OperationRegion == null)
                return;

            _waferPlottable.OperationRegionMinimumX = OperationRegion.MinimumX;
            _waferPlottable.OperationRegionMaximumX = OperationRegion.MaximumX;
            _waferPlottable.OperationRegionMinimumY = OperationRegion.MinimumY;
            _waferPlottable.OperationRegionMaximumY = OperationRegion.MaximumY;
            _waferPlottable.OperationRegionStrokeThickness = OperationRegion.StrokeThickness;
        }

        private void ApplySelectedDieCrosshairToPlottable()
        {
            if (_waferPlottable == null || SelectedDieCrosshairRegion == null)
                return;

            _waferPlottable.SelectedDieCrosshairMinimumX = SelectedDieCrosshairRegion.MinimumX;
            _waferPlottable.SelectedDieCrosshairMaximumX = SelectedDieCrosshairRegion.MaximumX;
            _waferPlottable.SelectedDieCrosshairMinimumY = SelectedDieCrosshairRegion.MinimumY;
            _waferPlottable.SelectedDieCrosshairMaximumY = SelectedDieCrosshairRegion.MaximumY;
            _waferPlottable.SelectedDieCrosshairStrokeThickness = SelectedDieCrosshairRegion.StrokeThickness;
        }
    }
}
