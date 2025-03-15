using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using log4net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.GameObjects;

namespace WvsBeta.Game
{
    public static class MessagePacket
    {
        private static ILog log = LogManager.GetLogger("ChatLog");
        private static ILog commandLog = LogManager.GetLogger("CommandLog");
        private static ILog buddyLog = LogManager.GetLogger("BuddyChatLog");
        private static ILog guildLog = LogManager.GetLogger("GuildChatLog");
        private static ILog partyLog = LogManager.GetLogger("PartyChatLog");
        private static ILog whisperLog = LogManager.GetLogger("WhisperChatLog");

        public enum MessageTypes : byte
        {
            Notice = 0x00, // Blue text no highlight
            PopupBox = 0x01, // Pop-up text window
            Megaphone = 0x02, // Blue text with highlight
            SuperMegaphone = 0x03, // Red text with bright pink highlight
            Header = 0x04, // Scrolling header
            RedText = 0x05 // Red text with no highlight
        }

        public enum MessageMode : byte
        {
            ToPlayer,
            ToMap,
            ToChannel
        }

        public enum MuteReasons : byte
        {
            FoulLanguage = 1,
            Advertising = 2,
            ImpersonationOfAGM = 3,
            AccountTrading = 4,
            ReportingOfFameTradeScams = 5,
            PenaltyAlert = 6,
            Wanker = 7,
            Harassment = 8,
        }

        public static string GetMuteReasonText(MuteReasons reason)
        {
            switch ((byte)reason)
            {
                case 1: return "Foul Language";
                case 2: return "Advertising";
                case 3: return "Impersonation of a GM";
                case 4: return "Account Trading";
                case 5: return "Reporting of fame/trade scams";
                case 6: return "Penalty Alert";
                case 7: return "Being a wanker";
                case 8: return "Harassment";
                default: return reason.ToString();
            }
        }

        public static MuteReasons ParseMuteReason(string input)
        {
            switch (input)
            {
                case "1":
                case "foullanguage": return MuteReasons.FoulLanguage;
                case "2":
                case "ads":
                case "advertising": return MuteReasons.Advertising;
                case "3":
                case "impersonation":
                case "fakegm": return MuteReasons.ImpersonationOfAGM;
                case "4":
                case "trading": return MuteReasons.AccountTrading;
                case "5":
                case "reportscam": return MuteReasons.ReportingOfFameTradeScams;
                case "6":
                case "penalty": return MuteReasons.PenaltyAlert;
                case "7":
                case "wanker":
                case "bellend": return MuteReasons.Wanker;
                case "8":
                case "harass":
                case "harrass":
                case "harrassment":
                case "harassment": return MuteReasons.Harassment;
                default: return 0;
            }
        }

        public static bool GetMuteMessage(Character chr, out string muteMessage)
        {
            chr.LastChat = MasterThread.CurrentTime;
            muteMessage = null;
            if (chr.MutedUntil > MasterThread.CurrentDate)
            {
                muteMessage = $"You are muted until {chr.MutedUntil:yyyy-MM-dd HH:mm:ss}, reason {GetMuteReasonText((MuteReasons)chr.MuteReason)}";
                return true;
            }

            return false;
        }

        public static bool ShowMuteMessage(Character chr)
        {
            if (GetMuteMessage(chr, out var muteMessage))
            {
                SendText(MessageTypes.RedText, muteMessage, chr, MessageMode.ToPlayer);
                return true;
            }

            return false;
        }

        /// <returns>true when it was a GM command (not telling if it was a valid one, however)</returns>
        public static bool TryHandleGMCommand(Character chr, string text)
        {
            if (!chr.IsGM) return false;
            if (!text.StartsWith('/') && !text.StartsWith('!')) return false;

            commandLog.Info(chr.Name + ": " + text);

            if (!CommandHandling.HandleGMCommand(chr, text))
            {
                SendNotice(chr, $"Unknown command {text}");
            }

            return true;
        }

