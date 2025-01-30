using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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
            
            var broadcastIPs = NetworkInterface.GetAllNetworkInterfaces()
                .Where(x => x.SupportsMulticast)
                .SelectMany(x => x.GetIPProperties().MulticastAddresses)
                .Select(x => x.Address)
                .Distinct()
                .Where(x => x.AddressFamily == AddressFamily.InterNetwork)
                .ToList();

            foreach (var broadcastIP in broadcastIPs)
            {
                try
                {
                    _udpClient.JoinMulticastGroup(broadcastIP);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unable to listen on an IP {broadcastIP}, ignoring. Error: {ex}");
                }
            }
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