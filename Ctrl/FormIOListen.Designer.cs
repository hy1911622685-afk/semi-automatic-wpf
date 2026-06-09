
namespace MotionCard.Ctrl
{
    partial class FormIOListen
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            AntdUI.Tabs.StyleLine styleLine1 = new AntdUI.Tabs.StyleLine();
            this.panel1 = new System.Windows.Forms.Panel();
            this.tabs1 = new AntdUI.Tabs();
            this.tabPage1 = new AntdUI.TabPage();
            this.collapse1 = new AntdUI.Collapse();
            this.collapseItem2 = new AntdUI.CollapseItem();
            this.flpInPort = new System.Windows.Forms.FlowLayoutPanel();
            this.collapseItem1 = new AntdUI.CollapseItem();
            this.flpOutPort = new AntdUI.In.FlowLayoutPanel();
            this.tabPage2 = new AntdUI.TabPage();
            this.collapse2 = new AntdUI.Collapse();
            this.collapseItem3 = new AntdUI.CollapseItem();
            this.flpInPort1 = new System.Windows.Forms.FlowLayoutPanel();
            this.collapseItem4 = new AntdUI.CollapseItem();
            this.flpOutPort1 = new AntdUI.In.FlowLayoutPanel();
            this.panel1.SuspendLayout();
            this.tabs1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.collapse1.SuspendLayout();
            this.collapseItem2.SuspendLayout();
            this.collapseItem1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.collapse2.SuspendLayout();
            this.collapseItem3.SuspendLayout();
            this.collapseItem4.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.tabs1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Margin = new System.Windows.Forms.Padding(4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1324, 931);
            this.panel1.TabIndex = 1;
            // 
            // tabs1
            // 
            this.tabs1.Controls.Add(this.tabPage1);
            this.tabs1.Controls.Add(this.tabPage2);
            this.tabs1.Cursor = System.Windows.Forms.Cursors.Default;
            this.tabs1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabs1.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tabs1.Location = new System.Drawing.Point(0, 0);
            this.tabs1.Margin = new System.Windows.Forms.Padding(4);
            this.tabs1.Name = "tabs1";
            this.tabs1.Pages.Add(this.tabPage1);
            this.tabs1.Pages.Add(this.tabPage2);
            this.tabs1.Size = new System.Drawing.Size(1324, 931);
            this.tabs1.Style = styleLine1;
            this.tabs1.TabIndex = 1;
            this.tabs1.Text = "tabs1";
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.collapse1);
            this.tabPage1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabPage1.Location = new System.Drawing.Point(4, 33);
            this.tabPage1.Margin = new System.Windows.Forms.Padding(7, 4, 7, 4);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Size = new System.Drawing.Size(1316, 894);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "   板卡0   ";
            // 
            // collapse1
            // 
            this.collapse1.Cursor = System.Windows.Forms.Cursors.Hand;
            this.collapse1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.collapse1.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F);
            this.collapse1.FontExpand = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.collapse1.Gap = 8;
            this.collapse1.Items.Add(this.collapseItem2);
            this.collapse1.Items.Add(this.collapseItem1);
            this.collapse1.Location = new System.Drawing.Point(0, 0);
            this.collapse1.Margin = new System.Windows.Forms.Padding(4);
            this.collapse1.Name = "collapse1";
            this.collapse1.Size = new System.Drawing.Size(1316, 894);
            this.collapse1.TabIndex = 5;
            this.collapse1.Text = "collapse1";
            // 
            // collapseItem2
            // 
            this.collapseItem2.Controls.Add(this.flpInPort);
            this.collapseItem2.Expand = true;
            this.collapseItem2.Location = new System.Drawing.Point(20, 65);
            this.collapseItem2.Margin = new System.Windows.Forms.Padding(4);
            this.collapseItem2.Name = "collapseItem2";
            this.collapseItem2.Size = new System.Drawing.Size(1276, 360);
            this.collapseItem2.TabIndex = 1;
            this.collapseItem2.Text = "输入";
            // 
            // flpInPort
            // 
            this.flpInPort.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpInPort.Location = new System.Drawing.Point(0, 0);
            this.flpInPort.Name = "flpInPort";
            this.flpInPort.Size = new System.Drawing.Size(1276, 360);
            this.flpInPort.TabIndex = 0;
            // 
            // collapseItem1
            // 
            this.collapseItem1.Controls.Add(this.flpOutPort);
            this.collapseItem1.Expand = true;
            this.collapseItem1.Location = new System.Drawing.Point(20, 510);
            this.collapseItem1.Margin = new System.Windows.Forms.Padding(4);
            this.collapseItem1.Name = "collapseItem1";
            this.collapseItem1.Size = new System.Drawing.Size(1276, 360);
            this.collapseItem1.TabIndex = 4;
            this.collapseItem1.Text = "输出";
            // 
            // flpOutPort
            // 
            this.flpOutPort.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpOutPort.Location = new System.Drawing.Point(0, 0);
            this.flpOutPort.Name = "flpOutPort";
            this.flpOutPort.Size = new System.Drawing.Size(1276, 360);
            this.flpOutPort.TabIndex = 0;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.collapse2);
            this.tabPage2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabPage2.Location = new System.Drawing.Point(4, 33);
            this.tabPage2.Margin = new System.Windows.Forms.Padding(7, 4, 7, 4);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Size = new System.Drawing.Size(1316, 894);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "   板卡1   ";
            // 
            // collapse2
            // 
            this.collapse2.Cursor = System.Windows.Forms.Cursors.Hand;
            this.collapse2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.collapse2.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F);
            this.collapse2.FontExpand = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.collapse2.Gap = 8;
            this.collapse2.Items.Add(this.collapseItem3);
            this.collapse2.Items.Add(this.collapseItem4);
            this.collapse2.Location = new System.Drawing.Point(0, 0);
            this.collapse2.Margin = new System.Windows.Forms.Padding(4);
            this.collapse2.Name = "collapse2";
            this.collapse2.Size = new System.Drawing.Size(1316, 894);
            this.collapse2.TabIndex = 6;
            this.collapse2.Text = "collapse2";
            // 
            // collapseItem3
            // 
            this.collapseItem3.Controls.Add(this.flpInPort1);
            this.collapseItem3.Expand = true;
            this.collapseItem3.Location = new System.Drawing.Point(20, 65);
            this.collapseItem3.Margin = new System.Windows.Forms.Padding(4);
            this.collapseItem3.Name = "collapseItem3";
            this.collapseItem3.Size = new System.Drawing.Size(1276, 360);
            this.collapseItem3.TabIndex = 1;
            this.collapseItem3.Text = "输入";
            // 
            // flpInPort1
            // 
            this.flpInPort1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpInPort1.Location = new System.Drawing.Point(0, 0);
            this.flpInPort1.Name = "flpInPort1";
            this.flpInPort1.Size = new System.Drawing.Size(1276, 360);
            this.flpInPort1.TabIndex = 0;
            // 
            // collapseItem4
            // 
            this.collapseItem4.Controls.Add(this.flpOutPort1);
            this.collapseItem4.Expand = true;
            this.collapseItem4.Location = new System.Drawing.Point(20, 510);
            this.collapseItem4.Margin = new System.Windows.Forms.Padding(4);
            this.collapseItem4.Name = "collapseItem4";
            this.collapseItem4.Size = new System.Drawing.Size(1276, 360);
            this.collapseItem4.TabIndex = 4;
            this.collapseItem4.Text = "输出";
            // 
            // flpOutPort1
            // 
            this.flpOutPort1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpOutPort1.Location = new System.Drawing.Point(0, 0);
            this.flpOutPort1.Name = "flpOutPort1";
            this.flpOutPort1.Size = new System.Drawing.Size(1276, 360);
            this.flpOutPort1.TabIndex = 0;
            // 
            // FormIOPort
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 21F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(1324, 931);
            this.Controls.Add(this.panel1);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Name = "FormIOPort";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "IO状态监控";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormIOPort_FormClosing);
            this.Load += new System.EventHandler(this.FormIOPort_Load);
            this.panel1.ResumeLayout(false);
            this.tabs1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.collapse1.ResumeLayout(false);
            this.collapseItem2.ResumeLayout(false);
            this.collapseItem1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.collapse2.ResumeLayout(false);
            this.collapseItem3.ResumeLayout(false);
            this.collapseItem4.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private AntdUI.Tabs tabs1;
        private AntdUI.TabPage tabPage1;
        private AntdUI.Collapse collapse1;
        private AntdUI.CollapseItem collapseItem2;
        private AntdUI.CollapseItem collapseItem1;
        private AntdUI.TabPage tabPage2;
        private System.Windows.Forms.FlowLayoutPanel flpInPort;
        private AntdUI.In.FlowLayoutPanel flpOutPort;
        private AntdUI.Collapse collapse2;
        private AntdUI.CollapseItem collapseItem3;
        private System.Windows.Forms.FlowLayoutPanel flpInPort1;
        private AntdUI.CollapseItem collapseItem4;
        private AntdUI.In.FlowLayoutPanel flpOutPort1;
    }
}