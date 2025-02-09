using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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
            Invoke((MethodInvoker)delegate
            {
                foreach (var control in flowLayoutPanel1.Controls)
                {
                    var entry = (ServerEntry)control;
                    if (entry.IsSame(e))
                    {
                        entry.UpdateBroadcastInfo(e);
                        return;
                    }
                }

                var newEntry = new ServerEntry(e);
                newEntry.OnStart += OnStartGame;
                newEntry.OnShortCut += OnShortCut;

                flowLayoutPanel1.Controls.Add(newEntry);
            });
        }

        private bool ValidateSelection(object? sender, out string ip, out ushort port)
        {
            ip = "";
            port = 0;

            if (MapleLocation == null || sender is not ServerEntry entry)
            {
                MessageBox.Show("Maple was not found. Please select one using the bottom bar...");
                return false;
            }

            if (entry.SelectedLoginServer == null)
            {
                MessageBox.Show("This server has no LoginServer available.");
                return false;
            }

            ip = (string)entry.IP;
            port = (ushort)entry.Port;

            return ValidateIPPort(ip, port);
        }

        private void OnShortCut(object? sender, EventArgs e)
        {
            if (!ValidateSelection(sender, out var ip, out var port)) return;

            CreateMapleShortCut(ip, port);
            MessageBox.Show("Shortcut on desktop created!");
        }

        private void OnStartGame(object? sender, EventArgs e)
        {
            if (!ValidateSelection(sender, out var ip, out var port)) return;

            StartGame(ip, port);
        }

        private void StartGame(string ip, ushort port)
        {
            Process.Start(new ProcessStartInfo(MapleLocation, new[] { ip, port.ToString() })
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
            if (System.IO.File.Exists(MAPLE_EXE))
            {
                Console.WriteLine($"Found maple in the current directory.");
                return Path.GetFullPath(MAPLE_EXE);
            }

            // Check registry
            var regNode = Registry.LocalMachine.GetValue(@"SOFTWARE\WOW6432Node\RSVP\ExecPath");
            if (regNode != null && regNode is string execPath)
            {
                var fullPath = Path.Join(execPath, MAPLE_EXE);
                if (System.IO.File.Exists(fullPath))
                {
                    return fullPath;
                }

                Console.WriteLine($"Could not find {fullPath} which was stored in registry");
            }

            // Other options?

            var defaultInstallLocation = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "RSVP", MAPLE_EXE);

            if (System.IO.File.Exists(defaultInstallLocation))
            {
                Console.WriteLine($"Found maple at {defaultInstallLocation}.");
                return defaultInstallLocation;
            }

            return null;
        }
        private bool ValidateManualInput(out string ip, out ushort port)
        {
            ip = "";
            port = 0;

            if (!ushort.TryParse(txtManualPort.Text, out port))
            {
                MessageBox.Show("Please enter a valid port.");
                return false;
            }

            ip = txtManualIP.Text;

            return ValidateIPPort(ip, port);
        }

        private bool ValidateIPPort(string ip, ushort port)
        {
            if (port <= 1024)
            {
                MessageBox.Show($"Invalid port: {port}");
                return false;
            }

            if (ip == null || !IPAddress.TryParse(ip, out _))
            {
                MessageBox.Show($"Invalid IP address: {ip}");
                return false;
            }

            return true;
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (!ValidateManualInput(out var ip, out var port)) return;

            StartGame(ip, port);
        }

        private void btnShortcut_Click(object sender, EventArgs e)
        {
            if (!ValidateManualInput(out var ip, out var port)) return;

            CreateMapleShortCut(ip, port);
        }


        void CreateMapleShortCut(string ip, ushort port)
        {
            if (MessageBox.Show($"Create a shortcut to {ip}:{port}?", "", MessageBoxButtons.OKCancel) != DialogResult.OK)
            {
                return;
            }

            var shortcutName = $"RSVP at {ip}";
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            var sfd = new SaveFileDialog();
            sfd.Filter = "Shortcut|*.lnk";
            sfd.FileName = Path.Combine(desktopPath, $"{shortcutName}.lnk");
            sfd.OverwritePrompt = true;
            sfd.InitialDirectory = Path.GetDirectoryName(sfd.FileName);
            if (sfd.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            var shortcutFile = sfd.FileName;
            var link = (IShellLink)new ShellLink();

            // setup shortcut information
            link.SetDescription($"Connect to RSVP server at {ip}:{port}");
            link.SetPath(MapleLocation);
            link.SetWorkingDirectory(Path.GetDirectoryName(MapleLocation));
            link.SetArguments($"{ip} {port}");

            // save it
            var file = (System.Runtime.InteropServices.ComTypes.IPersistFile)link;
            file.Save(shortcutFile, false);

            MessageBox.Show($"Shortcut '{Path.GetFileNameWithoutExtension(shortcutFile)}' created!");
        }


        //This could always be better adjusted to take more input instead of just being set.
        //Then outside of your class but in your namespace
        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        internal class ShellLink
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        internal interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }
    }
}