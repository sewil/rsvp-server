using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Channels;
using log4net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.GameObjects;
using WvsBeta.Game.Handlers;
using WvsBeta.Game.Packets;

namespace WvsBeta.Game
{
    public partial class CenterSocket : AbstractConnection
    {
        private static ILog _log = LogManager.GetLogger(typeof(CenterSocket));

        private bool disconnectExpected;

        private static PacketTimingTracker<ISServerMessages> ptt = new PacketTimingTracker<ISServerMessages>();

        static CenterSocket()
        {
            MasterThread.RepeatingAction.Start("PacketTimingTracker Flush CenterSocket", ptt.Flush, 0, 60 * 1000);
        }

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
            ptt.StartMeasurement();

            var startTime = MasterThread.CurrentTime;
            var msg = (ISServerMessages)packet.ReadByte();
            try
            {
                switch (msg)
                {
                    case ISServerMessages.Pong:
                    case ISServerMessages.Ping: break;

                    case ISServerMessages.ChangeCenterServer:
                        {
                            var ip = packet.ReadBytes(4);
                            var port = packet.ReadUShort();
                            disconnectExpected = true;
                            Server.Instance.CenterIP = new IPAddress(ip);
                            Server.Instance.CenterPort = port;
                            Server.Instance.CenterMigration = true;
                            Disconnect();
                            break;
                        }

                    case ISServerMessages.ChangeRates:
                        {
                            var mobexprate = packet.ReadDouble();
                            var mesosamountrate = packet.ReadDouble();
                            var dropchancerate = packet.ReadDouble();

                            if (mobexprate > 0 && mobexprate != Server.Instance.RateMobEXP)
                            {
                                Server.Instance.RateMobEXP = mobexprate;
                                Program.MainForm.LogAppend("Changed EXP Rate to {0}", mobexprate);
                            }

                            if (mesosamountrate > 0 && mesosamountrate != Server.Instance.RateMesoAmount)
                            {
                                Server.Instance.RateMesoAmount = mesosamountrate;
                                Program.MainForm.LogAppend("Changed Meso Rate to {0}", mesosamountrate);
                            }

                            if (dropchancerate > 0 && dropchancerate != Server.Instance.RateDropChance)
                            {
                                Server.Instance.RateDropChance = dropchancerate;
                                Program.MainForm.LogAppend("Changed Drop Rate to {0}", dropchancerate);
                            }

                            var currentDateTime = MasterThread.CurrentDate;
                            Server.Instance.CharacterList.ForEach(x => x.Value?.SetIncExpRate(currentDateTime.Day, currentDateTime.Hour));

                            SendUpdateRates();
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

                            var list = Server.Instance.GameHaProxyAcceptor.AllowedAddresses;
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

                    case ISServerMessages.WSE_ChangeScrollingHeader:
                        {
                            var str = packet.ReadString();
                            var newIsEmpty = string.IsNullOrEmpty(str);
                            var oldIsEmpty = string.IsNullOrEmpty(Server.Instance.ScrollingHeader);

                            // Do not update if there's already a message running 
                            if ((newIsEmpty && !oldIsEmpty) ||
                                (!newIsEmpty && oldIsEmpty))
                            {
                                Server.Instance.SetScrollingHeader(str);
                            }

                            break;
                        }

                    case ISServerMessages.ReloadNPCScript:
                        {
                            var scriptName = packet.ReadString();

                            Program.MainForm.LogAppend("Processing reload npc script request... Script: " + scriptName);

                            Server.Instance.ForceCompileScriptfile(
                                Server.Instance.GetScriptFilename(scriptName),
                                null
                            );
                            break;
                        }

                    case ISServerMessages.ServerAssignmentResult:
                        {
                            var inMigration = Server.Instance.InMigration = packet.ReadBool();
                            Server.Instance.ID = packet.ReadByte();

                            GlobalContext.Properties["ChannelID"] = Server.Instance.ID;

                            if (inMigration)
                            {
                                Program.MainForm.LogAppend("Server Migration in process...");
                                Server.Instance.IsNewServerInMigration = true;
                            }
                            else if (!Server.Instance.CenterMigration)
                            {
                                Server.Instance.StartListening();

                                Program.MainForm.LogAppend($"Handling as Game Server {Server.Instance.ID} on World {Server.Instance.WorldID} ({Server.Instance.WorldName})");
                            }
                            else
                            {
                                Program.MainForm.LogAppend("Reconnected to center server?");
                            }

                            Server.Instance.CenterMigration = false;
                            break;
                        }

                    case ISServerMessages.ServerMigrationUpdate:
                        {
                            var pw = new Packet(ISClientMessages.ServerMigrationUpdate);
                            switch ((ServerMigrationStatus)packet.ReadByte())
                            {
                                case ServerMigrationStatus.StartListening:
                                    {
                                        Server.Instance.StartListening();
                                        pw.WriteByte((byte)ServerMigrationStatus.DataTransferRequest);
                                        SendPacket(pw);
                                        break;
                                    }
                                case ServerMigrationStatus.DataTransferRequest:
                                    {
                                        pw.WriteByte((byte)ServerMigrationStatus.DataTransferResponse);

                                        using (var uncompressedPacket = new Packet())
                                        {
                                            var mapsWithDrops =
                                                MapProvider.Maps.Where(x => x.Value.DropPool.Drops.Count > 0)
                                                    .ToArray();
                                            uncompressedPacket.WriteInt(mapsWithDrops.Length);
                                            foreach (var map in mapsWithDrops)
                                            {
                                                uncompressedPacket.WriteInt(map.Key);
                                                map.Value.DropPool.EncodeForMigration(uncompressedPacket);
                                            }

                                            PartyData.EncodeForTransfer(uncompressedPacket);

                                            uncompressedPacket.GzipCompress(pw);

                                            Program.MainForm.LogAppend("Sent " + mapsWithDrops.Length + " map updates... (packet size: " + pw.Length + " bytes)");
                                        }

                                        SendPacket(pw);
                                        break;
                                    }
                                case ServerMigrationStatus.DataTransferResponse:
                                    {
                                        using (var gzipStream = new GZipStream(packet.MemoryStream, CompressionMode.Decompress, true))
                                        using (var decompressedPacket = new Packet(gzipStream))
                                        {
                                            var maps = decompressedPacket.ReadInt();
                                            for (var i = 0; i < maps; i++)
                                            {
                                                var mapid = decompressedPacket.ReadInt();
                                                MapProvider.Maps[mapid].DropPool
                                                    .DecodeForMigration(decompressedPacket);
                                            }

                                            if (decompressedPacket.Length != decompressedPacket.Position)
                                            {
                                                PartyData.DecodeForTransfer(decompressedPacket);
                                            }

                                            Program.MainForm.LogAppend("Updated " + maps + " maps...");
                                        }

                                        pw.WriteByte((byte)ServerMigrationStatus.FinishedInitialization);
                                        SendPacket(pw);
                                        break;
                                    }

                                case ServerMigrationStatus.FinishedInitialization:
                                    {
                                        Program.MainForm.LogAppend("Other side is ready, start CC. Connections: " + Pinger.CurrentLoggingConnections);

                                        var timeout = 15;

                                        var sentPacket = false;
                                        var disconnectedPlayers = false;
                                        MasterThread.RepeatingAction.Start(
                                            "Client Migration Thread",
                                            date =>
                                            {
                                                if (Pinger.CurrentLoggingConnections == 0 ||
                                                    (date - startTime) > timeout * 1000)
                                                {
                                                    Program.MainForm.LogAppend($"Almost done. Connections left: {Pinger.CurrentLoggingConnections}, timeout {(date - startTime) > timeout * 1000}");
                                                    if (sentPacket == false)
                                                    {
                                                        var _pw = new Packet(ISClientMessages.ServerMigrationUpdate);
                                                        _pw.WriteByte((byte)ServerMigrationStatus.PlayersMigrated);
                                                        SendPacket(_pw);
                                                        sentPacket = true;
                                                        Program.MainForm.LogAppend("Sent Migration Done packet");
                                                    }
                                                    else
                                                    {
                                                        Program.MainForm.Shutdown();
                                                    }
                                                }
                                                else if (!disconnectedPlayers)
                                                {
                                                    Server.Instance.PlayerList.Values.ForEach(x =>
                                                    {
                                                        var c = x.Character;
                                                        if (c == null) return;
                                                        try
                                                        {
                                                            c.DestroyAdditionalProcess();
                                                            if (c.IsAFK && !c.Field.Town && c.Field.Mobs.Count > 0)
                                                            {
                                                                c.ChangeMap(c.Field.TownMap);
                                                            }

                                                            if (c.HuskMode)
                                                            {
                                                                c.Disconnect();
                                                            }
                                                            else
                                                            {
                                                                x.Socket.DoChangeChannelReq(Server.Instance.ID);
                                                            }
                                                        }
                                                        catch { }
                                                    });
                                                    disconnectedPlayers = true;
                                                }
                                                else
                                                {
                                                    Program.MainForm.LogAppend("Waiting for DC...");
                                                }
                                            },
                                            0,
                                            5000
                                        );
                                        break;
                                    }

                                case ServerMigrationStatus.PlayersMigrated:
                                    {
                                        Server.Instance.InMigration = false;
                                        Program.MainForm.LogAppend("Other server is done");
                                        break;
                                    }

                                case ServerMigrationStatus.StartMigration:
                                    {
                                        Server.Instance.InMigration = true;
                                        Server.Instance.IsNewServerInMigration = false;
                                        Program.MainForm.LogAppend("Started migration to new server");
                                        Server.Instance.StopListening();
                                        pw.WriteByte((byte)ServerMigrationStatus.StartListening);
                                        SendPacket(pw);
                                        break;
                                    }
                            }

                            break;
                        }

                    default:
                        if (!Server.Instance.GuildManager.HandleServerUpdate(msg, packet) &&
                            !TryHandlePartyPacket(packet, msg) &&
                            !TryHandlePlayerPacket(packet, msg))
                        {
                            Program.MainForm.LogAppend("UNKNOWN CENTER PACKET: " + packet);
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                Program.MainForm.LogAppend(ex + "\r\nPACKET: " + packet);
            }
            finally
            {
                ptt.EndMeasurement((byte)msg);
            }
        }

        public bool TryHandlePlayerPacket(Packet packet, ISServerMessages msg)
        {
            switch (msg)
            {
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
                            if (port == 0)
                            {
                                var pw = new Packet(ServerMessages.TRANSFER_CHANNEL_REQ_IGNORED);
                                player.Character.SendPacket(pw);
                            }
                            else
                            {
                                player.Character.DestroyAdditionalProcess();
                                RedisBackend.Instance.SetPlayerCCIsBeingProcessed(charid);

                                player.IsCC = true;
                                player.Socket.SendConnectToServer(ip, port);
                            }
                        }
                        else
                        {
                            Program.MainForm.LogAppend("Tried to CC unknown player (unknown hash)");
                        }

                        break;
                    }
                case ISServerMessages.PlayerRenamed:
                    {
                        var session = packet.ReadString();
                        var player = Server.Instance.GetPlayer(session);
                        var chr = player?.Character;
                        if (chr == null)
                        {
                            Program.MainForm.LogAppend("Tried to handle rename packet for unknown player (unknown hash/no character)");
                            break;
                        }

                        var charid = packet.ReadInt();
                        var newName = packet.ReadString();
                        var succeeded = packet.ReadBool();

                        var message = succeeded ? $"Successfully renamed character to {newName}" : $"Unable to rename character to {newName}, check logs of Center";
                        MessagePacket.SendTextPlayer(MessagePacket.MessageTypes.Notice, message, chr);

                        var renamedChar = Server.Instance.GetCharacter(charid);
                        if (renamedChar != null)
                        {
                            renamedChar.Name = newName;
                        }

                        break;
                    }

                case ISServerMessages.PlayerWhisperOrFindOperationResult:
                    {
                        var whisper = packet.ReadBool();
                        var found = packet.ReadBool();
                        var victim = packet.ReadInt();
                        var victimChar = Server.Instance.GetCharacter(victim);
                        if (victimChar == null) break;
                        victimChar.Player.Socket.StartLogging();

                        if (whisper)
                        {
                            if (found)
                            {
                                var sender = packet.ReadString();
                                var channel = packet.ReadByte();
                                var message = packet.ReadString();
                                var direction = packet.ReadBool();
                                byte directionByte = 18;
                                if (direction)
                                {
                                    directionByte = 10;
                                }

                                MessagePacket.Whisper(victimChar, sender, channel, message, directionByte);
                            }
                            else
                            {
                                var sender = packet.ReadString();
                                MessagePacket.Find(victimChar, sender, -1, 0, false);
                            }
                        }
                        else
                        {
                            if (found)
                            {
                                var sender = packet.ReadString();
                                var channel = packet.ReadSByte();
                                var wat = packet.ReadSByte();
                                MessagePacket.Find(victimChar, sender, channel, wat, false);
                            }
                            else
                            {
                                var sender = packet.ReadString();
                                MessagePacket.Find(victimChar, sender, -1, 0, false);
                            }
                        }

                        break;
                    }

                case ISServerMessages.PlayerSuperMegaphone:
                    {
                        MessagePacket.SendSuperMegaphoneMessage(packet.ReadString(), packet.ReadBool(), packet.ReadByte());
                        break;
                    }

                case ISServerMessages.AdminMessage:
                    {
                        var message = packet.ReadString();
                        var type = packet.ReadByte();

                        var pw = new Packet(ServerMessages.BROADCAST_MSG);
                        pw.WriteByte(type);
                        pw.WriteString(message);
                        if (type == 4)
                        {
                            pw.WriteBool(message.Length != 0);
                        }

                        foreach (var kvp in MapProvider.Maps)
                        {
                            kvp.Value.SendPacket(pw);
                        }

                        break;
                    }

                case ISServerMessages.PlayerChangeServerData:
                    {
                        var charid = packet.ReadInt();
                        var readBufferPacket = new Packet(packet.ReadLeftoverBytes());
                        Server.Instance.CCIngPlayerList[charid] = new Tuple<Packet, long>(readBufferPacket, MasterThread.CurrentTime);
                        break;
                    }

                case ISServerMessages.KickPlayerResult:
                    {
                        var userId = packet.ReadInt();
                        foreach (var character in Server.Instance.CharacterList.Values.Where(x => x.UserID == userId))
                        {
                            Program.MainForm.LogAppend($"Handling centerserver kick request for userid {userId}: {character}");
                            character.Disconnect();
                        }

                        break;
                    }

                case ISServerMessages.PlayerSendPacket:
                    {
                        var pChar = Server.Instance.GetCharacter(packet.ReadInt());
                        if (pChar == null) break;

                        var offset = packet.Position;
                        var packetDataToSend = packet.ReadLeftoverBytes();
                        pChar.SendPacket(packetDataToSend);
                        packet.Position = offset;

                        switch (packet.ReadByte<ServerMessages>())
                        {
                            case ServerMessages.FRIEND_RESULT:
                                switch (packet.ReadByte<FriendResReq>())
                                {
                                    case FriendResReq.FriendRes_IncMaxCount_Done:
                                        pChar.PrimaryStats.BuddyListCapacity = packet.ReadByte();
                                        break;
                                }
                                break;
                        }
                        break;
                    }
                default: return false;
            }

            return true;
        }

