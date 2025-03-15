namespace WvsBeta.Launcher
{
    partial class ImageEdit
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
            pbCurrent = new PictureBox();
            pbNew = new PictureBox();
            label1 = new Label();
            label2 = new Label();
            btnChange = new Button();
            cbPixFormat = new ComboBox();
            label3 = new Label();
            btnSave = new Button();
            label4 = new Label();
            lblCurImageFormat = new Label();
            lblCurImageSize = new Label();
            lblNewImageSize = new Label();
            ((System.ComponentModel.ISupportInitialize)pbCurrent).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbNew).BeginInit();
            SuspendLayout();
            // 
            // pbCurrent
            // 
            pbCurrent.BorderStyle = BorderStyle.Fixed3D;
            pbCurrent.Location = new Point(12, 32);
            pbCurrent.Name = "pbCurrent";
            pbCurrent.Size = new Size(443, 336);
            pbCurrent.SizeMode = PictureBoxSizeMode.CenterImage;
            pbCurrent.TabIndex = 0;
            pbCurrent.TabStop = false;
            // 
            // pbNew
            // 
            pbNew.BorderStyle = BorderStyle.Fixed3D;
            pbNew.Location = new Point(535, 32);
            pbNew.Name = "pbNew";
            pbNew.Size = new Size(443, 336);
            pbNew.SizeMode = PictureBoxSizeMode.CenterImage;
            pbNew.TabIndex = 1;
            pbNew.TabStop = false;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(186, 9);
            label1.Name = "label1";
            label1.Size = new Size(103, 20);
            label1.TabIndex = 2;
            label1.Text = "Current image";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(711, 9);
            label2.Name = "label2";
            label2.Size = new Size(85, 20);
            label2.TabIndex = 3;
            label2.Text = "New image";
            // 
            // btnChange
            // 
            btnChange.Location = new Point(535, 400);
            btnChange.Name = "btnChange";
            btnChange.Size = new Size(173, 29);
            btnChange.TabIndex = 4;
            btnChange.Text = "Change image";
            btnChange.UseVisualStyleBackColor = true;
            btnChange.Click += button1_Click;
            // 
            // cbPixFormat
            // 
            cbPixFormat.DropDownStyle = ComboBoxStyle.DropDownList;
            cbPixFormat.FormattingEnabled = true;
            cbPixFormat.Location = new Point(823, 401);
            cbPixFormat.Name = "cbPixFormat";
            cbPixFormat.Size = new Size(155, 28);
            cbPixFormat.TabIndex = 5;
            cbPixFormat.SelectedIndexChanged += cbPixFormat_SelectedIndexChanged;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(717, 404);
            label3.Name = "label3";
            label3.Size = new Size(100, 20);
            label3.TabIndex = 6;
            label3.Text = "Image format";
            // 
            // btnSave
            // 
            btnSave.Location = new Point(12, 401);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(173, 27);
            btnSave.TabIndex = 7;
            btnSave.Text = "Save image";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(191, 405);
            label4.Name = "label4";
            label4.Size = new Size(100, 20);
            label4.TabIndex = 8;
            label4.Text = "Image format";
            // 
            // lblCurImageFormat
            // 
            lblCurImageFormat.AutoSize = true;
            lblCurImageFormat.Location = new Point(297, 405);
            lblCurImageFormat.Name = "lblCurImageFormat";
            lblCurImageFormat.Size = new Size(50, 20);
            lblCurImageFormat.TabIndex = 9;
            lblCurImageFormat.Text = "label5";
            // 
            // lblCurImageSize
            // 
            lblCurImageSize.Location = new Point(12, 371);
            lblCurImageSize.Name = "lblCurImageSize";
            lblCurImageSize.Size = new Size(443, 18);
            lblCurImageSize.TabIndex = 10;
            lblCurImageSize.Text = "label5";
            lblCurImageSize.TextAlign = ContentAlignment.TopCenter;
            // 
            // lblNewImageSize
            // 
            lblNewImageSize.Location = new Point(535, 371);
            lblNewImageSize.Name = "lblNewImageSize";
            lblNewImageSize.Size = new Size(443, 20);
            lblNewImageSize.TabIndex = 11;
            lblNewImageSize.Text = "label5";
            lblNewImageSize.TextAlign = ContentAlignment.TopCenter;
            // 
            // ImageEdit
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(999, 444);
            Controls.Add(lblNewImageSize);
            Controls.Add(lblCurImageSize);
            Controls.Add(lblCurImageFormat);
            Controls.Add(label4);
            Controls.Add(btnSave);
            Controls.Add(label3);
            Controls.Add(cbPixFormat);
            Controls.Add(btnChange);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(pbNew);
            Controls.Add(pbCurrent);
            Name = "ImageEdit";
            Text = "ImageEdit";
            FormClosing += ImageEdit_FormClosing;
            Load += ImageEdit_Load;
            ((System.ComponentModel.ISupportInitialize)pbCurrent).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbNew).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private PictureBox pbCurrent;
        private PictureBox pbNew;
        private Label label1;
        private Label label2;
        private Button btnChange;
        private ComboBox cbPixFormat;
        private Label label3;
        private Button btnSave;
        private Label label4;
        private Label lblCurImageFormat;
        private Label lblCurImageSize;
        private Label lblNewImageSize;
    }
}