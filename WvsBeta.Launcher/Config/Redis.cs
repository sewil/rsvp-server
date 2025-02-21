﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace WvsBeta.Launcher.Config
{
    public class Redis : IConfig
    {
        [ConfigField("redis.hostname", defaultValue: "127.0.0.1")]
        public string Host { get; set; } = "127.0.0.1";

        [ConfigField("redis.port", defaultValue: "6379")]
        public int Port { get; set; } = 6379;

        public string BindAddress { get; set; } = "127.0.0.1";
        

        [ConfigField("redis.password", defaultValue: "")]
        public string Password { get; set; } = "";

        public void Reload()
        {
            // TODO: Read from config
        }

        public void Write()
        {
            // TODO: Write to config (or pass as startup arg?)
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}