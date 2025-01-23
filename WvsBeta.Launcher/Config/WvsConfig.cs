using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WvsBeta.Common;

namespace WvsBeta.Launcher.Config
{
    public class WvsConfig : IConfig
    {

        protected ConfigReader cf;
        public string ServerName { get; }

        public string PublicIP { get; set; } = "127.0.0.1";
        public string PrivateIP { get; set; } = "127.0.0.1";
        public ushort Port { get; set; }

        protected ushort DefaultPort;
        public WvsConfig(string serverName, ushort defaultPort)
        {
            ServerName = serverName;
            cf = Read($"{serverName}.img");
            DefaultPort = defaultPort;
        }

        public virtual void Reload()
        {
            PublicIP = cf["PublicIP"]?.GetString() ?? "127.0.0.1";
            PrivateIP = cf["PrivateIP"]?.GetString() ?? "127.0.0.1";
            Port = cf["port"]?.GetUShort() ?? DefaultPort;
        }

        public virtual void Write()
        {
            cf.Set("PublicIP", PublicIP);
            cf.Set("PrivateIP", PrivateIP);
            cf.Set("port", Port.ToString());

            cf.Write();
        }

        public static ConfigReader Read(string filename)
        {
            return new ConfigReader(Path.Combine(Program.InstallationPath, "..", "DataSvr", filename));
        }
    }

    public class Database : IConfig
    {
        public string IP { get; set; }
        public ushort Port { get; set; }
        public string DatabaseName { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }


        private ConfigReader cf;

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
    }
}