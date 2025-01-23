using WvsBeta.Database;
using WvsBeta.Launcher.Config;

namespace WvsBeta.Launcher
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            var db = new Config.Database();
            db.Reload();

            ssMariaDB.Configuration = new MariaDB(db);
            ssMariaDB.Start += (sender, args) =>
            {
                ssMariaDB.StartProcess("mariadbd.exe");
            };

            ssMariaDB.Reinstall += ReinstallMariaDB;

            ssRedis.Configuration = new Redis();
            ssRedis.Start += (sender, args) =>
            {
                ssRedis.StartProcess("redis-server.exe", "redis.windows.conf");
            };

            var center = new Center("Center");
            ConfigureServerConfiguration(ssCenter, center, "Center");
            ConfigureServerConfiguration(ssGame0, new Game("Game0", center), "Game");
            ConfigureServerConfiguration(ssShop0, new Shop("Shop0", center), "Shop");
            ConfigureServerConfiguration(ssLogin0, new Login("Login0", center), "Login");
        }

        private void ReinstallMariaDB(object? sender, EventArgs e)
        {
            var dataPath = Path.Combine(ssMariaDB.FullWorkingDirectory, "..", "data");
            if (Directory.Exists(dataPath))
            {
                Directory.Delete(dataPath, true);
            }

            var config = ssMariaDB.Configuration as MariaDB;
            ssMariaDB.StartProcess("mariadb-install-db.exe",
                "-p", config.RootPassword,
                "-P", config.Port.ToString(),
                "-o"
            );

            ssMariaDB.WaitForExit();

            // Start the server
            ssMariaDB.StartProcess("mariadbd.exe");

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

            db.RunQuery(File.ReadAllText(Path.Combine(Program.InstallationPath, "..", "SQLs", "rsvp-structure.sql")));
            db.RunQuery(File.ReadAllText(Path.Combine(Program.InstallationPath, "..", "SQLs", "rsvp-evolutions.sql")));
        }

        private void ConfigureServerConfiguration(ServerStatus serverStatus, WvsConfig defaultConfig, string name)
        {
            defaultConfig.Reload();
            serverStatus.Configuration = defaultConfig;
            serverStatus.Start += (sender, eventArgs) =>
            {
                serverStatus.StartProcess($"WvsBeta.{name}.exe", defaultConfig.ServerName);
            };
        }


        private void serverStatus1_Load(object sender, EventArgs e)
        {
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }
    }
}