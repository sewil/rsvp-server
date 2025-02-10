namespace WvsBeta.Launcher
{
    partial class LANMode
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LANMode));
            lbInterfaces = new ListBox();
            label1 = new Label();
            button1 = new Button();
            SuspendLayout();
            // 
            // lbInterfaces
            // 
            lbInterfaces.FormattingEnabled = true;
            lbInterfaces.Location = new Point(14, 43);
            lbInterfaces.Margin = new Padding(3, 4, 3, 4);
            lbInterfaces.Name = "lbInterfaces";
            lbInterfaces.Size = new Size(404, 204);
            lbInterfaces.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(14, 12);
            label1.Name = "label1";
            label1.Size = new Size(250, 20);
            label1.TabIndex = 1;
            label1.Text = "Network to announce LAN server on:";
            // 
            // button1
            // 
            button1.DialogResult = DialogResult.Yes;
            button1.Location = new Point(14, 256);
            button1.Margin = new Padding(3, 4, 3, 4);
            button1.Name = "button1";
            button1.Size = new Size(405, 31);
            button1.TabIndex = 2;
            button1.Text = "Apply Settings";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // LANMode
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(432, 301);
            Controls.Add(button1);
            Controls.Add(label1);
            Controls.Add(lbInterfaces);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(3, 4, 3, 4);
            Name = "LANMode";
            Text = "LAN Mode";
            Load += LANMode_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ListBox lbInterfaces;
        private Label label1;
        private Button button1;
    }
}