        public static void HandleChat(Character chr, Packet packet)
        {
            var what = packet.ReadLocalizedString(chr.ClientActiveCodePage);

            if (!chr.IsGM && what.ToString().Length > 40) // Temporary fix until client crash is resolved
            {
                SendTextPlayer(MessageTypes.PopupBox, "Your have exceeded the maximum number of characters.", chr, true);
                return;
            }

            if (chr.IsGM == false && CheatInspector.CheckTextSpam(what))
            {
                log.Error("Disconnecting player for chat spam");
                chr.Disconnect();
                return;
            }

            if (TryHandleGMCommand(chr, what)) return;

            if (ShowMuteMessage(chr))
            {
                log.Info("[MUTED] " + chr.Name + ": " + what);
                return;
            }

            if (!chr.CanAttachAdditionalProcess)
            {
                SendTextPlayer(MessageTypes.RedText, "You cannot chat right now.", chr, true);
                return;
            }

            log.Info(chr.Name + ": " + what);

            if (!TrySendCannotChatMessage(chr))
                MapPacket.SendChatMessage(chr, what);
        }

        public static bool TrySendCannotChatMessage(Character chr)
        {
            // You cannot talk in this map
            if (!chr.IsGM && !chr.Field.ChatEnabled)
            {
                SendTextPlayer(MessageTypes.RedText, "You cannot chat right now.", chr, true);
                return true;
            }

            return false;
        }

        enum SpecialChatType
        {
            BuddyChat = 0,
            PartyChat = 1,
            GuildChat = 2
        }

        public static void HandleSpecialChat(Character chr, Packet packet)
        {
            if (!chr.Field.ChatEnabled) return;

            //to be handled via center server
            var Type = (SpecialChatType)packet.ReadByte();
            var CountOfRecipients = packet.ReadByte();
            // Not used
            var Recipients = new int[CountOfRecipients];

            for (var i = 0; i < CountOfRecipients; i++)
            {
                Recipients[i] = packet.ReadInt();
            }

            var Message = packet.ReadLocalizedString(chr.ClientActiveCodePage);

            if (TryHandleGMCommand(chr, Message))
            {
                return;
            }

            var logger = log;
            switch (Type)
            {
                case SpecialChatType.BuddyChat: logger = buddyLog; break;
                case SpecialChatType.PartyChat: logger = partyLog; break;
                case SpecialChatType.GuildChat: logger = guildLog; break;
            }

            if (ShowMuteMessage(chr))
            {
                logger.Info("[MUTED] " + chr.Name + ": " + Message);
                return;
            }

            if (Type == SpecialChatType.GuildChat && chr.Guild == null) return;

            // We don't log the chat here, but on center server
            // From there we have full logs

            switch (Type)
            {
                case SpecialChatType.BuddyChat:
                    Server.Instance.CenterConnection.BuddyChat(chr, Message, Recipients);
                    break;
                case SpecialChatType.PartyChat:
                    Server.Instance.CenterConnection.PartyChat(chr.ID, Message);
                    break;
                case SpecialChatType.GuildChat:
                    Server.Instance.CenterConnection.GuildChat(chr.Guild.ID, chr.ID, Message);
                    break;
            }
        }

