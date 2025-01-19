using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using log4net;

namespace WvsBeta.Common.Sessions
{
    public static class BinaryReaderExtensions
    {
        public static ushort ReadUInt16BE(this BinaryReader br)
        {
            var a = br.ReadByte();
            var b = br.ReadByte();
            return (ushort)((a << 8) | b);
        }
    }

    public class HaProxyAcceptor : Acceptor
    {
        private static ILog _log = LogManager.GetLogger(nameof(HaProxyAcceptor));

        public List<IPAddress> AllowedAddresses { get; } = new List<IPAddress>();
        public int ProxyVersion { get; set; } = 2;

        public HaProxyAcceptor(ushort pPort, params string[] allowedAddresses) : base(pPort)
        {
            AllowedAddresses.Add(IPAddress.Parse("127.0.0.1"));

            if (allowedAddresses != null)
            {
                AllowedAddresses.AddRange(allowedAddresses.Select(IPAddress.Parse));
            }
            
            _log.Info($"Allowed HaProxy addrs: {string.Join(", ", AllowedAddresses.Select(x => x.ToString()))}");
        }

        private static byte[] HaProxyHeader = new byte[12]
        {
            0x0d, 0x0a, 0x0d, 0x0a, 0x00, 0x0d, 0x0a, 0x51, 0x55, 0x49, 0x54, 0x0A
        };

        private enum Command : byte
        {
            LOCAL = 0,
            PROXY = 1,
        }

        private enum TransportFamily : byte
        {
            AF_UNSPEC = 0,
            AF_INET = 1,
            AF_INET6 = 2,
            AF_UNIX = 3,
        }

        private enum TransportProtocol : byte
        {
            UNSPEC = 0,
            STREAM = 1,
            DGRAM = 2,
        }

        private bool PreAcceptVersion2(IPAddress ipAddress, BinaryReader br, out IPEndPoint srcEndPoint, out IPEndPoint dstEndPoint)
        {
            srcEndPoint = null;
            dstEndPoint = null;

            // Read data
            var firstHeader = br.ReadBytes(HaProxyHeader.Length);

            if (!HaProxyHeader.SequenceEqual(firstHeader))
            {
                _log.Error($"[{ipAddress}] Received invalid header! Disconnecting.");
                return false;
            }

            var versionAndCommand = br.ReadByte();
            var version = versionAndCommand >> 4;
            var command = (Command)(versionAndCommand & 0x0F);

            if (version != 0x02)
            {
                _log.Error($"[{ipAddress}] Invalid protocol version {version}! Disconnecting.");
                return false;
            }

            if (command == Command.LOCAL || command == Command.PROXY)
            {
                var transportProtocolAndFamily = br.ReadByte();
                var transportFamily = (TransportFamily)(transportProtocolAndFamily >> 4);
                var transportProtocol = (TransportProtocol)(transportProtocolAndFamily & 0x0F);

                if (transportProtocol != TransportProtocol.STREAM && 
                    command != Command.LOCAL)
                {
                    _log.Error($"[{ipAddress}] Receiving {transportProtocol} protocol with a {command} command");
                    return false;
                }

                var len = br.ReadUInt16BE();
                if (len == 12)
                {
                    if (transportFamily != TransportFamily.AF_INET &&
                        command != Command.LOCAL)
                    {
                        _log.Error($"[{ipAddress}] Receiving ipv4 length AoB while family is {transportFamily}");
                        return false;
                    }
                    var srcAddr = br.ReadBytes(4);
                    var dstAddr = br.ReadBytes(4);
                    var srcPort = br.ReadUInt16BE();
                    var dstPort = br.ReadUInt16BE();

                    srcEndPoint = new IPEndPoint(new IPAddress(srcAddr), srcPort);
                    dstEndPoint = new IPEndPoint(new IPAddress(dstAddr), dstPort);
                }
                else if (len == 36)
                {
                    if (transportFamily != TransportFamily.AF_INET6 && 
                        command != Command.LOCAL)
                    {
                        _log.Error($"[{ipAddress}] Receiving ipv6 length AoB while family is {transportFamily}");
                        return false;
                    }
                    var srcAddr = br.ReadBytes(16);
                    var dstAddr = br.ReadBytes(16);
                    var srcPort = br.ReadUInt16BE();
                    var dstPort = br.ReadUInt16BE();

                    srcEndPoint = new IPEndPoint(new IPAddress(srcAddr), srcPort);
                    dstEndPoint = new IPEndPoint(new IPAddress(dstAddr), dstPort);
                }
                else
                {
                    _log.Error($"[{ipAddress}] Unknown length protocol info found {len}");
                    return false;
                }
            }
            else
            {
                _log.Error($"[{ipAddress}] Unknown command {command}! Disconnecting.");
                return false;
            }

            return true;
        }

        public override bool PreAccept(Socket pSocket, out IPEndPoint srcEndPoint, out IPEndPoint dstEndPoint)
        {
            srcEndPoint = null;
            dstEndPoint = null;

            var remoteEndpoint = pSocket.RemoteEndPoint as IPEndPoint;
            if (remoteEndpoint == null)
            {
                _log.Error($"Trying to get convert {pSocket.RemoteEndPoint} ({pSocket.RemoteEndPoint.GetType()}) to IPEndPoint, but it failed (null)");
                return false;
            }

            var ipAddress = remoteEndpoint.Address;

            // First check if this is an accepted IP address
            if (AllowedAddresses.Count > 0)
            {
                if (!AllowedAddresses.Contains(ipAddress))
                {
                    _log.Error($"[{ipAddress}] Received (possible) HaProxy connection on port {Port} but the ip {ipAddress} is not allowed.");
                    return false;
                }
            }

            using var ns = new NetworkStream(pSocket, false);
            ns.ReadTimeout = 20;
            using var br = new BinaryReader(ns, Encoding.Default, true);

            if (ProxyVersion == 2)
            {
                return PreAcceptVersion2(ipAddress, br, out srcEndPoint, out dstEndPoint);
            }

            return false;
        }
        
    }
}
