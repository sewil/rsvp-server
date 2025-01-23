using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WvsBeta.Launcher.Config;

namespace WvsBeta.Launcher
{
    public partial class ServerStatus : UserControl
    {
        [DefaultValue("Server Status")]
        public string Title
        {
            get => groupBox1.Text;
            set => groupBox1.Text = value;
        }

        [DefaultValue("")] public string WorkingDirectory { get; set; } = "";

        [DefaultValue(false)]
        public bool Reinstallable
        {
            get => btnReinstall.Enabled;
            set => btnReinstall.Enabled = value;
        }

        [DefaultValue(null)]
        public IConfig? Configuration
        {
            get => propertyGrid1.SelectedObject as IConfig;
            set => propertyGrid1.SelectedObject = value;
        }

        public Process? Process { get; set; }

        [DefaultValue(null)]
        public event EventHandler Reinstall;
        [DefaultValue(null)]
        public event EventHandler Start;

        public bool Started => !(Process?.HasExited ?? true);
        public string FullWorkingDirectory => Path.Combine(Program.InstallationPath, WorkingDirectory);

        public ServerStatus()
        {
            InitializeComponent();
            UpdateButtonStates();
        }

        public bool StartProcess(string filename, params string[] args)
        {
            if (Started && Process != null)
            {
                try
                {
                    Process.CloseMainWindow();
                }
                catch
                {
                    // ...
                }

                WaitForExit();
            }

            Process = new Process()
            {
                StartInfo = new ProcessStartInfo(Path.Combine(FullWorkingDirectory, filename), args)
                {
                    WorkingDirectory = FullWorkingDirectory,
                }
            };

            HookProcess();

            return Process.Start();
        }

        public void WaitForExit()
        {
            if (!Started || Process == null) return;
            Process.WaitForExit();
            Process.Close();
            Process = null;
        }

        private void HookProcess()
        {
            Process.Exited += (sender, eventArgs) =>
            {
                Invoke((MethodInvoker)UpdateButtonStates);
            };
        }

        public void UpdateButtonStates()
        {
            btnStart.Text = Started ? "Restart" : "Start";
            propertyGrid1.Enabled = !Started;
            btnReinstall.Enabled = !Started;
            btnReloadConfig.Enabled = !Started;
        }

        private void btnReloadConfig_Click(object sender, EventArgs e)
        {
            Configuration?.Reload();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            Configuration?.Write();

            Start?.Invoke(sender, e);
            UpdateButtonStates();
        }

        private void btnReinstall_Click(object sender, EventArgs e)
        {
            Reinstall?.Invoke(sender, e);
        }

        private void tmrUIUpdater_Tick(object sender, EventArgs e)
        {
            UpdateButtonStates();
        }
    }
}