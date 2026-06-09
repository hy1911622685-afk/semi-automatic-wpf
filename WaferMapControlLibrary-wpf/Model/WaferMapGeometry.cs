using MyAsset.Wpf.Infrastructure;

namespace WaferMap.Wpf.Model
{
    public sealed class WaferMapGeometry
    {
        private readonly WaferDataModel _dataModel;

        public WaferMapGeometry(WaferDataModel dataModel)
        {
            _dataModel = dataModel;
        }

        public double PhysicalToUiX(double physicalX) => physicalX / _dataModel.SafePitchX;

        public double PhysicalToUiY(double physicalY) => physicalY / _dataModel.SafePitchY;

        public (double X, double Y) PhysicalToUiPoint(double physicalX, double physicalY) =>
            (PhysicalToUiX(physicalX), PhysicalToUiY(physicalY));

        public (double X, double Y) GetGridCenter(int gridX, int gridY) =>
            (gridX * _dataModel.SafePitchX, gridY * _dataModel.SafePitchY);

        public (double X, double Y) PhysicalToMapDisplayPoint(double physicalX, double physicalY)
        {
            return _dataModel.EnsureMapCoordinateTransformer().PhysicalToMapDisplayPoint(
                physicalX,
                physicalY,
                _dataModel.PhysicalReferencePosition);
        }

        public (double MinimumX, double MaximumX, double MinimumY, double MaximumY) PhysicalToMapDisplayBounds(
            double physicalLeft,
            double physicalRight,
            double physicalBottom,
            double physicalTop)
        {
            return _dataModel.EnsureMapCoordinateTransformer().PhysicalToMapDisplayBounds(
                physicalLeft,
                physicalRight,
                physicalBottom,
                physicalTop,
                _dataModel.PhysicalReferencePosition);
        }

        public (double X, double Y) GetDisplayCenter(DieModel die)
        {
            if (_dataModel.UsePhysicalPositionForDisplay && die?.PhysicalPosition is { } physicalPosition)
                return PhysicalToMapDisplayPoint(physicalPosition.X, physicalPosition.Y);

            return die == null
                ? (0d, 0d)
                : GetGridCenter(die.GridX, die.GridY);
        }

        public (double MinimumX, double MaximumX, double MinimumY, double MaximumY) GetGridDieRect(int gridX, int gridY)
        {
            var center = GetGridCenter(gridX, gridY);
            return (
                center.X - _dataModel.DieHalfWidth,
                center.X + _dataModel.DieHalfWidth,
                center.Y - _dataModel.DieHalfHeight,
                center.Y + _dataModel.DieHalfHeight);
        }

        public (double MinimumX, double MaximumX, double MinimumY, double MaximumY) GetDisplayDieRect(DieModel die)
        {
            var center = GetDisplayCenter(die);
            return (
                center.X - _dataModel.DieHalfWidth,
                center.X + _dataModel.DieHalfWidth,
                center.Y - _dataModel.DieHalfHeight,
                center.Y + _dataModel.DieHalfHeight);
        }

        public (double X, double Y)[] GetGridCorners(int gridX, int gridY)
        {
            var center = GetGridCenter(gridX, gridY);
            return new[]
            {
                (center.X - _dataModel.DieHalfWidth, center.Y - _dataModel.DieHalfHeight),
                (center.X + _dataModel.DieHalfWidth, center.Y - _dataModel.DieHalfHeight),
                (center.X + _dataModel.DieHalfWidth, center.Y + _dataModel.DieHalfHeight),
                (center.X - _dataModel.DieHalfWidth, center.Y + _dataModel.DieHalfHeight)
            };
        }

        public Point2D GetGridCenterPoint(int gridX, int gridY)
        {
            var center = GetGridCenter(gridX, gridY);
            return new Point2D(center.X, center.Y);
        }
    }
}
