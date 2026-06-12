using System.Windows.Controls;
using System.Windows.Input;
using MotionCard.Model;
using MotionCard.Wpf.ViewModels;

namespace MotionCard.Wpf.Views
{
    public partial class MotionConfigView : UserControl
    {
        private Button _activeContinuousMoveButton;

        public MotionConfigView()
        {
            InitializeComponent();
        }

        private async void ContinuousMoveButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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

            _activeContinuousMoveButton = null;
            e.Handled = true;

            AxisDirType axisDirType = ToAxisDirType(direction);
            short result;
            try
            {
                result = await MyLTDMC.dmc_vmove(axisDirType, speed);
            }
            catch (System.Exception ex)
            {
                StopAxis(axisDirType);
                CancelButtonCapture(button);
                motionViewModel?.AppendLog("Motion", $"Continuous move start failed: {ex.Message}");
                return;
            }

            if (result != MotionResult.Success)
            {
                CancelButtonCapture(button);
                return;
            }

            if (Mouse.LeftButton != MouseButtonState.Pressed)
            {
                StopAxis(axisDirType);
                CancelButtonCapture(button);
                return;
            }

            _activeContinuousMoveButton = button;
            button.CaptureMouse();
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

        private void StopContinuousMove(object sender)
        {
            if (sender is not Button button || button.Tag is not string direction)
                return;

            bool shouldStop = ReferenceEquals(_activeContinuousMoveButton, button);
            _activeContinuousMoveButton = null;

            if (shouldStop)
                StopAxis(ToAxisDirType(direction));

            if (button.IsMouseCaptured)
                button.ReleaseMouseCapture();
        }

        private void CancelButtonCapture(Button button)
        {
            if (ReferenceEquals(_activeContinuousMoveButton, button))
                _activeContinuousMoveButton = null;

            if (button.IsMouseCaptured)
                button.ReleaseMouseCapture();
        }

        private static void StopAxis(AxisDirType axisDirType)
        {
            MyLTDMC.ConvertDirType(axisDirType, out ushort axis, out _);
            MyLTDMC.dmc_stop(axis);
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
