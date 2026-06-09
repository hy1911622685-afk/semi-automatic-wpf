using MyAsset.Wpf.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using WaferMap.Wpf.ViewModels;

namespace WaferMap.Wpf.Model
{
    /// <summary>
    /// 负责 Die 的空间索引、命中测试、测试队列移动顺序和轴移动量计算。
    /// </summary>
    public class WaferNavigator
    {
        private readonly WaferDataModel _dataModel;
        private Dictionary<(int GridX, int GridY), DieModel> _allDieGridMap = new Dictionary<(int GridX, int GridY), DieModel>();
        private Dictionary<DieModel, DieModel> _nextTestQueueDieMap = new Dictionary<DieModel, DieModel>();
        private Dictionary<DieModel, DieModel> _previousTestQueueDieMap = new Dictionary<DieModel, DieModel>();

        /// <summary>
        /// 当前测试队列按照移动策略排序后的结果。
        /// </summary>
        public IReadOnlyList<DieModel> TestQueueDiesInMoveOrder { get; private set; } = Array.Empty<DieModel>();

        /// <summary>
        /// 测试队列中每颗 Die 对应的下一颗 Die，用于快速获取下一步移动目标。
        /// </summary>
        public IReadOnlyDictionary<DieModel, DieModel> NextTestQueueDieMap => _nextTestQueueDieMap;

        /// <summary>
        /// 测试队列中每颗 Die 对应的上一颗 Die，用于反向移动。
        /// </summary>
        public IReadOnlyDictionary<DieModel, DieModel> PreviousTestQueueDieMap => _previousTestQueueDieMap;

        public WaferNavigator(WaferDataModel dataModel)
        {
            _dataModel = dataModel;
        }

        /// <summary>
        /// 获取坐标转换器；旧数据加载后如果为空会在这里兜底创建。
        /// </summary>
        private WaferMapCoordinateTransformer CoordinateTransformer =>
            _dataModel.MapCoordinateTransformer ??= new WaferMapCoordinateTransformer();

        /// <summary>
        /// 重建网格索引和测试队列移动关系。Die 拓扑或待测状态变化后必须调用。
        /// </summary>
        public void BuildSpatialIndex()
        {
            _allDieGridMap = _dataModel.AllDies.ToDictionary(d => (d.GridX, d.GridY));
            TestQueueDiesInMoveOrder = BuildMoveOrderedDies();

            _nextTestQueueDieMap = BuildAdjacentMap(TestQueueDiesInMoveOrder, 1);
            _previousTestQueueDieMap = BuildAdjacentMap(TestQueueDiesInMoveOrder, -1);
        }

        /// <summary>
        /// 按逻辑网格坐标查找 Die。
        /// </summary>
        public bool TryGetDieAtGrid(int gridX, int gridY, out DieModel die)
        {
            die = null;
            return _allDieGridMap != null && _allDieGridMap.TryGetValue((gridX, gridY), out die);
        }

