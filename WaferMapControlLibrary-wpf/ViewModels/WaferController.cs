using MyAsset.Wpf.Infrastructure;
using System;
using System.Collections.Generic;
using WaferMap.Wpf.Model;

namespace WaferMap.Wpf.ViewModels
{
    public partial class WaferController
    {
        private readonly WaferDataModel _dataModel;

        public WaferController(WaferDataModel dataModel)
        {
            _dataModel = dataModel;
        }

        public bool IsPointInDie(Point2D point, OperationRegion region)
        {
            if (region is null)
                return false;
            return point.X >= region.MinimumX &&
                   point.X <= region.MaximumX &&
                   point.Y >= region.MinimumY &&
                   point.Y <= region.MaximumY;
        }

        public bool IsPointInDie(Point2D point, DieModel die)
        {
            if (die is null)
                return false;

            var rect = _dataModel.Geometry.GetDisplayDieRect(die);
            return point.X >= rect.MinimumX &&
                   point.X <= rect.MaximumX &&
                   point.Y >= rect.MinimumY &&
                   point.Y <= rect.MaximumY;
        }

        public bool IsDieInRegion(OperationRegion rect, OperationRegion operationRegion)
        {
            if (rect is null || operationRegion is null)
                return false;

            return (rect.MaximumX >= operationRegion.MinimumX && rect.MinimumX <= operationRegion.MaximumX)
                   && (rect.MaximumY >= operationRegion.MinimumY && rect.MinimumY <= operationRegion.MaximumY);
        }

        public bool IsDieInRegion(DieModel die, OperationRegion operationRegion)
        {
            if (die is null || operationRegion is null)
                return false;

            var rect = _dataModel.Geometry.GetDisplayDieRect(die);
            return IsDieInRegion(
                new OperationRegion(rect.MinimumX, rect.MaximumX, rect.MinimumY, rect.MaximumY),
                operationRegion);
        }

        private List<DieModel> GetDiesInRegion(OperationRegion operationRegion)
        {
            if (operationRegion is null)
                return null;
            List<DieModel> dieModels = new List<DieModel>();

            foreach (var item in _dataModel.AllDies)
            {
                if (IsDieInRegion(item, operationRegion))
                {
                    dieModels.Add(item);
                }
            }
            return dieModels;
        }

        [Obsolete("Use IsPointInDie(Point2D, OperationRegion) instead.")]
        public bool IsPointInDie(Point2D point, SelectionRect rectangle) => IsPointInDie(point, (OperationRegion)rectangle);

        [Obsolete("Use IsDieInRegion(OperationRegion, OperationRegion) instead.")]
        public bool IsDieInRectangle(SelectionRect rect, SelectionRect mouseRect) => IsDieInRegion(rect, mouseRect);

        [Obsolete("Use IsDieInRegion(DieModel, OperationRegion) instead.")]
        public bool IsDieInRectangle(DieModel die, SelectionRect mouseRect) => IsDieInRegion(die, mouseRect);
    }
}
