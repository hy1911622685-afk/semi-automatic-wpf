using HKVision.Wpf.ViewModels;
using System.Windows;
using VM.Core;

namespace HKVision.Wpf.Views
{
    public partial class ParamConfigWindow : Window
    {
        public ParamConfigWindow(VmModule module)
        {
            InitializeComponent();
            DataContext = new ParamConfigViewModel(module);
            ParamConfigControl.ModuleSource = module;
        }
    }
}
