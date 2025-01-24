using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace WvsBeta.Launcher.Config
{
    public class MariaDB : IConfig
    {
        private Database _db;

        public string Username
        {
            get => _db.Username;
            set => _db.Username = value;
        }

        public string Password
        {
            get => _db.Password;
            set => _db.Password = value;
        }

        public string RootPassword { get; set; } = "";

        public ushort Port
        {
            get => _db.Port;
            set => _db.Port = value;
        }

        public string Database
        {
            get => _db.DatabaseName;
            set => _db.DatabaseName = value;
        }

        public MariaDB(Database db)
        {
            _db = db;
        }

        public void Reload()
        {
            _db.Reload();
        }

        public void Write()
        {
            _db.Write();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}