        public static void HandleCommand(Character chr, Packet packet)
        {
            if (!chr.Field.ChatEnabled) return;

            var type = packet.ReadByte();
            var victim = packet.ReadString();

            var victimChar = Server.Instance.GetCharacter(victim);

            // Block find or whisper
            if (victimChar != null && (victimChar.IsGM && !chr.IsGM))
            {
                Find(chr, victim, -1, 0, false);
                return;
            }

            switch (type)
            {
                case 0x05:
                    log.Info("[FIND][ " + chr.Name + " ] " + victim);
                    if (victimChar != null)
                    {
                        Find(chr, victim, victimChar.MapID, 0, true);
                    }
                    else
                    {
                        Server.Instance.CenterConnection.PlayerFind(chr.ID, victim);
                    }
                    break;
                case 0x06:
                    var message = packet.ReadLocalizedString(chr.ClientActiveCodePage);


                    if (ShowMuteMessage(chr))
                    {
                        whisperLog.Info("[MUTED][to " + victim + "] " + chr.Name + ":  " + message);
                        return;
                    }

                    whisperLog.Info("[to " + victim + "] " + chr.Name + ":  " + message);
                    if (victimChar != null)
                    {
                        Find(chr, victim, -1, 1, false);
                        Whisper(victimChar, chr.Name, Server.Instance.ID, message, 18);
                    }
                    else
                    {
                        Server.Instance.CenterConnection.PlayerWhisper(chr.ID, victim, message);
                    }
                    break;
            }
        }

        public static void SendMegaphoneMessage(string what)
        {
            log.Info("[MEGAPHONE] " + what);
            Server.Instance.ServerTraceDiscordReporter.Enqueue($"[MEGAPHONE] {what}");

            var pw = new Packet(ServerMessages.BROADCAST_MSG);
            pw.WriteByte(MessageTypes.Megaphone);
            pw.WriteString(what);

            foreach (var kvp in MapProvider.Maps)
            {
                kvp.Value.SendPacket(pw);
            }
        }

        public static void SendMegaphoneMessage(string what, int mapid)
        {
            log.Info("[MEGAPHONE] " + what);
            Server.Instance.ServerTraceDiscordReporter.Enqueue($"[MEGAPHONE:{mapid}] {what}");

            var pw = new Packet(ServerMessages.BROADCAST_MSG);
            pw.WriteByte(MessageTypes.Megaphone);
            pw.WriteString(what);
            MapProvider.Maps[mapid].SendPacket(pw);
        }

        public static void SendSuperMegaphoneMessage(string what, bool WhisperOrFind, byte channel)
        {
            log.Info("[SUPERMEGAPHONE] " + what);
            Server.Instance.ServerTraceDiscordReporter.Enqueue($"[SUPERMEGAPHONE] {what}");


            var pw = new Packet(ServerMessages.BROADCAST_MSG);
            pw.WriteByte(MessageTypes.SuperMegaphone);
            pw.WriteString(what);
            if (channel == 1) channel = 0; // Bugged O.o
            pw.WriteByte(channel);
            pw.WriteBool(WhisperOrFind);
            
            foreach (var kvp in MapProvider.Maps)
                kvp.Value.SendPacket(pw);
        }

        private static Packet BuildMessagePacket(MessageTypes type, string what, out bool trackMessage)
        {
            trackMessage = type != MessageTypes.Header;

            var pw = new Packet(ServerMessages.BROADCAST_MSG);
            pw.WriteByte(type);

            if (type == MessageTypes.Header)
            {
                pw.WriteBool(what != "");
            }
            pw.WriteString(what);

            return pw;
        }

        public static void SendTextPlayer(MessageTypes type, string what, Character victim, bool noDiscordMessage = false)
        {
            if (type == MessageTypes.SuperMegaphone) throw new InvalidOperationException("SuperMegaphone in SendText");

            var packet = BuildMessagePacket(type, what, out var trackMessage);

            if (trackMessage)
            {
                if (!noDiscordMessage)
                {
                    Server.Instance.ServerTraceDiscordReporter.Enqueue($"[ToPlayer:{victim.ID}] {what}");
                }

                log.Info($"[MSG][ ToPlayer ][{type}][{victim.ID}] {what}");
            }

            victim.SendPacket(packet);
        }

        public static void SendTextMaps(MessageTypes type, string what, params Map[] maps)
        {
            if (type == MessageTypes.SuperMegaphone) throw new InvalidOperationException("SuperMegaphone in SendText");

            var packet = BuildMessagePacket(type, what, out var trackMessage);

            if (trackMessage)
            {
                var mapidsText = string.Join(", ", maps.Select(x => x.ID));
                Server.Instance.ServerTraceDiscordReporter.Enqueue($"[ToMaps:{mapidsText}] {what}");
                log.Info($"[MSG][ ToMaps ][{type}][{mapidsText}] {what}");
            }

            maps.ForEach(x => x.SendPacket(packet));
        }

