using System.Net;
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
            ssMariaDB.Arguments = new[] {"--console"};
            ssMariaDB.Start += (sender, args) =>
            {
                ssMariaDB.StartProcess();
            };

            ssMariaDB.Reinstall += ReinstallMariaDB;

            ssRedis.Configuration = redis;
            ssRedis.ExecutableName = "redis-server.exe";
            ssRedis.Arguments = new[] {"redis.windows.conf"};
            ssRedis.Start += (sender, args) =>
            {
                ssRedis.StartProcess();
            };

            var center = new Center("Center", redis);
            ConfigureServerConfiguration(ssCenter, center, "Center");
            ConfigureServerConfiguration(ssGame0, new Game("Game0", center), "Game");
            ConfigureServerConfiguration(ssShop0, new Shop("Shop0", center), "Shop");
            ConfigureServerConfiguration(ssLogin0, new Login("Login0", redis, center), "Login");
        }

        private void ReinstallMariaDB(object? sender, EventArgs e)
        {
            var dataPath = new DirectoryInfo(Path.Combine(ssMariaDB.FullWorkingDirectory, "..", "data"));
            if (dataPath.Exists)
            {
                // Do not delete the main folder so we keep the permissions
                dataPath.GetFiles().ForEach(x => x.Delete());
                dataPath.GetDirectories().ForEach(x => x.Delete(true));
            }

            if (MessageBox.Show("This will erase everything in the database. Are you sure you want to continue?",
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
            File.AppendAllLines(Path.Join(dataPath.FullName, "my.ini"), new []
            {
                "[server]",
                "bind-address = 127.0.0.1"
            });

            // Start the server
            ssMariaDB.StartProcess();

            Thread.Sleep(4000);
            if (!ssMariaDB.Started)
            {
                MessageBox.Show("Unable to start MariaDB! Process exited early.");
                return;
            }

            // Start injecting data...

            using var db = new MySQL_Connection(MasterThread.Instance, "root", config.RootPassword,
                "information_schema",
                "127.0.0.1", config.Port);

            db.RunQuery($"""
                         CREATE DATABASE {config.Database};
                         CREATE USER '{config.Username}'@'localhost' IDENTIFIED BY '{config.Password}';
                         FLUSH PRIVILEGES;
                         GRANT ALTER, ALTER ROUTINE, CREATE, CREATE ROUTINE, CREATE TEMPORARY TABLES, CREATE VIEW, DELETE, DROP, EVENT, EXECUTE, INDEX, INSERT, LOCK TABLES, REFERENCES, SELECT, SHOW VIEW, TRIGGER, UPDATE ON `{config.Database}`.* TO '{config.Username}'@'localhost' WITH GRANT OPTION;
                         """);


            var preparedFiles = Path.Combine(Program.InstallationPath, "evolutions", "prepared");
            db.RunQuery(File.ReadAllText(Path.Combine(preparedFiles, "rsvp-structure.sql")));
            db.RunQuery(File.ReadAllText(Path.Combine(preparedFiles, "rsvp-evolutions.sql")));

            MessageBox.Show("MariaDB re-installed and data cleaned.");
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
            serverStatus.Arguments = new[] {defaultConfig.ServerName};

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

        private UdpClient broadcastClient = new UdpClient();

        private void tmrServerAnnouncer_Tick(object sender, EventArgs e)
        {
            var machineName = Environment.MachineName;

            using var packet = new Packet((byte) 0x00);
            packet.WriteLong(DateTime.Now.ToFileTimeUtc());
            packet.WriteString(machineName);
            packet.WriteByte((byte) loginServers.Count);
            foreach (var loginServer in loginServers)
            {
                var config = loginServer.Configuration as Login;
                packet.WriteString(config.PublicIP);
                packet.WriteUShort(config.Port);
                packet.WriteBool(loginServer.Started);
            }

            broadcastClient.Send(
                packet.ToArray(),
                new IPEndPoint(new IPAddress(new byte[] {224, 0, 0, 1}), 28484)
            );


            tsslLANStatus.Text = $"Transmitted beacon @ {DateTime.Now}";
        }

        private void eventManagerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var ev = new EventEditor();
            ev.ShowDialog();
        }
    }
}