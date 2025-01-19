using System;
using System.Data;
using System.Linq;
using log4net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Common.Tracking;
using WvsBeta.Game.Packets;

namespace WvsBeta.Game.Handlers
{
    public class GuildHandler
    {
        private static ILog _log = LogManager.GetLogger(typeof(GuildHandler));

        public enum GuildServerOpcodes : byte
        {
            GuildInfoUpdate,
            ReceiveInvite,
            UpdatePlayer,
            PlayerLeft,
            JoinPlayer,
            OpenChangeLogo,
        }
        
        enum GuildClientOpcodes
        {
            KickPlayer,
            LeaveGuild,
            InvitePlayer,
            InviteResult,
            ChangeRank,
            ChangeLogo,
        }
        
        public static void HandlePacket(Character character, Packet packet)
        {
            GuildCharacter GetCharacterByID(int id) => character.Guild?.Characters.FirstOrDefault(x => x.CharacterID == id);

            var guildCharacter = GetCharacterByID(character.ID);
            var guild = character.Guild;

            switch ((GuildClientOpcodes) packet.ReadByte())
            {
                case GuildClientOpcodes.KickPlayer:
                {
                    if (guildCharacter == null) return;

                    var kickId = packet.ReadInt();

                    var kickedCharacter = GetCharacterByID(kickId);
                    if (kickedCharacter == null) return;

                    if (kickedCharacter?.Rank >= guildCharacter?.Rank) return;
                    
                    var centerPacket = new Packet(ISClientMessages.GuildKickPlayer);
                    centerPacket.WriteInt(guildCharacter.CharacterID);
                    centerPacket.WriteInt(kickId);
                    Server.Instance.CenterConnection.SendPacket(centerPacket);
                    break;
                }
                case GuildClientOpcodes.LeaveGuild:
                {
                    if (guildCharacter == null) return;
                    if (guildCharacter?.Rank > GuildCharacter.Ranks.JrMaster) return;
                    
                    var centerPacket = new Packet(ISClientMessages.GuildLeavePlayer);
                    centerPacket.WriteInt(character.ID);
                    Server.Instance.CenterConnection.SendPacket(centerPacket);
                    
                    break;
                }
                case GuildClientOpcodes.ChangeRank:
                {
                    if (guildCharacter == null) return;
                    
                    // NOTE: We aren't updating the rank here _yet_ so we wait
                    // for a character update from center.

                    var victimId = packet.ReadInt();
                    var up = packet.ReadBool();
                    
                    var victim = GetCharacterByID(victimId);
                    if (victim == null) return;
                    
                    if (victim.Rank >= guildCharacter.Rank) return;
                    if (!up && victim.Rank <= GuildCharacter.Ranks.Member) return;
                    if (up && victim.Rank >= GuildCharacter.Ranks.JrMaster) return;

                    var centerPacket = new Packet(ISClientMessages.GuildRankUpdate);
                    centerPacket.WriteInt(guildCharacter.CharacterID);
                    centerPacket.WriteInt(character.Guild.ID);
                    victim.Encode(centerPacket);
                    centerPacket.WriteByte(victim.Rank + (up ? 1 : -1));

                    Server.Instance.CenterConnection.SendPacket(centerPacket);
                    
                    break;
                }
                case GuildClientOpcodes.InvitePlayer:
                {
                    if (guildCharacter == null) return;
                    
                    if (guildCharacter.Rank < GuildCharacter.Ranks.JrMaster) return;

                    if (character.Guild.Characters.Count >= character.Guild.Capacity)
                    {                            
                        MessagePacket.SendText(MessagePacket.MessageTypes.RedText, "Can't invite player because the guild is full.", character, MessagePacket.MessageMode.ToPlayer);
                        return;
                    }
                    
                    var other = packet.ReadString();
                    
                    var otherChar = Server.Instance.GetCharacter(other);
                    if (otherChar != null)
                    {
                        if (otherChar.Guild != null)
                        {
                            MessagePacket.SendText(MessagePacket.MessageTypes.RedText, "Character is already in a guild.", character, MessagePacket.MessageMode.ToPlayer);
                            return;
                        }

                        if (otherChar.GuildInvite != null)
                        {
                            MessagePacket.SendText(MessagePacket.MessageTypes.RedText, "Character is already handling an invite.", character, MessagePacket.MessageMode.ToPlayer);
                            return;
                        }
                        
                        var outPacket = new Packet(CfgServerMessages.CFG_GUILD);
                        outPacket.WriteByte(GuildServerOpcodes.ReceiveInvite);
                        outPacket.WriteString(character.Name);
                        outPacket.WriteString(character.Guild.Name);
                        otherChar.SendPacket(outPacket);

                        otherChar.GuildInvite = new Character.GuildInviteData
                        {
                            GuildID = character.Guild.ID,
                            InviterID = character.ID,
                        };
                    }
                    else
                    {
                        MessagePacket.SendText(MessagePacket.MessageTypes.RedText, "Could not find character on this channel.", character, MessagePacket.MessageMode.ToPlayer);
                    }
                    break;
                }
                case GuildClientOpcodes.InviteResult:
                {
                    if (character.GuildInvite == null) return;

                    var invite = character.GuildInvite;
                    character.GuildInvite = null;

                    var accepted = packet.ReadBool();

                    var invitedToGuild = Server.Instance.GetGuild(invite.GuildID);
                    if (invitedToGuild == null)
                    {
                        return;
                    }
                    
                    if (!accepted)
                    {
                        var inviter = Server.Instance.GetCharacter(invite.InviterID);
                        if (inviter != null)
                        {
                            MessagePacket.SendText(MessagePacket.MessageTypes.RedText, $"{character.Name} has declined to join your guild.", inviter, MessagePacket.MessageMode.ToPlayer);
                        }
                        
                        return;
                    }
                    
                    
                    var centerPacket = new Packet(ISClientMessages.GuildJoinPlayer);

                    centerPacket.WriteInt(invitedToGuild.ID);
                    new GuildCharacter
                    {
                        CharacterID = character.ID,
                        CharacterName = character.Name,
                        Job = character.Job,
                        Rank = GuildCharacter.Ranks.Member,
                        Level = character.Level,
                        Online = true,
                    }.Encode(centerPacket);
                        
                    Server.Instance.CenterConnection.SendPacket(centerPacket);
                    
                    break;
                }
                case GuildClientOpcodes.ChangeLogo:
                {
                    if (guildCharacter == null) return;
                    
                    if (guildCharacter.Rank < GuildCharacter.Ranks.Master) return;

                    var newLogo = new GuildLogo(packet);
                    guild.ChangeLogo(guildCharacter.CharacterID, newLogo);

                    // Just to make sure if we need to deduct something
                    if (character.SetGuildMarkCost != 0)
                    {
                        character.Inventory.Exchange(null, character.SetGuildMarkCost);
                        MesosTransfer.PlayerGuildMarkChange(character.ID, character.SetGuildMarkCost, null);
                    }
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}