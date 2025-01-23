using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WvsBeta.Launcher.Config
{
    public class Redis : IConfig
    {
        public int Port { get; set; } = 6379;
        public string BindAddress { get; set; } = "127.0.0.1";

        public string Password { get; set; }

        public void Reload()
        {
        }

        public void Write()
        {
        }
    }
}