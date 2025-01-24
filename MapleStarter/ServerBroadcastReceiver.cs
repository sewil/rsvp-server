using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MapleStarter
{
    internal class ServerBroadcastReceiver
    {
        private UdpClient _udpClient { get; }

        internal ServerBroadcastReceiver()
        {
            _udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 28484));
            _udpClient.JoinMulticastGroup(new IPAddress(new byte[] {224, 0, 0, 1}));
        }

        public event EventHandler<ServerBroadcast> OnBroadcastReceived;

        public void Start()
        {
            _udpClient.BeginReceive(Received, null);
        }

        private void Received(IAsyncResult ar)
        {
            IPEndPoint? sender = null;
            try
            {
                var buffer = _udpClient.EndReceive(ar, ref sender);

                using var packet = new Packet(buffer);

                var serverBroadcast = new ServerBroadcast(sender);
                serverBroadcast.Read(packet);

                OnBroadcastReceived.Invoke(this, serverBroadcast);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception while processing udp message. {ex}");
            }

            Start();
        }
    }
}