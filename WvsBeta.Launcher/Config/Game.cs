using System;
using System.Collections.Generic;
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
        
        public byte GameWorldId => center.GameWorldID;

        public CenterSlave(string serverName, ushort defaultPort, Center center) : base(serverName, defaultPort)
        {
            this.center = center;
        }

        public override void Write()
        {
            cf.Set("gameWorldId", GameWorldId.ToString());

            var centerNode = cf["center"];
            centerNode.Set("ip", center.PrivateIP);
            centerNode.Set("port", center.Port.ToString());
            centerNode.Set("worldName", center.Name);

            base.Write();
        }
    }

    internal class Game : CenterSlave
    {
        public Game(string serverName, Center center) : base(serverName, 8585, center)
        {
        }

        public bool MemoryAutobanEnabled { get; set; }
        
        public override void Reload()
        {
            MemoryAutobanEnabled = cf["memoryAutobanEnabled"]?.GetBool() ?? true;

            base.Reload();
        }
    }

    internal class Shop : CenterSlave
    {
        public Shop(string serverName, Center center) : base(serverName, 8989, center)
        {
        }

        public int SlotIncreasePrice { get; set; } = 2000;
        public int TrunkIncreasePrice { get; set; } = 2000;


        public override void Reload()
        {
            SlotIncreasePrice = cf["slotIncreasePrice"]?.GetInt() ?? 2000;
            TrunkIncreasePrice = cf["trunkIncreasePrice"]?.GetInt() ?? 2000;

            base.Reload();
        }

        public override void Write()
        {
            cf.Set("slotIncreasePrice", SlotIncreasePrice.ToString());
            cf.Set("trunkIncreasePrice", TrunkIncreasePrice.ToString());
            cf.Set("gameWorldId", GameWorldId.ToString());

            base.Write();
        }
    }

    internal class Center : WvsConfig
    {
        public Center(string serverName) : base(serverName, 8383)
        {
        }

        public string Name { get; set; }
        public byte GameWorldID { get; set; }
        public float UserNumberMultiplier { get; set; }
        public byte ChannelCount { get; set;  }

        public override void Reload()
        {
            GameWorldID = cf["gameWorldId"]?.GetByte() ?? 0;

            base.Reload();
        }

        public override void Write()
        {
            cf.Set("gameWorldId", GameWorldID.ToString());
            base.Write();
        }
    }

    internal class Login : WvsConfig
    {
        private Center[] centers;

        public Login(string serverName, params Center[] centers) : base(serverName, 8484)
        {
            this.centers = centers;
        }

        public uint DataChecksum { get; set; }

        public override void Reload()
        {
            DataChecksum = cf["dataChecksum"]?.GetUInt() ?? 0xF825B615;

            foreach (var node in cf["center"])
            {
                var privateIP = node["ip"]?.GetString() ?? "no ip";
                var port = node["port"]?.GetUShort() ?? 0;

                var center = centers.FirstOrDefault(x => x.Port == port && x.PrivateIP == privateIP);
                if (center == null)
                {
                    Debug.WriteLine($"No center found for addr {privateIP}:{port}");
                    continue;
                }

                center.Name = node.Name;
                center.ChannelCount = node["channelNo"]?.GetByte() ?? 1;
                center.UserNumberMultiplier = node["userNoMultiplier"]?.GetFloat() ?? 7.0f;
            }

            base.Reload();
        }

        public override void Write()
        {
            cf.Set("dataChecksum", "0x" + DataChecksum.ToString("X8"));

            cf.Set("center", centers.Select(x =>
            {
                var centerNode = new ConfigReader.Node()
                {
                    Name = x.Name,
                    SubNodes = new List<ConfigReader.Node>(),
                };

                centerNode.Set("ip", x.PrivateIP);
                centerNode.Set("port", x.Port.ToString());
                centerNode.Set("world", x.GameWorldID.ToString());
                centerNode.Set("channelNo", x.ChannelCount.ToString());
                centerNode.Set("userNoMultiplier", x.UserNumberMultiplier.ToString(ConfigReader.Node.NumberFormat));

                return centerNode;
            }).ToArray());

            base.Write();
        }
    }
}