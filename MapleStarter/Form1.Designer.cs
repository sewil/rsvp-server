namespace MapleStarter
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            flowLayoutPanel1 = new FlowLayoutPanel();
            label1 = new Label();
            statusStrip1 = new StatusStrip();
            toolStripStatusLabel2 = new ToolStripStatusLabel();
            tsslMapleLocation = new ToolStripStatusLabel();
            label2 = new Label();
            txtManualIP = new TextBox();
            txtManualPort = new TextBox();
            label3 = new Label();
            btnConnect = new Button();
            btnShortcut = new Button();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            flowLayoutPanel1.AutoScroll = true;
            flowLayoutPanel1.BorderStyle = BorderStyle.Fixed3D;
            flowLayoutPanel1.Location = new Point(14, 40);
            flowLayoutPanel1.Margin = new Padding(3, 4, 3, 4);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(628, 390);
            flowLayoutPanel1.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(14, 12);
            label1.Name = "label1";
            label1.Size = new Size(348, 20);
            label1.TabIndex = 1;
            label1.Text = "Listening for WvsBeta Launchers in your local area...";
            // 
            // statusStrip1
            // 
            statusStrip1.ImageScalingSize = new Size(20, 20);
            statusStrip1.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel2, tsslMapleLocation });
            statusStrip1.Location = new Point(0, 478);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Padding = new Padding(1, 0, 16, 0);
            statusStrip1.Size = new Size(654, 26);
            statusStrip1.TabIndex = 2;
            statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel2
            // 
            toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            toolStripStatusLabel2.Size = new Size(112, 20);
            toolStripStatusLabel2.Text = "Maple location:";
            // 
            // tsslMapleLocation
            // 
            tsslMapleLocation.Name = "tsslMapleLocation";
            tsslMapleLocation.Size = new Size(74, 20);
            tsslMapleLocation.Text = "not found";
            tsslMapleLocation.Click += toolStripStatusLabel1_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(14, 440);
            label2.Name = "label2";
            label2.Size = new Size(130, 20);
            label2.TabIndex = 3;
            label2.Text = "Or enter manually:";
            // 
            // txtManualIP
            // 
            txtManualIP.Location = new Point(150, 437);
            txtManualIP.Name = "txtManualIP";
            txtManualIP.Size = new Size(163, 27);
            txtManualIP.TabIndex = 4;
            // 
            // txtManualPort
            // 
            txtManualPort.Location = new Point(337, 437);
            txtManualPort.Name = "txtManualPort";
            txtManualPort.Size = new Size(55, 27);
            txtManualPort.TabIndex = 5;
            txtManualPort.Text = "8484";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(319, 440);
            label3.Name = "label3";
            label3.Size = new Size(12, 20);
            label3.TabIndex = 6;
            label3.Text = ":";
            // 
            // btnConnect
            // 
            btnConnect.Location = new Point(436, 436);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(94, 29);
            btnConnect.TabIndex = 7;
            btnConnect.Text = "Play!";
            btnConnect.UseVisualStyleBackColor = true;
            btnConnect.Click += btnConnect_Click;
            // 
            // btnShortcut
            // 
            btnShortcut.Location = new Point(536, 436);
            btnShortcut.Name = "btnShortcut";
            btnShortcut.Size = new Size(94, 29);
            btnShortcut.TabIndex = 8;
            btnShortcut.Text = "Save";
            btnShortcut.UseVisualStyleBackColor = true;
            btnShortcut.Click += btnShortcut_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(654, 504);
            Controls.Add(btnShortcut);
            Controls.Add(btnConnect);
            Controls.Add(label3);
            Controls.Add(txtManualPort);
            Controls.Add(txtManualIP);
            Controls.Add(label2);
            Controls.Add(statusStrip1);
            Controls.Add(label1);
            Controls.Add(flowLayoutPanel1);
            Margin = new Padding(3, 4, 3, 4);
            MaximizeBox = false;
            MaximumSize = new Size(672, 551);
            MinimumSize = new Size(672, 551);
            Name = "Form1";
            Text = "MapleStarter";
            Load += Form1_Load;
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private FlowLayoutPanel flowLayoutPanel1;
        private Label label1;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel tsslMapleLocation;
        private ToolStripStatusLabel toolStripStatusLabel2;
        private Label label2;
        private TextBox txtManualIP;
        private TextBox txtManualPort;
        private Label label3;
        private Button btnConnect;
        private Button btnShortcut;
    }
}
