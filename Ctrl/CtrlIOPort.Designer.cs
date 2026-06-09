
namespace MotionCard.Ctrl
{
    partial class CtrlIOPort
    {
        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.panel1 = new AntdUI.Panel();
            this.btn = new AntdUI.Button();
            this.lbHead = new AntdUI.Label();
            this.lbContent = new AntdUI.Label();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BorderWidth = 1F;
            this.panel1.Controls.Add(this.btn);
            this.panel1.Controls.Add(this.lbHead);
            this.panel1.Controls.Add(this.lbContent);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(110, 110);
            this.panel1.TabIndex = 0;
            this.panel1.Text = "panel1";
            // 
            // btn
            // 
            this.btn.BorderWidth = 1F;
            this.btn.IconHoverSvg = "";
            this.btn.IconRatio = 1.2F;
            this.btn.IconSvg = "";
            this.btn.Location = new System.Drawing.Point(38, 40);
            this.btn.Name = "btn";
            this.btn.Shape = AntdUI.TShape.Round;
            this.btn.Size = new System.Drawing.Size(30, 30);
            this.btn.TabIndex = 3;
            this.btn.WaveSize = 0;
            this.btn.Click += new System.EventHandler(this.btn_Click);
            // 
            // lbHead
            // 
            this.lbHead.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lbHead.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.lbHead.Location = new System.Drawing.Point(16, 10);
            this.lbHead.Name = "lbHead";
            this.lbHead.Size = new System.Drawing.Size(75, 23);
            this.lbHead.TabIndex = 4;
            this.lbHead.Text = "I00";
            this.lbHead.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lbContent
            // 
            this.lbContent.Location = new System.Drawing.Point(16, 80);
            this.lbContent.Name = "lbContent";
            this.lbContent.Size = new System.Drawing.Size(75, 23);
            this.lbContent.TabIndex = 2;
            this.lbContent.Text = "运行指示灯";
            this.lbContent.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // CtrlIOPort
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.panel1);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "CtrlIOPort";
            this.Size = new System.Drawing.Size(110, 110);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private AntdUI.Panel panel1;
        private AntdUI.Button btn;
        private AntdUI.Label lbContent;
        private AntdUI.Label lbHead;
    }
}
