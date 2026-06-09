using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScottPlot.WPF;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WaferMap.Wpf.Infrastructure;
using WaferMap.Wpf.Model;

namespace WaferMap.Wpf.ViewModels
{
    public partial class WaferMainViewModel : ObservableObject
    {
        private const int MouseInfoUpdateIntervalMs = 50;
        private long _nextMouseInfoUpdateTicks;

        public WaferMainViewModel()
            : this(new PlotHelper())
        {
        }

        public WaferMainViewModel(PlotHelper plotHelper)
        {
            PlotHelper = plotHelper ?? new PlotHelper();
            PlotHelper.OperationalModeChanged += mode => OperationalMode = mode;
            PlotHelper.SyncHomeChangeAction += () =>
            {
                OnPropertyChanged(nameof(IsSyncEnabled));
                OnPropertyChanged(nameof(DataModel));
            };

            RefreshOffsetOptions();
            RefreshBins();
        }

        public PlotHelper PlotHelper { get; }

        public ObservableCollection<GapDirection> GapDirections { get; } =
            new ObservableCollection<GapDirection>(Enum.GetValues<GapDirection>());

        public ObservableCollection<MapType> MapTypes { get; } =
            new ObservableCollection<MapType>(Enum.GetValues<MapType>());

        public ObservableCollection<AxisOrientationEnum> AxisOrientations { get; } =
            new ObservableCollection<AxisOrientationEnum>(Enum.GetValues<AxisOrientationEnum>());

        public ObservableCollection<MoveTrendEnum> MoveTrends { get; } =
            new ObservableCollection<MoveTrendEnum>(Enum.GetValues<MoveTrendEnum>());

        public ObservableCollection<int> OffsetOptions { get; } = new ObservableCollection<int>();

        public ObservableCollection<BinDefinitionViewModel> BinDefinitions { get; } =
            new ObservableCollection<BinDefinitionViewModel>();

        public WaferDataModel DataModel => PlotHelper.DataModel;

        public int SkipNum
        {
            get => Math.Max(2, PlotHelper.DataModel.SkipNum);
            set
            {
                int normalizedValue = Math.Max(2, value);
                if (PlotHelper.DataModel.SkipNum == normalizedValue)
                    return;

                PlotHelper.DataModel.SkipNum = normalizedValue;
                RefreshOffsetOptions();
                OnPropertyChanged();
                OnPropertyChanged(nameof(DataModel));
            }
        }

        public Func<double, double, Task<short>> SyncAbsMoveFunc
        {
            get => PlotHelper.SyncAbsMoveFunc;
            set => PlotHelper.SyncAbsMoveFunc = value;
        }

        public Func<double, double, Task<short>> SyncMoveFunc
        {
            get => PlotHelper.SyncMoveFunc;
            set => PlotHelper.SyncMoveFunc = value;
        }

        public Func<(double X, double Y)> ReadPhysicalPosEvent
        {
            get => PlotHelper.ReadPhysicalPosEvent;
            set => PlotHelper.ReadPhysicalPosEvent = value;
        }

        [ObservableProperty]
        private string mousePositionText = "Mouse: -";

