using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MotionCard.Ctrl
{
    public partial class CtrlIOPort : UserControl
    {

        private string _head;
        public string Head
        {
            get { return _head; }
            set
            {
                _head = value;
                lbHead.Text = _head;
            }
        }
        private Color _lightColor;
        public Color LightColor
        {
            get { return _lightColor; }
            set
            {
                _lightColor = value;
                lbHead.ForeColor = _lightColor;
            }
        }
        private string _content;
        public string Content
        {
            get { return _content; }
            set
            {
                _content = value;
                lbContent.Text = _content;
            }
        }


        public Action<int, int, bool> OutPortAction;

        public CtrlIOPort()
        {
            InitializeComponent();
        }

        public void SetReadOnly(bool data)
        {
            btn.Click -= btn_Click;
        }

        public void SetInPortState(int data)
        {
            btn.DefaultBack = data == 0 ? Color.White : Color.FromArgb(76, 175, 80);
        }
        public void SetOutPortState(int data)
        {
            btn.DefaultBack = data == 0 ? Color.White : Color.FromArgb(255, 152, 0);
        }

        public void SetOutPortSingle()
        {

        }

        private void btn_Click(object sender, EventArgs e)
        {
            OutPortAction(Convert.ToInt32(Regex.Replace(_head, @"\D", "")), btn.DefaultBack == Color.White ? 1 : 0, Tag is null);
            //if (btn.DefaultBack == Color.White)
            //{
            //    btn.DefaultBack = Color.FromArgb(255, 152, 0);
            //    OutPortAction(1)
            //}
            //else
            //{
            //    btn.DefaultBack = Color.White;
            //}
        }
    }
}
