using System.Windows.Controls;
using System.Windows.Input;
using MotionCard.Model;
using MotionCard.Wpf.ViewModels;

namespace MotionCard.Wpf.Views
{
    public partial class MotionConfigView : UserControl
    {
        public MotionConfigView()
        {
            InitializeComponent();
        }

        private void ContinuousMoveButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string direction)
                return;

            MotionMainViewModel motionViewModel = DataContext as MotionMainViewModel;
            if (motionViewModel?.IsProbeMode == true)
            {
                motionViewModel.NotifyContinuousMoveBlockedInProbeMode();
                e.Handled = true;
                return;
            }

            SpeedType speed = motionViewModel != null
                ? motionViewModel.ContinuousSpeed
                : SpeedType.Fast;

            if (motionViewModel != null && IsPlanarDirection(direction))
                motionViewModel.NotifyManualPlanarMoveStarted();

            _ = MyLTDMC.dmc_vmove(ToAxisDirType(direction), speed);
            button.CaptureMouse();
            e.Handled = true;
        }

        private void ContinuousMoveButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            StopContinuousMove(sender);
            e.Handled = true;
        }

        private void ContinuousMoveButton_LostMouseCapture(object sender, MouseEventArgs e)
        {
            StopContinuousMove(sender);
        }

        private static void StopContinuousMove(object sender)
        {
            if (sender is not Button button || button.Tag is not string direction)
                return;

            MyLTDMC.ConvertDirType(ToAxisDirType(direction), out ushort axis, out _);
            MyLTDMC.dmc_stop(axis);

            if (button.IsMouseCaptured)
                button.ReleaseMouseCapture();
        }

        private static AxisDirType ToAxisDirType(string direction)
        {
            return direction switch
            {
                "Front" => AxisDirType.Front,
                "Back" => AxisDirType.Back,
                "Left" => AxisDirType.Left,
                "Right" => AxisDirType.Right,
                "Up" => AxisDirType.Up,
                "Down" => AxisDirType.Down,
                "Redo" => AxisDirType.Redo,
                "Undo" => AxisDirType.Undo,
                _ => AxisDirType.Front
            };
        }

        private static bool IsPlanarDirection(string direction)
        {
            return direction is "Front" or "Back" or "Left" or "Right";
        }

        
    }
}
