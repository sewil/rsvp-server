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
            ssMariaDB = new ServerStatus();
            ssRedis = new ServerStatus();
            ssCenter = new ServerStatus();
            ssLogin0 = new ServerStatus();
            tableLayoutPanel1 = new TableLayoutPanel();
            ssShop0 = new ServerStatus();
            ssGame0 = new ServerStatus();
            tableLayoutPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // ssMariaDB
            // 
            ssMariaDB.Dock = DockStyle.Fill;
            ssMariaDB.Location = new Point(3, 338);
            ssMariaDB.Name = "ssMariaDB";
            ssMariaDB.Process = null;
            ssMariaDB.Reinstallable = true;
            ssMariaDB.Size = new Size(368, 330);
            ssMariaDB.TabIndex = 2;
            ssMariaDB.Title = "Database (MariaDB)";
            ssMariaDB.WorkingDirectory = "redist\\mariadb\\bin";
            // 
            // ssRedis
            // 
            ssRedis.Dock = DockStyle.Fill;
            ssRedis.Location = new Point(377, 338);
            ssRedis.Name = "ssRedis";
            ssRedis.Process = null;
            ssRedis.Reinstallable = true;
            ssRedis.Size = new Size(368, 330);
            ssRedis.TabIndex = 3;
            ssRedis.Title = "Concurrency / Cache (Redis)";
            ssRedis.WorkingDirectory = "redist\\redis";
            // 
            // ssCenter
            // 
            ssCenter.Dock = DockStyle.Fill;
            ssCenter.Location = new Point(377, 3);
            ssCenter.Name = "ssCenter";
            ssCenter.Process = null;
            ssCenter.Reinstallable = true;
            ssCenter.Size = new Size(368, 329);
            ssCenter.TabIndex = 4;
            ssCenter.Title = "Scania (Center)";
            // 
            // ssLogin0
            // 
            ssLogin0.Dock = DockStyle.Fill;
            ssLogin0.Location = new Point(3, 3);
            ssLogin0.Name = "ssLogin0";
            ssLogin0.Process = null;
            ssLogin0.Reinstallable = true;
            ssLogin0.Size = new Size(368, 329);
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
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Size = new Size(1123, 671);
            tableLayoutPanel1.TabIndex = 6;
            // 
            // ssShop0
            // 
            ssShop0.Dock = DockStyle.Fill;
            ssShop0.Location = new Point(751, 338);
            ssShop0.Name = "ssShop0";
            ssShop0.Process = null;
            ssShop0.Reinstallable = true;
            ssShop0.Size = new Size(369, 330);
            ssShop0.TabIndex = 7;
            ssShop0.Title = "CashShop (Shop0)";
            // 
            // ssGame0
            // 
            ssGame0.Dock = DockStyle.Fill;
            ssGame0.Location = new Point(751, 3);
            ssGame0.Name = "ssGame0";
            ssGame0.Process = null;
            ssGame0.Reinstallable = true;
            ssGame0.Size = new Size(369, 329);
            ssGame0.TabIndex = 6;
            ssGame0.Title = "Channel 1 (Game0)";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1123, 671);
            Controls.Add(tableLayoutPanel1);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            tableLayoutPanel1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
        private ServerStatus ssMariaDB;
        private ServerStatus ssRedis;
        private ServerStatus ssCenter;
        private ServerStatus ssLogin0;
        private TableLayoutPanel tableLayoutPanel1;
        private ServerStatus ssGame0;
        private ServerStatus ssShop0;
    }
}
