using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WvsBeta.Common;
using WvsBeta.Database;

namespace WvsBeta.Launcher.Config
{
    public class WvsConfig : IConfig
    {
        protected ConfigReader cf;
        public string ServerName { get; }
        [ConfigField("PublicIP")] public string PublicIP { get; set; } = "127.0.0.1";
        [ConfigField("PrivateIP")] public string PrivateIP { get; set; } = "127.0.0.1";
        [ConfigField("port")] public ushort Port { get; set; }

        protected Redis redis;

        protected ushort DefaultPort;

        public WvsConfig(string serverName, ushort defaultPort, Redis redis)
        {
            ServerName = serverName;
            cf = Read($"{serverName}.img");
            DefaultPort = defaultPort;
            this.redis = redis;
        }

        public virtual void Reload()
        {
            cf.LoadObject(this);
        }

        public virtual void Write()
        {
            cf.WriteObject(this);
            cf.WriteObject(redis);
            cf.Write();
        }

        public static ConfigReader Read(string filename)
        {
            var filePath = Path.Combine(Program.InstallationPath, "..", "DataSvr", filename);
            try
            {
                return new ConfigReader(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to load '{filePath}'. Application will exit. Error: " + ex);
                Environment.Exit(1);
                return null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class Database : IConfig
    {
        public string IP { get; set; } = "";
        public ushort Port { get; set; }
        public string DatabaseName { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";


        private ConfigReader? cf;

        public void Reload()
        {
            cf = WvsConfig.Read("database.img");
            IP = cf["dbHost"].GetString() ?? "127.0.0.1";
            Port = cf["dbPort"]?.GetUShort() ?? 3306;
            DatabaseName = cf["dbDatabase"].GetString() ?? "rsvp";
            Username = cf["dbUsername"].GetString() ?? "rsvp";
            Password = cf["dbPassword"]?.GetString() ?? "mypassword";
        }

        public void Write()
        {
            cf.Set("dbHost", IP);
            cf.Set("dbPort", Port.ToString());
            cf.Set("dbDatabase", DatabaseName);
            cf.Set("dbUsername", Username);
            cf.Set("dbPassword", Password);
            cf.Write();
        }

        public MySQL_Connection Connect()
        {
            return new MySQL_Connection(MasterThread.Instance, cf, true, false);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}