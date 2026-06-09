using MotionCard.Model;
using MotionCard.Wpf.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MotionCard.Wpf.Views
{
    public partial class CtrlIOPortView : UserControl
    {
        private static readonly string[] Card0InputNames =
        {
            "LOAD", "VACUUM", "CENTER", "THETA", "ADJ", "UP/DOWN", "INDEX", "MS-LT",
            "DB-LT", "P2", "P1", "P9", "SW", "SW", "SW", "SW",
            "//", "//", "z轴正限位", "z轴负限位", "START", "CHECK", "ABORT", "EMS",
            "INK1", "INK1", "INK1", "INK1", "INK1", "INK1", "P16", "P17"
        };

        private static readonly string[] Card0OutputNames =
        {
            "SW", "SW", "SW", "SW", "START-灯", "CHECK-灯", "//", "P10",
            "P12", "P23", "P5", "P6", "P7", "P8", "INK1", "INK2",
            "LED1", "LED2", "LED3", "LED4", "LED5", "LED6", "P24", "P25",
            "P26", "//", "//", "P11", "P27", "//", "//", "P28"
        };

        private static readonly string[] Card1InputNames =
        {
            "P13", "P14", "摇杆-右", "摇杆-左", "摇杆-调速", "电子手轮-待定义", "电子手轮-待定义", "摇杆-下",
            "摇杆-上", "JOYSTICK-待定义", "JOYSTICK-待定义", "JOYSTICK-待定义", "P15", "INK2", "INK2", "INK2",
            "INK2", "INK2", "INK2", "P18", "P19", "P20", "P21", "P22",
            "J11输入待定义", "J11输入待定义", "J11输入待定义", "J11输入待定义", "J11输入待定义", "J11输入待定义", "J11输入待定义", "J11输入待定义"
        };

        private static readonly string[] Card1OutputNames =
        {
            "MS-LT灯", "DB-LT灯", "LED7", "LOAD灯", "VACUUM灯", "CENTER灯", "THETA灯", "ADJ灯",
            "UP/DOWN灯", "INDEX灯", "LED8", "LED9", "J11输出待定义", "J11输出待定义", "J11输出待定义", "J11输出待定义",
            "J11输出待定义", "J11输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义",
            "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义"
        };

        private readonly IoListenViewModel _viewModel = new IoListenViewModel();
        private readonly DispatcherTimer _monitorTimer;
        private readonly byte[] _lastIn0 = new byte[4];
        private readonly byte[] _lastIn1 = new byte[4];
        private readonly byte[] _lastOut0 = new byte[4];
        private readonly byte[] _lastOut1 = new byte[4];
        private SingleController _singleController;
        private bool _isFirstRefresh = true;

        public CtrlIOPortView()
            : this(null)
        {
        }

        public CtrlIOPortView(SingleController singleController)
        {
            _singleController = singleController;
            InitializeComponent();
            DataContext = _viewModel;
            CreateBoards();

            _monitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _monitorTimer.Tick += MonitorTimer_Tick;

            Loaded += CtrlIOPortView_Loaded;
            Unloaded += CtrlIOPortView_Unloaded;
        }

        private void CtrlIOPortView_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureConnected();
            RefreshPorts();
            _monitorTimer.Start();
        }

        private void CtrlIOPortView_Unloaded(object sender, RoutedEventArgs e)
        {
            _monitorTimer.Stop();
        }

        private void CreateBoards()
        {
            if (_viewModel.Boards.Count > 0)
                return;

            var card0 = new IoBoardViewModel(0, "板卡0");
            AddPorts(card0.Inputs, card0.CardIndex, false, Card0InputNames);
            AddPorts(card0.Outputs, card0.CardIndex, true, Card0OutputNames);
            _viewModel.Boards.Add(card0);

            var card1 = new IoBoardViewModel(1, "板卡1");
            AddPorts(card1.Inputs, card1.CardIndex, false, Card1InputNames);
            AddPorts(card1.Outputs, card1.CardIndex, true, Card1OutputNames);
            _viewModel.Boards.Add(card1);
        }

        private void AddPorts(
            ObservableCollection<IoPortItemViewModel> target,
            int cardIndex,
            bool isOutput,
            string[] names)
        {
            for (int i = 0; i < names.Length; i++)
                target.Add(new IoPortItemViewModel(cardIndex, i, isOutput, names[i], ToggleOutput));
        }

        private void MonitorTimer_Tick(object sender, EventArgs e)
        {
            RefreshPorts();
        }

        private void EnsureConnected()
        {
            try
            {
                _singleController ??= new SingleController();
                if (!_singleController.IsConnect)
                    _singleController.Connect();

                _viewModel.IsConnected = _singleController.IsConnect;
                _viewModel.StatusText = _viewModel.IsConnected ? "IO监听运行中" : "IO控制卡未连接";
            }
            catch (Exception ex)
            {
                _viewModel.IsConnected = false;
                _viewModel.StatusText = "IO连接失败: " + ex.Message;
            }
        }

        private void RefreshPorts()
        {
            if (_singleController == null || !_singleController.IsConnect)
            {
                _viewModel.IsConnected = false;
                _viewModel.StatusText = "IO控制卡未连接";
                return;
            }

            try
            {
                for (int portIndex = 0; portIndex < 4; portIndex++)
                {
                    byte in0 = _singleController.ReadInputSingle(portIndex);
                    byte in1 = _singleController.ReadInputSingle1(portIndex);
                    byte out0 = _singleController.ReadOutputSingle(portIndex);
                    byte out1 = _singleController.ReadOutputSingle1(portIndex);

                    if (_isFirstRefresh || in0 != _lastIn0[portIndex])
                    {
                        UpdatePortStates(_viewModel.Boards[0].Inputs, portIndex, in0);
                        _lastIn0[portIndex] = in0;
                    }
                    if (_isFirstRefresh || out0 != _lastOut0[portIndex])
                    {
                        UpdatePortStates(_viewModel.Boards[0].Outputs, portIndex, out0);
                        _lastOut0[portIndex] = out0;
                    }
                    if (_isFirstRefresh || in1 != _lastIn1[portIndex])
                    {
                        UpdatePortStates(_viewModel.Boards[1].Inputs, portIndex, in1);
                        _lastIn1[portIndex] = in1;
                    }
                    if (_isFirstRefresh || out1 != _lastOut1[portIndex])
                    {
                        UpdatePortStates(_viewModel.Boards[1].Outputs, portIndex, out1);
                        _lastOut1[portIndex] = out1;
                    }
                }

                _isFirstRefresh = false;
                _viewModel.IsConnected = true;
                _viewModel.StatusText = "IO监听运行中";
            }
            catch (Exception ex)
            {
                _viewModel.IsConnected = false;
                _viewModel.StatusText = "IO刷新失败: " + ex.Message;
            }
        }

        private static void UpdatePortStates(
            ObservableCollection<IoPortItemViewModel> ports,
            int portIndex,
            byte portData)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                int itemIndex = portIndex * 8 + bit;
                if (itemIndex >= ports.Count)
                    return;

                ports[itemIndex].IsActive = ((portData >> bit) & 0x1) == 1;
            }
        }

        private void ToggleOutput(IoPortItemViewModel port)
        {
            if (port == null || !port.IsOutput)
                return;

            if (_singleController == null || !_singleController.IsConnect)
                EnsureConnected();

            if (_singleController == null || !_singleController.IsConnect)
                return;

            byte value = port.IsActive ? (byte)0 : (byte)1;
            int portIndex = port.BitIndex / 8;
            int bitIndex = port.BitIndex % 8;

            try
            {
                if (port.CardIndex == 0)
                    _singleController.WriteSingle(portIndex, bitIndex, value);
                else
                    _singleController.WriteSingle1(portIndex, bitIndex, value);

                port.IsActive = value == 1;
            }
            catch (Exception ex)
            {
                _viewModel.StatusText = "IO写入失败: " + ex.Message;
            }
        }
    }
}
