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

        [DefaultValue("")] public string ExecutableName { get; set; } = "";

        [DefaultValue(new string[] { })] public string[] Arguments { get; set; } = new string[] { };

        [DefaultValue(false)] public bool Reinstallable { get; set; }

        [DefaultValue(null)]
        public IConfig? Configuration
        {
            get => propertyGrid1.SelectedObject as IConfig;
            set
            {
                if (propertyGrid1.SelectedObject != null)
                {
                    ((IConfig)propertyGrid1.SelectedObject).PropertyChanged -= ConfigurationChanged;
                }

                propertyGrid1.SelectedObject = value;

                if (value != null)
                {
                    value.PropertyChanged += ConfigurationChanged;
                }
            }
        }

        private void ConfigurationChanged(object? sender, PropertyChangedEventArgs e)
        {
            propertyGrid1.Refresh();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Process? Process { get; set; }
        private bool ProcessCanThrowExit { get; set; }

        [DefaultValue(null)] public event EventHandler? Reinstall;

        [DefaultValue(null)] public event EventHandler? Start;

        [DefaultValue(null)] public event EventHandler? OnStarted;

        [DefaultValue(null)] public event EventHandler? OnStopped;

        [DefaultValue(false)]
        public bool StartingDisabled { get; set; } = false;
        public bool Started
        {
            get
            {
                try
                {
                    if (!ProcessCanThrowExit)
                    {
                        // Update it by checking if it still exists
                        UpdateProcess();
                    }

                    return Process != null;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public string FullWorkingDirectory => Path.Combine(Program.InstallationPath, WorkingDirectory);

        public ServerStatus()
        {
            InitializeComponent();
            UpdateButtonStates();
        }

        public Process? FindProcess()
        {
            if (ExecutableName == null) return null;

            var simpleProcessName = ExecutableName.Replace(".exe", "");
            // NOTE: Cannot use StartInfo here
            var processes = System.Diagnostics.Process.GetProcesses().Where(x => x.ProcessName == simpleProcessName).ToList();

            if (processes.Count == 0)
            {
                return null;
            }

            if (processes.Count > 1)
            {
                Console.WriteLine("Found more than 1 process that looks like what we need??");
                return null;
            }

            return processes.First();
        }

        public void UpdateProcess()
        {
            var process = FindProcess();
            if (process != null)
            {
                if (Process != null && Process.Id == process.Id) return;
                Process = process;
                HookProcess(process);
            }
        }

        public void StartProcess()
        {
            StartProcess(ExecutableName, Arguments);
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

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo(Path.Combine(FullWorkingDirectory, filename), args)
                {
                    WorkingDirectory = FullWorkingDirectory,
                }
            };

            HookProcess(process);
            try
            {
                process.Start();
                Process = process;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to start process: {ex}");
                return false;
            }
        }

        private bool stopping = false;
        public void WaitForExit()
        {
            if (!Started || Process == null) return;
            stopping = true;
            try
            {
                if (!Process.WaitForExit(TimeSpan.FromSeconds(10)))
                {
                    MessageBox.Show("Process did not exit in time?");
                }

            }
            finally
            {
                stopping = false;
                Process?.Close();
                Process = null;
            }
        }

        private void HookProcess(Process process)
        {
            process.Exited += Process_Exited;
            try
            {
                process.EnableRaisingEvents = true;
                ProcessCanThrowExit = true;
            }
            catch (Exception)
            {
                // We need special care for these processes.
                ProcessCanThrowExit = false;
            }
        }

        private void Process_Exited(object? sender, EventArgs e)
        {
            // Don't do extra steps if we are stopping this process
            if (stopping) return;

            if (Process != null)
            {
                Process.Exited -= Process_Exited;
                Process.Close();
            }

            Process = null;
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate
                {
                    UpdateButtonStates();
                });
            }
            else
            {
                UpdateButtonStates();
            }
        }

        private bool _wasStarted = false;
        public void UpdateButtonStates()
        {
            var started = Started;

            var startButtonEnabled = !StartingDisabled;
            if (startButtonEnabled && started && !ProcessCanThrowExit) startButtonEnabled = false;
            btnStart.Enabled = startButtonEnabled;


            btnStart.Text = started ? "Restart" : "Start";
            propertyGrid1.Enabled = !started;
            btnReinstall.Enabled = Reinstallable && !started;
            btnReloadConfig.Enabled = !started;

            if (started && !_wasStarted) OnStarted?.Invoke(null, null);
            if (!started && _wasStarted) OnStopped?.Invoke(null, null);
            _wasStarted = started;
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
            if (DesignMode) return;
            if (!Visible) return;

            if (!Started)
            {
                UpdateProcess();
            }

            UpdateButtonStates();
        }
    }
}