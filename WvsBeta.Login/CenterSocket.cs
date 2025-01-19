using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using log4net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Login
{
    public class CenterSocket : AbstractConnection
    {
        private Center _center;
        private static ILog _log = LogManager.GetLogger(typeof(CenterSocket));

        private static PacketTimingTracker<ISServerMessages> ptt = new PacketTimingTracker<ISServerMessages>();

        static CenterSocket()
        {
            MasterThread.RepeatingAction.Start("PacketTimingTracker Flush CenterSocket", ptt.Flush, 0, 60 * 1000);
        }

        public CenterSocket(string ip, ushort port, Center center)
            : base(ip, port)
        {
            _center = center;
            UseIvanPacket = true;
        }

        public override void OnDisconnect()
        {
            Program.MainForm.LogAppend("Disconnected from the CenterServer!");
            // release all connections
        }

        public override void OnHandshakeInbound(Packet pPacket)
        {
            var packet = new Packet(ISClientMessages.ServerRequestAllocation);
            packet.WriteString(Server.Instance.Name);
            packet.WriteString(Constants.AUTH_KEY);
            packet.WriteString(Server.Instance.PublicIP.ToString());
            packet.WriteUShort(Server.Instance.Port);
            SendPacket(packet);

            Program.MainForm.LogAppend("Connected to the CenterServer!");
        }

        public override void AC_OnPacketInbound(Packet packet)
        {
            ptt.StartMeasurement();

            var opcode = (ISServerMessages)packet.ReadByte();
            try
            {
                switch (opcode)
                {
                    case ISServerMessages.ChangeCenterServer:
                        {
                            var ip = packet.ReadBytes(4);
                            var port = packet.ReadUShort();
                            _center.IP = new IPAddress(ip);
                            _center.Port = port;
                            _log.Error($"Changing center server: {ip}:{port}");
                            this.Disconnect();
                            _center.Connect();
                            break;
                        }
                    case ISServerMessages.ServerSetUserNo:
                        {
                            for (var i = 0; i < _center.Channels; i++)
                            {
                                _center.UserNo[i] = packet.ReadInt();
                            }

                            break;
                        }

                    case ISServerMessages.PlayerChangeServerResult:
                        {
                            var session = packet.ReadString();
                            var player = Server.Instance.GetPlayer(session);
                            if (player != null)
                            {
                                player.Socket.StartLogging();

                                var charid = packet.ReadInt();
                                var ip = packet.ReadBytes(4);
                                var port = packet.ReadUShort();

                                player.Socket.ConnectToServer(charid, ip, port);
                            }

                            break;
                        }
                    case ISServerMessages.PlayerRequestWorldLoadResult:
                        {
                            var session = packet.ReadString();
                            var player = Server.Instance.GetPlayer(session);
                            player?.Socket.StartLogging();
                            player?.Socket.HandleWorldLoadResult(packet);

                            break;
                        }
                    case ISServerMessages.PlayerRequestChannelStatusResult:
                        {
                            var session = packet.ReadString();
                            var player = Server.Instance.GetPlayer(session);
                            if (player == null) break;
                            player.Socket.StartLogging();

                            player.Socket.HandleChannelSelectResult(packet);


                            break;
                        }
                    case ISServerMessages.PlayerCreateCharacterResult:
                        {
                            var hash = packet.ReadString();
                            var player = Server.Instance.GetPlayer(hash);
                            if (player == null) break;
                            player.Socket.StartLogging();

                            player.Socket.HandleCreateNewCharacterResult(packet);

                            break;
                        }

                    case ISServerMessages.PlayerCreateCharacterNamecheckResult:
                        {
                            var hash = packet.ReadString();
                            var player = Server.Instance.GetPlayer(hash);
                            if (player == null) break;
                            player.Socket.StartLogging();

                            player.Socket.RequestOut = false;

                            var name = packet.ReadString();
                            var taken = packet.ReadBool();

                            var pack = new Packet(ServerMessages.CHECK_CHARACTER_NAME_AVAILABLE);
                            pack.WriteString(name);
                            pack.WriteBool(taken);
                            player.Socket.SendPacket(pack);

                            break;
                        }

                    case ISServerMessages.PlayerDeleteCharacterResult:
                        {
                            var hash = packet.ReadString();
                            var player = Server.Instance.GetPlayer(hash);
                            if (player == null) break;
                            player.Socket.StartLogging();

                            var charid = packet.ReadInt();
                            var result = packet.ReadByte();
                            player.Socket.HandleCharacterDeletionResult(charid, result);

                            break;
                        }

                    case ISServerMessages.UpdateHaProxyIPs:
                        {
                            var ipCount = packet.ReadShort();
                            var ips = new List<IPAddress>();
                            for (var i = 0; i < ipCount; i++)
                            {
                                ips.Add(new IPAddress(packet.ReadBytes(4)));
                            }

                            var list = Server.Instance.LoginHaProxyAcceptor.AllowedAddresses;
                            list.Clear();
                            list.AddRange(ips);

                            _log.Info($"Updated allowed HaProxy IPs to: {string.Join(", ", ips.Select(x => x.ToString()))}");

                            break;
                        }

                    case ISServerMessages.PublicIPUpdated:
                        {
                            var serverName = packet.ReadString();
                            var ip = packet.ReadString();
                            var result = packet.ReadBool();

                            _log.Info($"The public ip update of {serverName} to {ip} was {result}");

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                _log.Error("Exception while handling packet from CenterServer", ex);
            }
            finally
            {
                ptt.EndMeasurement((byte)opcode);
            }
        }

        public void UpdateConnections(int value)
        {
            var packet = new Packet(ISClientMessages.ServerSetConnectionsValue);
            packet.WriteInt(value);
            SendPacket(packet);
        }

        public void RequestCharacterConnectToWorld(string Hash, int charid, byte world, byte channel)
        {
            var packet = new Packet(ISClientMessages.PlayerChangeServer);
            packet.WriteString(Hash);
            packet.WriteInt(charid);
            packet.WriteByte(world);
            packet.WriteByte(channel);
            packet.WriteBool(false);
            SendPacket(packet);
        }

        public void RequestCharacterGetWorldLoad(string Hash, byte world)
        {
            var packet = new Packet(ISClientMessages.PlayerRequestWorldLoad);
            packet.WriteString(Hash);
            packet.WriteByte(world);
            SendPacket(packet);
        }

        public void CheckCharacternameTaken(string Hash, string name)
        {
            var packet = new Packet(ISClientMessages.PlayerCreateCharacterNamecheck);
            packet.WriteString(Hash);
            packet.WriteString(name);
            SendPacket(packet);
        }

        public void RequestCharacterIsChannelOnline(string Hash, byte world, byte channel, int accountId)
        {
            var packet = new Packet(ISClientMessages.PlayerRequestChannelStatus);
            packet.WriteString(Hash);
            packet.WriteByte(world);
            packet.WriteByte(channel);
            packet.WriteInt(accountId);
            SendPacket(packet);
        }

        public void RequestDeleteCharacter(string Hash, int accountId, int characterId)
        {
            var packet = new Packet(ISClientMessages.PlayerDeleteCharacter);
            packet.WriteString(Hash);
            packet.WriteInt(accountId);
            packet.WriteInt(characterId);
            SendPacket(packet);
        }
    }
}