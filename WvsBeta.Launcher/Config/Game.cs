using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WvsBeta.Common;

namespace WvsBeta.Launcher.Config
{
    internal class CenterSlave : WvsConfig
    {
        private Center center;

        [ConfigField("gameWorldId", "0")] public byte GameWorldId => center.GameWorldID;

        [ConfigField("center.ip", "127.0.0.1")]
        public string CenterPrivateIP => center.PrivateIP;

        [ConfigField("center.port", "8383")] public ushort CenterPort => center.Port;

        [ConfigField("center.worldName", "Scania")]
        public string CenterWorldName => center.Name;

        public CenterSlave(string serverName, ushort defaultPort, Center center) : base(serverName, defaultPort,
            center.GetRedis())
        {
            this.center = center;
        }
    }

    internal class Game : CenterSlave
    {
        public Game(string serverName, Center center) : base(serverName, 8585, center)
        {
        }

        [ConfigField("memoryAutobanEnabled", "true")]
        public bool MemoryAutobanEnabled { get; set; }

        [ConfigField("lazyLoadScripts", "false")]
        public bool LazyLoadScripts { get; set; }

        [ConfigField("tespia", "false")] public bool Tespia { get; set; }
        [ConfigField("scrollingHeader", "")] public string ScrollingHeader { get; set; }
    }

    internal class Shop : CenterSlave
    {
        public Shop(string serverName, Center center) : base(serverName, 8989, center)
        {
        }

        [ConfigField("slotIncreasePrice", "2000")]
        public int SlotIncreasePrice { get; set; } = 2000;

        [ConfigField("trunkIncreasePrice", "2000")]
        public int TrunkIncreasePrice { get; set; } = 2000;
    }

    internal class Center : WvsConfig
    {
        public Center(string serverName, Redis redis) : base(serverName, 8383, redis)
        {
        }

        // Helper function to fetch redis off Center
        public Redis GetRedis()
        {
            return redis;
        }

        public string Name { get; set; } = "";

        [ConfigField("gameWorldId", "0")] public byte GameWorldID { get; set; }

        [ConfigField("userWarning", "400")] public int UserWarning { get; set; }

        [ConfigField("userLimit", "600")] public int UserLimit { get; set; }

        [ConfigField("tespia", "false")] public bool Tespia { get; set; }
    }

    internal class LoginCenterInfo : IConfig
    {
        [ConfigField("userNoMultiplier", "7.0")]
        public float UserNumberMultiplier { get; set; }

        [ConfigField("channelNo", "7.0")] public byte ChannelCount { get; set; }
        [ConfigField("eventDesc", "")] public string EventDescription { get; set; }
        [ConfigField("worldState", "0")] public byte WorldState { get; set; }
        [ConfigField("adult", "false")] public bool AdultChannel { get; set; }

        [ConfigField("BlockCharCreation", "false")]
        public bool BlockCharCreation { get; set; }

        [ConfigField("ip", "127.0.0.1")] public string IP => center.PrivateIP;
        [ConfigField("port", "8383")] public ushort Port => center.Port;
        [ConfigField("world", "0")] public byte World => center.GameWorldID;

        public string Name
        {
            get => center.Name;
            set => center.Name = value;
        }

        private readonly Center center;

        public LoginCenterInfo(Center center)
        {
            this.center = center;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Reload()
        {
        }

        public void Write()
        {
        }
    }

    internal class Login : WvsConfig
    {
        private LoginCenterInfo[] centers;

        public Login(string serverName, Redis redis, params Center[] centers) : base(serverName, 8484, redis)
        {
            this.centers = centers.Select(x => new LoginCenterInfo(x)).ToArray();
        }

        [ConfigField("dataChecksum", "0xF825B615")]
        public int DataChecksum { get; set; }

        [ConfigField("adminsRequirePublicKeyAuth", "false")]
        public bool AdminsRequirePublicKeyAuth { get; set; }

        [ConfigField("tespia", "false")] public bool Tespia { get; set; }

        [ConfigField("requireEULA", "false")] public bool RequireEULA { get; set; }

        public override void Reload()
        {
            foreach (var node in cf["center"])
            {
                var privateIP = node["ip"]?.GetString() ?? "no ip";
                var port = node["port"]?.GetUShort() ?? 0;

                var center = centers.FirstOrDefault(x => x.Port == port && x.IP == privateIP);
                if (center == null)
                {
                    Debug.WriteLine($"No center found for addr {privateIP}:{port}");
                    continue;
                }

                center.Name = node.Name;
                node.LoadObject(center);
            }

            base.Reload();
        }

        public override void Write()
        {
            cf.Set("center", centers.Select(x =>
            {
                var centerNode = new Node()
                {
                    Name = x.Name,
                    SubNodes = new List<Node>(),
                };

                centerNode.WriteObject(x);

                return centerNode;
            }).ToArray());

            base.Write();
        }
    }
}