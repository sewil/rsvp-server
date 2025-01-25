using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MapleStarter
{
    public class ServerBroadcast
    {
        public class LoginServer
        {
            public string PublicIP { get; private set; } = "";
            public ushort Port { get; private set; }
            public bool Started { get; private set; }

            public void Read(Packet packet)
            {
                PublicIP = packet.ReadString();
                Port = packet.ReadUShort();
                Started = packet.ReadBool();
            }
        }

        public string MachineName { get; private set; } = "";
        public DateTime BroadcastAt { get; private set; }

        public List<LoginServer> LoginServers { get; } = new();

        public IPEndPoint SentBy { get; }

        public ServerBroadcast(IPEndPoint sender)
        {
            SentBy = sender;
        }

        public void Read(Packet packet)
        {
            packet.ReadByte(); // opcode

            BroadcastAt = DateTime.FromFileTimeUtc(packet.ReadLong());
            MachineName = packet.ReadString();

            var loginServers = packet.ReadByte();

            for (var i = 0; i < loginServers; i++)
            {
                var ls = new LoginServer();
                ls.Read(packet);

                LoginServers.Add(ls);
            }
        }
    }
}