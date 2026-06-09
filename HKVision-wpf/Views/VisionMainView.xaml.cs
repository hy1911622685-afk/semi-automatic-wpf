using HKVision.Wpf.Services;
using HKVision.Wpf.ViewModels;
using MyAsset.Wpf;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HKVision.Wpf.Views
{
    public partial class VisionMainView : UserControl
    {
        public static readonly DependencyProperty IsCompactProperty =
            DependencyProperty.Register(
                nameof(IsCompact),
                typeof(bool),
                typeof(VisionMainView),
                new PropertyMetadata(false));

        public VisionMainView()
            : this(new HikVisionHelper())
        {
        }

        public VisionMainView(HikVisionHelper hikVisionHelper)
        {
            ProcessHelper.KillProcess("VisionMasterServerApp");
            ProcessHelper.KillProcess("VisionMaster");
            ProcessHelper.KillProcess("VmModuleProxy");

            InitializeComponent();

            hikVisionHelper.VmManager.BindRenderHost(RenderHost);
            DataContext = new VisionMainViewModel(hikVisionHelper);
        }

        public VisionMainViewModel ViewModel => DataContext as VisionMainViewModel;

        public void RefreshFromVmManager()
        {
            if (Dispatcher.CheckAccess())
            {
                ViewModel?.RefreshFromVmManager();
                return;
            }

            Dispatcher.BeginInvoke((Action)RefreshFromVmManager);
        }

        public bool IsCompact
        {
            get => (bool)GetValue(IsCompactProperty);
            set => SetValue(IsCompactProperty, value);
        }

        public Func<Task> StartFovFunc
        {
            get => ViewModel?.StartFovFunc;
            set
            {
                if (ViewModel != null)
                    ViewModel.StartFovFunc = value;
            }
        }

        public Action StopFovFunc
        {
            get => ViewModel?.StopFovFunc;
            set
            {
                if (ViewModel != null)
                    ViewModel.StopFovFunc = value;
            }
        }
    }
}
