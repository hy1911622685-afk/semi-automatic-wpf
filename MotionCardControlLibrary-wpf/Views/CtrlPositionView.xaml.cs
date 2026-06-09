using System;
using System.Windows.Controls;
using System.Windows.Threading;
using MotionCard.Model;

namespace MotionCard.Wpf.Views
{
    public partial class CtrlPositionView : UserControl
    {
        private readonly DispatcherTimer _positionTimer;

        public CtrlPositionView()
        {
            InitializeComponent();

            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _positionTimer.Tick += PositionTimer_Tick;

            Loaded += CtrlPositionView_Loaded;
            Unloaded += CtrlPositionView_Unloaded;
        }

        private void CtrlPositionView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            RefreshAxisPositions();
            _positionTimer.Start();
        }

        private void CtrlPositionView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _positionTimer.Stop();
        }

        private void PositionTimer_Tick(object sender, EventArgs e)
        {
            RefreshAxisPositions();
        }

        private void RefreshAxisPositions()
        {
            AxisXPositionText.Text = FormatAxisPosition(AxisType.AxisX);
            AxisYPositionText.Text = FormatAxisPosition(AxisType.AxisY);
            AxisZPositionText.Text = FormatAxisPosition(AxisType.AxisZ);
            AxisTPositionText.Text = FormatAxisPosition(AxisType.AxisT);
        }

        private static string FormatAxisPosition(ushort axis)
        {
            try
            {
                return MyLTDMC.dmc_get_position(axis).ToString("F3");
            }
            catch
            {
                return "--";
            }
        }
    }
}
