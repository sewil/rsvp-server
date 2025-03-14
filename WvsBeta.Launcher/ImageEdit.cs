using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WzTools.Extra;
using WzTools.Helpers.Helpers;

namespace WvsBeta.Launcher
{
    public partial class ImageEdit : Form
    {
        public string? NewImagePath { get; set; }
        public WzPixFormat? NewFormat { get; set; }
        public Bitmap? NewBitmap { get; set; }

        public ImageEdit(WzCanvas canvas)
        {
            InitializeComponent();

            pbCurrent.Image = canvas.GetImage();

            var pixFormats = new[]
            {
                new PixFormatEntry(WzPixFormat.A8R8G8B8, "A8R8G8B8 (32-bit)"),
                new PixFormatEntry(WzPixFormat.A4R4G4B4, "A4R4G4B4 (16-bit)"),
                new PixFormatEntry(WzPixFormat.R5G6B5, "R5G6B5 (16-bit)"),
            };

            cbPixFormat.Items.AddRange(pixFormats);

            foreach (var item in pixFormats.Index())
            {
                if (item.Item.Format != canvas.PixFormat) continue;

                cbPixFormat.SelectedIndex = item.Index;
                lblCurImageFormat.Text = item.Item.Text;
            }

            lblCurImageSize.Text = $"{canvas.Width}x{canvas.Height}";
        }

        class PixFormatEntry
        {
            public WzPixFormat Format;
            public string Text;
            public PixFormatEntry(WzPixFormat format, string text)
            {
                Format = format;
                Text = text;
            }

            public override string ToString()
            {
                return Text;
            }
        }

        private void ImageEdit_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "PNG|*.png";

            if (ofd.ShowDialog() != DialogResult.OK) return;
            NewImagePath = ofd.FileName;

            ChangeImageFormat();
        }

        private void ChangeImageFormat()
        {
            if (NewImagePath == null) return;
            using var bmp = Bitmap.FromFile(NewImagePath) as Bitmap;
            var selectedPixformat = (cbPixFormat.SelectedItem as PixFormatEntry).Format;

            NewFormat = selectedPixformat;
            NewBitmap = WzCanvas.Convert(bmp, selectedPixformat);
            pbNew.Image = NewBitmap;

            lblNewImageSize.Text = $"{bmp.Width}x{bmp.Height}";
        }

        private void cbPixFormat_SelectedIndexChanged(object sender, EventArgs e)
        {
            ChangeImageFormat();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            var sfd = new SaveFileDialog();
            sfd.Filter = "PNG|*.png";

            if (sfd.ShowDialog() != DialogResult.OK) return;

            pbCurrent.Image.Save(sfd.FileName);
        }

        private void ImageEdit_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            if (NewBitmap != null && MessageBox.Show("Use the new image?", "", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                DialogResult = DialogResult.Yes;
            }
        }
    }
}
