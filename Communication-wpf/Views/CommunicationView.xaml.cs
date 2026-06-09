using Communication.Wpf.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Communication.Wpf.Views
{
    public partial class CommunicationView : UserControl, IDisposable
    {
        public static readonly DependencyProperty CommandBinderProperty =
            DependencyProperty.Register(
                nameof(CommandBinder),
                typeof(Action<AsyncCommandExecutor>),
                typeof(CommunicationView),
                new PropertyMetadata(null, OnCommandBinderChanged));
        public static readonly DependencyProperty ListenStartedActionProperty =
            DependencyProperty.Register(
                nameof(ListenStartedAction),
                typeof(Action),
                typeof(CommunicationView),
                new PropertyMetadata(null, OnListenStartedActionChanged));
        public static readonly DependencyProperty ListenStoppedActionProperty =
            DependencyProperty.Register(
                nameof(ListenStoppedAction),
                typeof(Action),
                typeof(CommunicationView),
                new PropertyMetadata(null, OnListenStoppedActionChanged));
        public static readonly DependencyProperty DeviceConnectedActionProperty =
            DependencyProperty.Register(
                nameof(DeviceConnectedAction),
                typeof(Action),
                typeof(CommunicationView),
                new PropertyMetadata(null, OnDeviceConnectedActionChanged));
        public static readonly DependencyProperty DeviceDisconnectedActionProperty =
            DependencyProperty.Register(
                nameof(DeviceDisconnectedAction),
                typeof(Action),
                typeof(CommunicationView),
                new PropertyMetadata(null, OnDeviceDisconnectedActionChanged));

        private readonly CommunicationViewModel _viewModel;

        public CommunicationView()
        {
            InitializeComponent();
            _viewModel = new CommunicationViewModel();
            DataContext = _viewModel;
        }

        public Action<AsyncCommandExecutor> CommandBinder
        {
            get => (Action<AsyncCommandExecutor>)GetValue(CommandBinderProperty);
            set => SetValue(CommandBinderProperty, value);
        }

        public Action ListenStartedAction
        {
            get => (Action)GetValue(ListenStartedActionProperty);
            set => SetValue(ListenStartedActionProperty, value);
        }

        public Action ListenStoppedAction
        {
            get => (Action)GetValue(ListenStoppedActionProperty);
            set => SetValue(ListenStoppedActionProperty, value);
        }

        public Action DeviceConnectedAction
        {
            get => (Action)GetValue(DeviceConnectedActionProperty);
            set => SetValue(DeviceConnectedActionProperty, value);
        }

        public Action DeviceDisconnectedAction
        {
            get => (Action)GetValue(DeviceDisconnectedActionProperty);
            set => SetValue(DeviceDisconnectedActionProperty, value);
        }

        private static void OnCommandBinderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CommunicationView view && view._viewModel != null)
                view._viewModel.CommandBinder = e.NewValue as Action<AsyncCommandExecutor>;
        }

        private static void OnListenStartedActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CommunicationView view && view._viewModel != null)
                view._viewModel.ListenStartedAction = e.NewValue as Action;
        }

        private static void OnListenStoppedActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CommunicationView view && view._viewModel != null)
                view._viewModel.ListenStoppedAction = e.NewValue as Action;
        }

        private static void OnDeviceConnectedActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CommunicationView view && view._viewModel != null)
                view._viewModel.DeviceConnectedAction = e.NewValue as Action;
        }

        private static void OnDeviceDisconnectedActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CommunicationView view && view._viewModel != null)
                view._viewModel.DeviceDisconnectedAction = e.NewValue as Action;
        }

        public void StopListen()
        {
            _viewModel.StopListening();
        }

        public void Dispose()
        {
            if (DataContext is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
