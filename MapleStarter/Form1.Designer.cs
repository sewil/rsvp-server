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
            tsslMapleLocation = new ToolStripStatusLabel();
            toolStripStatusLabel2 = new ToolStripStatusLabel();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.AutoScroll = true;
            flowLayoutPanel1.Location = new Point(12, 30);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(550, 331);
            flowLayoutPanel1.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 9);
            label1.Name = "label1";
            label1.Size = new Size(280, 15);
            label1.TabIndex = 1;
            label1.Text = "Listening for WvsBeta Launchers in your local area...";
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel2, tsslMapleLocation });
            statusStrip1.Location = new Point(0, 364);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(574, 22);
            statusStrip1.TabIndex = 2;
            statusStrip1.Text = "statusStrip1";
            // 
            // tsslMapleLocation
            // 
            tsslMapleLocation.Name = "tsslMapleLocation";
            tsslMapleLocation.Size = new Size(60, 17);
            tsslMapleLocation.Text = "not found";
            tsslMapleLocation.Click += toolStripStatusLabel1_Click;
            // 
            // toolStripStatusLabel2
            // 
            toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            toolStripStatusLabel2.Size = new Size(89, 17);
            toolStripStatusLabel2.Text = "Maple location:";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(574, 386);
            Controls.Add(statusStrip1);
            Controls.Add(label1);
            Controls.Add(flowLayoutPanel1);
            MaximizeBox = false;
            MaximumSize = new Size(590, 425);
            MinimumSize = new Size(590, 425);
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
    }
}
