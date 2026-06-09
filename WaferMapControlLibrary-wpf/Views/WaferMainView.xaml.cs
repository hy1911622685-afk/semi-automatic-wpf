using ScottPlot.WPF;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WaferMap.Wpf.Model;
using WaferMap.Wpf.ViewModels;

namespace WaferMap.Wpf.Views
{
    public partial class WaferMainView : UserControl
    {
        public static readonly DependencyProperty IsCompactProperty =
            DependencyProperty.Register(
                nameof(IsCompact),
                typeof(bool),
                typeof(WaferMainView),
                new PropertyMetadata(false));

        private bool _isRightButtonPanning;
        private ScottPlot.Pixel _rightPanLastPixel;

        public WaferMainView()
            : this(new WaferMainViewModel())
        {
        }

        public WaferMainView(WaferMainViewModel viewModel)
        {
            InitializeComponent();
            ConfigurePlotInteraction();
            DataContext = viewModel ?? new WaferMainViewModel();
            Loaded += WaferMainView_Loaded;
        }

        public WaferMainViewModel ViewModel => DataContext as WaferMainViewModel;

        public WpfPlot PlotView => PART_PlotView;

        public bool IsCompact
        {
            get => (bool)GetValue(IsCompactProperty);
            set => SetValue(IsCompactProperty, value);
        }

        private void WaferMainView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ViewModel?.AttachPlotView(PART_PlotView);
        }

        private void ConfigurePlotInteraction()
        {
            PART_PlotView.Menu = null;
            PART_PlotView.UserInputProcessor.DoubleLeftClickBenchmark(false);
            PART_PlotView.UserInputProcessor.RightClickDragZoom(false, false, false);
        }

        private void PlotView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PART_PlotView.CaptureMouse();
            ViewModel?.HandlePlotMouseDown(e, e.GetPosition(PART_PlotView));
        }

        private async void PlotView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            await HandleMouseUpAsync(e);
        }

        private async Task HandleMouseUpAsync(MouseButtonEventArgs e)
        {
            var point = e.GetPosition(PART_PlotView);
            ViewModel?.HandlePlotMouseUp(point);
            if (ViewModel?.OperationalMode == WaferMapOperationalEnum.Move)
            {
                await ViewModel.HandlePlotMouseClickAsync(e, point);
            }

            PART_PlotView.ReleaseMouseCapture();
        }

        private void PlotView_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isRightButtonPanning)
            {
                var currentPixel = GetPlotPixel(e);
                PART_PlotView.Plot.Axes.Pan(_rightPanLastPixel, currentPixel);
                _rightPanLastPixel = currentPixel;
                ViewModel?.PlotHelper.MyRenderer.EnforceViewportZoomLimits();
                PART_PlotView.Refresh();
                e.Handled = true;
                return;
            }

            ViewModel?.HandlePlotMouseMove(e.GetPosition(PART_PlotView));
        }

        private void PlotView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isRightButtonPanning = true;
            _rightPanLastPixel = GetPlotPixel(e);
            PART_PlotView.CaptureMouse();
            e.Handled = true;
        }

        private void PlotView_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isRightButtonPanning)
                return;

            _isRightButtonPanning = false;
            PART_PlotView.ReleaseMouseCapture();
            e.Handled = true;
        }

        private ScottPlot.Pixel GetPlotPixel(MouseEventArgs e)
        {
            var point = e.GetPosition(PART_PlotView);
            return new ScottPlot.Pixel((float)point.X, (float)point.Y);
        }

        private void PlotView_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ViewModel?.PlotHelper.MyRenderer.EnforceViewportZoomLimits() == true)
                PART_PlotView.Refresh();
        }
    }
}