        public static void SendTextMap(MessageTypes type, string what, Map map)
        {
            if (type == MessageTypes.SuperMegaphone) throw new InvalidOperationException("SuperMegaphone in SendText");

            var packet = BuildMessagePacket(type, what, out var trackMessage);

            if (trackMessage)
            {
                Server.Instance.ServerTraceDiscordReporter.Enqueue($"[ToMap:{map.ID}] {what}");
                log.Info($"[MSG][ ToMap ][{type}][{map.ID}] {what}");
            }

            map.SendPacket(packet);
        }

        public static void SendTextChannel(MessageTypes type, string what)
        {
            if (type == MessageTypes.SuperMegaphone) throw new InvalidOperationException("SuperMegaphone in SendText");

            var packet = BuildMessagePacket(type, what, out var trackMessage);

            if (trackMessage)
            {
                Server.Instance.ServerTraceDiscordReporter.Enqueue($"[ToChannel] {what}");
                log.Info($"[MSG][ ToChannel ][{type}] {what}");
            }

            foreach (var kvp in Server.Instance.CharacterList.Values)
            {
                kvp.SendPacket(packet);
            }
        }

        public static void SendText(MessageTypes type, string what, Character victim, MessageMode mode)
        {
            if (type == MessageTypes.SuperMegaphone) return;

            switch (mode)
            {
                case MessageMode.ToPlayer: SendTextPlayer(type, what, victim); break;
                case MessageMode.ToMap: SendTextMap(type, what, victim.Field); break;
                case MessageMode.ToChannel: SendTextChannel(type, what); break;
            }
        }


        public static void SendAdminWarning(Map map, string what)
        {
            log.Info("[ADMIN WARNING MAP][" + map.ID + "] " + what);
            var pw = new Packet(ServerMessages.WARN_MESSAGE);
            pw.WriteString(what);
            map.SendPacket(pw);

            Server.Instance.ServerTraceDiscordReporter.Enqueue($"[ToMap:{map.ID}] [Admin Warning] {what}");
        }

        public static void SendAdminWarning(Character victim, string what)
        {
            log.Info("[ADMIN WARNING][" + victim.ID + "] " + what);
            var pw = new Packet(ServerMessages.WARN_MESSAGE);
            pw.WriteString(what);
            victim.SendPacket(pw);
            Server.Instance.ServerTraceDiscordReporter.Enqueue($"[ToPlayer:{victim.ID}] [Admin Warning] {what}");
        }


        public static void SendNotice(Character victim, string what, params object[] args)
        {
            if (args.Length > 0) what = string.Format(what, args);

            var pw = new Packet(ServerMessages.BROADCAST_MSG);
            pw.WriteByte(MessageTypes.Notice);
            pw.WriteString(what);
            if (victim == null)
            {
                log.Info("[SERVERNOTICE] " + what);
                Server.Instance.PlayerList.ForEach(x => x.Value.Socket.SendPacket(pw));
            }
            else
            {
                log.Info("[NOTICE][" + victim.ID + "] " + what);
                victim.SendPacket(pw);
            }
        }

        public static void ScriptNotice(Character victim, string what)
        {
            log.Info("[USER MESSAGE][" + victim.Name + "] " + what);
            var pw = new Packet(ServerMessages.BROADCAST_MSG);
            pw.WriteByte(MessageTypes.RedText);
            pw.WriteString(what);

            victim?.SendPacket(pw);
        }

