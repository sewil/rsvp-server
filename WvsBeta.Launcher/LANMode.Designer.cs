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
            lbInterfaces = new ListBox();
            label1 = new Label();
            button1 = new Button();
            SuspendLayout();
            // 
            // lbInterfaces
            // 
            lbInterfaces.FormattingEnabled = true;
            lbInterfaces.Location = new Point(12, 32);
            lbInterfaces.Name = "lbInterfaces";
            lbInterfaces.Size = new Size(354, 154);
            lbInterfaces.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 9);
            label1.Name = "label1";
            label1.Size = new Size(202, 15);
            label1.TabIndex = 1;
            label1.Text = "Network to announce LAN server on:";
            // 
            // button1
            // 
            button1.DialogResult = DialogResult.Yes;
            button1.Location = new Point(12, 192);
            button1.Name = "button1";
            button1.Size = new Size(354, 23);
            button1.TabIndex = 2;
            button1.Text = "Apply Settings";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // LANMode
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(378, 226);
            Controls.Add(button1);
            Controls.Add(label1);
            Controls.Add(lbInterfaces);
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