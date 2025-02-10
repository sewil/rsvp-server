namespace WvsBeta.Launcher
{
    partial class DataEdit
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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DataEdit));
            splitContainer1 = new SplitContainer();
            tvImg = new TreeView();
            cmsTreeView = new ContextMenuStrip(components);
            addWzPropertyBeforeToolStripMenuItem = new ToolStripMenuItem();
            addWzPropertyAfterToolStripMenuItem = new ToolStripMenuItem();
            addWzPropertyInsideToolStripMenuItem = new ToolStripMenuItem();
            deleteWzPropertyToolStripMenuItem = new ToolStripMenuItem();
            saveIMGToolStripMenuItem = new ToolStripMenuItem();
            dataGridView1 = new DataGridView();
            menuStrip1 = new MenuStrip();
            saveIMGToolStripMenuItem1 = new ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            cmsTreeView.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 28);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(tvImg);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(dataGridView1);
            splitContainer1.Size = new Size(1245, 663);
            splitContainer1.SplitterDistance = 415;
            splitContainer1.TabIndex = 1;
            // 
            // tvImg
            // 
            tvImg.ContextMenuStrip = cmsTreeView;
            tvImg.Dock = DockStyle.Fill;
            tvImg.Location = new Point(0, 0);
            tvImg.Name = "tvImg";
            tvImg.Size = new Size(415, 663);
            tvImg.TabIndex = 0;
            tvImg.BeforeExpand += tvImg_BeforeExpand;
            tvImg.BeforeSelect += tvImg_BeforeSelect;
            tvImg.AfterSelect += tvImg_AfterSelect;
            // 
            // cmsTreeView
            // 
            cmsTreeView.ImageScalingSize = new Size(20, 20);
            cmsTreeView.Items.AddRange(new ToolStripItem[] { addWzPropertyBeforeToolStripMenuItem, addWzPropertyAfterToolStripMenuItem, addWzPropertyInsideToolStripMenuItem, deleteWzPropertyToolStripMenuItem, saveIMGToolStripMenuItem });
            cmsTreeView.Name = "cmsTreeView";
            cmsTreeView.Size = new Size(236, 152);
            cmsTreeView.Opening += cmsTreeView_Opening;
            // 
            // addWzPropertyBeforeToolStripMenuItem
            // 
            addWzPropertyBeforeToolStripMenuItem.Name = "addWzPropertyBeforeToolStripMenuItem";
            addWzPropertyBeforeToolStripMenuItem.Size = new Size(235, 24);
            addWzPropertyBeforeToolStripMenuItem.Text = "Add WzProperty Before";
            // 
            // addWzPropertyAfterToolStripMenuItem
            // 
            addWzPropertyAfterToolStripMenuItem.Name = "addWzPropertyAfterToolStripMenuItem";
            addWzPropertyAfterToolStripMenuItem.Size = new Size(235, 24);
            addWzPropertyAfterToolStripMenuItem.Text = "Add WzProperty After";
            // 
            // addWzPropertyInsideToolStripMenuItem
            // 
            addWzPropertyInsideToolStripMenuItem.Name = "addWzPropertyInsideToolStripMenuItem";
            addWzPropertyInsideToolStripMenuItem.Size = new Size(235, 24);
            addWzPropertyInsideToolStripMenuItem.Text = "Add WzProperty Inside";
            addWzPropertyInsideToolStripMenuItem.Click += addWzPropertyInsideToolStripMenuItem_Click;
            // 
            // deleteWzPropertyToolStripMenuItem
            // 
            deleteWzPropertyToolStripMenuItem.Name = "deleteWzPropertyToolStripMenuItem";
            deleteWzPropertyToolStripMenuItem.Size = new Size(235, 24);
            deleteWzPropertyToolStripMenuItem.Text = "Delete WzProperty";
            deleteWzPropertyToolStripMenuItem.Click += deleteWzPropertyToolStripMenuItem_Click;
            // 
            // saveIMGToolStripMenuItem
            // 
            saveIMGToolStripMenuItem.Name = "saveIMGToolStripMenuItem";
            saveIMGToolStripMenuItem.Size = new Size(235, 24);
            saveIMGToolStripMenuItem.Text = "Save IMG";
            saveIMGToolStripMenuItem.Click += saveIMGToolStripMenuItem_Click;
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToResizeRows = false;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.Location = new Point(0, 0);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersWidth = 51;
            dataGridView1.Size = new Size(826, 663);
            dataGridView1.TabIndex = 1;
            dataGridView1.CellEndEdit += dataGridView1_CellEndEdit;
            dataGridView1.UserDeletedRow += dataGridView1_UserDeletedRow;
            dataGridView1.UserDeletingRow += dataGridView1_UserDeletingRow;
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(20, 20);
            menuStrip1.Items.AddRange(new ToolStripItem[] { saveIMGToolStripMenuItem1 });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1245, 28);
            menuStrip1.TabIndex = 2;
            menuStrip1.Text = "menuStrip1";
            // 
            // saveIMGToolStripMenuItem1
            // 
            saveIMGToolStripMenuItem1.Name = "saveIMGToolStripMenuItem1";
            saveIMGToolStripMenuItem1.Size = new Size(85, 24);
            saveIMGToolStripMenuItem1.Text = "Save IMG";
            saveIMGToolStripMenuItem1.Click += saveIMGToolStripMenuItem_Click;
            // 
            // DataEdit
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1245, 691);
            Controls.Add(splitContainer1);
            Controls.Add(menuStrip1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = menuStrip1;
            Name = "DataEdit";
            Text = "DataEdit";
            Load += DataEdit_Load;
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            cmsTreeView.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private SplitContainer splitContainer1;
        private DataGridView dataGridView1;
        private TreeView tvImg;
        private ContextMenuStrip cmsTreeView;
        private ToolStripMenuItem deleteWzPropertyToolStripMenuItem;
        private ToolStripMenuItem saveIMGToolStripMenuItem;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem saveIMGToolStripMenuItem1;
        private ToolStripMenuItem addWzPropertyBeforeToolStripMenuItem;
        private ToolStripMenuItem addWzPropertyAfterToolStripMenuItem;
        private ToolStripMenuItem addWzPropertyInsideToolStripMenuItem;
    }
}