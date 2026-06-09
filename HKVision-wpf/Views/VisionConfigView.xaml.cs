using HKVision.Wpf.ViewModels;
using MyAsset.Wpf.Messaging;
using System.Windows.Controls;

namespace HKVision.Wpf.Views
{
    public partial class VisionConfigView : UserControl
    {
        private readonly VisionConfigViewModel _viewModel;

        public VisionConfigView()
            : this(new HikVisionHelper(), new MessageBoxService("HKVision-wpf", "视觉对话框", "视觉错误", "视觉警告"))
        {
        }

        public VisionConfigView(HikVisionHelper hikVisionHelper, IMessageBoxService messageBoxService = null)
        {
            InitializeComponent();
            _viewModel = new VisionConfigViewModel(hikVisionHelper, messageBoxService);
            DataContext = _viewModel;
        }

        public System.Func<(double X, double Y)> ReadAxisPositionFunc
        {
            get => _viewModel.ReadAxisPositionFunc;
            set => _viewModel.ReadAxisPositionFunc = value;
        }

        public System.Func<double, System.Threading.Tasks.Task> MoveAxisTFunc
        {
            get => _viewModel.MoveAxisTFunc;
            set => _viewModel.MoveAxisTFunc = value;
        }

        public System.Action<bool> CalibrationAction
        {
            get => _viewModel.CalibrationAction;
            set => _viewModel.CalibrationAction = value;
        }

        public System.Action<bool> LevelingAction
        {
            get => _viewModel.LevelingAction;
            set => _viewModel.LevelingAction = value;
        }

        public void AnalysisPositionData((double, double) pos, string info)
        {
            _viewModel.AnalysisPositionData(pos, info);
        }
    }
}