        /// <summary>
        /// 根据 Map 坐标命中 Die；先按网格定位，再用 Die 矩形做精确判断。
        /// </summary>
        public DieModel HitTestDie(Point2D point)
        {
            int centerGridX = (int)Math.Round(point.X / _dataModel.SafePitchX, MidpointRounding.AwayFromZero);
            int centerGridY = (int)Math.Round(point.Y / _dataModel.SafePitchY, MidpointRounding.AwayFromZero);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (!TryGetDieAtGrid(centerGridX + dx, centerGridY + dy, out var die))
                        continue;

                    var rect = _dataModel.Geometry.GetDisplayDieRect(die);
                    if (point.X >= rect.MinimumX &&
                        point.X <= rect.MaximumX &&
                        point.Y >= rect.MinimumY &&
                        point.Y <= rect.MaximumY)
                    {
                        return die;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 获取与框选区域相交的 Die。这里返回相交而不是完全包含，符合用户框选直觉。
        /// </summary>
        public IEnumerable<DieModel> GetDiesInRegion(OperationRegion operationRegion)
        {
            if (operationRegion == null || _allDieGridMap == null)
                yield break;

            int minGridX = (int)System.Math.Floor((operationRegion.MinimumX - _dataModel.DieHalfWidth) / _dataModel.SafePitchX);
            int maxGridX = (int)System.Math.Ceiling((operationRegion.MaximumX + _dataModel.DieHalfWidth) / _dataModel.SafePitchX);
            int minGridY = (int)System.Math.Floor((operationRegion.MinimumY - _dataModel.DieHalfHeight) / _dataModel.SafePitchY);
            int maxGridY = (int)System.Math.Ceiling((operationRegion.MaximumY + _dataModel.DieHalfHeight) / _dataModel.SafePitchY);

            for (int gridY = minGridY; gridY <= maxGridY; gridY++)
            {
                for (int gridX = minGridX; gridX <= maxGridX; gridX++)
                {
                    if (!TryGetDieAtGrid(gridX, gridY, out var die))
                        continue;

                    var dieRect = _dataModel.Geometry.GetGridDieRect(die.GridX, die.GridY);
                    if (dieRect.MaximumX >= operationRegion.MinimumX &&
                        dieRect.MinimumX <= operationRegion.MaximumX &&
                        dieRect.MaximumY >= operationRegion.MinimumY &&
                        dieRect.MinimumY <= operationRegion.MaximumY)
                    {
                        yield return die;
                    }
                }
            }
        }

        /// <summary>
        /// 获取当前选中 Die 到下一颗待测 Die 的轴相对移动量。
        /// </summary>
        public (double X, double Y)? NextDiePos(out DieModel nextDie)
        {
            nextDie = null;

            if (_dataModel.SelectedDie == null)
                return null;

            if (!_nextTestQueueDieMap.TryGetValue(_dataModel.SelectedDie, out nextDie))
                return null;

            return CalculateAxisMoveOffset(_dataModel.SelectedDie, nextDie);
        }

        /// <summary>
        /// 获取当前选中 Die 到上一颗待测 Die 的轴相对移动量。
        /// </summary>
        public (double X, double Y)? PreviousDiePos(out DieModel previousDie)
        {
            previousDie = null;

            if (_dataModel.SelectedDie == null)
                return null;

            if (!_previousTestQueueDieMap.TryGetValue(_dataModel.SelectedDie, out previousDie))
                return null;

            return CalculateAxisMoveOffset(_dataModel.SelectedDie, previousDie);
        }

        /// <summary>
        /// 获取上一颗 Die 的相对移动量，并在成功时同步更新当前选中 Die。
        /// </summary>
        public (double X, double Y)? PreviousDiePos()
        {
            var position = PreviousDiePos(out var previousDie);
            if (position != null)
                _dataModel.SelectedDie = previousDie;
            return position;
        }

        /// <summary>
        /// 计算当前选中 Die 到目标 Die 的轴相对移动量。
        /// </summary>
        public (double X, double Y)? CalculateAxisMoveOffset(DieModel target) => CalculateAxisMoveOffset(_dataModel.SelectedDie, target);

        /// <summary>
        /// 计算 Home Die 到目标 Die 的轴相对移动量。
        /// </summary>
        public (double X, double Y)? CalculateHomeToDieAxisMoveOffset(DieModel target) => CalculateAxisMoveOffset(_dataModel.HomeDie, target);

        /// <summary>
        /// 所有 Die 间移动量都统一经过坐标转换器，避免方向规则散落在业务代码中。
        /// </summary>
        private (double X, double Y)? CalculateAxisMoveOffset(DieModel source, DieModel target)
        {
            return CoordinateTransformer.DieToAxisMoveOffset(
                source,
                target,
                _dataModel.PitchX,
                _dataModel.PitchY);
        }

        /// <summary>
        /// 获取第一颗待测 Die 的轴绝对坐标，而不是相对移动量。
        /// </summary>
        public (double X, double Y)? FirstDiePos(out DieModel die)
        {
            die = TestQueueDiesInMoveOrder.FirstOrDefault();

            if (die == null)
                return null;

            return CalculateAbsolutePosition(_dataModel.HomeDie, die);
        }

        /// <summary>
        /// 根据已同步 Home Die 计算目标 Die 的轴绝对坐标。
        /// </summary>
        public (double X, double Y)? CalculateAbsolutePosition(DieModel source, DieModel target)
        {
            return CoordinateTransformer.DieToPhysicalPosition(
                source,
                target,
                _dataModel.PitchX,
                _dataModel.PitchY,
                _dataModel.PhysicalReferencePosition);
        }

        /// <summary>
        /// 为有序待测队列建立相邻关系表。
        /// </summary>
        private static Dictionary<DieModel, DieModel> BuildAdjacentMap(IReadOnlyList<DieModel> orderedDies, int direction)
        {
            var map = new Dictionary<DieModel, DieModel>();
            for (int i = 0; i < orderedDies.Count; i++)
            {
                int nextIndex = i + direction;
                if (nextIndex >= 0 && nextIndex < orderedDies.Count)
                    map[orderedDies[i]] = orderedDies[nextIndex];
            }

            return map;
        }

        /// <summary>
        /// 根据用户选择的移动趋势生成待测队列顺序。
        /// </summary>
        private List<DieModel> BuildMoveOrderedDies()
        {
            var dies = _dataModel.AllDies.Where(d => d.IsInTestQueue).ToList();

            return _dataModel.MoveTrendMode switch
            {
                MoveTrendEnum.SnakeLeftToRight => BuildSnakeLeftToRightOrder(dies),
                MoveTrendEnum.SnakeTopToBottom => BuildSnakeTopToBottomOrder(dies),
                MoveTrendEnum.LeftToRight => BuildLeftToRightOrder(dies),
                MoveTrendEnum.UpToDown => BuildUpToDownOrder(dies),
                _ => BuildSnakeLeftToRightOrder(dies)
            };
        }

        /// <summary>
        /// 按列蛇形移动：列从左到右，列内上下方向交替。
        /// </summary>
        private List<DieModel> BuildSnakeLeftToRightOrder(IEnumerable<DieModel> dies)
        {
            var orderedDies = new List<DieModel>();
            bool reverseColumn = false;

            foreach (var column in dies.GroupBy(d => d.GridX).OrderBy(g => g.Key))
            {
                var columnDies = column.OrderByDescending(d => d.GridY).ToList();
                if (reverseColumn)
                    columnDies.Reverse();

                orderedDies.AddRange(columnDies);
                reverseColumn = !reverseColumn;
            }

            return orderedDies;
        }

        /// <summary>
        /// 按行蛇形移动：行从上到下，行内左右方向交替。
        /// </summary>
        private List<DieModel> BuildSnakeTopToBottomOrder(IEnumerable<DieModel> dies)
        {
            var orderedDies = new List<DieModel>();
            bool reverseRow = false;

            foreach (var row in dies.GroupBy(d => d.GridY).OrderByDescending(g => g.Key))
            {
                var rowDies = row.OrderBy(d => d.GridX).ToList();
                if (reverseRow)
                    rowDies.Reverse();

                orderedDies.AddRange(rowDies);
                reverseRow = !reverseRow;
            }

            return orderedDies;
        }

        /// <summary>
        /// 按列从左到右移动，列内始终从上到下。
        /// </summary>
        private List<DieModel> BuildLeftToRightOrder(IEnumerable<DieModel> dies)
        {
            return dies
                .GroupBy(d => d.GridX)
                .OrderBy(g => g.Key)
                .SelectMany(column => column.OrderByDescending(d => d.GridY))
                .ToList();
        }

        /// <summary>
        /// 按行从上到下移动，行内始终从左到右。
        /// </summary>
        private List<DieModel> BuildUpToDownOrder(IEnumerable<DieModel> dies)
        {
            return dies
                .GroupBy(d => d.GridY)
                .OrderByDescending(g => g.Key)
                .SelectMany(row => row.OrderBy(d => d.GridX))
                .ToList();
        }
    }
}