        public static void ScriptNotice(Map field, string what)
        {
            log.Info("[MAP MESSAGE][" + field.ID + "] " + what);
            var pw = new Packet(ServerMessages.BROADCAST_MSG);
            pw.WriteByte(MessageTypes.RedText);
            pw.WriteString(what);

            field.SendPacket(pw);
        }

        public static void SendNoticeGMs(string what, MessageTypes severity = MessageTypes.Notice)
        {
            log.Info("[GM NOTICE] " + what);
            Trace.WriteLine("[GM NOTICE] " + what);

            var pw = new Packet(ServerMessages.BROADCAST_MSG);
            pw.WriteByte(severity);
            pw.WriteString(what);

            Server.Instance.StaffCharacters
                .Where(c => c.IsGM)
                .ForEach(a => a.SendPacket(pw));
        }

        public static void SendNoticeMap(string what, int mapid)
        {
            log.Info("[MAPNOTICE][" + mapid + "] " + what);
            var pw = new Packet(ServerMessages.BROADCAST_MSG);
            pw.WriteByte(MessageTypes.Notice);
            pw.WriteString(what);
            MapProvider.Maps[mapid].SendPacket(pw);
        }

        public static void SendAdminMessage(Character chr, string what, byte Type, byte to)
        {
            var pw = new Packet(ServerMessages.BROADCAST_MSG);
            pw.WriteByte(Type);
            if (Type == 4)
            {
                pw.WriteBool(what.Length != 0);
            }
            pw.WriteString(what);
            switch (to)
            {
                case 0x00:
                    Server.Instance.CenterConnection.AdminMessage(what, Type);
                    break;
                case 0x01:
                    foreach (var kvp in MapProvider.Maps)
                    {
                        kvp.Value.SendPacket(pw);
                    }
                    break;
                case 0x02:
                    chr.Field.SendPacket(pw);
                    break;
            }
        }

        public static void Whisper(Character victim, string who, byte channel, LocalizedString message, byte msgDirection)
        {
            var pw = new Packet(ServerMessages.WHISPER);
            pw.WriteByte(msgDirection);
            pw.WriteString(who);
            pw.WriteByte(channel);
            pw.WriteString(message);
            victim.SendPacket(pw);
        }

        public static void MonsterBookAdded(Character chr, int monsterBookId, int newCount, bool wasAlreadyDone)
        {
            var pw = new Packet(CfgServerMessages.CFG_MONSTER_BOOK_ADDED);
            pw.WriteInt(monsterBookId);
            pw.WriteByte((byte)newCount);
            pw.WriteBool(wasAlreadyDone);
            chr.SendPacket(pw);
        }

        public static void SendScrMessage(Character chr, string message, byte fontType = 0, byte outlineType = 1)
        {
            var p = new Packet(CfgServerMessages.CFG_SCRMSG);
            p.WriteString(message);
            p.WriteByte(fontType);
            p.WriteByte(outlineType);
            chr.SendPacket(p);
        }

        public static void SendPopup(Character chr, string message)
        {
            var pw = new Packet(ServerMessages.BROADCAST_MSG);
            pw.WriteByte(MessageTypes.PopupBox);
            pw.WriteString(message);
            chr.SendPacket(pw);
        }

        public static void Find(Character victim, string who, int map, sbyte dunno, bool isChannel)
        {
            var pw = new Packet(ServerMessages.WHISPER);

            if (map != -1)
            {
                pw.WriteByte(0x09);
                pw.WriteString(who);
                if (map == -2)
                {
                    // In cashshop
                    pw.WriteByte(0x02);
                    pw.WriteInt(0);
                }
                else if (isChannel)
                {
                    // In a channel
                    pw.WriteByte(0x01);
                    pw.WriteInt(map); // The channel ID
                }
                else
                {
                    // In a map
                    pw.WriteByte(0x03);
                    pw.WriteInt(map);
                }
            }
            else
            {
                pw.WriteByte(0x0A);
                pw.WriteString(who);
                pw.WriteSByte(dunno);
            }
            victim.SendPacket(pw);
        }
    }
}
