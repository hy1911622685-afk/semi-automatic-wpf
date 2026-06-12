using HKVision.Wpf.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            DisableParamConfigRightClick();
        }

        private void DisableParamConfigRightClick()
        {
            ParamConfigControl.ContextMenu = null;
            ParamConfigControl.ContextMenuOpening += SuppressContextMenuOpening;
            ParamConfigControl.AddHandler(PreviewMouseRightButtonDownEvent, new MouseButtonEventHandler(SuppressRightClick), true);
            ParamConfigControl.AddHandler(PreviewMouseRightButtonUpEvent, new MouseButtonEventHandler(SuppressRightClick), true);
            ParamConfigControl.AddHandler(MouseRightButtonDownEvent, new MouseButtonEventHandler(SuppressRightClick), true);
            ParamConfigControl.AddHandler(MouseRightButtonUpEvent, new MouseButtonEventHandler(SuppressRightClick), true);
        }

        private static void SuppressRightClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private static void SuppressContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
        }
    }
}