        public bool TryHandlePartyPacket(Packet packet, ISServerMessages msg)
        {
            switch (msg)
            {
                case ISServerMessages.ChangeParty:
                    {
                        var fucker = Server.Instance.GetCharacter(packet.ReadInt());
                        if (fucker == null) break;

                        fucker.PartyID = packet.ReadInt();
                        if (fucker.PartyID != 0 && DoorManager.TryGetDoor(fucker, out var md))
                        {
                            // Tell the door info to the server
                            PartyDoorChanged(fucker.ID, md);
                        }
                        break;
                    }

                case ISServerMessages.UpdateHpParty:
                    {
                        var fucker = Server.Instance.GetCharacter(packet.ReadInt());
                        if (fucker != null && fucker.PartyID != 0)
                        {
                            fucker.FullPartyHPUpdate();
                        }

                        break;
                    }

                case ISServerMessages.PartyInformationUpdate:
                    {
                        var ptId = packet.ReadInt();
                        var leader = packet.ReadInt();
                        var members = new int[Constants.MaxPartyMembers];
                        for (var i = 0; i < members.Length; i++)
                        {
                            members[i] = packet.ReadInt();
                        }

                        PartyData pd;
                        if (PartyData.Parties.TryGetValue(ptId, out pd))
                        {
                            pd.Members = members;
                        }
                        else
                        {
                            pd = PartyData.Parties[ptId] = new PartyData(leader, members, ptId);
                        }

                        PartyData.TryUpdatePartyDataInInstances(pd);
                        break;
                    }

                case ISServerMessages.PartyDisbanded:
                    {
                        var ptId = packet.ReadInt();

                        PartyData.Parties.Remove(ptId);
                        break;
                    }

                default: return false;
            }

            return true;
        }
    }
}