        [ObservableProperty]
        private string selectedIndexText = "Die: -";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsMoveMode))]
        [NotifyPropertyChangedFor(nameof(IsSelectMode))]
        [NotifyPropertyChangedFor(nameof(IsAddMode))]
        private WaferMapOperationalEnum operationalMode = WaferMapOperationalEnum.Move;

        [ObservableProperty]
        private bool isAxisVisible;

        [ObservableProperty]
        private int selectedOffsetNum;

        [ObservableProperty]
        private BinDefinitionViewModel selectedBinDefinition;

        public bool IsMoveMode => OperationalMode == WaferMapOperationalEnum.Move;
        public bool IsSelectMode => OperationalMode == WaferMapOperationalEnum.SelectorDeselect;
        public bool IsAddMode => OperationalMode == WaferMapOperationalEnum.AddorRemove;
        public bool IsSyncEnabled => PlotHelper.IsSync;

        public void ExitSyncState()
        {
            PlotHelper.ExitSyncState();
        }

        public void AttachPlotView(WpfPlot plotView)
        {
            PlotHelper.AttachPlotView(plotView);
            PlotHelper.RebuildMap();
        }

        public async Task HandlePlotMouseClickAsync(System.Windows.Input.MouseButtonEventArgs e, System.Windows.Point point)
        {
            await PlotHelper.HandlePlotMouseClickAsync(e, point);
            UpdateSelectionText();
        }

        public void HandlePlotMouseDown(System.Windows.Input.MouseButtonEventArgs e, System.Windows.Point point)
        {
            PlotHelper.HandlePlotMouseDown(e, point);
        }

        public void HandlePlotMouseMove(System.Windows.Point point)
        {
            PlotHelper.HandlePlotMouseMove(point);
            UpdateMouseText(point);
        }

        public void HandlePlotMouseUp(System.Windows.Point point)
        {
            PlotHelper.HandlePlotMouseUp(point);
            UpdateSelectionText();
        }

        private void UpdateMouseText(System.Windows.Point point)
        {
            if (!PlotHelper.MyRenderer.HasPlotView)
                return;

            long now = Environment.TickCount64;
            if (now < _nextMouseInfoUpdateTicks)
                return;

            _nextMouseInfoUpdateTicks = now + MouseInfoUpdateIntervalMs;

            var dataPoint = PlotHelper.MyRenderer.ToMapPoint(point);
            var die = PlotHelper.MyNavigator.HitTestDie(dataPoint);

            string dieText = die == null
                ? "Die -"
                : $"Die Index:{die.Index} ({die.GridX},{die.GridY}) {GetDieStateText(die)}";

            if (SelectedIndexText != dieText)
                SelectedIndexText = dieText;
        }

        private void UpdateSelectionText()
        {
            var selectedDie = PlotHelper.DataModel.SelectedDie;
            SelectedIndexText = selectedDie == null
                ? "Die: -"
                : $"Selected Die #{selectedDie.Index} ({selectedDie.GridX}, {selectedDie.GridY}) {GetDieStateText(selectedDie)}";
        }

        private static string GetDieStateText(DieModel die)
        {
            if (die == null)
                return "-";

            if (die.HasBin)
                return die.BinCommand;

            if (die.IsSelectedForTest)
                return "SelectedForTest";

            return "None";
        }

        [RelayCommand]
        private void LoadMap()
        {
            if (!PlotHelper.DataModel.LoadFromDialog("Load Wafer Map JSON"))
                return;

            PlotHelper.DataModel.EnsureMapCoordinateTransformer();
            PlotHelper.RefreshMapFromData();
            RefreshOffsetOptions();
            RefreshBins();
            OnPropertyChanged(nameof(DataModel));
        }

        [RelayCommand]
        private void SaveMap()
        {
            PlotHelper.DataModel.SaveToDialog("WaferMap.json", "Save Wafer Map JSON");
        }

        [RelayCommand]
        private void RebuildMap()
        {
            PlotHelper.RebuildMap();
        }

        [RelayCommand]
        private void RefreshMap()
        {
            PlotHelper.RefreshMapFromData();
        }

        [RelayCommand]
        private void SetMode(WaferMapOperationalEnum mode)
        {
            PlotHelper.WaferMapOperationalMode = mode;
            OperationalMode = mode;
        }

        [RelayCommand]
        private void SetHome()
        {
            PlotHelper.MyRenderer.UpdateHomeDieDisplay(PlotHelper.DataModel.SelectedDie);
        }

        [RelayCommand]
        private void ToggleSyncHome()
        {
            PlotHelper.ToggleSyncHome();
            OnPropertyChanged(nameof(DataModel));
        }

        [RelayCommand]
        private void SelectAll()
        {
            PlotHelper.SelectAllForTest();
        }

        [RelayCommand]
        private void SelectWithoutEdge()
        {
            PlotHelper.SelectNonEdgeForTest();
        }

        [RelayCommand]
        private void ClearSelection()
        {
            PlotHelper.ClearTestQueue();
        }

        [RelayCommand]
        private void ApplyLayout()
        {
            PlotHelper.RebuildMap();
            RefreshOffsetOptions();
            OnPropertyChanged(nameof(DataModel));
        }

        [RelayCommand]
        private void ApplySkipRule()
        {
            // 先把所有非边缘启用 Die 纳入队列并重排 Index，再按 Index 应用跳点规则。
            using (PlotHelper.DataModel.BeginBatchUpdate())
            {
                foreach (var item in PlotHelper.DataModel.AllDies.Where(o => o.IsEnabled && !o.IsEdge))
                {
                    item.SelectForTest();
                    PlotHelper.DataModel.UpdateDieState(item);
                }
            }
            //需要依赖 item.Index重新计算的功能来正确应用跳点规则，所以放在外面统一调用，而不是在循环内调用。
            PlotHelper.RecalculateDieIndexes();

            using (PlotHelper.DataModel.BeginBatchUpdate())
            {
                foreach (var item in PlotHelper.MyNavigator.TestQueueDiesInMoveOrder.ToList())
                {
                    if (item.Index % PlotHelper.DataModel.SkipNum == PlotHelper.DataModel.OffsetNum)
                        item.SelectForTest();
                    else
                        item.SkipTest();
                    PlotHelper.DataModel.UpdateDieState(item);
                }
            }

            PlotHelper.RecalculateDieIndexes();
        }

        [RelayCommand]
        private async Task MoveFirstAsync()
        {
            // 第一颗 Die 需要移动到轴绝对坐标，不是相对位移。
            await MoveAxisAbsoluteAsync(PlotHelper.MyNavigator.FirstDiePos(out _));
        }

        [RelayCommand]
        private async Task MovePreviousAsync()
        {
            await MoveAxisAsync(PlotHelper.MyNavigator.PreviousDiePos());
        }

        [RelayCommand]
        private async Task MoveNextAsync()
        {
            await MoveAxisAsync(PlotHelper.MyNavigator.NextDiePos(out _));
        }

        private async Task MoveAxisAsync((double X, double Y)? offset)
        {
            if (offset == null)
                return;

            // 上一颗/下一颗/指定 Die 间移动使用相对位移。
            if (SyncMoveFunc != null)
                await SyncMoveFunc(offset.Value.X, offset.Value.Y);
        }

        private async Task MoveAxisAbsoluteAsync((double X, double Y)? position)
        {
            if (position == null)
                return;

            // Home 同步后的目标定位使用绝对坐标。
            if (SyncAbsMoveFunc != null)
                await SyncAbsMoveFunc(position.Value.X, position.Value.Y);
        }

        [RelayCommand]
        private void ToggleAxis()
        {
            IsAxisVisible = !IsAxisVisible;
            PlotHelper.MyRenderer.SetAxisVisible(IsAxisVisible);
        }

        [RelayCommand]
        private void ZoomFit()
        {
            PlotHelper.MyRenderer.AutoZoom(0);
        }

        [RelayCommand]
        private void PrepareBlindScan()
        {
            PlotHelper.PrepareBlindScanMap();
        }

        [RelayCommand]
        private void CompleteBlindScan()
        {
            PlotHelper.CompleteBlindScanMap();
        }

        [RelayCommand]
        private void AddBin()
        {
            int commandNumber = GetNextBinCommandNumber();
            var vm = new BinDefinitionViewModel
            {
                BinCommand = $"Bin{commandNumber}",
                Description = "Custom",
                ColorHex = "#87CEEB",
                IsSystemDefault = false
            };

            BinDefinitions.Add(vm);
            RefreshBinDisplayIndexes();
            SelectedBinDefinition = vm;
        }

        [RelayCommand]
        private void DeleteBin()
        {
            if (SelectedBinDefinition == null || SelectedBinDefinition.IsSystemDefault)
                return;

            BinDefinitions.Remove(SelectedBinDefinition);
            RefreshBinDisplayIndexes();
            SelectedBinDefinition = null;
        }

        [RelayCommand]
        private void SaveBins()
        {
            NormalizeBinDefinitions();

            var invalid = BinDefinitions.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.BinCommand));
            if (invalid != null)
            {
                SelectedBinDefinition = invalid;
                return;
            }

            var duplicateCommand = BinDefinitions
                .GroupBy(x => x.BinCommand?.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1);
            if (duplicateCommand != null)
                return;

            var customDefinitions = BinDefinitions
                .Where(x => !x.IsSystemDefault)
                .Select(item => new BinDefinition
                {
                    BinCommand = item.BinCommand,
                    Description = item.Description,
                    Color = item.GetColorOrDefault(PlotHelper.DataModel.ColorManager.DefaultColor),
                    IsSystemDefault = false
                });

            PlotHelper.DataModel.ColorManager.SyncCustomStates(customDefinitions);
            RefreshBins();
            PlotHelper.DataModel.RefreshPlot();
        }

        partial void OnSelectedOffsetNumChanged(int value)
        {
            PlotHelper.DataModel.OffsetNum = value;
        }

        private void RefreshOffsetOptions()
        {
            OffsetOptions.Clear();

            int count = Math.Max(2, PlotHelper.DataModel.SkipNum);
            if (PlotHelper.DataModel.SkipNum != count)
                PlotHelper.DataModel.SkipNum = count;

            for (int i = 0; i < count; i++)
            {
                OffsetOptions.Add(i);
            }

            SelectedOffsetNum = Math.Min(PlotHelper.DataModel.OffsetNum, OffsetOptions.Count - 1);
            OnPropertyChanged(nameof(SkipNum));
        }

        private void RefreshBins()
        {
            BinDefinitions.Clear();
            foreach (var definition in PlotHelper.DataModel.ColorManager.GetStateDefinitions())
            {
                BinDefinitions.Add(new BinDefinitionViewModel(definition));
            }

            RefreshBinDisplayIndexes();
        }

        private void NormalizeBinDefinitions()
        {
            foreach (var item in BinDefinitions)
            {
                item.BinCommand = item.BinCommand?.Trim();
                item.Description = item.Description?.Trim();
            }
        }

        private void RefreshBinDisplayIndexes()
        {
            for (int i = 0; i < BinDefinitions.Count; i++)
            {
                BinDefinitions[i].DisplayIndex = i + 1;
            }
        }

        private int GetNextBinCommandNumber()
        {
            var usedNumbers = BinDefinitions
                .Select(x => x.BinCommand?.Trim())
                .Where(x => x != null && x.StartsWith("Bin", StringComparison.OrdinalIgnoreCase))
                .Select(x => int.TryParse(x.Substring(3), out int number) ? number : 0)
                .Where(x => x > 0)
                .ToHashSet();

            int number = 1;
            while (usedNumbers.Contains(number))
            {
                number++;
            }

            return number;
        }
    }
}
