using System;
using System.Collections.Generic;
using System.Linq;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.Handlers;

namespace WvsBeta.Game.Packets
{
    public static class GuildPacket
    {
        public static void UpdatePlayer(this Guild guild, Character character, bool goingOffline = false)
        {
            var gCharacter = guild.GetCharacter(character.ID);

            if (gCharacter == null) return;

            gCharacter.Job = character.Job;
            gCharacter.Level = character.Level;
            gCharacter.Online = goingOffline ? false : true;

            gCharacter.SendPlayerUpdateToCenter(Server.Instance.CenterConnection, guild);
        }

        public static void DisbandGuild(this Guild guild, int byCharacterID)
        {
            guild.RequestDisband(Server.Instance.CenterConnection, byCharacterID);
        }

        public static void ResizeGuild(this Guild guild, int byCharacterID, byte newSize)
        {
            guild.RequestResize(Server.Instance.CenterConnection, byCharacterID, newSize);
        }

        public static void CreateGuild(string name, Character guildMaster, IEnumerable<Character> members)
        {
            var pw = new Packet(ISClientMessages.GuildCreate);
            pw.WriteInt(guildMaster.ID);
            pw.WriteString(name);

            var characters = members as Character[] ?? members.ToArray();
            pw.WriteByte((byte)characters.Length);
            characters.ForEach(character =>
            {
                pw.WriteInt(character.ID);
                pw.WriteString(character.Name);
                pw.WriteShort(character.Job);
                pw.WriteByte(character.Level);
            });

            Server.Instance.CenterConnection.SendPacket(pw);
        }

        public static void RenameGuild(this Guild guild, int byCharacterID, string name)
        {
            guild.RequestRename(Server.Instance.CenterConnection, byCharacterID, name);
        }

        public static void ChangeLogo(this Guild guild, int byCharacterID, GuildLogo logo)
        {
            guild.RequestLogoChange(Server.Instance.CenterConnection, byCharacterID, logo);
            guild.SendGuildMarkUpdatedMessage();
        }

        private static void SendGuildMarkUpdatedMessage(this Guild guild)
        {
            foreach (var guildCharacter in guild.Characters.Where(x => x.Rank >= GuildCharacter.Ranks.JrMaster))
            {
                var chr = Server.Instance.GetCharacter(guildCharacter.CharacterID);
                if (chr == null) continue;

                MessagePacket.SendPopup(chr, "It is completed.");
            }
        }

        public static bool OpenChangeLogo(this Guild guild, int price)
        {
            // Find master
            var master = Server.Instance.GetCharacter(guild.GuildMaster);
            if (master == null) return false;

            // Keep track of the price to pay.
            master.SetGuildMarkCost = -Math.Abs(price);

            // Send it the change dialog
            var pw = new Packet(CfgServerMessages.CFG_GUILD);
            pw.WriteByte(GuildHandler.GuildServerOpcodes.OpenChangeLogo);
            master.SendPacket(pw);
            return true;
        }

        public static bool DemoteGuildMaster(this Guild guild, int byCharacterID)
        {
            var oldMaster = guild.GetCharacter(guild.GuildMaster);
            var newMaster = guild.GetCharacter(byCharacterID);
            if (oldMaster == null || newMaster == null) return false;
            guild.RequestDemoteGuildMaster(Server.Instance.CenterConnection, oldMaster, newMaster);
            return true;
        }

        private static void EncodeCharacterGuildInfo(Packet pw, Guild guild, Character chr = null)
        {
            pw.WriteBool(chr?.IsGM ?? false); // rainbow
            pw.WriteString(guild?.Name ?? "");
            pw.WriteShort(guild?.Logo?.Background ?? 0);
            pw.WriteByte(guild?.Logo?.BackgroundColor ?? 0);
            pw.WriteShort(guild?.Logo?.Foreground ?? 0);
            pw.WriteByte(guild?.Logo?.ForegroundColor ?? 0);
        }

        public static void SendGuildInfoUpdate(this Guild guild, Character chr = null)
        {
            var pw = new Packet(CfgServerMessages.CFG_GUILD);
            pw.WriteByte(GuildHandler.GuildServerOpcodes.GuildInfoUpdate);
            pw.WriteBool(true); // is self update

            EncodeCharacterGuildInfo(pw, guild, chr);

            pw.WriteShort((short)guild.Characters.Count);

            foreach (var guildCharacter in guild.Characters)
            {
                guildCharacter.Encode(pw);
            }

            if (chr != null)
                chr.SendPacket(pw);
            else
                guild.BroadcastPacket(pw);
        }

        public static void SendGuildMemberInfoUpdate(this Guild guild, Character chr, Character toCharacter = null)
        {
            var pw = new Packet(CfgServerMessages.CFG_GUILD);
            pw.WriteByte(GuildHandler.GuildServerOpcodes.GuildInfoUpdate);
            pw.WriteBool(false); // is self update
            EncodeCharacterGuildInfo(pw, guild, chr);

            pw.WriteInt(chr.ID);

            if (toCharacter == null)
                chr.Field.SendPacket(chr, pw, chr);
            else
                toCharacter.SendPacket(pw);
        }

        public static void BroadcastPacket(this Guild guild, Packet p, int exceptId = -1)
        {
            guild.Characters
                .Select(gc => gc.CharacterID)
                .Where(id => id != exceptId)
                .ForEach(id => Server.Instance.GetCharacter(id)?.SendPacket(p));
        }
    }
}