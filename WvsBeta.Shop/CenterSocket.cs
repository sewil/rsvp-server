using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using log4net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Shop
{
    public class CenterSocket : AbstractConnection
    {
        private static ILog _log = LogManager.GetLogger(typeof(CenterSocket));

        private bool disconnectExpected = false;

        public CenterSocket()
            : base(Server.Instance.CenterIP.ToString(), (ushort)Server.Instance.CenterPort)
        {
            UseIvanPacket = true;
        }

        public override void OnDisconnect()
        {
            Server.Instance.CenterConnection = null;
            if (disconnectExpected)
            {
                Server.Instance.ConnectToCenter();
            }
            else
            {
                Program.MainForm.LogAppend("Disconnected from the Center Server! Something went wrong! :S");
                // release all connections
                Program.MainForm.Shutdown();
            }
        }

        public override void OnHandshakeInbound(Packet pPacket)
        {
            var packet2 = new Packet(ISClientMessages.ServerRequestAllocation);
            packet2.WriteString(Server.Instance.Name);
            packet2.WriteString(Constants.AUTH_KEY);
            packet2.WriteString(Server.Instance.PublicIP.ToString());
            packet2.WriteUShort(Server.Instance.Port);
            packet2.WriteByte(Server.Instance.WorldID);
            packet2.WriteString(Server.Instance.WorldName);
            SendPacket(packet2);
        }

        public override void AC_OnPacketInbound(Packet packet)
        {
            var smsg = (ISServerMessages)packet.ReadByte();
            switch (smsg)
            {
                case ISServerMessages.ChangeCenterServer:
                    {
                        var ip = packet.ReadBytes(4);
                        var port = packet.ReadUShort();
                        disconnectExpected = true;
                        Server.Instance.CenterIP = new IPAddress(ip);
                        Server.Instance.CenterPort = port;
                        Server.Instance.CenterMigration = true;
                        this.Disconnect();
                        break;
                    }

                case ISServerMessages.PlayerChangeServerResult:
                    {
                        var session = packet.ReadString();
                        var player = Server.Instance.GetPlayer(session);
                        if (player != null)
                        {
                            var charid = packet.ReadInt();
                            var ip = packet.ReadBytes(4);
                            var port = packet.ReadUShort();
                            if (port == 0)
                            {
                                player.Socket.Disconnect();
                            }
                            else
                            {
                                RedisBackend.Instance.SetPlayerCCIsBeingProcessed(charid);

                                player.IsCC = true;
                                player.Socket.SendConnectToServer(ip, port);
                            }
                        }

                        break;
                    }
                case ISServerMessages.ServerAssignmentResult:
                    {
                        if (!Server.Instance.CenterMigration)
                        {
                            Server.Instance.StartListening();

                            Program.MainForm.LogAppend($"Handling as CashShop on World {Server.Instance.WorldID} ({Server.Instance.WorldName})");
                        }
                        else
                        {
                            Program.MainForm.LogAppend("Reconnected to center server");
                        }

                        Server.Instance.CenterMigration = false;
                        break;
                    }

                case ISServerMessages.PlayerChangeServerData:
                    {
                        var charid = packet.ReadInt();
                        var readBufferPacket = new Packet(packet.ReadLeftoverBytes());
                        Server.Instance.CCIngPlayerList[charid] = readBufferPacket;
                        break;
                    }
                case ISServerMessages.ReloadCashshopData:
                    {
                        Program.MainForm.LogAppend("Reloading cashshop data");
                        Server.Instance.LoadCashshopData();
                        ShopProvider.Reload();
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

                        var list = Server.Instance.HaProxyAcceptor.AllowedAddresses;
                        list.Clear();
                        list.AddRange(ips);

                        _log.Info($"Updated allowed HaProxy IPs to: {string.Join(", ", ips.Select(x => x.ToString()))}");

                        break;
                    }

                case ISServerMessages.PublicIPUpdated:
                    {
                        var serverName = packet.ReadString();
                        var ip = packet.ReadString();
                        var succeeded = packet.ReadBool();

                        if (succeeded)
                        {
                            if (serverName == Server.Instance.Name)
                            {
                                _log.Info($"Updating local Public IP to {ip}");
                                Server.Instance.PublicIP = IPAddress.Parse(ip);
                            }
                        }

                        break;
                    }

                default:
                    Server.Instance.GuildManager.HandleServerUpdate(smsg, packet);
                    break;
            }
        }

        public void updateConnections(int value)
        {
            var packet = new Packet(ISClientMessages.ServerSetConnectionsValue);
            packet.WriteInt(value);
            SendPacket(packet);
        }

        public void CharacterExitCashshop(string Hash, int charid, byte world)
        {
            var packet = new Packet(ISClientMessages.PlayerQuitCashShop);
            packet.WriteString(Hash);
            packet.WriteInt(charid);
            packet.WriteByte(world);
            if (Server.Instance.CCIngPlayerList.TryGetValue(charid, out var p))
            {
                Server.Instance.CCIngPlayerList.Remove(charid);
                packet.WriteBytes(p.ReadLeftoverBytes());
            }
            else
            {
                packet.WriteInt(0);
            }

            SendPacket(packet);
        }

        public void UnregisterCharacter(int charid, bool cc)
        {
            var packet = new Packet(ISClientMessages.ServerRegisterUnregisterPlayer);
            packet.WriteInt(charid);
            packet.WriteBool(false);
            packet.WriteBool(cc);
            SendPacket(packet);
        }

        public void RegisterCharacter(Character character)
        {
            var packet = new Packet(ISClientMessages.ServerRegisterUnregisterPlayer);
            packet.WriteInt(character.ID);
            packet.WriteBool(true);
            packet.WriteString(character.Name);
            packet.WriteShort(character.CharacterStat.Job);
            packet.WriteByte(character.CharacterStat.Level);
            packet.WriteByte(character.GMLevel);
            SendPacket(packet);
        }

    }
}