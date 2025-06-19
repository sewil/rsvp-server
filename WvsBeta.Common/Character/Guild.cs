using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Common
{
    public class GuildLogo
    {
        public short Background, Foreground;
        public byte BackgroundColor, ForegroundColor;

        public GuildLogo() { }

        public GuildLogo(Packet packet)
        {
            Background = packet.ReadShort();
            BackgroundColor = packet.ReadByte();
            Foreground = packet.ReadShort();
            ForegroundColor = packet.ReadByte();
        }

        public void Encode(Packet packet)
        {
            packet.WriteShort(Background);
            packet.WriteByte(BackgroundColor);
            packet.WriteShort(Foreground);
            packet.WriteByte(ForegroundColor);
        }

    }

    public class Guild
    {
        public int ID { get; set; }
        public string Name { get; set; }

        public GuildLogo Logo { get; set; }

        public byte Capacity;

        public List<GuildCharacter> Characters { get; } = new List<GuildCharacter>();

        public GuildCharacter GetCharacter(int characterID) =>
            Characters.FirstOrDefault(x => x.CharacterID == characterID);

        /// <summary>
        /// Gets the (first) GuildMaster. If no GuildMaster exists, this will return 0.
        /// </summary>
        public int GuildMaster => Characters.FirstOrDefault(x => x.Rank == GuildCharacter.Ranks.Master)?.CharacterID ?? 0;

        public void RemoveCharacter(int characterId) => Characters.RemoveAll(x => x.CharacterID == characterId);

        public Guild() { }

        public Guild(Packet packet)
        {
            ID = packet.ReadInt();
            Name = packet.ReadString();
            Logo = new GuildLogo(packet);
            Capacity = packet.ReadByte();

            var amount = packet.ReadShort();
            for (var i = 0; i < amount; i++)
            {
                Characters.Add(new GuildCharacter(packet));
            }
        }

        public void Encode(Packet packet)
        {
            packet.WriteInt(ID);
            packet.WriteString(Name);
            Logo.Encode(packet);
            packet.WriteByte(Capacity);

            packet.WriteShort((short)Characters.Count);
            Characters.ForEach(x => x.Encode(packet));
        }
        
        public void RequestDisband(AbstractConnection centerConnection, int byCharacterID)
        {
            var pw = new Packet(ISClientMessages.GuildDisband);
            pw.WriteInt(ID);
            pw.WriteInt(byCharacterID);
            centerConnection?.SendPacket(pw);
        }
        
        public void RequestResize(AbstractConnection centerConnection, int byCharacterID, byte newSize)
        {
            var pw = new Packet(ISClientMessages.GuildResize);
            pw.WriteInt(ID);
            pw.WriteInt(byCharacterID);
            pw.WriteByte(newSize);

            centerConnection?.SendPacket(pw);
        }

        public void RequestRename(AbstractConnection centerConnection, int byCharacterID, string newName)
        {
            var pw = new Packet(ISClientMessages.GuildRename);
            pw.WriteInt(ID);
            pw.WriteInt(byCharacterID);
            pw.WriteString(newName);
            
            centerConnection?.SendPacket(pw);
        }
        
        public void RequestLogoChange(AbstractConnection centerConnection, int byCharacterID, GuildLogo logo)
        {
            var pw = new Packet(ISClientMessages.GuildChangeLogo);
            pw.WriteInt(ID);
            pw.WriteInt(byCharacterID);
            logo.Encode(pw);

            centerConnection?.SendPacket(pw);
        }

        public void RequestDemoteGuildMaster(AbstractConnection centerConnection, GuildCharacter oldMaster, GuildCharacter newMaster)
        {
            var pw = new Packet(ISClientMessages.GuildDemoteGuildMaster);
            pw.WriteInt(ID);
            oldMaster.Encode(pw);
            newMaster.Encode(pw);
            centerConnection?.SendPacket(pw);
        }

        public bool HasGuildMark => Logo != null && (Logo.Background != 0 || Logo.BackgroundColor != 0 || Logo.Foreground != 0 || Logo.ForegroundColor != 0);

        public override string ToString() => Name + " (" + ID + ")";
    }

    public class GuildCharacter
    {
        public int CharacterID;
        public string CharacterName;
        public short Job;
        public byte Level;
        public bool Online;
        public Ranks Rank;
        public enum Ranks
        {
            Member = 1,
            JrMaster = 2,
            Master = 3,
        }

        public GuildCharacter() { }

        public GuildCharacter(Packet p)
        {
            Decode(p);
        }

        public void Decode(Packet p)
        {
            CharacterID = p.ReadInt();
            CharacterName = p.ReadString();
            Job = p.ReadShort();
            Level = p.ReadByte();
            Online = p.ReadBool();
            Rank = p.ReadByte<Ranks>();
        }

        public void Encode(Packet p)
        {
            p.WriteInt(CharacterID);
            p.WriteString(CharacterName);
            p.WriteShort(Job);
            p.WriteByte(Level);
            p.WriteBool(Online);
            p.WriteByte(Rank);
        }
        
        public void SendPlayerUpdateToCenter(AbstractConnection centerConnection, Guild guild)
        {
            var pw = new Packet(ISClientMessages.GuildUpdatePlayer);
            pw.WriteInt(guild.ID);
            Encode(pw);
            centerConnection?.SendPacket(pw);
        }

    }

    public class GuildManager
    {
        private static ILog _log = LogManager.GetLogger("GuildManager");
        public HashSet<Guild> Guilds { get; } = new HashSet<Guild>();
        
        public Guild GetGuild(int guildId) => Guilds.FirstOrDefault(x => x.ID == guildId);

        public Guild GetGuild(string name)
        {
            name = name.Trim();
            var guild = Guilds.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.InvariantCultureIgnoreCase));
            if (guild == null && int.TryParse(name, out var guildId))
                guild = GetGuild(guildId);

            return guild;
        }

        public Guild GetGuildForCharacterID(int characterId) => Guilds.FirstOrDefault(x => x.Characters.Exists(y => y.CharacterID == characterId));

        public (Guild, GuildCharacter) GetInfoForCharacterID(int characterId)
        {
            foreach (var guild in Guilds)
            {
                var gc = guild.Characters.FirstOrDefault(x => x.CharacterID == characterId);
                if (gc == null) continue;

                return (guild, gc);
            }

            return (null, null);
        }


        public Action<Guild> OnGuildUpdated;
        public Action<Guild, GuildCharacter> OnPlayerJoined;
        public Action<Guild, GuildCharacter> OnPlayerLeft;
        public Action<Guild, GuildCharacter> OnPlayerKicked;
        public Action<Guild, GuildCharacter> OnPlayerUpdated;
        public Action<Guild, GuildCharacter, string> OnPlayerChat;
        public Action<Guild> OnGuildDisbanded;

        public void PlayerDisconnected(AbstractConnection centerConnection, int characterId)
        {
            var (guild, gc) = GetInfoForCharacterID(characterId);
            if (guild == null) return;

            gc.Online = false;
            gc.SendPlayerUpdateToCenter(centerConnection, guild);
        }
        

        public bool HandleServerUpdate(ISServerMessages smsg, Packet packet)
        {
            switch (smsg)
            {

                case ISServerMessages.GuildResized:
                    {
                        var guildId = packet.ReadInt();
                        var guild = GetGuild(guildId);
                        
                        if (guild == null)
                        {
                            _log.Error($"{smsg}: guild not found: {guildId}");
                            return true;
                        }

                        var newSize = packet.ReadByte();
                        guild.Capacity = newSize;

                        _log.Info($"Guild capacity of {guild} updated to {guild.Capacity}");
                        return true;
                    }

                case ISServerMessages.GuildUpdate:
                    {
                        Guilds.Clear();

                        var count = packet.ReadInt();

                        for (var i = 0; i < count; i++)
                        {
                            var guild = new Guild(packet);

                            Guilds.Add(guild);

                            OnGuildUpdated?.Invoke(guild);
                        }
                        
                        _log.Info($"Loaded {count} guilds.");
                        return true;
                    }

                case ISServerMessages.GuildUpdateSingle:
                    {
                        var guild = new Guild(packet);

                        Guilds.RemoveWhere(x => x.ID == guild.ID);
                        Guilds.Add(guild);
                        
                        OnGuildUpdated?.Invoke(guild);
                        
                        return true;
                    }

                case ISServerMessages.GuildLeavePlayer:
                    {
                        var charId = packet.ReadInt();
                        var kicked = packet.ReadBool();
                        
                        var guild = GetGuildForCharacterID(charId);

                        if (guild != null)
                        {
                            var guildCharacter = guild.GetCharacter(charId);
                            if (guildCharacter == null) return true; // Not sure how this would happen but OK

                            if (kicked) OnPlayerKicked?.Invoke(guild, guildCharacter);
                            else OnPlayerLeft?.Invoke(guild, guildCharacter);
                            
                            // Now actually remove the character
                            guild.RemoveCharacter(charId);

                            _log.Info($"Charid {charId} {(kicked ? "kicked from" : "left")} guild {guild}.");
                        }
                        else
                        {
                            _log.Warn($"Unable to find guild for charid {charId}.");
                        }
                        
                        return true;
                    }

                case ISServerMessages.GuildUpdatePlayer:
                    {
                        var guildId = packet.ReadInt();
                        var guild = GetGuild(guildId);

                        if (guild == null)
                        {
                            _log.Error($"{smsg}: guild not found: {guildId}");
                            return true;
                        }
                        
                        var x = new GuildCharacter(packet);
                        // Make sure we clean up the data
                        guild.RemoveCharacter(x.CharacterID);
                        guild.Characters.Add(x);
                        
                        OnPlayerUpdated?.Invoke(guild, x);
                        
                        return true;
                    }

                case ISServerMessages.GuildDisbanded:
                    {
                        var guildId = packet.ReadInt();
                        var guild = GetGuild(guildId);
                        
                        if (guild == null)
                        {
                            _log.Error($"{smsg}: guild not found: {guildId}");
                            return true;
                        }

                        OnGuildDisbanded?.Invoke(guild);
                        
                        Guilds.Remove(guild);
                        
                        return true;
                    }

                case ISServerMessages.GuildJoinPlayer:
                    {
                        var guildId = packet.ReadInt();
                        var guild = GetGuild(guildId);

                        if (guild == null)
                        {
                            _log.Error($"{smsg}: guild not found: {guildId}");
                            return true;
                        }

                        var character = new GuildCharacter(packet);

                        guild.RemoveCharacter(character.CharacterID);
                        guild.Characters.Add(character);
                        
                        OnPlayerJoined?.Invoke(guild, character);

                        _log.Info($"{character} joined guild {guild}");
                        
                        return true;
                    }

                case ISServerMessages.GuildChat:
                    {
                        var guildId = packet.ReadInt();
                        var guild = GetGuild(guildId);

                        if (guild == null)
                        {
                            _log.Error($"{smsg}: guild not found: {guildId}");
                            return true;
                        }

                        var characterID = packet.ReadInt();
                        var text = packet.ReadString();

                        var guildCharacter = guild.GetCharacter(characterID);
                        if (guildCharacter == null) 
                        {
                            _log.Error($"{smsg}: guild character {characterID} not found in guild {guild}");
                            return true;
                        }

                        OnPlayerChat?.Invoke(guild, guildCharacter, text);
                        
                        return true;
                    }

                case ISServerMessages.GuildRename:
                    {
                        var guildId = packet.ReadInt();
                        var guild = GetGuild(guildId);

                        if (guild == null)
                        {
                            _log.Error($"{smsg}: guild not found: {guildId}");
                            return true;
                        }

                        var oldGuildName = guild.Name;

                        guild.Name = packet.ReadString();
                        OnGuildUpdated?.Invoke(guild);
                        _log.Info($"Guild {guild} updated name from {oldGuildName} to {guild.Name}");
                        
                        return true;
                    }

                case ISServerMessages.GuildChangeLogo:
                    {
                        var guildId = packet.ReadInt();
                        var guild = GetGuild(guildId);

                        if (guild == null)
                        {
                            _log.Error($"{smsg}: guild not found: {guildId}");
                            return true;
                        }

                        guild.Logo = new GuildLogo(packet);
                        OnGuildUpdated?.Invoke(guild);
                        
                        return true;
                    }
            }


            return false;
        }

    }
}