using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using log4net;
using MySqlConnector;
using WvsBeta.Center.DBAccessor;
using WvsBeta.Center.Guild;
using WvsBeta.Common;
using WvsBeta.Common.Character;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Center
{
    public class LocalConnection : AbstractConnection
    {
        private static ILog _log = LogManager.GetLogger("LocalConnection");
        private static ILog guildChatLog = LogManager.GetLogger("GuildChatLog");
        private static ILog guildLog = LogManager.GetLogger("GuildLog");

        public static WorldServer World => CenterServer.Instance.World;

        public LocalServer Server { get; set; }

        private static PacketTimingTracker<ISClientMessages> ptt = new PacketTimingTracker<ISClientMessages>();

        static LocalConnection()
        {
            MasterThread.RepeatingAction.Start("PacketTimingTracker Flush LocalConnection", ptt.Flush, 0, 60 * 1000);
        }


        public LocalConnection(System.Net.Sockets.Socket pSocket) : base(pSocket)
        {
            UseIvanPacket = true;
        }

        public void Init()
        {
            Pinger.Add(this);
            SendHandshake(1, "WvsBeta Server", 8);
        }

        public void SendRates()
        {
            var packet = new Packet(ISServerMessages.ChangeRates);
            packet.WriteDouble(Server.RateMobEXP);
            packet.WriteDouble(Server.RateMesoAmount);
            packet.WriteDouble(Server.RateDropChance);
            SendPacket(packet);

            var e = World.RunningEvent;
            if (e != null && string.IsNullOrEmpty(e.ScrollingHeader) == false)
            {
                var p = new Packet(ISServerMessages.WSE_ChangeScrollingHeader);
                p.WriteString(e.ScrollingHeader);
                SendPacket(p);
            }
        }
        
        public void SendGuilds(bool broadcast = true)
        {
            var packet = new Packet(ISServerMessages.GuildUpdate);

            var guilds = GuildData.LoadGuilds();

            packet.WriteInt(guilds.Count);
            foreach (var guild in guilds)
            {
                foreach (var guildCharacter in guild.Characters)
                {
                    guildCharacter.Online = CenterServer.Instance.FindCharacter(guildCharacter.CharacterID) != null;
                }

                guild.Encode(packet);
            }

            if (!broadcast) SendPacket(packet);
            else BroadcastGuildPacket(packet);
        }

        public void SendGuild(int guildId)
        {
            var guild = GuildData.LoadGuilds(guildId).FirstOrDefault(x => x.ID == guildId);

            if (guild == null)
            {
                guildLog.Error($"Could not find guild {guildId}???");
                return;
            }

            var packet = new Packet(ISServerMessages.GuildUpdateSingle);
            guild.Encode(packet);
            
            BroadcastGuildPacket(packet);
        }


        public void BroadcastGuildPacket(Packet packet)
        {
            World.SendPacketToEveryShopserver(packet);
            World.SendPacketToEveryGameserver(packet);
        }

        public void SendUserNoUpdateToLogins()
        {
            var packet = new Packet(ISServerMessages.ServerSetUserNo);

            // Should be initialized from loginserver
            var world = World;

            for (byte i = 0; i < world.Channels; i++)
            {
                world.GameServers.TryGetValue(i, out var game);
                packet.WriteInt(game?.Connections ?? 0);
            }

            CenterServer.Instance.BroadcastPacketToLoginServers(packet);
        }

        public override void OnDisconnect()
        {
            if (Server != null)
            {
                Program.MainForm.LogAppend($"Server disconnected: {Server.Name}");

                Server.SetConnection(null);

                Server = null;
            }

            Pinger.Remove(this);
        }

        public override void AC_OnPacketInbound(Packet packet)
        {
            ptt.StartMeasurement();

            var opcode = (ISClientMessages) packet.ReadByte();
            try
            {
                if (Server == null)
                {
                    switch (opcode)
                    {
                        case ISClientMessages.ServerRequestAllocation:
                        {
                            var serverName = packet.ReadString();
                            if (!CenterServer.Instance.LocalServers.TryGetValue(serverName, out var ls))
                            {
                                Program.MainForm.LogAppend("Server doesn't exist in configuration: " + serverName + ". Disconnecting.");
                                Disconnect();
                                return;
                            }

                            var authKey = packet.ReadString();
                            if (authKey != Constants.AUTH_KEY)
                            {
                                CenterServer.Instance.ServerTraceDiscordReporter.Enqueue($"Received connection request with wrong auth key! IP {IP}");
                                Program.MainForm.LogAppend("Wrong auth key!!! Disconnecting.");
                                Disconnect();
                                return;
                            }

                            var publicIp = System.Net.IPAddress.Parse(packet.ReadString());
                            var port = packet.ReadUShort();



                            Program.MainForm.LogAppend(
                                $"Server connecting... Name: {serverName}, Public IP: {publicIp}, Port {port}");

                            if (ls.Type == LocalServerType.Game || ls.Type == LocalServerType.Shop)
                            {
                                var worldid = packet.ReadByte();
                                if (World.ID != worldid)
                                {
                                    Program.MainForm.LogAppend(
                                        $"{serverName} disconnected because it didn't have a valid world ID ({worldid})");
                                    Disconnect();
                                    return;
                                }
                            }


                            if (ls.Connected)
                            {
                                if (ls.InMaintenance)
                                {
                                    Program.MainForm.LogAppend(
                                        $"Server is already connected: {serverName}, but already in maintenance. Disconnecting.");
                                    Disconnect();
                                    return;
                                }

                                Program.MainForm.LogAppend(
                                    $"Server is already connected: {serverName}. Setting up transfer...");
                                ls.InMaintenance = true;
                            }

                            Server = ls;
                            Server.PublicIP = publicIp;
                            Server.Port = port;
                            Server.SetConnection(this);

                            var pw = new Packet(ISServerMessages.ServerAssignmentResult);
                            pw.WriteBool(Server.InMaintenance);

                            if (ls.Type == LocalServerType.Game || 
                                ls.Type == LocalServerType.Shop)
                            {
                                pw.WriteByte(Server.ChannelID);
                            }

                            if (Server.Type == LocalServerType.Game)
                            {
                                Program.MainForm.LogAppend(
                                    $"Gameserver assigned! Name {serverName}; Channel ID {Server.ChannelID}");
                            }
                            else if (Server.Type == LocalServerType.Login)
                            {
                                Program.MainForm.LogAppend("Login connected.");
                            }
                            else if (Server.Type == LocalServerType.Shop)
                            {
                                Program.MainForm.LogAppend($"Shopserver assigned on idx {Server.ChannelID}");
                            }

                            SendPacket(pw);

                            SendRates();

                            if (Server.Type == LocalServerType.Game || 
                                Server.Type == LocalServerType.Shop)
                            {
                                SendGuilds(false);
                            }


                            break;
                        }
                    }
                }
                else
                {
                    switch (opcode)
                    {
                        case ISClientMessages.UpdatePublicIP:
                        {
                            var serverName = packet.ReadString();
                            var ip = packet.ReadString();
                            var p = new Packet(ISServerMessages.PublicIPUpdated);
                            p.WriteString(serverName);
                            p.WriteString(ip);

                            if (!CenterServer.Instance.LocalServers.TryGetValue(serverName, out var ls))
                            {
                                _log.Error($"Trying to update public ip for unknown server: {serverName}");
                                p.WriteBool(false);
                            }
                            else if (!IPAddress.TryParse(ip, out var actualIP))
                            {
                                _log.Error($"Unable to update server ip of {serverName} as {ip} is an invalid IP");
                                p.WriteBool(false);
                            }
                            else
                            {
                                ls.PublicIP = actualIP;
                                _log.Info($"Updated public ip of {serverName} to {actualIP}");
                                p.WriteBool(true);
                            }
                            SendPacket(p);
                            World.SendPacketToEveryGameserver(p);
                            World.SendPacketToEveryShopserver(p);
                            break;
                        }
                        case ISClientMessages.ServerMigrationUpdate:
                        {
                            if (!Server.InMaintenance)
                            {
                                Program.MainForm.LogAppend("Received ServerMigrationUpdate while not in maintenance!");
                                break;
                            }

                            var forwardPacket = new Packet(ISServerMessages.ServerMigrationUpdate);
                            forwardPacket.WriteBytes(packet.ReadLeftoverBytes());

                            // Figure out what way we need to send the packet
                            if (Server.Connection == this)
                                Server.TransferConnection.SendPacket(forwardPacket);
                            else
                                Server.Connection.SendPacket(forwardPacket);

                            break;
                        }

                        case ISClientMessages.ChangeRates:
                        {
                            Server.RateMobEXP = packet.ReadDouble();
                            Server.RateMesoAmount = packet.ReadDouble();
                            Server.RateDropChance = packet.ReadDouble();
                            break;
                        }
                        case ISClientMessages.ServerSetConnectionsValue:
                        {
                            Server.Connections = packet.ReadInt();
                            break;
                        }
                        case ISClientMessages.PlayerChangeServer:
                        {
                            var hash = packet.ReadString();
                            var charid = packet.ReadInt();
                            var world = packet.ReadByte();
                            var channel = packet.ReadByte();
                            var CCing = packet.ReadBool();

                            var chr = CenterServer.Instance.FindCharacter(charid);

                            var pw = new Packet(ISServerMessages.PlayerChangeServerResult);
                            pw.WriteString(hash);
                            pw.WriteInt(charid);

                            var found = true;
                            LocalServer ls = null;
                            // this will null the key, so if there were two instances CCing,
                            // both would probably get killed.
                            if (RedisBackend.Instance.PlayerIsMigrating(charid, false))
                            {
                                Program.MainForm.LogAppend("Character {0} tried to CC while already CCing.", charid);
                                pw.WriteInt(0);
                                pw.WriteShort(0);
                                found = false;
                            }
                            else if (channel < 50 &&
                                     World.GameServers.TryGetValue(channel, out ls) &&
                                     ls.Connected)
                            {
                                pw.WriteBytes(ls.PublicIP.GetAddressBytes());
                                pw.WriteUShort(ls.Port);

                                RedisBackend.Instance.SetMigratingPlayer(charid);

                                if (chr != null)
                                {
                                    chr.isCCing = true;
                                }

                                if (Server.Type == LocalServerType.Login)
                                {
                                    CharacterDBAccessor.UpdateRank(charid);
                                }
                            }
                            else if (channel >= 50 &&
                                     World.ShopServers.TryGetValue((byte) (channel - 50), out ls) &&
                                     ls.Connected)
                            {
                                pw.WriteBytes(ls.PublicIP.GetAddressBytes());
                                pw.WriteUShort(ls.Port);

                                RedisBackend.Instance.SetMigratingPlayer(charid);

                                if (chr != null)
                                {
                                    chr.isCCing = true;
                                    chr.LastChannel = chr.ChannelID;
                                    chr.InCashShop = true;
                                }
                            }
                            else
                            {
                                Program.MainForm.LogAppend("Character {0} tried to CC to channel that is not online.", charid);
                                pw.WriteInt(0);
                                pw.WriteShort(0);
                                found = false;
                            }


                            if (CCing && found && chr != null && ls != null)
                            {
                                chr.FriendsList.SaveBuddiesToDb();
                                chr.isConnectingFromLogin = false;

                                // Give the channel server some info from this server
                                var channelPacket = new Packet(ISServerMessages.PlayerChangeServerData);
                                channelPacket.WriteInt(charid);
                                channelPacket.WriteBytes(packet.ReadLeftoverBytes());

                                if (Server.ChannelID == channel &&
                                    Server.InMaintenance)
                                {
                                    // Server in maintenance...
                                    ls.TransferConnection?.SendPacket(channelPacket);
                                }
                                else
                                {
                                    // Changing channels, meh
                                    ls.Connection?.SendPacket(channelPacket);
                                }
                            }

                            SendPacket(pw);
                            break;
                        }

                        case ISClientMessages.ServerRegisterUnregisterPlayer: // Register/unregister character
                        {
                            var charid = packet.ReadInt();
                            var add = packet.ReadBool();
                            if (add)
                            {
                                var charname = packet.ReadString();
                                var job = packet.ReadShort();
                                var level = packet.ReadByte();
                                var gmLevel = packet.ReadByte();

                                var character = CenterServer.Instance.AddCharacter(charname, charid, Server.ChannelID, job, level, gmLevel);

                                if (Party.Parties.TryGetValue(character.PartyID, out var party))
                                {
                                    party.SilentUpdate(character.ID);
                                }
                                else if (character.PartyID != 0)
                                {
                                    Program.MainForm.LogAppend("Trying to register a character, but the party was not found??? PartyID: {0}, character ID {1}", character.PartyID, charid);
                                    character.PartyID = 0;
                                }

                                var friendsList = character.FriendsList;
                                if (friendsList != null)
                                {
                                    friendsList.OnOnlineCC(true, false);
                                    friendsList.SendBuddyList(FriendResReq.FriendRes_LoadFriend_Done);
                                    friendsList.SendNextAwaitingRequest();
                                }
                            }
                            else
                            {
                                var ccing = packet.ReadBool();
                                var character = CenterServer.Instance.FindCharacter(charid);
                                if (ccing == false)
                                {
                                    character.IsOnline = false;
                                    character.InCashShop = false;

                                    if (Party.Parties.TryGetValue(character.PartyID, out var party))
                                    {
                                        if (party.leader.CharacterID == charid)
                                        {
                                            // Disband the party
                                            party.Leave(character);
                                        }
                                        else
                                        {
                                            party.SilentUpdate(character.ID);
                                        }
                                    }

                                    character.FriendsList?.OnOnlineCC(true, true);
                                    
                                    // Fix this. When you log back in, the chat has 2 of you.
                                    // Messenger.LeaveMessenger(character.ID);
                                }

                                if (Party.Invites.ContainsKey(character.ID)) Party.Invites.Remove(character.ID);
                            }

                            SendUserNoUpdateToLogins();

                            break;
                        }


                        case ISClientMessages.BroadcastPacketToGameservers:
                        {
                            var p = new Packet(packet.ReadLeftoverBytes());
                            World.SendPacketToEveryGameserver(p);
                            break;
                        }

                        case ISClientMessages.BroadcastPacketToShopservers:
                        {
                            var p = new Packet(packet.ReadLeftoverBytes());
                            World.SendPacketToEveryShopserver(p);
                            break;
                        }
                        case ISClientMessages.BroadcastPacketToAllServers:
                        {
                            var p = new Packet(packet.ReadLeftoverBytes());
                            CenterServer.Instance.LocalServers.Values.Where(x => x.Connected).ForEach(x => x.Connection?.SendPacket(p));
                            break;
                        }

                        case ISClientMessages.RenamePlayer:
                        {
                            var hash = packet.ReadString();
                            var characterID = packet.ReadInt();
                            var newName = packet.ReadString();

                            _log.Info($"Trying to rename charid {characterID} to {newName}");

                            var chr = CenterServer.Instance.FindCharacter(characterID, false);

                            var nameCheckResult = NameCheck.Check(newName);

                            var p = new Packet(ISServerMessages.PlayerRenamed);
                            p.WriteString(hash);
                            p.WriteInt(characterID);
                            p.WriteString(newName);

                            bool doUpdate()
                            {
                                // Check if name is available
                                if (nameCheckResult != NameCheck.Result.OK)
                                {
                                    _log.Error($"Validation of name failed: {nameCheckResult} for {newName}");
                                    return false;
                                }
                                
                                if (chr == null)
                                {
                                    _log.Error("Character not online when changing name.");
                                    return false;
                                }
                                
                                if (CharacterDBAccessor.CheckDuplicateID(newName))
                                {
                                    _log.Error($"Name already exists: {newName}");
                                    return false;
                                }

                                // Okay, its available, lets change stuff...

                                chr.Name = newName;
                                // Buddylist
                                chr.FriendsList.OwnerRenamed(chr.Name);

                                var party = chr.Party;
                                party?.ForAllMembers(x =>
                                {
                                    if (x.CharacterID == characterID)
                                    {
                                        x.CharacterName = chr.Name;
                                        party.SilentUpdate(characterID);
                                    }
                                });

                                // Messenger is too complicated

                                // Some last DB things...
                                if (!CharacterDBAccessor.RenameCharacter(characterID, chr.Name))
                                {
                                    _log.Error("Unable to update database while renaming character!");
                                    return false;
                                }

                                _log.Info("Character renamed!");

                                return true;
                            }

                            p.WriteBool(doUpdate());
                            SendPacket(p);

                            break;
                        }

                        default:
                            switch (Server.Type)
                            {
                                case LocalServerType.Game:
                                    HandleGamePacket(opcode, packet);
                                    break;
                                case LocalServerType.Login:
                                    HandleLoginPacket(opcode, packet);
                                    break;
                                case LocalServerType.Shop:
                                    HandleShopPacket(opcode, packet);
                                    break;
                            }

                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error("Exception while handling LocalConnection packet", ex);
            }
            finally
            {
                ptt.EndMeasurement((byte) opcode);
            }
        }

        private void HandleLoginPacket(ISClientMessages opcode, Packet packet)
        {
            switch (opcode)
            {
                case ISClientMessages.PlayerRequestWorldLoad:
                {
                    var hash = packet.ReadString();
                    var world = packet.ReadByte();

                    var pw = new Packet(ISServerMessages.PlayerRequestWorldLoadResult);
                    pw.WriteString(hash);

                    if (World.ID == world)
                    {
                        World.AddWarning(pw);
                    }
                    else
                    {
                        pw.WriteByte(WorldLoadState.Full);
                    }

                    SendPacket(pw);
                    break;
                }
                case ISClientMessages.PlayerRequestChannelStatus: // channel online check
                {
                    var hash = packet.ReadString();
                    var world = packet.ReadByte();
                    var channel = packet.ReadByte();
                    var accountId = packet.ReadInt();

                    var pw = new Packet(ISServerMessages.PlayerRequestChannelStatusResult);
                    pw.WriteString(hash);

                    if (World.ID != world ||
                        World.GameServers.TryGetValue(channel, out var ls) == false ||
                        ls.InMaintenance ||
                        !ls.Connected)
                    {
                        pw.WriteByte(LoginResCode.Unknown); // Channel Offline
                    }
                    else
                    {
                        pw.WriteByte(LoginResCode.SuccessChannelSelect);
                        pw.WriteByte(channel);

                        try
                        {
                            var ids = CharacterDBAccessor.GetCharacterIdList(accountId).ToArray();
                            pw.WriteByte((byte) ids.Length);

                            foreach (var id in ids)
                            {
                                var ad = CharacterDBAccessor.LoadAvatar(id);
                                var ranking = CharacterDBAccessor.LoadRank(id);

                                ad.Encode(pw);
                                pw.WriteBool(ranking != null);
                                if (ranking != null)
                                {
                                    var (worldRank, worldRankMove, jobRank, jobRankMove) = ranking.Value;
                                    pw.WriteInt(worldRank);
                                    pw.WriteInt(worldRankMove);
                                    pw.WriteInt(jobRank);
                                    pw.WriteInt(jobRankMove);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Program.MainForm.LogAppend("Error while building packet for characterselect! {0}", ex);
                            _log.Error(ex);
                            // Rebuilding packet here because we cannot be sure the packet contents are OK
                            pw = new Packet(ISServerMessages.PlayerRequestChannelStatusResult);
                            pw.WriteString(hash);
                            pw.WriteByte(LoginResCode.Unknown);
                        }
                    }

                    SendPacket(pw);
                    break;
                }

                case ISClientMessages.PlayerDeleteCharacter:
                {
                    var hash = packet.ReadString();
                    var accountId = packet.ReadInt();
                    var charId = packet.ReadInt();

                    var p = new Packet(ISServerMessages.PlayerDeleteCharacterResult);
                    p.WriteString(hash);
                    p.WriteInt(charId);
                    var foundChar = CenterServer.Instance.FindCharacter(charId, false);
                    if (foundChar != null && foundChar.IsOnline)
                    {
                        _log.Error($"Trying to delete character {foundChar.Name} that is still online!");
                        p.WriteByte(LoginResCode.AlreadyOnline);
                        SendPacket(p);
                        break;
                    }


                    try
                    {
                        var deleteCharacterResult = CharacterDBAccessor.DeleteCharacter(accountId, charId);
                        p.WriteByte(deleteCharacterResult);

                        if (deleteCharacterResult == LoginResCode.SuccessChannelSelect)
                        {
                            DoLeaveGuild(charId);

                            if (foundChar != null)
                            {
                                if (foundChar.PartyID != 0 &&
                                    Party.Parties.TryGetValue(foundChar.PartyID, out var party))
                                {
                                    party.Leave(foundChar);
                                }

                                if (foundChar.Messenger != null)
                                {
                                    foundChar.Messenger.RemoveCharacter(foundChar);
                                }
                                
                                // Registered, so get rid of it
                                CenterServer.Instance.CharacterStore.Remove(foundChar);
                            }

                            // Delete from buddies...

                            foreach (var chr in CenterServer.Instance.CharacterStore.Where(chr => chr.FriendsList.HasBuddy(charId)))
                            {
                                chr.FriendsList.RemoveBuddy(charId);
                            }


                            // Delete also from offline buddies

                            BuddyList.ClearDatabaseFromCharacterID(charId);

                            // Get rid of the old buddylist data
                            BuddyList.Remove(charId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex);
                        Program.MainForm.LogAppend("Error while deleting character! {0}", ex);
                        p.WriteByte(LoginResCode.Timeout);
                    }

                    SendPacket(p);
                    break;
                }

                case ISClientMessages.PlayerCreateCharacterNamecheck:
                {
                    var hash = packet.ReadString();
                    var charname = packet.ReadString();

                    var p = new Packet(ISServerMessages.PlayerCreateCharacterNamecheckResult);
                    p.WriteString(hash);
                    p.WriteString(charname);
                    try
                    {
                        p.WriteBool(CharacterDBAccessor.CheckDuplicateID(charname));
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex);
                        Program.MainForm.LogAppend("Error while checking for duplicate ID! {0}", ex);
                        p.WriteBool(true);
                    }

                    SendPacket(p);
                    break;
                }

                case ISClientMessages.PlayerCreateCharacter:
                {
                    var hash = packet.ReadString();
                    var accountId = packet.ReadInt();
                    var gender = packet.ReadByte();

                    var charname = packet.ReadString();

                    var face = packet.ReadInt();
                    var hair = packet.ReadInt();
                    var haircolor = packet.ReadInt();
                    var skin = packet.ReadInt();

                    var top = packet.ReadInt();
                    var bottom = packet.ReadInt();
                    var shoes = packet.ReadInt();
                    var weapon = packet.ReadInt();

                    var str = packet.ReadByte();
                    var dex = packet.ReadByte();
                    var intt = packet.ReadByte();
                    var luk = packet.ReadByte();

                    var p = new Packet(ISServerMessages.PlayerCreateCharacterResult);
                    p.WriteString(hash);

                    try
                    {
                        if (CharacterDBAccessor.CheckDuplicateID(charname))
                        {
                            p.WriteBool(false);
                        }
                        else
                        {
                            var id = CharacterDBAccessor.CreateNewCharacter(
                                accountId,
                                charname,
                                gender,
                                face, hair, haircolor, skin,
                                str, dex, intt, luk,
                                top, bottom, shoes, weapon
                            );

                            var ad = CharacterDBAccessor.LoadAvatar(id);

                            p.WriteBool(true);

                            ad.Encode(p);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex);
                        Program.MainForm.LogAppend("Error while creating character! {0}", ex);

                        p = new Packet(ISServerMessages.PlayerCreateCharacterResult);
                        p.WriteString(hash);
                        p.WriteBool(false);
                    }

                    SendPacket(p);

                    break;
                }
            }
        }

        private void DoLeaveGuild(int characterId)
        {
            if (!GuildData.LeaveGuild(characterId)) return;

            var charName = GetCharacterName(characterId);
            if (charName == null)
            {
                guildLog.Error($"DoLeaveGuild({characterId}) unable to resolve character name??");
            }
            else
            {
                guildLog.Info($"{charName} left their guild");
            }

            var pw = new Packet(ISServerMessages.GuildLeavePlayer);
            pw.WriteInt(characterId);
            pw.WriteBool(false);
            BroadcastGuildPacket(pw);
        }

        private void HandleGuildPacket(ISClientMessages opcode, Packet packet)
        {
            void HandleGuildJoinPlayer(int guildId, GuildCharacter c)
            {
                GuildData.JoinGuild(c.CharacterID, guildId, c.Rank);
                // For tracking purposes
                guildLog.Info($"{c.CharacterName} joined guild id {guildId}");

                c.Online = CenterServer.Instance.FindCharacter(c.CharacterID) != null;

                var pw = new Packet(ISServerMessages.GuildJoinPlayer);
                pw.WriteInt(guildId);
                c.Encode(pw);
                World.SendPacketToEveryGameserver(pw);
            }

            switch (opcode)
            {
                
                #region Guild

                case ISClientMessages.GuildKickPlayer:
                {
                    var kicker = packet.ReadInt();
                    var toKick = packet.ReadInt();

                    if (!GuildData.LeaveGuild(toKick))
                    {
                        guildLog.Error($"Unable to kick {toKick} from guild, no guild info recorded for user? Possibly already kicked");
                        break;
                    }

                    var kickerCharName = GetCharacterName(kicker);
                    var kickedCharName = GetCharacterName(toKick);
                    if (kickedCharName == null)
                    {
                        guildLog.Error($"Unable to resolve charname for charid {toKick} in Kick function");
                        break;
                    }
                    
                    if (kickerCharName == null)
                    {
                        guildLog.Error($"Unable to resolve charname for charid {kicker} in Kick function");
                        break;
                    }

                    guildLog.Info($"{kickedCharName} was kicked by {kickerCharName} from their guild");
                    

                    var pw = new Packet(ISServerMessages.GuildLeavePlayer);
                    pw.WriteInt(toKick);
                    pw.WriteBool(true);
                    BroadcastGuildPacket(pw);

                    if (!CenterServer.Instance.IsOnline(toKick))
                    {
                        guildLog.Info("Sending note to user because he got kicked but he's not online.");
                        CenterServer.Instance.CharacterDatabase.SendNoteToUser(
                            "Admin",
                            toKick,
                            "You have been kicked from the guild."
                        );
                    }

                    break;
                }

                case ISClientMessages.GuildLeavePlayer:
                {
                    var characterId = packet.ReadInt();
                    DoLeaveGuild(characterId);

                    break;
                }

                case ISClientMessages.GuildRankUpdate:
                {
                    var updaterId = packet.ReadInt();
                    var guildId = packet.ReadInt();
                    var gc = new GuildCharacter(packet);
                    var newRank = packet.ReadByte<GuildCharacter.Ranks>();
                    gc.Rank = newRank;

                    if (!GuildData.ChangeRank(gc.CharacterID, newRank))
                    {
                        guildLog.Info($"Unable to update {gc.CharacterName}'s rank...?");
                        return;
                    }
                    
                    guildLog.Info($"{gc.CharacterName}'s rank has been changed to {newRank} by {GetCharacterName(updaterId)}");

                    var pw = new Packet(ISServerMessages.GuildUpdatePlayer);
                    pw.WriteInt(guildId);
                    gc.Encode(pw);
                    BroadcastGuildPacket(pw);
                    break;
                }

                case ISClientMessages.GuildJoinPlayer:
                {
                    var guildId = packet.ReadInt();
                    var gc = new GuildCharacter(packet);

                    var chr = CenterServer.Instance.FindCharacter(gc.CharacterID);
                    if (chr == null) return;
                    
                    HandleGuildJoinPlayer(guildId, gc);
                    break;
                }

                case ISClientMessages.GuildUpdatePlayer:
                {
                    // Forward this to all channels
                    var pw = new Packet(ISServerMessages.GuildUpdatePlayer);
                    pw.WriteBytes(packet.ReadLeftoverBytes());

                    BroadcastGuildPacket(pw);

                    break;
                }

                case ISClientMessages.GuildResize:
                {
                    var guildId = packet.ReadInt();
                    var byCharacterID = packet.ReadInt();
                    var newSize = packet.ReadByte();

                    guildLog.Info($"Guild {guildId} was resized to {newSize}, by character {byCharacterID}");
                    
                    if (!GuildData.ResizeGuild(guildId, newSize))
                    {
                        guildLog.Error("Disbanding guild failed. (No records update)");
                        return;
                    }

                    var pw = new Packet(ISServerMessages.GuildResized);
                    pw.WriteInt(guildId);
                    pw.WriteByte(newSize);
                    BroadcastGuildPacket(pw);

                    break;
                }

                case ISClientMessages.GuildDisband:
                {
                    var guildId = packet.ReadInt();
                    var byCharacterID = packet.ReadInt();
                    guildLog.Info($"Guild {guildId} was disbanded, by character {byCharacterID}");
                    
                    if (!GuildData.DisbandGuild(guildId))
                    {
                        guildLog.Error("Disbanding guild failed. (No records update)");
                        return;
                    }

                    var pw = new Packet(ISServerMessages.GuildDisbanded);
                    pw.WriteInt(guildId);
                    BroadcastGuildPacket(pw);

                    break;
                }

                case ISClientMessages.GuildCreate:
                {
                    var guildMasterId = packet.ReadInt();
                    var name = packet.ReadString();

                    var chr = CenterServer.Instance.FindCharacter(guildMasterId);
                    guildLog.Info($"{chr.Name} created guild {name}");

                    var members = new List<GuildCharacter>();

                    var memberCount = packet.ReadByte();
                    for (var i = 0; i < memberCount; i++)
                    {
                        var gCharacter = new GuildCharacter
                        {
                            CharacterID = packet.ReadInt(),
                            CharacterName = packet.ReadString(),
                            Job = packet.ReadShort(),
                            Level = packet.ReadByte(),
                            Online = true,
                        };

                        gCharacter.Rank = guildMasterId == gCharacter.CharacterID ? GuildCharacter.Ranks.Master : GuildCharacter.Ranks.Member;

                        // We'll be adding them later
                        members.Add(gCharacter);
                    }

                    var guildId = GuildData.CreateGuild(guildMasterId, name);

                    // NOTE: No members here (yet)
                    SendGuild(guildId);

                    members.ForEach(gc => HandleGuildJoinPlayer(guildId, gc));

                    break;
                }

                case ISClientMessages.GuildRename:
                {
                    var guildId = packet.ReadInt();
                    var byCharacterID = packet.ReadInt();
                    var newName = packet.ReadString();

                    guildLog.Info($"Guild {guildId} was renamed to {newName}, by character {byCharacterID}");

                    if (!GuildData.RenameGuild(guildId, newName))
                    {
                        guildLog.Error("Updating guild name failed. (No records update)");
                        return;
                    }

                    var pw = new Packet(ISServerMessages.GuildRename);
                    pw.WriteInt(guildId);
                    pw.WriteString(newName);
                    BroadcastGuildPacket(pw);

                    break;
                }

                case ISClientMessages.GuildChangeLogo:
                {
                    var guildId = packet.ReadInt();
                    var byCharacterID = packet.ReadInt();
                    var logo = new GuildLogo(packet);

                    guildLog.Info($"Guild {guildId} changed logo to {logo.Background}, {logo.BackgroundColor}, {logo.Foreground}, {logo.ForegroundColor}, by character {byCharacterID}");

                    if (!GuildData.ChangeGuildLogo(guildId, logo))
                    {
                        guildLog.Error("Updating guild logo failed. (No records update)");
                        return;
                    }

                    var pw = new Packet(ISServerMessages.GuildChangeLogo);
                    pw.WriteInt(guildId);
                    logo.Encode(pw);
                    BroadcastGuildPacket(pw);

                    break;
                }

                #endregion
            }
        }
        
        string GetCharacterName(int characterID)
        {
            return CenterServer.Instance.FindCharacter(characterID)?.Name ?? 
                   CenterServer.Instance.CharacterDatabase.CharacterNameById(characterID);
        }

        private void HandleGamePacket(ISClientMessages opcode, Packet packet)
        {
            switch (opcode)
            {
                #region Messenger

                case ISClientMessages.MessengerJoin:
                    Messenger.JoinMessenger(packet);
                    break;

                case ISClientMessages.MessengerLeave:
                    Messenger.LeaveMessenger(packet.ReadInt());
                    break;

                case ISClientMessages.MessengerInvite:
                    Messenger.SendInvite(packet.ReadInt(), packet.ReadString());
                    break;
                case ISClientMessages.MessengerBlocked:
                    Messenger.Block(packet);
                    break;
                case ISClientMessages.MessengerAvatar:
                    Messenger.OnAvatar(packet);
                    break;
                case ISClientMessages.MessengerChat:
                    Messenger.Chat(packet.ReadInt(), packet.ReadString());
                    break;

                #endregion

                #region Party

                case ISClientMessages.PartyCreate:
                {
                    var fuker = packet.ReadInt();
                    var fucker = CenterServer.Instance.FindCharacter(fuker);
                    if (fucker == null) return;
                    var doorInfo = new DoorInformation(packet);
                    Party.CreateParty(fucker, doorInfo);
                    break;
                }

                case ISClientMessages.PartyInvite:
                {
                    var fuker1 = packet.ReadInt();
                    var fuker2 = packet.ReadInt();
                    var fucker1 = CenterServer.Instance.FindCharacter(fuker1);
                    if (fucker1 != null && Party.Parties.TryGetValue(fucker1.PartyID, out var party))
                    {
                        party.Invite(fuker1, fuker2);
                    }

                    break;
                }

                case ISClientMessages.PartyAccept:
                {
                    var AcceptorID = packet.ReadInt();
                    var fucker1 = CenterServer.Instance.FindCharacter(AcceptorID);

                    if (fucker1 != null && Party.Invites.TryGetValue(AcceptorID, out var party))
                    {
                        party.TryJoin(fucker1);
                    }

                    break;
                }

                case ISClientMessages.PartyLeave:
                {
                    var LeaverID = packet.ReadInt();
                    var fucker = CenterServer.Instance.FindCharacter(LeaverID);

                    if (fucker != null && Party.Parties.TryGetValue(fucker.PartyID, out var party))
                    {
                        party.Leave(fucker);
                    }

                    break;
                }

                case ISClientMessages.PartyExpel:
                {
                    var leader = packet.ReadInt();
                    var expelledCharacter = packet.ReadInt();
                    var fucker = CenterServer.Instance.FindCharacter(leader);
                    if (fucker != null && Party.Parties.TryGetValue(fucker.PartyID, out var party))
                    {
                        party.Expel(leader, expelledCharacter);
                    }

                    break;
                }

                case ISClientMessages.PartyDecline:
                {
                    var decliner = packet.ReadInt();
                    var declinerName = packet.ReadString();
                    var chr = CenterServer.Instance.FindCharacter(decliner);
                    if (chr != null && Party.Invites.TryGetValue(decliner, out var party))
                    {
                        party.DeclineInvite(chr);
                    }

                    break;
                }

                case ISClientMessages.PartyChat:
                {
                    var chatter = packet.ReadInt();
                    var msg = packet.ReadString();
                    var chr = CenterServer.Instance.FindCharacter(chatter);
                    if (chr != null && Party.Parties.TryGetValue(chr.PartyID, out var party))
                    {
                        party.Chat(chatter, msg);
                    }

                    break;
                }

                case ISClientMessages.GuildChat:
                {
                    var guild = packet.ReadInt();
                    var chatter = packet.ReadInt();
                    var msg = packet.ReadString();

                    var chr = CenterServer.Instance.FindCharacter(chatter);
                    chr.WrappedLogging(() =>
                    {
                        guildChatLog.Info($"{chr.Name}: {msg}");
                    });

                    var outPacket = new Packet(ISServerMessages.GuildChat);
                    outPacket.WriteInt(guild);
                    outPacket.WriteInt(chatter);
                    outPacket.WriteString(msg);
                    CenterServer.Instance.World.SendPacketToEveryGameserver(outPacket);

                    break;
                }

                case ISClientMessages.GuildReload:
                {
                    SendGuilds();
                    break;
                }

                case ISClientMessages.PlayerUpdateMap:
                {
                    var id = packet.ReadInt();
                    var map = packet.ReadInt();
                    var fucker = CenterServer.Instance.FindCharacter(id);

                    if (fucker != null)
                    {
                        fucker.MapID = map;
                        if (Party.Parties.TryGetValue(fucker.PartyID, out var party))
                        {
                            party.SilentUpdate(id);
                        }
                    }

                    break;
                }

                case ISClientMessages.PartyDoorChanged:
                {
                    var chrid = packet.ReadInt();
                    var door = new DoorInformation(packet);

                    var chr = CenterServer.Instance.FindCharacter(chrid);
                    if (chr != null && Party.Parties.TryGetValue(chr.PartyID, out var party))
                    {
                        party.UpdateDoor(door, chrid);
                    }

                    break;
                }

                #endregion

                #region Buddy

                case ISClientMessages.BuddyInvite:
                    BuddyList.HandleBuddyInvite(packet);
                    break;
                case ISClientMessages.BuddyUpdate:
                {
                    var id = packet.ReadInt();
                    var name = packet.ReadString();
                    var toUpdate = CenterServer.Instance.FindCharacter(id);
                    toUpdate.FriendsList.OnOnlineCC(true, false);
                    break;
                }
                case ISClientMessages.BuddyInviteAnswer:
                {
                    var id = packet.ReadInt();
                    var name = packet.ReadString();
                    var toAccept = CenterServer.Instance.FindCharacter(id);
                    toAccept.FriendsList.AcceptRequest();
                    break;
                }
                case ISClientMessages.BuddyListExpand:
                {
                    var chr = CenterServer.Instance.FindCharacter(packet.ReadInt());
                    chr?.FriendsList.IncreaseCapacity(packet.ReadByte());
                    break;
                }
                case ISClientMessages.BuddyChat:
                {
                    var fWho = packet.ReadInt();
                    var Who = packet.ReadString();
                    var what = packet.ReadString();
                    int recipientCount = packet.ReadByte();
                    var recipients = new int[recipientCount];
                    for (var i = 0; i < recipientCount; i++) recipients[i] = packet.ReadInt();

                    var pWho = CenterServer.Instance.FindCharacter(fWho);

                    pWho?.FriendsList.BuddyChat(what, recipients);
                    break;
                }
                case ISClientMessages.BuddyDeclineOrDelete:
                {
                    var Who = CenterServer.Instance.FindCharacter(packet.ReadInt());
                    var victimId = packet.ReadInt();
                    Who?.FriendsList.RemoveBuddyOrRequest(victimId);
                    break;
                }

                #endregion


                case ISClientMessages.PlayerUsingSuperMegaphone:
                {
                    var pw = new Packet(ISServerMessages.PlayerSuperMegaphone);
                    pw.WriteString(packet.ReadString());
                    pw.WriteBool(packet.ReadBool());
                    pw.WriteByte(packet.ReadByte());
                    World.SendPacketToEveryGameserver(pw);
                    break;
                }

                case ISClientMessages.PlayerWhisperOrFindOperation: // WhisperOrFind
                {
                    var sender = packet.ReadInt();
                    var senderChar = CenterServer.Instance.FindCharacter(sender);
                    if (senderChar == null)
                        return;

                    var whisper = packet.ReadBool();
                    var receiver = packet.ReadString();
                    var receiverChar = CenterServer.Instance.FindCharacter(receiver);

                    if (whisper)
                    {
                        var message = packet.ReadString();
                        if ((receiverChar == null ||
                             !World.GameServers.ContainsKey(receiverChar.ChannelID)) ||
                            (receiverChar.IsGM && !senderChar.IsGM))
                        {
                            var pw = new Packet(ISServerMessages.PlayerWhisperOrFindOperationResult);
                            pw.WriteBool(true); // Whisper
                            pw.WriteBool(false); // Not found.
                            pw.WriteInt(sender);
                            pw.WriteString(receiver);
                            SendPacket(pw);
                        }
                        else
                        {
                            var pw = new Packet(ISServerMessages.PlayerWhisperOrFindOperationResult);
                            pw.WriteBool(false); // Find
                            pw.WriteBool(true); // Found.
                            pw.WriteInt(sender);
                            pw.WriteString(receiver);
                            pw.WriteSByte(-1);
                            pw.WriteSByte(-1);
                            SendPacket(pw);

                            pw = new Packet(ISServerMessages.PlayerWhisperOrFindOperationResult);
                            pw.WriteBool(true); // Whisper
                            pw.WriteBool(true); // Found.
                            pw.WriteInt(receiverChar.ID);
                            pw.WriteString(senderChar.Name);
                            pw.WriteByte(senderChar.ChannelID);
                            pw.WriteString(message);
                            pw.WriteBool(false); // false is '>>'
                            var victimChannel = World.GameServers[receiverChar.ChannelID];
                            victimChannel.Connection.SendPacket(pw);
                        }
                    }
                    else
                    {
                        if (receiverChar == null ||
                            !World.GameServers.ContainsKey(receiverChar.ChannelID) ||
                            (receiverChar.IsGM && !senderChar.IsGM))
                        {
                            var pw = new Packet(ISServerMessages.PlayerWhisperOrFindOperationResult);
                            pw.WriteBool(false); // Find
                            pw.WriteBool(false); // Not found.
                            pw.WriteInt(sender);
                            pw.WriteString(receiver);
                            SendPacket(pw);
                        }
                        else
                        {
                            var pw = new Packet(ISServerMessages.PlayerWhisperOrFindOperationResult);
                            pw.WriteBool(false); // Find
                            pw.WriteBool(true); // Found.
                            pw.WriteInt(senderChar.ID);
                            pw.WriteString(receiverChar.Name);
                            // Cashshop
                            if (receiverChar.InCashShop)
                                pw.WriteSByte(-2);
                            else
                                pw.WriteByte(receiverChar.ChannelID);
                            pw.WriteSByte(0);
                            SendPacket(pw);
                        }
                    }

                    break;
                }

                case ISClientMessages.UpdatePlayerJobLevel:
                {
                    var charId = packet.ReadInt();
                    var character = CenterServer.Instance.FindCharacter(charId);
                    if (character == null)
                        return;

                    character.Job = packet.ReadShort();
                    character.Level = packet.ReadByte();
                    break;
                }


                case ISClientMessages.AdminMessage:
                {
                    var pw = new Packet(ISServerMessages.AdminMessage);
                    pw.WriteString(packet.ReadString());
                    pw.WriteByte(packet.ReadByte());
                    World.SendPacketToEveryGameserver(pw);
                    break;
                }

                case ISClientMessages.KickPlayer:
                {
                    var userId = packet.ReadInt();
                    Program.MainForm.LogAppend("Globally kicking user " + userId);
                    var pw = new Packet(ISServerMessages.KickPlayerResult);
                    pw.WriteInt(userId);
                    World.SendPacketToEveryGameserver(pw);
                    World.SendPacketToEveryShopserver(pw);
                    break;
                }

                case ISClientMessages.ReloadEvents:
                    CenterServer.Instance.ReloadEvents();
                    break;

                default:
                    HandleGuildPacket(opcode, packet);
                    break;
            }
        }

        private void HandleShopPacket(ISClientMessages opcode, Packet packet)
        {
            switch (opcode)
            {
                case ISClientMessages.PlayerQuitCashShop: // CC back to channel from cashserver
                {
                    var hash = packet.ReadString();
                    var charid = packet.ReadInt();
                    var world = packet.ReadByte();
                    var chr = CenterServer.Instance.FindCharacter(charid);
                    if (chr == null) return;

                    var pw = new Packet(ISServerMessages.PlayerChangeServerResult);
                    pw.WriteString(hash);
                    pw.WriteInt(charid);

                    if (World.ID == world &&
                        World.GameServers.TryGetValue(chr.LastChannel, out var ls))
                    {
                        pw.WriteBytes(ls.PublicIP.GetAddressBytes());
                        pw.WriteUShort(ls.Port);

                        RedisBackend.Instance.SetMigratingPlayer(charid);

                        chr.InCashShop = false;
                        chr.isCCing = true;
                        chr.LastChannel = 0;

                        // Give the channel server some info from this server
                        var channelPacket = new Packet(ISServerMessages.PlayerChangeServerData);
                        channelPacket.WriteInt(charid);
                        channelPacket.WriteBytes(packet.ReadLeftoverBytes());

                        if (Server.InMaintenance)
                        {
                            // Server in maintenance...
                            ls.TransferConnection?.SendPacket(channelPacket);
                        }
                        else
                        {
                            // Changing channels, meh
                            ls.Connection?.SendPacket(channelPacket);
                        }
                    }
                    else
                    {
                        pw.WriteInt(0);
                        pw.WriteShort(0);
                    }

                    SendPacket(pw);

                    break;
                }

                default:
                    HandleGuildPacket(opcode, packet);
                    break;
            }
        }
    }
}