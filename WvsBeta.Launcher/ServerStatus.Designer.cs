namespace WvsBeta.Launcher
{
    partial class ServerStatus
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            groupBox1 = new GroupBox();
            btnReloadConfig = new Button();
            propertyGrid1 = new PropertyGrid();
            btnReinstall = new Button();
            btnStart = new Button();
            tmrUIUpdater = new System.Windows.Forms.Timer(components);
            groupBox1.SuspendLayout();
            SuspendLayout();
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(btnReloadConfig);
            groupBox1.Controls.Add(propertyGrid1);
            groupBox1.Controls.Add(btnReinstall);
            groupBox1.Controls.Add(btnStart);
            groupBox1.Dock = DockStyle.Fill;
            groupBox1.Location = new Point(0, 0);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(578, 344);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "groupBox1";
            // 
            // btnReloadConfig
            // 
            btnReloadConfig.Location = new Point(168, 22);
            btnReloadConfig.Name = "btnReloadConfig";
            btnReloadConfig.Size = new Size(75, 23);
            btnReloadConfig.TabIndex = 3;
            btnReloadConfig.Text = "Reload Cfg";
            btnReloadConfig.UseVisualStyleBackColor = true;
            btnReloadConfig.Click += btnReloadConfig_Click;
            // 
            // propertyGrid1
            // 
            propertyGrid1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            propertyGrid1.BackColor = SystemColors.Control;
            propertyGrid1.Location = new Point(6, 51);
            propertyGrid1.Name = "propertyGrid1";
            propertyGrid1.Size = new Size(566, 287);
            propertyGrid1.TabIndex = 2;
            // 
            // btnReinstall
            // 
            btnReinstall.Location = new Point(87, 22);
            btnReinstall.Name = "btnReinstall";
            btnReinstall.Size = new Size(75, 23);
            btnReinstall.TabIndex = 1;
            btnReinstall.Text = "Reinstall";
            btnReinstall.UseVisualStyleBackColor = true;
            btnReinstall.Click += btnReinstall_Click;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(6, 22);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(75, 23);
            btnStart.TabIndex = 0;
            btnStart.Text = "Start";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // tmrUIUpdater
            // 
            tmrUIUpdater.Enabled = true;
            tmrUIUpdater.Interval = 1000;
            tmrUIUpdater.Tick += tmrUIUpdater_Tick;
            // 
            // ServerStatus
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(groupBox1);
            Name = "ServerStatus";
            Size = new Size(578, 344);
            groupBox1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private GroupBox groupBox1;
        private Button btnStart;
        private Button btnReinstall;
        private Button btnReloadConfig;
        private PropertyGrid propertyGrid1;
        private System.Windows.Forms.Timer tmrUIUpdater;
    }
}
