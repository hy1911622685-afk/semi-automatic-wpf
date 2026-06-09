using MotionCard.Model;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace MotionCard.Ctrl
{
    public partial class FormIOListen : Form
    {
        private string[] _inputContentAry = new string[32] { "LOAD", "VACUUM", "CENTER", "THETA", "ADJ", "UP/DOWN", "INDEX", "MS-LT", "DB-LT", "P2", "P1", "P9", "SW", "SW", "SW", "SW", "//", "//", "z轴正限位", "z轴负限位", "START", "CHECK", "ABORT", "EMS", "INK1", "INK1", "INK1", "INK1", "INK1", "INK1", "P16", "P17" };
        private string[] _outputContentAry = new string[32] { "SW", "SW", "SW", "SW", "START-灯", "CHECK-灯", "//", "P10", "P12", "P23", "P5", "P6", "P7", "P8", "INK1", "INK2", "LED1", "LED2", "LED3", "LED4", "LED5", "LED6", "P24", "P25", "P26", "//", "//", "P11", "P27", "//", "//", "P28" };
        private string[] _inputContentAry1 = new string[32] { "P13", "P14", "摇杆-右", "摇杆-左", "摇杆-调速", "电子手轮-待定义", "电子手轮-待定义", "摇杆-下", "摇杆-上", "JOYSTICK-待定义", "JOYSTICK-待定义", "JOYSTICK-待定义", "P15", "INK2", "INK2", "INK2", "INK2", "INK2", "INK2", "P18", "P19", "P20", "P21", "P22", "J11输入待定义", "J11输入待定义", "J11输入待定义", "J11输入待定义", "J11输入待定义", "J11输入待定义", "J11输入待定义", "J11输入待定义" };
        private string[] _outputContentAry1 = new string[32] { "MS-LT灯", "DB-LT灯", "LED7", "LOAD灯", "VACUUM灯", "CENTER灯", "THETA灯", "ADJ灯", "UP/DOWN灯", "INDEX灯", "LED8", "LED9", "J11输出待定义", "J11输出待定义", "J11输出待定义", "J11输出待定义", "J11输出待定义", "J11输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义", "J20输出待定义" };

        private SingleController _singleController;

        // UI 轮询定时器
        private System.Windows.Forms.Timer _monitorTimer;

        // 状态缓存（用于比对新旧状态，防止 UI 频繁重绘导致闪烁）
        private byte[] _lastIn0 = new byte[4];
        private byte[] _lastIn1 = new byte[4];
        private byte[] _lastOut0 = new byte[4];
        private byte[] _lastOut1 = new byte[4];
        private bool _isFirstLoad = true;

        public FormIOListen(SingleController singleController)
        {
            _singleController = singleController;
            InitializeComponent();
        }

        public FormIOListen()
        {
            InitializeComponent();
        }

        private void FormIOPort_Load(object sender, EventArgs e)
        {
            // 1. 动态生成 UI 控件
            for (int i = 0; i < _inputContentAry.Length; i++)
            {
                CtrlIOPort iPort = new CtrlIOPort() { Head = "I" + i, Content = _inputContentAry[i] };
                iPort.SetReadOnly(true);
                flpInPort.Controls.Add(iPort);
            }
            for (int i = 0; i < _outputContentAry.Length; i++)
            {
                CtrlIOPort oPort = new CtrlIOPort() { Head = "O" + i, Content = _outputContentAry[i], LightColor = Color.DarkOrange };
                oPort.OutPortAction += SendSingle;
                flpOutPort.Controls.Add(oPort);
            }
            for (int i = 0; i < _inputContentAry1.Length; i++)
            {
                CtrlIOPort iPort = new CtrlIOPort() { Head = "I" + i, Content = _inputContentAry1[i] };
                iPort.SetReadOnly(true);
                flpInPort1.Controls.Add(iPort);
            }
            for (int i = 0; i < _outputContentAry1.Length; i++)
            {
                CtrlIOPort oPort = new CtrlIOPort() { Head = "O" + i, Content = _outputContentAry1[i], LightColor = Color.DarkOrange, Tag = i };
                oPort.OutPortAction += SendSingle;
                flpOutPort1.Controls.Add(oPort);
            }

            // 2. 确保硬件连接
            if (_singleController is null)
                _singleController = new SingleController();
            if (!_singleController.IsConnect)
                _singleController.Connect();

            // 3. 启动定时器，开始接管界面刷新 (100ms 刷新率，丝滑且不卡顿)
            _monitorTimer = new System.Windows.Forms.Timer();
            _monitorTimer.Interval = 100;
            _monitorTimer.Tick += MonitorTimer_Tick;
            _monitorTimer.Start();
        }

        /// <summary>
        /// 定时器触发：主动去硬件拉取状态并更新 UI
        /// </summary>
        private void MonitorTimer_Tick(object sender, EventArgs e)
        {
            if (_singleController == null || !_singleController.IsConnect) return;

            for (int i = 0; i < 4; i++) // 假设最多读取前4个Port (32 bit)
            {
                // 读取瞬时状态
                byte in0 = _singleController.ReadInputSingle(i);
                byte in1 = _singleController.ReadInputSingle1(i);
                byte out0 = _singleController.ReadOutputSingle(i);
                byte out1 = _singleController.ReadOutputSingle1(i);

                // 只有当状态发生变化（或者是第一次加载）时，才去遍历更新控件
                if (_isFirstLoad || in0 != _lastIn0[i])
                {
                    UpdatePortPanel(flpInPort, i, in0, true);
                    _lastIn0[i] = in0;
                }
                if (_isFirstLoad || in1 != _lastIn1[i])
                {
                    UpdatePortPanel(flpInPort1, i, in1, true);
                    _lastIn1[i] = in1;
                }
                if (_isFirstLoad || out0 != _lastOut0[i])
                {
                    UpdatePortPanel(flpOutPort, i, out0, false);
                    _lastOut0[i] = out0;
                }
                if (_isFirstLoad || out1 != _lastOut1[i])
                {
                    UpdatePortPanel(flpOutPort1, i, out1, false);
                    _lastOut1[i] = out1;
                }
            }
            _isFirstLoad = false;
        }

        /// <summary>
        /// 批量更新指定面板的 8 个指示灯
        /// </summary>
        private void UpdatePortPanel(FlowLayoutPanel flp, int portIndex, byte portData, bool isInputPanel)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                int controlIndex = portIndex * 8 + bit;
                if (controlIndex < flp.Controls.Count)
                {
                    int bitValue = (portData >> bit) & 0x1;
                    var ctrl = (CtrlIOPort)flp.Controls[controlIndex];

                    if (isInputPanel)
                        ctrl.SetInPortState(bitValue);
                    else
                        ctrl.SetOutPortState(bitValue);
                }
            }
        }

        private void SendSingle(int index, int data, bool isCard0)
        {
            if (isCard0)
                _singleController.WriteSingle(index / 8, index % 8, (byte)data);
            else
                _singleController.WriteSingle1(index / 8, index % 8, (byte)data);
        }

        private void FormIOPort_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 窗口关闭时安全销毁定时器，释放资源
            if (_monitorTimer != null)
            {
                _monitorTimer.Stop();
                _monitorTimer.Dispose();
            }
        }
    }
}