using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Database;
using WvsBeta.Launcher.Config;

namespace WvsBeta.Launcher
{
    public partial class Form1 : Form
    {
        private Config.Database db { get; }
        private Config.Redis redis { get; }
        private List<WvsConfig> servers = new();
        private List<ServerStatus> loginServers = new();

        private LANMode.Interface? LANModeInterface { get; set; }

        public Form1()
        {
            InitializeComponent();

            db = new Config.Database();
            db.Reload();

            redis = new Config.Redis();

            ssMariaDB.Configuration = new MariaDB(db);
            ssMariaDB.ExecutableName = "mariadbd.exe";
            ssMariaDB.Arguments = new[] { "--console" };
            ssMariaDB.Start += (sender, args) =>
            {
                ssMariaDB.StartProcess();
            };

            ssMariaDB.Reinstall += ReinstallMariaDB;
            ssMariaDB.OnStarted += (_, _) => UpdateServerStartingDisabled();
            ssMariaDB.OnStopped += (_, _) => UpdateServerStartingDisabled();

            ssRedis.Configuration = redis;
            ssRedis.ExecutableName = "redis-server.exe";
            ssRedis.Arguments = new[] { "redis.windows.conf" };
            ssRedis.Start += (sender, args) =>
            {
                ssRedis.StartProcess();
            };

            ssRedis.OnStarted += (_, _) => UpdateServerStartingDisabled();
            ssRedis.OnStopped += (_, _) => UpdateServerStartingDisabled();

            var center = new Center("Center", redis);
            ConfigureServerConfiguration(ssCenter, center, "Center");
            ConfigureServerConfiguration(ssGame0, new Game("Game0", center), "Game");
            ConfigureServerConfiguration(ssShop0, new Shop("Shop0", center), "Shop");
            ConfigureServerConfiguration(ssLogin0, new Login("Login0", redis, center), "Login");

            if (!MariaDBDataFolder.Exists)
            {
                ssMariaDB.StartingDisabled = true;
            }

            UpdateServerStartingDisabled();

        }

        private void UpdateServerStartingDisabled()
        {
            var enabled = ssMariaDB.Started && ssRedis.Started;
            ssCenter.StartingDisabled =
                ssGame0.StartingDisabled = ssShop0.StartingDisabled = ssLogin0.StartingDisabled = !enabled;
        }

        DirectoryInfo MariaDBDataFolder => new DirectoryInfo(Path.Combine(ssMariaDB.FullWorkingDirectory, "..", "data"));

        private void ReinstallMariaDB(object? sender, EventArgs e)
        {
            var dataPath = MariaDBDataFolder;
            if (dataPath.Exists)
            {
                // Do not delete the main folder so we keep the permissions
                dataPath.GetFiles().ForEach(x => x.Delete());
                dataPath.GetDirectories().ForEach(x => x.Delete(true));
            }

            if (dataPath.Exists &&
                MessageBox.Show("This will erase everything in the database. Are you sure you want to continue?",
                    "Wait a minute", MessageBoxButtons.OKCancel) == DialogResult.Cancel)
            {
                return;
            }

            var config = ssMariaDB.Configuration as MariaDB;
            ssMariaDB.StartProcess("mariadb-install-db.exe",
                "-p", config.RootPassword,
                "-P", config.Port.ToString(),
                "-o"
            );

            ssMariaDB.WaitForExit();

            // Add network config
            // passing '-c' for config seems to be broken
            File.AppendAllLines(Path.Join(dataPath.FullName, "my.ini"), new[]
            {
                "[server]",
                "bind-address = 127.0.0.1"
            });

            /*
             *  performance_schema=ON
                performance-schema-instrument='stage/%=ON'
                performance-schema-consumer-events-stages-current=ON
                performance-schema-consumer-events-stages-history=ON
                performance-schema-consumer-events-stages-history-long=ON
             */


            // Start the server
            ssMariaDB.StartProcess();

            Thread.Sleep(4000);
            if (!ssMariaDB.Started)
            {
                MessageBox.Show("Unable to start MariaDB! Process exited early.");
                return;
            }
            
            // Make sure the person is watching this dialog, not the mariadb one
            Activate();

            // Start injecting data...
            try
            {
                using var db = new MySQL_Connection(
                    MasterThread.Instance,
                    "root", config.RootPassword,
                    "information_schema",
                    "127.0.0.1", config.Port,
                    noRecovery: true,
                    pinger: false);

                db.RunQuery($"""
                             CREATE DATABASE {config.Database};
                             CREATE USER '{config.Username}'@'localhost' IDENTIFIED BY '{config.Password}';
                             FLUSH PRIVILEGES;
                             GRANT ALTER, ALTER ROUTINE, CREATE, CREATE ROUTINE, CREATE TEMPORARY TABLES, CREATE VIEW, DELETE, DROP, EVENT, EXECUTE, INDEX, INSERT, LOCK TABLES, REFERENCES, SELECT, SHOW VIEW, TRIGGER, UPDATE ON `{config.Database}`.* TO '{config.Username}'@'localhost' WITH GRANT OPTION;
                             """);


                var preparedFiles = Path.Combine(Program.InstallationPath, "evolutions", "prepared");
                db.RunQuery(File.ReadAllText(Path.Combine(preparedFiles, "rsvp-structure.sql")));
                db.RunQuery(File.ReadAllText(Path.Combine(preparedFiles, "rsvp-evolutions.sql")));


                ssMariaDB.StartingDisabled = false;
                MessageBox.Show("MariaDB re-installed and data cleaned.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception while preparing database: {ex}");
            }
        }

