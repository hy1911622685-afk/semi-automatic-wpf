using System.Windows.Controls;
using WaferSystem.Wpf.ViewModels;

namespace WaferSystem.Wpf.Views
{
    public partial class WorkflowControlView : UserControl
    {
        public WorkflowControlView()
        {
            InitializeComponent();
            Loaded += WorkflowControlView_Loaded;
            Unloaded += WorkflowControlView_Unloaded;
        }

        private void WorkflowControlView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel viewModel)
                viewModel.SocketStopListeningBinder?.Invoke(CommunicationViewHost.StopListen);
        }

        private void WorkflowControlView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel viewModel)
                viewModel.SocketStopListeningBinder?.Invoke(null);
        }
    }
}
