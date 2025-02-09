namespace WvsBeta.Launcher
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
            components = new System.ComponentModel.Container();
            ssMariaDB = new ServerStatus();
            ssRedis = new ServerStatus();
            ssCenter = new ServerStatus();
            ssLogin0 = new ServerStatus();
            tableLayoutPanel1 = new TableLayoutPanel();
            ssShop0 = new ServerStatus();
            ssGame0 = new ServerStatus();
            menuStrip1 = new MenuStrip();
            userManagerToolStripMenuItem = new ToolStripMenuItem();
            eventManagerToolStripMenuItem = new ToolStripMenuItem();
            configureLANModeToolStripMenuItem = new ToolStripMenuItem();
            statusStrip1 = new StatusStrip();
            toolStripStatusLabel1 = new ToolStripStatusLabel();
            tsslLANStatus = new ToolStripStatusLabel();
            tmrServerAnnouncer = new System.Windows.Forms.Timer(components);
            toolStripMenuItem1 = new ToolStripMenuItem();
            tableLayoutPanel1.SuspendLayout();
            menuStrip1.SuspendLayout();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // ssMariaDB
            // 
            ssMariaDB.Dock = DockStyle.Fill;
            ssMariaDB.ExecutableName = null;
            ssMariaDB.Location = new Point(3, 481);
            ssMariaDB.Margin = new Padding(3, 5, 3, 5);
            ssMariaDB.Name = "ssMariaDB";
            ssMariaDB.Reinstallable = true;
            ssMariaDB.Size = new Size(421, 466);
            ssMariaDB.TabIndex = 2;
            ssMariaDB.Title = "Database (MariaDB)";
            ssMariaDB.WorkingDirectory = "redist\\mariadb\\bin";
            // 
            // ssRedis
            // 
            ssRedis.Dock = DockStyle.Fill;
            ssRedis.ExecutableName = null;
            ssRedis.Location = new Point(430, 481);
            ssRedis.Margin = new Padding(3, 5, 3, 5);
            ssRedis.Name = "ssRedis";
            ssRedis.Reinstallable = true;
            ssRedis.Size = new Size(421, 466);
            ssRedis.TabIndex = 3;
            ssRedis.Title = "Concurrency / Cache (Redis)";
            ssRedis.WorkingDirectory = "redist\\redis";
            // 
            // ssCenter
            // 
            ssCenter.Dock = DockStyle.Fill;
            ssCenter.ExecutableName = null;
            ssCenter.Location = new Point(430, 5);
            ssCenter.Margin = new Padding(3, 5, 3, 5);
            ssCenter.Name = "ssCenter";
            ssCenter.Size = new Size(421, 466);
            ssCenter.TabIndex = 4;
            ssCenter.Title = "Scania (Center)";
            // 
            // ssLogin0
            // 
            ssLogin0.Dock = DockStyle.Fill;
            ssLogin0.ExecutableName = null;
            ssLogin0.Location = new Point(3, 5);
            ssLogin0.Margin = new Padding(3, 5, 3, 5);
            ssLogin0.Name = "ssLogin0";
            ssLogin0.Size = new Size(421, 466);
            ssLogin0.TabIndex = 5;
            ssLogin0.Title = "Login 1 (Login0)";
            ssLogin0.Load += serverStatus1_Load;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanel1.ColumnCount = 3;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333321F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333321F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333321F));
            tableLayoutPanel1.Controls.Add(ssShop0, 2, 1);
            tableLayoutPanel1.Controls.Add(ssGame0, 2, 0);
            tableLayoutPanel1.Controls.Add(ssLogin0, 0, 0);
            tableLayoutPanel1.Controls.Add(ssRedis, 1, 1);
            tableLayoutPanel1.Controls.Add(ssCenter, 1, 0);
            tableLayoutPanel1.Controls.Add(ssMariaDB, 0, 1);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            tableLayoutPanel1.Location = new Point(0, 30);
            tableLayoutPanel1.Margin = new Padding(3, 4, 3, 4);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Size = new Size(1283, 952);
            tableLayoutPanel1.TabIndex = 6;
            // 
            // ssShop0
            // 
            ssShop0.Dock = DockStyle.Fill;
            ssShop0.ExecutableName = null;
            ssShop0.Location = new Point(857, 481);
            ssShop0.Margin = new Padding(3, 5, 3, 5);
            ssShop0.Name = "ssShop0";
            ssShop0.Size = new Size(423, 466);
            ssShop0.TabIndex = 7;
            ssShop0.Title = "CashShop (Shop0)";
            // 
            // ssGame0
            // 
            ssGame0.Dock = DockStyle.Fill;
            ssGame0.ExecutableName = null;
            ssGame0.Location = new Point(857, 5);
            ssGame0.Margin = new Padding(3, 5, 3, 5);
            ssGame0.Name = "ssGame0";
            ssGame0.Size = new Size(423, 466);
            ssGame0.TabIndex = 6;
            ssGame0.Title = "Channel 1 (Game0)";
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(20, 20);
            menuStrip1.Items.AddRange(new ToolStripItem[] { userManagerToolStripMenuItem, eventManagerToolStripMenuItem, configureLANModeToolStripMenuItem, toolStripMenuItem1 });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Padding = new Padding(7, 3, 0, 3);
            menuStrip1.Size = new Size(1283, 30);
            menuStrip1.TabIndex = 7;
            menuStrip1.Text = "menuStrip1";
            // 
            // userManagerToolStripMenuItem
            // 
            userManagerToolStripMenuItem.Name = "userManagerToolStripMenuItem";
            userManagerToolStripMenuItem.Size = new Size(115, 24);
            userManagerToolStripMenuItem.Text = "User Manager";
            userManagerToolStripMenuItem.Click += userManagerToolStripMenuItem_Click;
            // 
            // eventManagerToolStripMenuItem
            // 
            eventManagerToolStripMenuItem.Name = "eventManagerToolStripMenuItem";
            eventManagerToolStripMenuItem.Size = new Size(103, 24);
            eventManagerToolStripMenuItem.Text = "Event Editor";
            eventManagerToolStripMenuItem.Click += eventManagerToolStripMenuItem_Click;
            // 
            // configureLANModeToolStripMenuItem
            // 
            configureLANModeToolStripMenuItem.Name = "configureLANModeToolStripMenuItem";
            configureLANModeToolStripMenuItem.Size = new Size(163, 24);
            configureLANModeToolStripMenuItem.Text = "Configure LAN mode";
            configureLANModeToolStripMenuItem.Click += configureLANModeToolStripMenuItem_Click;
            // 
            // statusStrip1
            // 
            statusStrip1.ImageScalingSize = new Size(20, 20);
            statusStrip1.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel1, tsslLANStatus });
            statusStrip1.Location = new Point(0, 982);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Padding = new Padding(1, 0, 16, 0);
            statusStrip1.Size = new Size(1283, 26);
            statusStrip1.TabIndex = 8;
            statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            toolStripStatusLabel1.Size = new Size(121, 20);
            toolStripStatusLabel1.Text = "LAN Server state:";
            toolStripStatusLabel1.Click += toolStripStatusLabel1_Click;
            // 
            // tsslLANStatus
            // 
            tsslLANStatus.Name = "tsslLANStatus";
            tsslLANStatus.Size = new Size(31, 20);
            tsslLANStatus.Text = "n/a";
            // 
            // tmrServerAnnouncer
            // 
            tmrServerAnnouncer.Enabled = true;
            tmrServerAnnouncer.Interval = 2000;
            tmrServerAnnouncer.Tick += tmrServerAnnouncer_Tick;
            // 
            // toolStripMenuItem1
            // 
            toolStripMenuItem1.Name = "toolStripMenuItem1";
            toolStripMenuItem1.Size = new Size(99, 24);
            toolStripMenuItem1.Text = "Data Editor";
            toolStripMenuItem1.Click += toolStripMenuItem1_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1283, 1008);
            Controls.Add(tableLayoutPanel1);
            Controls.Add(menuStrip1);
            Controls.Add(statusStrip1);
            MainMenuStrip = menuStrip1;
            Margin = new Padding(3, 4, 3, 4);
            Name = "Form1";
            Text = "WvsBeta Launcher";
            Load += Form1_Load;
            tableLayoutPanel1.ResumeLayout(false);
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private ServerStatus ssMariaDB;
        private ServerStatus ssRedis;
        private ServerStatus ssCenter;
        private ServerStatus ssLogin0;
        private TableLayoutPanel tableLayoutPanel1;
        private ServerStatus ssGame0;
        private ServerStatus ssShop0;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem userManagerToolStripMenuItem;
        private ToolStripMenuItem configureLANModeToolStripMenuItem;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel toolStripStatusLabel1;
        private ToolStripStatusLabel tsslLANStatus;
        private System.Windows.Forms.Timer tmrServerAnnouncer;
        private ToolStripMenuItem eventManagerToolStripMenuItem;
        private ToolStripMenuItem toolStripMenuItem1;
    }
}
