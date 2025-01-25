using System.Diagnostics;
using Microsoft.Win32;

namespace MapleStarter
{
    public partial class Form1 : Form
    {
        private ServerBroadcastReceiver _receiver { get; } = new();

        private string? _mapleLocation;

        private string? MapleLocation
        {
            get => _mapleLocation;
            set
            {
                _mapleLocation = value;
                tsslMapleLocation.Text = (_mapleLocation ?? "Not found") + " (click to change)";
            }
        }

        public Form1()
        {
            InitializeComponent();
            _receiver.OnBroadcastReceived += BroadcastReceived;
            _receiver.Start();

            MapleLocation = DetectMapleLocation();
        }

        private void BroadcastReceived(object? sender, ServerBroadcast e)
        {
            Invoke((MethodInvoker) delegate
            {
                foreach (var control in flowLayoutPanel1.Controls)
                {
                    var entry = (ServerEntry) control;
                    if (entry.IsSame(e))
                    {
                        entry.UpdateBroadcastInfo(e);
                        return;
                    }
                }

                var newEntry = new ServerEntry(e);
                newEntry.OnStart += StartGame;

                flowLayoutPanel1.Controls.Add(newEntry);
            });
        }

        private void StartGame(object? sender, EventArgs e)
        {
            if (MapleLocation == null || sender is not ServerEntry entry)
            {
                MessageBox.Show("Maple was not found. Please select one using the bottom bar...");
                return;
            }

            if (entry.SelectedLoginServer == null)
            {
                MessageBox.Show("This server has no LoginServer available.");
                return;
            }

            Process.Start(new ProcessStartInfo(MapleLocation, new[] { entry.IP, entry.Port.ToString() })
            {
                WorkingDirectory = Path.GetDirectoryName(MapleLocation)
            });
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private void toolStripStatusLabel1_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "Executables|*.exe";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                MapleLocation = ofd.FileName;
            }
        }


        private static readonly string MAPLE_EXE = "MapleStory.exe";

        private static string? DetectMapleLocation()
        {
            if (File.Exists(MAPLE_EXE))
            {
                Console.WriteLine($"Found maple in the current directory.");
                return Path.GetFullPath(MAPLE_EXE);
            }

            // Check registry
            var regNode = Registry.LocalMachine.GetValue(@"SOFTWARE\WOW6432Node\RSVP\ExecPath");
            if (regNode != null && regNode is string execPath)
            {
                var fullPath = Path.Join(execPath, MAPLE_EXE);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }

                Console.WriteLine($"Could not find {fullPath} which was stored in registry");
            }

            // Other options?

            var defaultInstallLocation = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "RSVP", MAPLE_EXE);

            if (File.Exists(defaultInstallLocation))
            {
                Console.WriteLine($"Found maple at {defaultInstallLocation}.");
                return defaultInstallLocation;
            }

            return null;
        }
    }
}