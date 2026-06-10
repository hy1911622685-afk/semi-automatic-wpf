using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScottPlot.WPF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
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
            AttachStatisticsSources();
            RefreshTestStatistics();
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

        public ObservableCollection<BinTestStatisticViewModel> BinTestStatistics { get; } =
            new ObservableCollection<BinTestStatisticViewModel>();

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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(JoinedTestSummary))]
        private int joinedTestCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TestedSummary))]
        private int testedCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TestedSummary))]
        private double testedPercentOfJoined;

        [ObservableProperty]
        private bool isGeneratingWaferMap;

        public bool IsMoveMode => OperationalMode == WaferMapOperationalEnum.Move;
        public bool IsSelectMode => OperationalMode == WaferMapOperationalEnum.SelectorDeselect;
        public bool IsAddMode => OperationalMode == WaferMapOperationalEnum.AddorRemove;
        public bool IsSyncEnabled => PlotHelper.IsSync;
        public string JoinedTestSummary => $"{JoinedTestCount}";
        public string TestedSummary => $"{TestedCount} ({TestedPercentOfJoined:F1}%)";

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

        private void AttachStatisticsSources()
        {
            PlotHelper.DataModel.RefreshRequested -= HandleStatisticsRefreshRequested;
            PlotHelper.DataModel.OnSingleDieChanged -= HandleStatisticsSingleDieChanged;
            PlotHelper.DataModel.OnBatchUpdateCompleted -= HandleStatisticsBatchUpdateCompleted;

            PlotHelper.DataModel.RefreshRequested += HandleStatisticsRefreshRequested;
            PlotHelper.DataModel.OnSingleDieChanged += HandleStatisticsSingleDieChanged;
            PlotHelper.DataModel.OnBatchUpdateCompleted += HandleStatisticsBatchUpdateCompleted;
        }

        private void HandleStatisticsRefreshRequested()
        {
            RefreshTestStatistics();
        }

        private void HandleStatisticsSingleDieChanged(DieModel die)
        {
            RefreshTestStatistics();
        }

        private void HandleStatisticsBatchUpdateCompleted()
        {
            RefreshTestStatistics();
        }

        public void RefreshTestStatistics()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(RefreshTestStatisticsCore);
                return;
            }

            RefreshTestStatisticsCore();
        }

        public void ApplyBinToDie(DieModel die, string binCommand)
        {
            if (die == null)
                return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => ApplyBinToDie(die, binCommand));
                return;
            }

            die.ApplyBin(binCommand);
            PlotHelper.DataModel.UpdateDieState(die);
            RefreshTestStatistics();
        }

        private void RefreshTestStatisticsCore()
        {
            var testDies = PlotHelper.DataModel.AllDies
                .Where(d => d != null && d.IsInTestQueue)
                .ToList();

            JoinedTestCount = testDies.Count;

            var testedDies = testDies
                .Where(d => !string.IsNullOrWhiteSpace(d.BinCommand))
                .ToList();

            TestedCount = testedDies.Count;
            TestedPercentOfJoined = Percent(TestedCount, JoinedTestCount);

            var registeredBins = PlotHelper.DataModel.ColorManager
                .GetStateDefinitions()
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.BinCommand))
                .Select(x => new
                {
                    Key = GetBinCommandKey(x.BinCommand),
                    Definition = x
                })
                .GroupBy(x => x.Key, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();

            var testedCounts = testedDies
                .GroupBy(d => GetBinCommandKey(d.BinCommand), StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

            var statistics = registeredBins
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x =>
                {
                    int count = testedCounts.TryGetValue(x.Key, out int value) ? value : 0;
                    testedCounts.Remove(x.Key);

                    return new BinTestStatisticViewModel
                    {
                        BinCommand = x.Definition.BinCommand,
                        Description = x.Definition.Description,
                        Count = count,
                        PercentOfTested = Percent(count, TestedCount),
                        ColorBrush = ToBrush(x.Definition.Color)
                    };
                })
                .ToList();

            foreach (var item in testedCounts.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                statistics.Add(new BinTestStatisticViewModel
                {
                    BinCommand = item.Key,
                    Count = item.Value,
                    PercentOfTested = Percent(item.Value, TestedCount),
                    ColorBrush = ToBrush(PlotHelper.DataModel.ColorManager.DefaultColor)
                });
            }

            BinTestStatistics.Clear();
            foreach (var item in statistics)
            {
                BinTestStatistics.Add(item);
            }
        }

        private static double Percent(int count, int total)
        {
            return total == 0 ? 0 : count * 100.0 / total;
        }

        private static SolidColorBrush ToBrush(System.Drawing.Color color)
        {
            return new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
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
            RefreshTestStatistics();
            OnPropertyChanged(nameof(DataModel));
        }

        [RelayCommand]
        private void SaveMap()
        {
            PlotHelper.DataModel.SaveToDialog("WaferMap.json", "Save Wafer Map JSON");
        }

        [RelayCommand]
        private async Task RebuildMap()
        {
            if (IsGeneratingWaferMap)
                return;

            IsGeneratingWaferMap = true;
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.Render);

                PlotHelper.RebuildMap();
            }
            finally
            {
                IsGeneratingWaferMap = false;
            }
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
            RefreshTestStatistics();
        }

        [RelayCommand]
        private void SelectWithoutEdge()
        {
            PlotHelper.SelectNonEdgeForTest();
            RefreshTestStatistics();
        }

        [RelayCommand]
        private void ClearSelection()
        {
            PlotHelper.ClearTestQueue();
            RefreshTestStatistics();
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
            RefreshTestStatistics();
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
            if (!TrySelectEmptyBinCommand())
                return;

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
            TrimBinDescriptions();

            if (!TryValidateBinDefinitions())
                return;

            PlotHelper.DataModel.ColorManager.SyncCustomStates(CreateCustomBinDefinitions());
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

        private void TrimBinDescriptions()
        {
            foreach (var item in BinDefinitions)
            {
                item.Description = item.Description?.Trim();
            }
        }

        private bool TryValidateBinDefinitions()
        {
            if (!TrySelectEmptyBinCommand())
                return false;

            var usedCommands = new HashSet<string>(StringComparer.Ordinal);

            foreach (var item in BinDefinitions)
            {
                if (!usedCommands.Add(GetBinCommandKey(item.BinCommand)))
                {
                    SelectedBinDefinition = item;
                    return false;
                }
            }

            return true;
        }

        private bool TrySelectEmptyBinCommand()
        {
            var invalid = BinDefinitions.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.BinCommand));
            if (invalid == null)
                return true;

            SelectedBinDefinition = invalid;
            return false;
        }

        private IEnumerable<BinDefinition> CreateCustomBinDefinitions()
        {
            return BinDefinitions
                .Where(x => !x.IsSystemDefault)
                .Select(item => new BinDefinition
                {
                    BinCommand = GetBinCommandKey(item.BinCommand),
                    Description = item.Description,
                    Color = item.GetColorOrDefault(PlotHelper.DataModel.ColorManager.DefaultColor),
                    IsSystemDefault = false
                });
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
                .Select(x => GetBinCommandKey(x.BinCommand))
                .Where(x => x != null && x.StartsWith("Bin", StringComparison.Ordinal))
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

        private static string GetBinCommandKey(string binCommand) => binCommand?.Trim() ?? string.Empty;
    }
}