        private void ConfigureServerConfiguration(ServerStatus serverStatus, WvsConfig defaultConfig, string name)
        {
            servers.Add(defaultConfig);
            if (defaultConfig is Login)
            {
                loginServers.Add(serverStatus);
            }

            defaultConfig.Reload();
            serverStatus.Configuration = defaultConfig;
            serverStatus.ExecutableName = $"WvsBeta.{name}.exe";
            serverStatus.Arguments = new[] { defaultConfig.ServerName };

            serverStatus.Start += (sender, eventArgs) =>
            {
                serverStatus.StartProcess();
            };
        }


        private void serverStatus1_Load(object sender, EventArgs e)
        {
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private void userManagerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!ssMariaDB.Started)
            {
                MessageBox.Show("Start MariaDB first");
                return;
            }

            using var conn = db.Connect();
            using var userManager = new UserManager(conn);
            userManager.ShowDialog();
        }

        private void configureLANModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var lanMode = new LANMode(servers.ToArray());
            lanMode.ShowDialog();

            LANModeInterface = lanMode.SelectedInterface;
        }

        private void toolStripStatusLabel1_Click(object sender, EventArgs e)
        {
        }

        private UdpClient broadcastClient = new();

        private void tmrServerAnnouncer_Tick(object sender, EventArgs e)
        {
            var broadcastIPs = NetworkInterface.GetAllNetworkInterfaces()
                .Where(x => x.SupportsMulticast)
                .SelectMany(x => x.GetIPProperties().MulticastAddresses)
                .Select(x => x.Address)
                .Distinct()
                .Where(x => x.AddressFamily == AddressFamily.InterNetwork)
                .ToList();

            var machineName = Environment.MachineName;

            using var packet = new Packet((byte)0x00);
            packet.WriteLong(DateTime.Now.ToFileTimeUtc());
            packet.WriteString(machineName);
            packet.WriteByte((byte)loginServers.Count);
            foreach (var loginServer in loginServers)
            {
                var config = loginServer.Configuration as Login;
                packet.WriteString(config.PublicIP);
                packet.WriteUShort(config.Port);
                packet.WriteBool(loginServer.Started);
            }

            var rawPacket = packet.ToArray();

            var anyFailed = false;

            foreach (var ip in broadcastIPs)
            {
                try
                {
                    broadcastClient.Send(
                        rawPacket,
                        new IPEndPoint(ip, 28484)
                    );
                }
                catch (Exception ex)
                {
                    anyFailed = true;
                }
            }

            tsslLANStatus.Text =
                $"Transmitted beacon @ {DateTime.Now} on {string.Join(", ", broadcastIPs.Select(x => x.ToString()))}";

            if (anyFailed)
            {
                tsslLANStatus.Text += "(failed to tx on some addrs)";
            }
        }

        private void eventManagerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var ev = new EventEditor();
            ev.ShowDialog();
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            var de = new DataEdit();
            de.ShowDialog();
        }

        private void fromMariaDBDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var folder = MariaDBDataFolder;
            if (!folder.Exists)
            {
                MessageBox.Show($"Could not find {folder}");
                return;
            }

            if (ssMariaDB.Started)
            {
                MessageBox.Show("Please stop the MariaDB server prior to backing up.");
                return;
            }

            var sfd = new SaveFileDialog();
            sfd.InitialDirectory = Program.DataSvr;

            sfd.Filter = "Zip file|*.zip";
            sfd.FileName = $"mariadb-data-{DateTime.Now:yyyyMMdd-HHmmss}.zip";

            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                if (File.Exists(sfd.FileName))
                {
                    File.Delete(sfd.FileName);
                }

                ZipFile.CreateFromDirectory(folder.FullName, sfd.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to create backup. Make sure database is not running!\n\n{ex}");
                return;
            }

            MessageBox.Show($"Database backup of MariaDB made at {sfd.FileName}.\nNext time you can just replace the {folder} data with the content in the zip...!");
        }
    }
}