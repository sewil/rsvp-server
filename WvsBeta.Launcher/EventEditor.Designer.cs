namespace WvsBeta.Launcher
{
    partial class EventEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EventEditor));
            button1 = new Button();
            button2 = new Button();
            dgvEvents = new DataGridView();
            ((System.ComponentModel.ISupportInitialize)dgvEvents).BeginInit();
            SuspendLayout();
            // 
            // button1
            // 
            button1.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            button1.DialogResult = DialogResult.Yes;
            button1.Location = new Point(321, 311);
            button1.Margin = new Padding(3, 4, 3, 4);
            button1.Name = "button1";
            button1.Size = new Size(122, 31);
            button1.TabIndex = 0;
            button1.Text = "Save";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // button2
            // 
            button2.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            button2.DialogResult = DialogResult.Cancel;
            button2.Location = new Point(450, 311);
            button2.Margin = new Padding(3, 4, 3, 4);
            button2.Name = "button2";
            button2.Size = new Size(122, 31);
            button2.TabIndex = 1;
            button2.Text = "Cancel";
            button2.UseVisualStyleBackColor = true;
            // 
            // dgvEvents
            // 
            dgvEvents.AllowUserToAddRows = false;
            dgvEvents.AllowUserToDeleteRows = false;
            dgvEvents.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvEvents.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvEvents.Location = new Point(14, 16);
            dgvEvents.Margin = new Padding(3, 4, 3, 4);
            dgvEvents.Name = "dgvEvents";
            dgvEvents.RowHeadersWidth = 51;
            dgvEvents.Size = new Size(559, 287);
            dgvEvents.TabIndex = 2;
            // 
            // EventEditor
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(586, 357);
            Controls.Add(dgvEvents);
            Controls.Add(button2);
            Controls.Add(button1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(3, 4, 3, 4);
            Name = "EventEditor";
            Text = "EventEditor";
            Load += EventEditor_Load;
            ((System.ComponentModel.ISupportInitialize)dgvEvents).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Button button1;
        private Button button2;
        private DataGridView dgvEvents;
    }
}