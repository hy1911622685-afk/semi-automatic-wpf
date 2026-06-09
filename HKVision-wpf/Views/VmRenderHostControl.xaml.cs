using HKVision.Wpf.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VMControls.Interface;

namespace HKVision.Wpf.Views
{
    public partial class VmRenderHostControl : UserControl, IVmRenderHost
    {
        public VmRenderHostControl()
        {
            InitializeComponent();
            RenderControl.ChangeTopBarVisibility(false);
            DisableRenderControlRightClick();
            UpdateHorizontalLinePosition();
        }

        public IVmModule ModuleSource
        {
            get => RenderControl.ModuleSource;
            set => RenderControl.ModuleSource = value;
        }

        private void RootGrid_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            UpdateHorizontalLinePosition();
        }

        private void DisableRenderControlRightClick()
        {
            RenderControl.ContextMenu = null;
            RenderControl.ContextMenuOpening += SuppressContextMenuOpening;
            //RenderControl.AddHandler(PreviewMouseRightButtonDownEvent, new MouseButtonEventHandler(SuppressRightClick), true);
            //RenderControl.AddHandler(PreviewMouseRightButtonUpEvent, new MouseButtonEventHandler(SuppressRightClick), true);
            RenderControl.AddHandler(MouseRightButtonDownEvent, new MouseButtonEventHandler(SuppressRightClick), true);
            RenderControl.AddHandler(MouseRightButtonUpEvent, new MouseButtonEventHandler(SuppressRightClick), true);
        }

        private static void SuppressRightClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private static void SuppressContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
        }

        private void UpdateHorizontalLinePosition()
        {
            double y = Math.Max(0, RootGrid.ActualHeight / 2);
            HorizontalLine.Y1 = y;
            HorizontalLine.Y2 = y;
        }
    }
}
