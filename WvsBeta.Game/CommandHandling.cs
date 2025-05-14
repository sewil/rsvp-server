using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using log4net;
using Newtonsoft.Json.Linq;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.Events.GMEvents;
using WvsBeta.Game.GameObjects;
using WvsBeta.Game.Packets;
using WvsBeta.SharedDataProvider.Templates;

namespace WvsBeta.Game
{
    public static class CommandHandling
    {
        private static readonly ILog _log = LogManager.GetLogger("CommandHandling");
        public static Dictionary<string, int> MapNameList { get; } = new Dictionary<string, int>
        {
            // Job maps
            {"gm", 180000000},
            {"3rd", 211000001},
            {"mage", 101000003},
            {"bowman", 100000201},
            {"thief", 103000003},
            {"warrior", 102000003},
            // Miscellaneous
            {"happyville", 209000000},
            {"cafe", 193000000},
            {"guild", 200000301},
            // Maple Island
            {"southperry", 60000},
            {"amherst", 1010000},
            // Victoria
            {"henesys", 100000000},
            {"perion", 102000000},
            {"ellinia", 101000000},
            {"sleepy", 105040300},
            {"sleepywood", 105040300},
            {"lith", 104000000},
            {"florina", 110000000},
            {"kerning", 103000000},
            // Ossyria
            {"orbis", 200000000},
            {"elnath", 211000000},
            {"nath", 211000000},
            // Ludus Lake area
            {"ludi", 220000000},
            {"omega", 221000000},
            {"aqua", 230000000},
            // Training Areas
            {"hhg1", 104040000},
            {"kerningconstruct", 103010000},
            {"westrockymountain1", 102020000},
            {"pigbeach", 104010001},
            {"fog", 106010102},
            {"subwayb1", 103000902},
            {"subwayb2", 103000905},
            {"subwayb3", 103000909},
            // Free Markets
            {"henfm", 100000110},
            {"perionfm", 102000100},
            {"elnathfm", 211000110},
            {"ludifm", 220000200},
            // Dungeon areas
            {"dungeon", 105090200},
            {"mine", 211041400},
            // Area boss maps
            {"jrbalrog", 105090900},
            {"mushmom", 100000005},
            // PQ maps
            {"kpqexit", 103000890},
            {"kpqbonus", 103000805},
            {"kingslime", 103000804},
            {"kpq5", 103000804},
            {"kpq4", 103000803},
            {"kpq3", 103000802},
            {"kpq2", 103000801},
            {"kpq1", 103000800},
            // Boss maps
            {"zakum", 280030000},
            {"zakumdoor", 211042300},
            {"pianus",230040420},
            {"papulatus", 220080001},
            {"papulatusdoor", 220080000},
            // Contimove
            {"elliniastation", 101000300},
            {"orbisstation", 200000100},
            {"orbiselliniastation", 200000111},
            {"orbisludistation", 200000121},
            {"orbiselliniatakeoff", 200000112},
            {"orbiselliniaboat", 200090000},
            {"elliniaorbistakeoff", 101000301},
            {"elliniaorbisboat", 200090010},
            // Events
            {"findthejewel", 109010000},
            {"jewel", 109010000},
            {"ftj", 109010000},
            {"snowballwait", 109060001},
            {"snowball", 109060000},
            {"fitness", 109040000},
            {"ox", 109020001},
            {"quiz", 109020001},
            {"oxquiz", 109020001},
            {"coconut", Map_Coconut.CoconutMapID},
            {"cokeplay", Map_Coconut.CokeplayMapID},
            {"fm", MapProvider.CurrentFM}
        };

        public static int GetMapidFromName(string name)
        {
            if (MapNameList.ContainsKey(name)) return MapNameList[name];
            return -1;
        }

        private static BanReasons GetBanReasonFromText(CommandArg arg)
        {
            switch (arg)
            {
                // Your account has been blocked for hacking or illegal use of third-party programs.
                case "1":
                case "ct":
                case "h":
                case "hack":
                case "hax": return BanReasons.Hack;

                // Your account has been blocked for using macro / auto-keyboard.
                case "2":
                case "bot":
                case "botting":
                case "macro": return BanReasons.Macro;

                // Your account has been blocked for illicit promotion and advertising.
                case "3":
                case "promo":
                case "ad":
                case "ads":
                case "advertisement": return BanReasons.Advertisement;

                // Your account has been blocked for for harassment.
                case "4":
                case "haras":
                case "harasment":
                case "harass":
                case "harassment": return BanReasons.Harassment;

                // Your account has been blocked for using profane language.
                case "5":
                case "trol":
                case "trolling":
                case "curse":
                case "badlanguage": return BanReasons.BadLanguage;

                // Your account has been blocked for scamming.
                case "6":
                case "scamming":
                case "scam": return BanReasons.Scam;

                // Your account has been blocked for misconduct.
                case "7":
                case "ks":
                case "mc":
                case "multiclient":
                case "misconduct": return BanReasons.Misconduct;

                // Your account has been blocked for illegal cash transaction
                case "8":
                case "sell":
                case "rwt":
                case "irlmoney": return BanReasons.Sell;

                // Your account has been blocked for illegal charging/funding. Please contact customer support for further details.
                case "9":
                case "moneyloundry":
                case "icash": return BanReasons.ICash;

                default: return BanReasons.Hack;
            }
        }

        enum UserIdFetchResult
        {
            Found,
            UserNotFound,
            PlayerNotFound,
            IDNotFound,
            UnknownType
        }

        private static UserIdFetchResult GetUserIDFromArgs(CommandArg argType, CommandArg argValue, out int userId)
        {
            userId = 0;
            switch (argType)
            {
                case "uid":
                case "userid":
                    if (!int.TryParse(argValue, out userId) || !Server.Instance.CharacterDatabase.ExistsUser(userId))
                        return UserIdFetchResult.UserNotFound;

                    break;
                case "user":
                case "username":
                    userId = Server.Instance.CharacterDatabase.UserIDByUsername(argValue);
                    if (userId == -1) return UserIdFetchResult.UserNotFound;

                    break;
                case "name":
                case "player":
                case "character":
                case "charname":
                    userId = Server.Instance.CharacterDatabase.UserIDByCharacterName(argValue);
                    if (userId == -1) return UserIdFetchResult.PlayerNotFound;

                    break;
                case "cid":
                case "charid":
                    userId = Server.Instance.CharacterDatabase.UserIDByCharID(int.Parse(argValue));
                    if (userId == -1) return UserIdFetchResult.IDNotFound;

                    break;
                default:
                    return UserIdFetchResult.UnknownType;
            }

            return UserIdFetchResult.Found;
        }

        static bool shuttingDown;


        private static CommandArgs Args;
        private static Character _character;

        private static void SendRed(string text, Character toCharacter = null)
        {
            toCharacter ??= _character;
            MessagePacket.SendTextPlayer(MessagePacket.MessageTypes.RedText, text, toCharacter, true);
        }

        private static void SendNotice(string text, Character toCharacter = null)
        {
            toCharacter ??= _character;
            MessagePacket.SendTextPlayer(MessagePacket.MessageTypes.Notice, text, toCharacter, true);
        }

        /// <summary>
        /// Calls cb with the User ID parsed from the input
        /// </summary>
        /// <param name="argsStart">Offset to the type and value arguments in input string</param>
        /// <param name="cb">callback with (userid, enteredValue, restOfArgs) </param>
        /// <returns></returns>
        static bool ParseUserArg(int argsStart, Action<int, string, CommandArg[]> cb)
        {
            if (argsStart >= Args.Count)
            {
                SendRed("Missing 'type' for character selection in command.");
                return true;
            }

            if (argsStart + 1 >= Args.Count)
            {
                SendRed("Missing 'value' for character selection in command.");
                return true;
            }

            var argType = Args[argsStart];
            var argValue = Args[argsStart + 1];

            var argText = argType + " " + argValue;

            var extraArgs = Args.Args.Skip(argsStart + 2).ToArray();
            switch (GetUserIDFromArgs(argType, argValue, out var userId))
            {
                case UserIdFetchResult.UnknownType:
                    SendNotice($"Unknown type {argType} passed as argument.");
                    break;
                case UserIdFetchResult.IDNotFound:
                    SendNotice($"User with char id {argValue} does not exist");
                    return true;
                case UserIdFetchResult.PlayerNotFound:
                    SendNotice($"Player {argValue} does not exist.");
                    return true;
                case UserIdFetchResult.UserNotFound:
                    SendNotice($"User {argValue} does not exist.");
                    return true;
                case UserIdFetchResult.Found:
                    cb(userId, argText, extraArgs);
                    return true;
            }

            return true;
        }

        private static int ArgToHours(string arg)
        {
            int hours;
            if (arg.EndsWith("w") || arg.EndsWith("h") || arg.EndsWith("d") || arg.EndsWith("m"))
            {
                hours = int.Parse(arg.Substring(0, arg.Length - 1));
                var type = arg.Substring(arg.Length - 1)[0];
                switch (type)
                {
                    case 'm':
                        // month
                        hours *= 30 * 24;
                        break;
                    case 'w':
                        // week
                        hours *= 7 * 24;
                        break;
                    case 'd':
                        // day
                        hours *= 24;
                        break;
                }
            }
            else
            {
                hours = int.Parse(arg);
            }

            return hours;
        }

        private static bool HandleGMCommandLevel1()
        {
            switch (Args.Command.ToLowerInvariant())
            {
                #region Map / Goto, change your map

                case "m":
                case "map":
                case "goto":
                    {
                        if (Args.Count == 0)
                        {
                            SendNotice($"You are currently in map {_character.Field.ID}");
                        }
                        else if (Args.Count > 0)
                        {
                            var FieldID = -1;

                            if (!Args[0].IsNumber())
                            {
                                var MapStr = Args[0];
                                var TempID = GetMapidFromName(MapStr);

                                if (TempID == -1)
                                {
                                    switch (MapStr)
                                    {
                                        case "here":
                                            FieldID = _character.MapID;
                                            break;
                                        case "town":
                                            FieldID = _character.Field.ReturnMap;
                                            break;
                                    }
                                }
                                else
                                    FieldID = TempID;
                            }
                            else
                                FieldID = Args[0].GetInt32();

                            var teleCharacter = _character;
                            if (Args.Count > 1)
                            {
                                var other = Args[1].Value;
                                var otherChar = Server.Instance.GetCharacter(other);
                                if (otherChar == null)
                                {
                                    SendRed($"Character {other} not found");
                                    return true;
                                }

                                teleCharacter = otherChar;
                            }

                            if (MapProvider.Maps.ContainsKey(FieldID))
                                teleCharacter.ChangeMap(FieldID);
                            else
                                SendRed($"Map {FieldID} not found");
                        }

                        return true;
                    }
                case "mapportal":
                case "gotoportal":
                    {
                        if (Args.Count == 0)
                        {
                            SendNotice($"You are currently in map {_character.Field.ID}");
                        }
                        else if (Args.Count < 2)
                        {
                            SendRed("Missing portal name");
                        }
                        else if (Args.Count > 0)
                        {
                            var FieldID = -1;

                            if (!Args[0].IsNumber())
                            {
                                var MapStr = Args[0];
                                var TempID = GetMapidFromName(MapStr);

                                if (TempID == -1)
                                {
                                    switch (MapStr)
                                    {
                                        case "here":
                                            FieldID = _character.MapID;
                                            break;
                                        case "town":
                                            FieldID = _character.Field.ReturnMap;
                                            break;
                                    }
                                }
                                else
                                    FieldID = TempID;
                            }
                            else
                                FieldID = Args[0].GetInt32();

                            if (!MapProvider.Maps.TryGetValue(FieldID, out Map map))
                            {
                                SendRed($"Map {FieldID} not found");
                            }
                            else
                            {
                                Portal portal;
                                if (byte.TryParse(Args[1], out var portalID))
                                {
                                    portal = map.SpawnPoints.FirstOrDefault(p => p.ID == portalID);
                                    portal ??= map.DoorPoints.FirstOrDefault(p => p.ID == portalID);
                                }
                                else
                                {
                                    string portalName = Args[1];
                                    map.Portals.TryGetValue(portalName, out portal);
                                }
                                if (portal == null)
                                {
                                    SendRed("Portal not found");
                                }
                                else
                                {
                                    _character.ChangeMap(FieldID, portal);
                                }
                            }
                        }
                        return true;
                    }
                #endregion

                #region Chase / Warp to player

                case "chase":
                case "warp":
                case "warpto":
                    {
                        if (Args.Count > 0)
                        {
                            var other = Args[0].Value;
                            var otherChar = Server.Instance.GetCharacter(other);
                            if (otherChar != null)
                            {
                                if (_character.MapID != otherChar.MapID)
                                {
                                    _character.ChangeMap(otherChar.MapID);
                                }

                                CfgPacket.SetPosition(_character, otherChar.Position);
                                return true;
                            }

                            SendRed($"Victim {other} not found.");
                        }

                        return true;
                    }

                case "randomchase":
                case "randomchaseall":
                    {
                        var victim =
                            Server.Instance.GetRandomCharacter(Args.Command.ToLowerInvariant() == "randomchase");

                        if (victim != null)
                        {
                            if (_character.MapID != victim.MapID)
                            {
                                _character.ChangeMap(victim.MapID);
                            }

                            CfgPacket.SetPosition(_character, victim.Position);
                            return true;
                        }

                        return true;
                    }

                #endregion

                #region ChaseHere / WarpHere, teleport someone to you

                case "chasehere":
                case "warphere":
                    {
                        if (Args.Count > 0)
                        {
                            var other = Args[0].Value.ToLower();
                            var otherChar = Server.Instance.GetCharacter(other);
                            if (otherChar != null)
                            {
                                if (otherChar.MapID != _character.MapID)
                                {
                                    otherChar.ChangeMap(_character.MapID);
                                }

                                CfgPacket.SetPosition(otherChar, _character.Position);
                                return true;
                            }

                            SendRed("Victim not found.");
                        }

                        return true;
                    }

                #endregion

                #region Online

                case "online":
                    {
                        var onlineStats = OnlineStats.Instance;
                        onlineStats.Calculate();

                        void WriteStats(string name, OnlineStats.Category cat)
                        {
                            SendNotice($"{name} ({cat.Total}): {string.Join(", ", cat.Names)}");
                        }

                        SendNotice($"Players online ({onlineStats.Total.Total}):");
                        WriteStats("Playing", onlineStats.ActivePlayers);
                        WriteStats("AFK", onlineStats.AFKs);
                        WriteStats("Husk", onlineStats.Husks);
                        WriteStats("GMs", onlineStats.GMs);
                        return true;
                    }

                #endregion

                #region DC / Kick

                case "dc":
                case "kick":
                    {
                        if (Args.Count > 0)
                        {
                            var victim = Args[0].Value.ToLower();
                            var who = Server.Instance.GetCharacter(victim);

                            if (who != null)
                                who.Disconnect();
                            else
                                SendRed("You have entered an incorrect name.");
                        }

                        return true;
                    }

                #endregion

                #region Ban

                case "banhelp":
                    {
                        SendNotice("Help: Use !permaban <userid/charname/charid> <value> (reason) to ban permanently. Use !suspend <userid/charname/charid> <value> <days to suspend> (reason)");
                        return true;
                    }

                case "permban":
                case "permaban":
                    {
                        if (Args.Count >= 2)
                        {
                            var banReason = Args.Count >= 3
                                ? GetBanReasonFromText(Args[2])
                                : BanReasons.Hack;

                            return ParseUserArg(0, (userId, argText, args) =>
                            {
                                Server.Instance.CharacterDatabase.PermaBan(userId, (byte)banReason, _character.Name, "");
                                Server.Instance.CenterConnection.KickUser(userId);

                                var msg = $"[{_character.Name}] Permabanned {argText} (userid {userId}), reason {banReason}";
                                Server.Instance.BanDiscordReporter.Enqueue(msg);
                                MessagePacket.SendNoticeGMs(msg, MessagePacket.MessageTypes.RedText);
                            });
                        }

                        SendNotice("Usage: !permaban <userid/charname/charid> <value> (reason)");

                        return true;
                    }

                case "suspend":
                case "tempban":
                case "ban":
                    {
                        if (Args.Count >= 3)
                        {
                            var hours = ArgToHours(Args[2].Value);

                            var banReason = Args.Count > 3
                                ? GetBanReasonFromText(Args[3])
                                : BanReasons.Hack;


                            return ParseUserArg(0, (userId, argText, _) =>
                            {
                                Server.Instance.CharacterDatabase.TempBan(userId, (byte)banReason, hours, _character.Name);
                                Server.Instance.CenterConnection.KickUser(userId);

                                var msg = $"[{_character.Name}] Tempbanned {argText} (userid {userId}), reason {banReason}, hours {hours}";
                                Server.Instance.BanDiscordReporter.Enqueue(msg);
                                MessagePacket.SendNoticeGMs(msg, MessagePacket.MessageTypes.RedText);
                            });
                        }

                        SendNotice("Usage: !suspend/tempban/ban <userid/charname/charid> <value> <hours> (reason)");
                        return true;
                    }

                #endregion

                #region Unban

                case "unban":
                    {
                        if (Args.Count == 2)
                        {
                            return ParseUserArg(0, (userId, argText, _) =>
                            {
                                Server.Instance.CharacterDatabase.RunQuery(
                                    "UPDATE users SET ban_expire = @expire_date WHERE ID = @id",
                                    "@id", userId,
                                    "@expire_date", MasterThread.CurrentDate.AddDays(-1).ToUniversalTime()
                                );

                                var msg = $"[{_character.Name}] Unbanned {argText} (userid {userId})";
                                Server.Instance.BanDiscordReporter.Enqueue(msg);
                                MessagePacket.SendNoticeGMs(msg, MessagePacket.MessageTypes.RedText);
                            });
                        }

                        SendNotice("Usage: !unban <userid/charname/charid> <value>");

                        return true;
                    }

                #endregion

                #region Muting

                case "muteban":
                case "mute":
                    {
                        if (Args.Count >= 3)
                        {
                            var hours = ArgToHours(Args[2].Value);

                            var banReason = Args.Count > 3
                                ? MessagePacket.ParseMuteReason(Args[3])
                                : MessagePacket.MuteReasons.FoulLanguage;

                            if (banReason == 0)
                            {
                                SendNotice("Unknown mute reason.");
                                return true;
                            }

                            return ParseUserArg(0, (userId, argText, _) =>
                            {
                                Server.Instance.CharacterDatabase.MuteBan(userId, (byte)banReason, hours);

                                var localPlayers = Server.Instance.CharacterList.Values
                                    .Where(x => x.UserID == userId).ToArray();
                                if (localPlayers.Length == 0)
                                {
                                    Server.Instance.CenterConnection.KickUser(userId);
                                }
                                else
                                {
                                    localPlayers.ForEach(x =>
                                    {
                                        x.MutedUntil = MasterThread.CurrentDate.AddHours(hours);
                                        x.MuteReason = (byte)banReason;
                                    });
                                }

                                var msg = $"[{_character.Name}] Muted {argText} (userid {userId}), reason {banReason}, hours {hours}";
                                Server.Instance.MutebanDiscordReporter.Enqueue(msg);
                                MessagePacket.SendNoticeGMs(msg, MessagePacket.MessageTypes.RedText);
                            });
                        }

                        SendNotice("Usage: !muteban/mute <userid/charname/charid> <value> <hours> (reason)");
                        return true;
                    }

                case "unmute":
                    {
                        if (Args.Count == 2)
                        {
                            return ParseUserArg(0, (userId, argText, _) =>
                            {
                                Server.Instance.CharacterDatabase.RunQuery(
                                    "UPDATE users SET quiet_ban_expire = @date WHERE ID = @id",
                                    "@id", userId,
                                    "@date", MasterThread.CurrentDate);

                                var localPlayers = Server.Instance.CharacterList.Values
                                    .Where(x => x.UserID == userId).ToArray();
                                if (localPlayers.Length == 0)
                                {
                                    Server.Instance.CenterConnection.KickUser(userId);
                                }
                                else
                                {
                                    localPlayers.ForEach(x =>
                                    {
                                        x.MutedUntil = MasterThread.CurrentDate;
                                    });
                                }

                                var msg = $"[{_character.Name}] Unmuted {argText} (userid {userId})";
                                Server.Instance.MutebanDiscordReporter.Enqueue(msg);
                                MessagePacket.SendNoticeGMs(msg, MessagePacket.MessageTypes.RedText);
                            });
                        }

                        SendNotice("Usage: !unmute <userid/charname/charid> <value>");

                        return true;
                    }

                #endregion

                #region Hackmute / Hackunmute

                case "hackmute":
                    {
                        if (Args.Count == 2)
                        {

                            var chr = Server.Instance.GetCharacter(Args[0]);
                            if (chr == null)
                            {
                                SendNotice($"Character {Args[0]} not found on this channel.");
                            }
                            else
                            {
                                var hours = ArgToHours(Args[1].Value);
                                chr.HacklogMuted = MasterThread.CurrentDate.AddHours(hours);
                                RedisBackend.Instance.MuteCharacter(_character.ID, chr.ID, hours);
                                MessagePacket.SendNoticeGMs(
                                    $"[{_character.Name}] Muted character {Args[0]}'s hack warnings for {hours} hours.",
                                    MessagePacket.MessageTypes.RedText
                                );
                            }

                            return true;
                        }

                        SendNotice("Usage: !hackmute <charactername> <hours>");
                        return true;
                    }

                case "hackunmute":
                    {
                        if (Args.Count == 1)
                        {
                            var chr = Server.Instance.GetCharacter(Args[0]);
                            if (chr == null)
                            {
                                SendNotice($"Character {Args[0]} not found on this channel.");
                            }
                            else
                            {
                                chr.HacklogMuted = DateTime.MinValue;
                                RedisBackend.Instance.UnmuteCharacter(chr.ID);
                                MessagePacket.SendNoticeGMs(
                                    $"[{_character.Name}] Unmuted character {Args[0]}'s hack warnings",
                                    MessagePacket.MessageTypes.RedText
                                );
                            }

                            return true;
                        }

                        SendNotice("Usage: !hackunmute <charactername>");
                        return true;
                    }

                #endregion

                #region MoveTrace

                case "movetracepet":
                case "movetraceplayer":
                case "movetracemob":
                case "movetracesummon":
                    {
                        MovePath.MovementSource source = 0;
                        switch (Args.Command.Replace("movetrace", ""))
                        {
                            case "pet":
                                source = MovePath.MovementSource.Pet;
                                break;
                            case "player":
                                source = MovePath.MovementSource.Player;
                                break;
                            case "mob":
                                source = MovePath.MovementSource.Mob;
                                break;
                            case "summon":
                                source = MovePath.MovementSource.Summon;
                                break;
                        }

                        if (Args.Count == 3 && Args[2].IsNumber())
                        {
                            var amount = Math.Min(Args[2].GetInt32(), 10);
                            return ParseUserArg(0, (userId, argText, _) =>
                            {
                                var localPlayers = Server.Instance.CharacterList.Values
                                    .Where(x => x.UserID == userId).ToArray();
                                if (localPlayers.Length > 0)
                                {
                                    localPlayers.ForEach(x =>
                                    {
                                        x.MoveTraceCount = amount;
                                        x.MoveTraceSource = source;
                                    });
                                    SendNotice($"Tracing player type {source} amount {amount}!");
                                }
                            });
                        }

                        SendNotice("Usage: !movetrace(pet|player|mob|summon) <userid/charname/charid> <value> <amount>");
                        return true;
                    }

                #endregion

                #region Warn

                case "w":
                case "warn":
                    {
                        if (Args.Count >= 2)
                        {
                            var charname = Args[0];
                            var victim = Server.Instance.GetCharacter(charname);
                            if (victim != null)
                            {
                                AdminPacket.SentWarning(_character, true);
                                MessagePacket.SendAdminWarning(victim, string.Join(" ", Args.Args.Skip(1)));
                            }
                            else
                            {
                                AdminPacket.SentWarning(_character, false);
                            }

                            return true;
                        }

                        SendNotice("Usage: !warn charname <text...>");
                        return true;
                    }

                case "wm":
                case "warnmap":
                    {
                        if (Args.Count >= 1)
                        {
                            AdminPacket.SentWarning(_character, true);
                            MessagePacket.SendAdminWarning(_character.Field, string.Join(" ", Args.Args));

                            return true;
                        }

                        SendNotice("Usage: !warnmap <text...>");
                        return true;
                    }

                #endregion

                #region MaxSkills

                case "maxskills":
                    {
                        var mMaxedSkills = new Dictionary<int, byte>();
                        foreach (var kvp in DataProvider.Skills)
                        {
                            var level = kvp.Value.MaxLevel;
                            _character.Skills.SetSkillPoint(kvp.Key, level, false);
                            mMaxedSkills.Add(kvp.Key, level);
                        }

                        SkillPacket.SendSetSkillPoints(_character, mMaxedSkills); // 1 packet for all skills
                        mMaxedSkills.Clear();
                        return true;
                    }

                case "minskills":
                    {
                        var mMaxedSkills = new Dictionary<int, byte>();
                        foreach (var kvp in DataProvider.Skills)
                        {
                            if (kvp.Key == Constants.Gm.Skills.Hide) continue;
                            _character.Skills.SetSkillPoint(kvp.Key, 0, false);
                            mMaxedSkills.Add(kvp.Key, 0);
                        }

                        SkillPacket.SendSetSkillPoints(_character, mMaxedSkills); // 1 packet for all skills
                        mMaxedSkills.Clear();
                        return true;
                    }

                #endregion

                #region Job

                case "job":
                    {
                        if (Args.Count <= 0 || !Args[0].IsNumber()) return true;

                        var Job = Convert.ToInt16(Args[0].GetInt16());
                        if (DataProvider.HasJob(Job) || Job == 0)
                            _character.SetJob(Job);
                        else
                            SendNotice($"Job {Job} does not exist.");

                        return true;
                    }

                #endregion

                #region MP

                case "mp":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            _character.SetMPAndMaxMP(Args[0].GetInt16());
                        return true;
                    }

                #endregion

                #region HP

                case "hp":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            _character.SetHPAndMaxHP(Args[0].GetInt16());
                        return true;
                    }

                #endregion

                #region Str

                case "str":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            _character.SetStr(Args[0].GetInt16());
                        return true;
                    }

                #endregion

                #region Dex

                case "dex":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            _character.SetDex(Args[0].GetInt16());
                        return true;
                    }

                #endregion

                #region Int

                case "int":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            _character.SetInt(Args[0].GetInt16());
                        return true;
                    }

                #endregion

                #region Luk

                case "luk":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            _character.SetLuk(Args[0].GetInt16());
                        return true;
                    }

                #endregion

                #region AP

                case "ap":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            _character.SetAP(Args[0].GetInt16());
                        return true;
                    }

                #endregion

                #region SP

                case "sp":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            _character.SetSP(Args[0].GetInt16());
                        return true;
                    }

                #endregion

                #region AddSP

                case "addsp":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            _character.AddSP(Args[0].GetInt16());
                        return true;
                    }

                #endregion

                #region level/lvl

                case "level":
                case "lvl":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            _character.SetLevel(Args[0].GetByte());
                        return true;
                    }

                #endregion

                #region MaxSlots

                case "maxslots":
                    {
                        _character.Inventory.SetInventorySlots(1, 100);
                        _character.Inventory.SetInventorySlots(2, 100);
                        _character.Inventory.SetInventorySlots(3, 100);
                        _character.Inventory.SetInventorySlots(4, 100);
                        _character.Inventory.SetInventorySlots(5, 100);
                        return true;
                    }

                #endregion

                #region MaxStats

                case "maxstats":
                    {
                        _character.SetHPAndMaxHP(30000);
                        _character.SetMPAndMaxMP(30000);
                        _character.SetLuk(30000);
                        _character.SetStr(30000);
                        _character.SetInt(30000);
                        _character.SetDex(30000);
                        _character.SetAP(0);
                        _character.SetSP(2000);
                        return true;
                    }

                #endregion

                #region Pos

                case "pos":
                case "pos1": //prevent client limitation when spamming this command during testing.
                case "pos2":
                case "pos3":
                    {
                        var ret = "Position of " + _character.Name + ". X: " + _character.Position.X +
                                  ". Y: " + _character.Position.Y + ". Fh: " + _character.Foothold + ".";
                        SendNotice(ret);
                        return true;
                    }

                #endregion

                #region Undercover

                case "undercover":
                    if (Args.TryGetBool(0, out var undercover))
                    {
                        RedisBackend.Instance.SetUndercover(_character.ID, undercover);
                        _character.Undercover = undercover;
                        SendNotice("You are now " + (undercover ? "" : "not") + " undercover.");
                        return true;
                    }

                    SendNotice("Usage: !undercover <true/false>");
                    return true;

                #endregion

                #region Autohide

                case "autohide":
                    if (Args.TryGetBool(0, out var autohide))
                    {
                        _character.GMAutoHideEnabled = autohide;
                        SendNotice("Autohide is now " + (autohide ? "enabled" : "disabled"));
                        return true;
                    }

                    SendNotice("Usage: !autohide <true/false>");
                    return true;

                #endregion

                #region Hide

                case "hide":
                case "h":
                    Args.TryGetBool(0, out var hide, true);
                    _character.SetHide(hide, false);
                    return true;

                #endregion

                #region guildreload

                case "guildreload":
                    {
                        var packet = new Packet(ISClientMessages.GuildReload);
                        Server.Instance.CenterConnection.SendPacket(packet);

                        SendNotice("Guild reload request has been sent.");

                        return true;
                    }

                #endregion

                #region whowashere

                case "whowashere":
                    {
                        const int MaxAmount = 10;
                        SendNotice("These are the last (at most) " + MaxAmount + " players that entered the map:");
                        var lastPlayers = _character.Field.PlayersThatHaveBeenHere.ToList();
                        lastPlayers.Sort((x, y) => (int)(y.Value - x.Value));

                        var str = string.Join(", ", lastPlayers.Take(MaxAmount).Select(x =>
                        {
                            var secondsAgo = (MasterThread.CurrentTime - x.Value) / 1000;
                            return x.Key + " (" + secondsAgo + "s ago)";
                        }));

                        SendNotice(str);

                        return true;
                    }

                #endregion

                #region runscript

                case "run":
                case "runscript":
                    {
                        if (Args.Count == 1)
                        {
                            NpcPacket.StartScript(_character, Args[0].Value);
                        }

                        return true;
                    }
                case "event":
                    {
                        NpcPacket.StartScript(_character, "event");
                        return true;
                    }
                case "toggleevent":
                case "eventtoggle":
                    {
                        string eventName = Args[0].Value;
                        if (!EventDateMan.GetEventNames().Contains(eventName))
                        {
                            SendRed("Unknown event!");
                        }
                        else if (EventDateMan.ForceActive.Contains(eventName))
                        {
                            EventDateMan.ForceActive.Remove(eventName);
                            SendRed("Event disabled");
                        }
                        else
                        {
                            EventDateMan.ForceActive.Add(eventName);
                            SendRed("Event enabled");
                        }
                        return true;
                    }
                #endregion

                #region discord

                case "discordmsg":
                    {
                        Server.Instance.ServerTraceDiscordReporter.Enqueue(Args.CommandText);
                        return true;
                    }

                #endregion

                #region itemcount

                case "itemcount":
                    {
                        if (Args.TryGetInt32(0, out var itemID))
                        {
                            SendNotice($"Amount of {itemID}: {_character.Inventory.ItemCount(itemID)}");
                        }
                        else
                        {
                            SendRed("!itemcount <itemid>");
                        }

                        return true;
                    }

                #endregion

                #region godmode

                case "god":
                case "godmode":
                    {
                        Args.TryGetBool(0, out var enableGodmode);
                        _character.GMGodMode = enableGodmode;
                        SendNotice($"Godmode is now {(enableGodmode ? "Enabled" : "Disabled")}");
                        return true;
                    }

                #endregion

                #region fixeddamage

                case "fd":
                case "fixeddamage":
                case "fixdamage":
                case "fixdam":
                case "fixeddmg":
                    {
                        Args.TryGetInt32(0, out var fixedDamage);
                        _character.GMFixedDamage = fixedDamage;
                        if (fixedDamage == -1)
                            SendNotice("Fixed damage cleared");
                        else
                            SendNotice($"Fixed damage set to {fixedDamage}, set to -1 to clear.");
                        return true;
                    }

                #endregion


                case "fart":
                    {
                        _character.Field.CreateMist(_character, _character.ID, Constants.FPMage.Skills.PoisonMyst, 20, 30 * 1000, -300, -300, 300, 300, 0);
                        return true;
                    }

                case "reset":
                case "resetmap":
                    {
                        Args.TryGetBool(0, out var shuffleReactors);
                        _character.Field.Reset(shuffleReactors);
                        SendNotice($"Reset {_character.Field} shuffleReactors: {shuffleReactors}");

                        return true;
                    }

                case "memo":
                case "sendmemo":
                    {
                        if (!Args.TryGetString(0, out var charname))
                        {
                            SendRed("Missing character name.");
                            return true;
                        }

                        var message = string.Join(" ", Args.Args.Skip(1));
                        if (MemoPacket.SendNewMemo(_character, charname, message))
                        {
                            SendNotice("Memo sent!");
                        }
                        else
                        {
                            SendRed("Unable to send memo. Check recipient.");
                        }
                        return true;
                    }

                case "renameplayer":
                case "renamechar":
                case "charrename":
                    {
                        if (!Args.TryGetString(0, out var oldCharname))
                        {
                            SendRed("Missing old character name.");
                            return true;
                        }

                        if (!Args.TryGetString(1, out var newCharname))
                        {
                            SendRed("Missing new character name.");
                            return true;
                        }

                        var charId = Server.Instance.CharacterDatabase.CharacterIdByName(oldCharname);

                        if (charId == null)
                        {
                            SendRed($"Unable to find character named {oldCharname}");
                            return true;
                        }

                        // Time to rename
                        Server.Instance.CenterConnection.RenamePlayer(_character.Player.SessionHash, charId.Value, newCharname);

                        return true;
                    }

                case "utf8test":
                    {

                        var pw = new Packet(ServerMessages.WARN_MESSAGE);
                        pw.WriteUTF8String("Hello ㅁㅈㄷ로9져ㅗㄹ9ㅈ . 你好世界 你好世界 Բարեւ աշխարհ ");
                        _character.SendPacket(pw);

                        return true;
                    }

                case "lookup":
                    {
                        Func<string, string, bool> findItem = (name, query) => name?.ToLowerInvariant().Contains(query.ToLowerInvariant()) ?? false;
                        var query = Enumerable.Empty<(int id, string name)>();
                        if (Args.Count < 2)
                        {
                            SendRed("Too few arguments!");
                        }
                        else if (Args[0] == "item")
                        {
                            query = DataProvider.Items.Select(i => (i.Value.ID, i.Value.Name));
                        }
                        else if (Args[0] == "equip")
                        {
                            query = DataProvider.Equips.Select(i => (i.Value.ID, i.Value.Name));
                        }
                        else if (Args[0] == "map")
                        {
                            query = MapProvider.Maps.Select(i => (i.Value.ID, i.Value.Name));
                        }
                        else if (Args[0] == "mob")
                        {
                            query = DataProvider.Mobs.Select(i => (i.Value.ID, i.Value.Name));
                        }
                        else if (Args[0] == "quest")
                        {
                            query = QuestNamesProvider.QuestNames.Select(i => ((int)i.Key, i.Value));
                        }
                        else if (Args[0] == "npc")
                        {
                            query = DataProvider.NPCs.Select(i => (i.Key, i.Value.Name));
                        }
                        else if (Args[0] == "skill")
                        {
                            query = SkillNamesProvider.SkillNames.Select(i => (i.Key, i.Value));
                        }
                        else
                        {
                            SendRed("Unknown lookup type!");
                            return true;
                        }
                        string searchQuery = string.Join(" ", Args.Args.Skip(1));

                        var results = query.Where(i => findItem(i.name, searchQuery)).Take(10).ToList();
                        if (results.Count == 0)
                        {
                            SendRed("Lookup query returned no results.");
                        }
                        else
                        {
                            SendNotice($"Lookup query returned {results.Count} results:");
                            results.Select(i => $"({i.id}) {i.name}").ForEach(msg => SendNotice(msg));
                        }
                        return true;
                    }
            }

            return false;
        }


        private static bool HandleGMCommandLevel2()
        {
            switch (Args.Command.ToLowerInvariant())
            {
                #region Create / Item

                case "create":
                case "item":
                    {
                        try
                        {
                            if (Args.Count > 0 && Args[0].IsNumber())
                            {
                                short Amount = 1;
                                var ItemID = Args[0].GetInt32();
                                var Inv = (byte)(ItemID / 1000000);

                                if (Inv <= 0 || Inv > 5 || !DataProvider.HasItem(ItemID))
                                {
                                    SendNotice("Item not found :(");
                                    return true;
                                }

                                var FreeSlots = _character.Inventory.ItemAmountAvailable(ItemID);
                                if (Args.Count >= 2)
                                {
                                    if (Args[1] == "max" || Args[1] == "fill" || Args[1] == "full")
                                        Amount = (short)(FreeSlots > short.MaxValue
                                            ? short.MaxValue
                                            : FreeSlots);
                                    else if (Args[1].IsNumber())
                                    {
                                        Amount = Args[1].GetInt16();
                                        if (Amount > FreeSlots)
                                            Amount = (short)(FreeSlots > short.MaxValue
                                                ? short.MaxValue
                                                : FreeSlots);
                                    }
                                }

                                if (Amount == 0)
                                {
                                    DropPacket.CannotLoot(_character, -1);
                                }
                                else
                                {
                                    var actuallyGivenAmount = _character.Inventory.AddNewItem(ItemID, Amount);
                                    CharacterStatsPacket.SendGainDrop(_character, false, ItemID, actuallyGivenAmount);
                                }
                            }
                            else
                                SendNotice($"Command syntax: !{Args.Command} [itemid] {{amount}}");

                            return true;
                        }
                        catch (Exception ex)
                        {
                            SendNotice($"Command syntax: !{Args.Command} [itemid] {{amount}}");
                            if (_character.IsGM)
                            {
                                SendNotice(string.Format("LOLEXCEPTION: {0}", ex));
                            }

                            return true;
                        }
                    }

                #endregion

                #region Summon / Spawn

                case "summon":
                case "spawn":
                    {
                        if (Args.Count > 0)
                        {
                            var Amount = 1;
                            var MobID = -1;

                            if (Args[0].IsNumber())
                                MobID = Args[0].GetInt32();

                            if (Args.Count > 1 && Args[1].IsNumber())
                                Amount = Args[1].GetInt32();

                            Amount = _character.IsAdmin ? Amount : Math.Min(Amount, 100);

                            if (DataProvider.Mobs.ContainsKey(MobID))
                            {
                                for (var i = 0; i < Amount; i++)
                                {
                                    _character.Field.CreateMobWithoutMobGen(
                                        MobID,
                                        _character.Position,
                                        _character.Foothold,
                                        facesLeft: _character.IsFacingLeft()
                                    );
                                }
                            }
                            else
                                SendRed("Mob not found.");
                        }

                        return true;
                    }

                #endregion

                #region VarSet

                case "varset":
                    {
                        if (Args.Count <= 1)
                            SendNotice("Usable args are Hp, Mp, Exp, MaxHp, MaxMp, Ap, Sp, Str, Dex, Int, Luk, Job, Level, Gender, Skin, Face, and Hair for Users and Name, Level, Tameness, Hunger for Pets.");
                        else if (Args.Count == 2 ||
                                 (Args[0].Value.ToLower() == "skill" && (Args.Count == 3 || Args.Count == 4)))
                            _character.OnVarset(_character, Args[0].Value, Args[1].Value,
                                (Args.Count >= 3) ? Args[2].Value : null,
                                (Args.Count == 4) ? Args[3].Value : null);
                        else if (Args.Count == 3 ||
                                 (Args[1].Value.ToLower() == "skill" && (Args.Count == 4 || Args.Count == 5)))
                        {
                            if (Args[0].Value.ToLower() == "pet")
                                _character.OnPetVarset(Args[1].Value, Args[2].Value, true);
                            else
                            {
                                var Player = _character.Field.FindUser(Args[0].Value);
                                if (Player != null)
                                    Player.OnVarset(_character, Args[1].Value, Args[2].Value,
                                        (Args.Count >= 4) ? Args[3].Value : null,
                                        (Args.Count == 5) ? Args[4].Value : null);
                                else
                                    SendNotice($"Unable to find {Args.Args[0].Value}");
                            }
                        }
                        else if (Args.Args.Count == 3)
                        {
                            var Player = _character.Field.FindUser(Args[0].Value);
                            if (Player != null && Args[1].Value.ToLower() == "pet")
                                Player.OnPetVarset(Args[2].Value, Args[3].Value, false);
                            else
                                SendNotice("Unable to find the user or pet");
                        }
                        else
                            SendNotice("Too many or not enough args!");

                        return true;
                    }

                #endregion

                #region NpcVarSet / NpcVarGet

                case "npcvarset":
                    {
                        if (Args.TryGetInt32(0, out var templateID) &&
                            Args.TryGetString(1, out var variable) &&
                            Args.TryGetString(2, out var value))
                        {
                            if (_character.Field.SetNpcVar(templateID, variable, value))
                            {
                                SendNotice($"Updated {variable} on npc {templateID} to {value}");
                            }
                            else
                            {
                                SendRed($"Unable to updated {templateID}, not found in current map?");
                            }
                        }
                        else
                        {
                            SendNotice($"Usage: {Args.Command} <templateid> <variable> <value>");
                        }

                        return true;
                    }

                case "npcvarget":
                    {
                        if (Args.TryGetInt32(0, out var templateID) &&
                            Args.TryGetString(1, out var variable))
                        {
                            if (_character.Field.GetNpcVar(templateID, variable, out var value))
                            {
                                SendNotice($"{variable} on npc {templateID} is set to {value}");
                            }
                            else
                            {
                                SendRed($"Unable to find npc {templateID} in current map");
                            }
                        }
                        else
                        {
                            SendNotice($"Usage: {Args.Command} <templateid> <variable>");
                        }

                        return true;
                    }

                #endregion

                #region QuestRecordSet

                case "qrget":
                    {
                        if (Args.Count == 2)
                        {
                            var other = Args[0].Value;
                            var questId = Args[1].GetInt32();
                            var otherChar = Server.Instance.GetCharacter(other);
                            if (otherChar == null && other == "me") otherChar = _character;
                            if (otherChar != null)
                            {
                                SendNotice($"QR for {questId}: {otherChar.Quests.GetQuestData(questId)}");

                                return true;
                            }

                            SendRed($"Victim {other} not found.");

                            return true;
                        }

                        SendNotice("Usage: !qrget <charactername or id> <questid>");
                        return true;
                    }
                case "qrset":
                    {
                        if (Args.Count == 3)
                        {
                            var other = Args[0].Value;
                            var questId = Args[1].GetInt32();
                            var otherChar = Server.Instance.GetCharacter(other);
                            if (otherChar == null && other == "me") otherChar = _character;
                            if (otherChar != null)
                            {
                                otherChar.Quests.SetQuestData(questId, Args[2].Value);
                                SendNotice($"Set QR for {questId} to {otherChar.Quests.GetQuestData(questId)}");

                                return true;
                            }

                            SendRed($"Victim {other} not found.");

                            return true;
                        }

                        SendNotice("Usage: !qrset <charactername or id> <questid> <questdata>");
                        return true;

                        break;
                    }
                case "qrdel":
                case "qrremove":
                case "qrunset":
                    {
                        if (Args.Count == 2)
                        {
                            var other = Args[0].Value;
                            var questId = Args[1].GetInt32();
                            var otherChar = Server.Instance.GetCharacter(other);
                            if (otherChar == null && other == "me") otherChar = _character;
                            if (otherChar != null)
                            {
                                otherChar.Quests.RemoveQuest(questId);
                                SendNotice($"Removed QR for {questId}");

                                return true;
                            }

                            SendRed($"Victim {other} not found.");

                            return true;
                        }

                        SendNotice($"Usage: {Args.Command} <charactername or id> <questid>");
                        return true;

                        break;
                    }

                #endregion

                #region FieldsetVarSet

                case "fsnokick":
                    {
                        var FieldSet = _character.Field.ParentFieldSet;

                        if (FieldSet == null)
                        {
                            SendNotice("Not in a fieldset!");
                            return true;
                        }

                        Args.TryGetBool(0, out var noKick);

                        FieldSet.SetVar("nokick", noKick ? "1" : "0");
                        SendNotice("Fieldset player limit check " + (noKick ? "deactivated" : "active"));
                        return true;
                    }

                case "fsvarset":
                case "fsvarget":
                    {
                        var FieldSet = _character.Field.ParentFieldSet;

                        if (FieldSet == null)
                        {
                            SendNotice("Not in a fieldset!");
                            return true;
                        }

                        if (Args.Command.ToLowerInvariant().EndsWith("set") && Args.Count == 2)
                        {
                            FieldSet.SetVar(Args[0].Value, Args[1].Value);
                            SendNotice($"{Args[0].Value} set to {Args[1].Value}");
                        }
                        else if (Args.Command.ToLowerInvariant().EndsWith("get"))
                        {
                            if (Args.Count == 1)
                            {
                                var variable = FieldSet.GetVar(Args[0].Value);
                                if (variable == null)
                                {
                                    SendNotice($"Variable {Args[0].Value} does not exist in fieldset.");
                                    return true;
                                }

                                SendNotice($"{Args[0].Value}: {variable}");
                                return true;
                            }

                            foreach (var variable in FieldSet.Variables)
                            {
                                SendNotice($"{variable.Key}: {variable.Value}");
                            }
                        }

                        return true;
                    }

                #endregion

                #region d / delete

                case "d":
                case "delete":
                    {
                        if (Args.Count == 1 && Args[0].IsNumber())
                        {
                            var inv = Args[0].GetByte();

                            if (inv <= 4)
                            {
                                // Find first item to delete
                                var slot = _character.Inventory.DeleteFirstItemInInventory(inv);
                                if (slot != 0)
                                {
                                    // Update the inventory of the client
                                    InventoryPacket.SwitchSlots(_character, slot, 0, (byte)(inv + 1));
                                }
                                else
                                {
                                    SendNotice("No item to delete found.");
                                }

                                return true;
                            }
                        }

                        SendNotice("Usage: !delete <inventory, 0=equip, 1=use, etc>");
                        return true;
                    }

                #endregion

                #region ClearDrops

                case "cleardrops":
                    {
                        _character.Field.DropPool.Clear();
                        return true;
                    }

                #endregion

                #region dropinfo

                case "dropinfo":
                    {
                        if (Args.Count == 2)
                        {
                            var type = Args[0];
                            var id = Args[1].GetInt32();
                            var messageLines = new List<string>();

                            string formatDrop(DropData dt)
                            {
                                var percent = (dt.Chance / DropData.DropChanceCalcFloat);
                                var percentText = (percent * 100.0).ToString("0.########") + "%";
                                percentText = $"1 of {(1 / percent):0.##} drops";
                                var premiumText = dt.Premium ? " (premium)" : "";
                                var limitedName = !string.IsNullOrWhiteSpace(dt.LimitedName) ? $" (limitedname {dt.LimitedName})" : "";
                                if (dt.Mesos > 0)
                                    return $"Mesos {IScriptV2.number(dt.Mesos)} @ {percentText}" + premiumText + limitedName;
                                else
                                {
                                    var minmax = dt.Min == 0 && dt.Max == 0 ? "" : $" ({dt.Min} - {dt.Max})";
                                    return $"{IScriptV2.itemIconAndName(dt.ItemID)} ({dt.ItemID}) {minmax} @ {percentText}" + premiumText + limitedName;
                                }
                            }

                            if (type == "mob")
                            {
                                if (DataProvider.Drops.TryGetValue(id, out var drops))
                                {
                                    messageLines.Add($"Drops for {IScriptV2.mobName(id)}");
                                    foreach (var dt in drops.OrderBy(x => x.Chance))
                                    {
                                        messageLines.Add(formatDrop(dt));
                                    }
                                }
                                else
                                {
                                    SendNotice("Unknown mob!");
                                    return true;
                                }
                            }
                            else if (type == "item")
                            {
                                var found = false;

                                foreach (var kvp in DataProvider.Drops)
                                {
                                    foreach (var dt in kvp.Value.Where(x => x.Mesos == 0 && x.ItemID == id).OrderBy(x => x.Chance))
                                    {
                                        found = true;
                                        messageLines.Add($"{IScriptV2.mobName(kvp.Key)} ({kvp.Key}): {formatDrop(dt)}");
                                    }
                                }

                                if (!found)
                                {
                                    SendNotice("Unknown item or item not dropped!");
                                    return true;
                                }
                            }

                            if (messageLines.Count > 0)
                            {
                                NpcPacket.SendNPCChatTextSimple(_character, Constants.MapleAdminNpc, string.Join(IScriptV2.Newline, messageLines), NpcChatSimpleTypes.OK);
                                return true;
                            }
                        }


                        SendNotice($"Command syntax: !{Args.Command} (mob|item) {{id}}");
                        return true;
                    }

                #endregion

                #region globaldrops
                case "globaldrops":
                    {
                        List<string> messageLines = ["Global drops"];
                        foreach (var dt in DataProvider.GlobalDrops)
                        {
                            var percent = (dt.Chance / DropData.DropChanceCalcFloat);
                            var percentText = (percent * 100.0).ToString("0.########") + "%";
                            percentText = $"1 of {(1 / percent):0.##} drops";
                            var premiumText = dt.Premium ? " (premium)" : "";
                            var mobLevel = (dt.MobMinLevel > 0 || dt.MobMaxLevel < int.MaxValue) ? $" (mob lv. {(dt.MobMinLevel > 0 ? dt.MobMinLevel : "")}-{(dt.MobMaxLevel < int.MaxValue ? dt.MobMaxLevel : "")})" : "";
                            var limitedName = !string.IsNullOrWhiteSpace(dt.LimitedName) ? $" (limitedname {dt.LimitedName})" : "";
                            var expire = dt.DateExpire < DateTime.MaxValue ? $" (expires on {dt.DateExpire})" : "";
                            if (dt.Mesos > 0)
                            {
                                messageLines.Add($"Mesos {IScriptV2.number(dt.Mesos)} @ {percentText}" + premiumText + mobLevel + limitedName + expire);
                            }
                            else
                            {
                                var minmax = dt.Min == 0 && dt.Max == 0 ? "" : $" ({dt.Min} - {dt.Max})";
                                messageLines.Add($"{IScriptV2.itemIconAndName(dt.ItemID)} ({dt.ItemID}) {minmax} @ {percentText}" + premiumText + mobLevel + limitedName + expire);
                            }
                        }

                        NpcPacket.SendNPCChatTextSimple(_character, Constants.MapleAdminNpc, string.Join(IScriptV2.Newline, messageLines), NpcChatSimpleTypes.OK);
                        return true;
                    }
                #endregion

                #region KillMobs / KillAll

                case "killmobs":
                case "killall":
                    {
                        Args.TryGetBool(0, out var withDrops, false);
                        Args.TryGetBool(1, out var forced, true);
                        var amount = _character.Field.KillAllMobs(0, withDrops ? _character : null, DropOwnType.Explosive_NoOwn, forced);
                        SendNotice("Amount of mobs killed: " + amount);
                        return true;
                    }

                #endregion

                #region KillMobsDMG

                case "killalldmg":
                case "killmobsdmg":
                    {
                        Args.TryGetInt32(0, out var dmg, 0);
                        Args.TryGetBool(1, out var withDrops, false);
                        Args.TryGetBool(2, out var forced, true);
                        var amount = _character.Field.KillAllMobs(dmg, withDrops ? _character : null, DropOwnType.Explosive_NoOwn, forced);
                        SendNotice("Amount of mobs killed: " + amount);
                        return true;
                    }

                #endregion

                #region dmgall

                case "dmgall":
                    {
                        Args.TryGetInt32(0, out var dmg, 1337);
                        foreach (var mob in _character.Field.Mobs.Values.ToArray())
                        {
                            MobPacket.SendMobDamageOrHeal(mob, dmg, false, false);
                            mob.GiveDamage(_character, dmg, AttackPacket.AttackTypes.Magic, false);
                        }
                        return true;
                    }

                #endregion

                #region MapNotice

                case "mapnotice":
                    {
                        if (Args.Count > 0)
                            MessagePacket.SendText(MessagePacket.MessageTypes.PopupBox,
                                $"[{_character.Name}] : {Args.CommandText}", _character,
                                MessagePacket.MessageMode.ToMap);
                        return true;
                    }

                #endregion

                #region reloadeventdate

                case "reloadeventdate":
                case "reloadlimitedname":
                    {
                        SendNotice("Reloading events...");
                        EventDateMan.ReloadEvents();
                        SendNotice("Done... (see server log for info)");
                        return true;
                    }

                #endregion

                #region events

                case "events":
                case "listevents":
                    {
                        SendNotice("Event info:");
                        foreach (var eventName in EventDateMan.GetEventNames())
                        {
                            var (startDate, endDate) = EventDateMan.GetEventData(eventName).Value;
                            SendNotice($"{eventName}: from {startDate} to {endDate}, active: {EventDateMan.IsEventActive(eventName)}");
                        }
                        return true;
                    }

                #endregion

                #region ditto/datto

                case "ditto":
                    {
                        if (Args.TryGetString(0, out var characterName))
                        {
                            var charid = Server.Instance.CharacterDatabase.CharacterIdByName(characterName);
                            if (charid == null)
                            {
                                if (int.TryParse(characterName, out var tmp) == false)
                                {
                                    SendNotice($"Character {characterName} not found??");
                                    return true;
                                }

                                var tmpCharname = Server.Instance.CharacterDatabase.CharacterNameById(tmp);
                                if (tmpCharname == null)
                                {
                                    SendNotice($"Character {characterName} not found??");
                                    return true;
                                }

                                characterName = tmpCharname;

                                charid = tmp;
                            }

                            RedisBackend.Instance.SetImitateID(_character.ID, charid.Value);
                            MessagePacket.SendNoticeGMs($"[{_character.Name}] Imitating character {characterName}.",
                                MessagePacket.MessageTypes.RedText);
                            // CC
                            _character.Player.Socket.DoChangeChannelReq(Server.Instance.ID);
                            return true;
                        }

                        SendNotice("Usage: !ditto <charactername or id>");
                        return true;
                    }

                case "datto":
                    {
                        if (_character.ImitatedName == null)
                        {
                            SendRed("You are already yourself.");
                        }
                        else
                        {
                            RedisBackend.Instance.SetImitateID(_character.ID, 0);
                            MessagePacket.SendNoticeGMs(
                                $"[{_character.Name}] Stopped imitating {_character.ImitatedName}. Glad to have you back",
                                MessagePacket.MessageTypes.RedText);
                            _character.Player.Socket.DoChangeChannelReq(Server.Instance.ID);
                        }

                        return true;
                    }

                #endregion

                #region memoryautoban

                case "memoryautoban":
                    {
                        if (Args.Count > 0)
                        {
                            Server.Instance.MemoryAutobanEnabled = Args[0].GetBool();
                        }

                        SendNotice($"Memory Autoban enabled: {Server.Instance.MemoryAutobanEnabled}. Toggle with !memoryautoban <true|false>");
                        return true;
                    }

                #endregion

                #region Relog

                case "relog":
                case "reconnect":
                    _character.Player.Socket.DoChangeChannelReq(Server.Instance.ID);
                    return true;

                #endregion

                #region Notice

                case "notice":
                    {
                        if (Args.Count > 0)
                        {
                            MessagePacket.SendText(MessagePacket.MessageTypes.Notice, Args.CommandText, null,
                                MessagePacket.MessageMode.ToChannel);
                        }

                        return true;
                    }

                #endregion

                #region SetSP

                case "setsp":
                    {
                        if (Args.Count > 1 && Args[0].IsNumber())
                        {
                            var SkillID = Args[0].GetInt32();
                            byte Level = 1;
                            var MaxLevel = (byte)(DataProvider.Skills.TryGetValue(SkillID, out var sd)
                                ? sd.MaxLevel
                                : 0);

                            if (MaxLevel > 0)
                            {
                                if (Args[1] == "max")
                                    Level = MaxLevel;
                                else if (Args[1].IsNumber())
                                    Level = Args[1].GetByte();
                                else
                                    Level = 1;

                                _character.Skills.SetSkillPoint(SkillID, Level);
                            }
                            else
                                SendNotice("Skill not found.");
                        }

                        return true;
                    }

                #endregion

                #region Job

                case "job":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            _character.SetJob(Args[0].GetInt16());
                        return true;
                    }

                #endregion

                #region Heal

                case "heal":
                    {
                        var hpHealed = _character.PrimaryStats.GetMaxHP() - _character.PrimaryStats.HP;
                        _character.ModifyHP(_character.PrimaryStats.GetMaxHP());
                        _character.ModifyMP(_character.PrimaryStats.GetMaxMP());
                        // CharacterStatsPacket.SendCharacterDamage(character, 0, -hpHealed, 0, 0, 0, 0, null);
                        return true;
                    }

                #endregion

                #region AP

                case "ap":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            _character.SetAP(Args[0].GetInt16());
                        return true;
                    }

                #endregion

                #region SP

                case "sp":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            _character.SetSP(Args[0].GetInt16());
                        return true;
                    }

                #endregion

                #region AddSP

                case "addsp":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            _character.AddSP(Args[0].GetInt16());
                        return true;
                    }

                #endregion

                #region GiveEXP

                case "giveexp":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            _character.AddEXP(Args[0].GetInt32());
                        return true;
                    }

                #endregion

                #region Mesos

                case "mesos":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            _character.SetMesos(Args[0].GetInt32());
                        return true;
                    }

                #endregion

                #region pton/ptoff

                case "pton":
                case "ptoff":
                    {
                        if (Args.Count > 0)
                        {
                            if (!_character.Field.Portals.TryGetValue(Args[0], out var pt))
                            {
                                SendNotice("Portal not found.");
                            }
                            else
                            {
                                var enabled = pt.Enabled = Args.Command.ToLowerInvariant() == "pton";
                                SendNotice("Portal " + Args[0] + " is now " + (enabled ? "enabled" : "disabled"));
                            }
                        }

                        return true;
                    }

                #endregion

                #region portals

                case "portals":
                    {
                        var portalsInRange = _character.Field.Portals.Values
                            .OrderBy(x => new Pos(x.X, x.Y) - _character.Position).Take(3).ToArray();
                        if (portalsInRange.Length == 0)
                        {
                            SendNotice("No portals found.");
                        }
                        else
                        {
                            foreach (var portal in portalsInRange)
                            {
                                SendNotice($"Portal '{portal.Name}' id {portal.ID} script '{portal.Script}' enabled {portal.Enabled} Distance {new Pos(portal.X, portal.Y) - _character.Position} ToMap {portal.ToMapID} ToName {portal.ToName} Type {portal.Type}");
                            }
                        }

                        return true;
                    }

                #endregion

                //Event Stuff

                #region EventReset

                case "eventreset":
                    {
                        var ytd = new DateTime(2010, 1, 1);
                        Server.Instance.CharacterDatabase.RunQuery("UPDATE characters SET event = @eventDate WHERE ID = @charid", "@charid", _character.ID, "@eventDate",  ytd.ToString("yyyy-MM-dd HH:mm:ss"));
                        SendNotice("Reset event participation time.");
                        return true;
                    }

                #endregion

                #region EventHelp

                case "oldevent":
                case "oldevents":
                case "oldeventhelp":
                    {
                        var HelpMessages = new List<string>
                    {
                        "============= GM Event Help Guide =============",
                        "Each event has its own help guide that can be brought up via command, and a lobby map.",
                        "In any event lobby map, use !eventdesc to display an event description message to all players.",
                        " ",
                        "AVAILABLE EVENTS:",
                        "Find the Jewel. Help: !ftjhelp Map: /map jewel",
                        "Snowball. Help: !snowballhelp Map: /map snowball for event map, !map 109060001 for lobby",
                        "Fitness. Help: !fitnesshelp Map: /map fitness",
                        "Quiz. Help: !quizhelp Map: /map quiz"
                    };

                        HelpMessages.ForEach(m => SendNotice(m));
                        return true;
                    }

                #endregion

                #region eventdesc

                case "eventdesc":
                    MapPacket.SendGMEventInstructions(_character.Field);
                    SendNotice("Sent event description to everybody");
                    return true;

                #endregion

                #region Find The Jewel

                case "ftjhelp":
                    {
                        var HelpMessages = new List<string>
                    {
                        "============= Find The Jewel GM Help Guide =============",
                        "Treasure scroll item ID: 4031018. Devil Scroll: 4031019. Entry map is 109010000. Other maps are 109010100-3, 109010200-3, 109010300-3, 109010400-3.",
                        "Use '!spawn <mobid>' to create mobs at your location. Mob IDs: Super slime - 9100000, Super Jr. Necki - 9100001, Super Stirge - 9100002.",
                        "Spawn reactors by moving and using '!ftjreactorhere <id> <jewel>', where <id> is the reactor id, and <jewel> indicates if it contains the treasure: 1 for treasure, 0 for nothing",
                        "Make sure id does not exceed map limit, or client will crash. Example: !ftjreactorhere 1 1 places a reactor with id 1 and contains the treasure",
                        "Big maps will have 20 reactors (rid's 0 - 19) and small maps have 2 (rid's 0 - 1). Going past these limits will crash everything and require a server reboot",
                        "Use !ftjenable to allow players to enter the entry map (via NPC Billy) before the event starts.",
                        "Use !ftjstart to enable the portals, disably entry, and start the event. It will stop automatically when the timer runs out. Stop early with !ftjstop.",
                        "Tip: From here, the viking NPCs will take care of the rest. It may be worth going into hide and going into maps in case some try to cheat/hack.",
                        "Tip: Use AdminFly to get to platforms for mob and reactor placement, BUT, make sure to turn it off and land on the platform before placing things.",
                        "Tip: For the most authentic experience, stirges only go in the kerning map, while slimes and necki go in the others. Stirge in hene maps looks weird.",
                        "Tip: It is recommended (and GMS-like) to run the event with more than 1 hidden jewel. Put a handful, like 5-15."
                    };

                        HelpMessages.ForEach(m => SendNotice(m));
                        return true;
                    }

                case "ftjenable":
                    {
                        var jewelEvent = EventManager.Instance.EventInstances[EventType.Jewel];
                        if (jewelEvent.InProgress)
                        {
                            MessagePacket.SendNoticeGMs("FTJ already in progress. Did not enable entry!");
                        }
                        else
                        {
                            MessagePacket.SendNoticeGMs("Enabled joining FTJ. Portals Disabled until start.");
                            jewelEvent.Prepare();
                        }

                        return true;
                    }

                case "ftjstart":
                    {
                        var jewelEvent = EventManager.Instance.EventInstances[EventType.Jewel];
                        if (jewelEvent.InProgress)
                        {
                            MessagePacket.SendNoticeGMs("FTJ already in progress. Did not start a new one!");
                        }
                        else
                        {
                            MessagePacket.SendNoticeGMs(
                                "Started FTJ. Portals enabled, and outsiders can no longer join the event.");
                            jewelEvent.Start();
                        }

                        return true;
                    }

                case "ftjstop":
                    {
                        MessagePacket.SendNoticeGMs(
                            "Stopped FTJ early. Kicking everyone if event was in progress...");
                        var jewelEvent = EventManager.Instance.EventInstances[EventType.Jewel];
                        jewelEvent.Stop();
                        return true;
                    }

                case "ftjreactorhere":
                    {
                        if (Args.Count < 2)
                        {
                            SendNotice("Usage: !ftjreactorhere <reactor id> <jewel>, <jewel> = 0 for no treasure or 1 for treasure");
                        }
                        else
                        {
                            int maxFTJReactors()
                            {
                                switch (_character.MapID)
                                {
                                    case 109010100:
                                    case 109010200:
                                    case 109010300:
                                    case 109010400:
                                        return 20;
                                    case 109010101:
                                    case 109010102:
                                    case 109010103:
                                    case 109010201:
                                    case 109010202:
                                    case 109010203:
                                    case 109010301:
                                    case 109010302:
                                    case 109010303:
                                    case 109010401:
                                    case 109010402:
                                    case 109010403:
                                        return 1;
                                    default:
                                        return -1;
                                }
                            }

                            int rid = short.Parse(Args[0]);
                            if (rid > maxFTJReactors() || rid < 0)
                            {
                                SendNotice("Exceeded max reactor limit for this map!!! Did not place.");
                                return true;
                            }

                            if (!_character.Field.Reactors.TryGetValue(rid, out var reactor))
                            {
                                SendRed($"Cannot find reactor with ID {rid}");
                                return true;
                            }

                            reactor.X = _character.Position.X;
                            reactor.Y = _character.Position.Y;
                            reactor.Enabled = true;
                            reactor.Respawn();

                            if (int.Parse(Args[1]) == 1)
                            {
                                reactor.Rewards.Clear();
                                reactor.Rewards.Add(new DropData
                                {
                                    Chance = int.MaxValue,
                                    ItemID = 4031018,
                                    Max = 1,
                                    Min = 1,
                                });
                            }
                        }

                        return true;
                    }

                #endregion

                #region Fitness

                case "fitnesshelp":
                case "fithelp":
                    {
                        var HelpMessages = new List<string>
                    {
                        "============= Fitness Event GM Help Guide =============",
                        "1. Use !fitenable or !fitnessenable to allow entry to the hub map via event NPCs.",
                        "2. Use !fitstart or !fitnessstart to begin the event. Characters are warped to the starting spot automatically",
                        "3. To stop early, use !fitstop or !fitnessstop. Otherwise, the event will run until",
                        "the timer runs out. All who make it past stage 4 are automatically taken to the victory map by the portal."
                    };

                        HelpMessages.ForEach(m => SendNotice(m));
                        return true;
                    }

                case "fitnessenable":
                case "fitenable":
                    {
                        var fitnessEvent = EventManager.Instance.EventInstances[EventType.Fitness];
                        if (fitnessEvent.InProgress)
                        {
                            MessagePacket.SendNoticeGMs("Fitness already in progress. Did not enable entry!");
                        }
                        else
                        {
                            MessagePacket.SendNoticeGMs(
                                "Enabled joining Fitness. Portals Disabled until start.");
                            fitnessEvent.Prepare();
                        }

                        return true;
                    }

                case "fitnessstart":
                case "fitstart":
                    {
                        var fitnessEvent = EventManager.Instance.EventInstances[EventType.Fitness];
                        if (fitnessEvent.InProgress)
                        {
                            MessagePacket.SendNoticeGMs(
                                "Fitness already in progress. Did not start a new one!");
                        }
                        else
                        {
                            MessagePacket.SendNoticeGMs(
                                "Started Fitness. Portals enabled, and outsiders can no longer join the event.");
                            fitnessEvent.Start();
                        }

                        return true;
                    }

                case "fitnessstop":
                case "fitstop":
                    {
                        var fitnessEvent = EventManager.Instance.EventInstances[EventType.Fitness];
                        fitnessEvent.Stop();
                        MessagePacket.SendNoticeGMs(
                            "Stopped Fitness early. Kicking everyone if event was in progress.");
                        return true;
                    }

                #endregion

                #region Ola Ola

                case "olaolahelp":
                case "olahelp":
                    {
                        var HelpMessages = new List<string>
                    {
                        "============= Ola Ola Event GM Help Guide =============",
                        "1. Use !olaenable or !olaolaenable followed by number between 1 and 5 to",
                        "   allow entry to the hub map via event NPCs.",
                        "2. Use !olastart or !olaolastart with the same number to begin the event.",
                        "   Characters are warped to the starting spot automatically.",
                        "3. To stop early, use !olastop or !olaolastop with the same number.",
                        "   Otherwise, the event will run until the timer runs out.",
                        "All who make it past stage 3 are automatically taken to the victory map by the portal."
                    };

                        HelpMessages.ForEach(m => SendNotice(m));
                        return true;
                    }

                case "olaolaenable":
                case "olaenable":
                    {
                        if (Args.Count == 1 && Args[0].IsNumber())
                        {
                            var olaEventType = Args[0].Value switch
                            {
                                "1" => EventType.Ola1,
                                "2" => EventType.Ola2,
                                "3" => EventType.Ola3,
                                "4" => EventType.Ola4,
                                "5" => EventType.Ola5,
                                _ => throw new ArgumentOutOfRangeException()
                            };

                            var olaEvent = EventManager.Instance.EventInstances[olaEventType];

                            if (olaEvent.InProgress)
                            {
                                MessagePacket.SendNoticeGMs(
                                    $"Ola Ola {Args[0]} already in progress. Did not enable entry!");
                            }
                            else
                            {
                                MessagePacket.SendNoticeGMs(
                                    $"Enabled joining Ola Ola {Args[0]}. Portals Disabled until start.");
                                olaEvent.Prepare();
                            }
                        }
                        else
                        {
                            MessagePacket.SendNoticeGMs(
                                "Please choose a stage variation. Use !olaolahelp for more info.");
                        }

                        return true;
                    }

                case "olaolastart":
                case "olastart":
                    {
                        if (Args.Count == 1 && Args[0].IsNumber())
                        {
                            var olaEventType = Args[0].Value switch
                            {
                                "1" => EventType.Ola1,
                                "2" => EventType.Ola2,
                                "3" => EventType.Ola3,
                                "4" => EventType.Ola4,
                                "5" => EventType.Ola5,
                                _ => throw new ArgumentOutOfRangeException()
                            };

                            var olaEvent = EventManager.Instance.EventInstances[olaEventType];

                            if (olaEvent.InProgress)
                            {
                                MessagePacket.SendNoticeGMs(
                                    $"Ola Ola {Args[0]} already in progress. Did not start a new one!");
                            }
                            else
                            {
                                MessagePacket.SendNoticeGMs(
                                    $"Started Ola Ola {Args[0]}. Portals enabled, and outsiders can no longer join the event.");
                                olaEvent.Start();
                            }
                        }
                        else
                        {
                            MessagePacket.SendNoticeGMs(
                                "Please choose a stage variation. Use !olaolahelp for more info.");
                        }

                        return true;
                    }

                case "olaolastop":
                case "olastop":
                    {
                        if (Args.Count == 1 && Args[0].IsNumber())
                        {
                            var olaEventType = Args[0].Value switch
                            {
                                "1" => EventType.Ola1,
                                "2" => EventType.Ola2,
                                "3" => EventType.Ola3,
                                "4" => EventType.Ola4,
                                "5" => EventType.Ola5,
                                _ => throw new ArgumentOutOfRangeException()
                            };

                            var olaEvent = EventManager.Instance.EventInstances[olaEventType];
                            olaEvent.Stop();
                            MessagePacket.SendNoticeGMs(
                                $"Stopped Ola Ola {Args[0]} early. Kicking everyone if event was in progress.");
                        }
                        else
                        {
                            MessagePacket.SendNoticeGMs(
                                "Please choose a stage variation. Use !olaolahelp for more info.");
                        }

                        return true;
                    }

                #endregion

                #region Cash

                case "givemapcash":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                        {
                            _character.Field.ForEachCharacters(c =>
                            {
                                c.AddCash(Args[0].GetInt32(), $"Map Cash given by {_character.Name}");
                            });

                            SendRed($"Gave the map {Args[0].GetInt32()} cash.");
                        }

                        return true;
                    }
                case "givemapmaplepoints":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                        {
                            _character.Field.ForEachCharacters(c =>
                            {
                                c.AddMaplePoints(Args[0].GetInt32(), $"Map Cash given by {_character.Name}");

                            });
                            SendRed($"Gave the map {Args[0].GetInt32()} maple points.");
                        }

                        return true;
                    }

                case "givemaplepoints":
                case "givecash":
                    {
                        if (Args.Count >= 2)
                        {
                            var victim = Args[0].Value.ToLower();
                            var who = Server.Instance.GetCharacter(victim);

                            if (who != null)
                            {
                                var points = Args[1].GetInt32();
                                if (Args.Command.ToLowerInvariant() == "givemaplepoints")
                                    who.AddMaplePoints(points, $"Maple Points given by {_character.Name}");
                                else
                                    who.AddCash(points, $"Cash given by {_character.Name}");

                                SendRed($"Gave {who.Name} {points} cash/maple points.");
                            }
                            else
                            {
                                SendRed("You have entered an incorrect name.");
                            }
                        }

                        return true;
                    }

                #endregion

                #region Credits

                case "givecredits":
                    {
                        if (Args.Count >= 4)
                        {
                            var victim = Args[0].Value.ToLower();
                            var who = Server.Instance.GetCharacter(victim);

                            if (who != null)
                            {
                                var rate = Args[1].GetDouble();
                                var typeStr = Args[2];
                                var credit = Args[3].GetInt32();
                                var comment = string.Join(" ", Args.Args.Skip(4));

                                var type = RateCredits.TypeFromString(typeStr);
                                if (type != null)
                                {
                                    comment = $"[{_character.Name} gave credits] {comment}";
                                    who.RateCredits.AddCredits(
                                        type.Value,
                                        credit,
                                        rate,
                                        comment
                                    );

                                    SendNotice($"Gave {who} {rate}x {type} for {credit} with message: {comment}");
                                    return true;
                                }
                            }
                            else
                            {
                                SendRed("You have entered an incorrect name.");
                                return true;
                            }
                        }

                        SendRed($"Usage: {Args.Command} <charname> <rate> <exp|mesos|drop> <amount> <reason...>");
                        SendRed($"Example: {Args.Command} diamondo25 1.5 exp 1000 Because boostgate");

                        return true;
                    }

                case "givecreditsmap":
                    {
                        if (Args.Count >= 4)
                        {
                            var rate = Args[0].GetDouble();
                            var typeStr = Args[1];
                            var credit = Args[2].GetInt32();
                            var comment = string.Join(" ", Args.Args.Skip(3));

                            var type = RateCredits.TypeFromString(typeStr);
                            if (type != null)
                            {
                                comment = $"[{_character.Name} gave credits in map {_character.MapID}] {comment}";
                                _character.Field.ForEachCharacters(x =>
                                {
                                    x.RateCredits.AddCredits(
                                        type.Value,
                                        credit,
                                        rate,
                                        comment
                                    );
                                });

                                SendNotice($"Gave everyone in the map {rate}x {type} for {credit} with message: {comment}");
                                return true;
                            }
                        }

                        SendRed($"Usage: {Args.Command} <rate> <exp|mesos|drop> <amount> <reason...>");
                        SendRed($"Example: {Args.Command} 1.5 exp 1000 Because boostgate");

                        return true;
                    }

                #endregion

                #region Fieldset

                case "fsoff":
                    {
                        FieldSet.DisabledForMaintenance = true;
                        SendNotice("Fieldsets are now disabled.");
                        return true;
                    }
                case "fson":
                    {
                        FieldSet.DisabledForMaintenance = false;
                        SendNotice("Fieldsets are now enabled.");

                        return true;
                    }
                case "fsfinish":
                    {
                        FieldSet.Instances.ForEach(x => x.Value.End());
                        SendNotice("Fieldsets are ended..");

                        return true;
                    }
                case "fsinfo":
                    {
                        Args.TryGetString(0, out var fsName);
                        foreach (var kvp in FieldSet.Instances)
                        {
                            if (fsName != null && kvp.Key != fsName) continue;
                            var fs = kvp.Value;
                            SendNotice($"[{kvp.Key}] Available: {FieldSet.IsAvailable(kvp.Key)}, UserCount {fs.UserCount}, Started: {fs.FieldSetStart}");
                            SendNotice($"- occupied timer {fs.OccupiedTimer}");
                            SendNotice($"- enter timer {fs.EnterTimer}");
                            SendNotice($"- time out timer {fs.TimeOutTimer}");
                        }

                        return true;
                    }

                case "fssettimeout":
                case "fstimeout":
                    {
                        Args.TryGetInt32(0, out var secondsLeft, 5);
                        _character.Field.ParentFieldSet?.ResetTimeOut(TimeSpan.FromSeconds(secondsLeft));
                        SendNotice($"Reduced timeout to {secondsLeft} seconds");

                        return true;
                    }

                #endregion


                case "dcall":
                    {
                        foreach (var c in Server.Instance.CharacterList.Values)
                        {
                            if (c == _character) continue;
                            try
                            {
                                c.Disconnect();
                            }
                            catch
                            {
                                SendNotice($"Unable to dc {c}");
                            }
                        }

                        return true;
                    }

                case "reactors":
                    {
                        _character.Field.Reactors.ForEach(r =>
                            SendNotice($"Reactor {r.Key} ({r.Value.Template.ID}): name {r.Value.Name}, state {r.Value.State}, page {r.Value.PageID}, piece {r.Value.PieceID}")
                        );
                        return true;
                    }
                case "nextreactorstate":
                    {
                        if (Args.Count > 1 &&
                            Args[0].TryGetInt32(out var reactorIdx) &&
                            Args[1].TryGetByte(out var eventId))
                        {
                            if (_character.Field.Reactors.TryGetValue(reactorIdx, out var reactor))
                            {
                                SendRed("Invoking next state");
                                reactor.SetStateByEvent(eventId, 0);
                            }
                            else
                            {
                                SendRed("Reactor not found");
                            }
                        }
                        else
                        {
                            SendRed($"Invalid args. {Args.Command} [reactoridx] [event]");
                        }

                        return true;
                    }
            }

            return false;
        }



        private static bool HandleGMCommandLevel3()
        {

            switch (Args.Command.ToLowerInvariant())
            {
                #region Shutdown

                case "shutdown":
                    {
                        if (!shuttingDown)
                        {
                            var len = 10;
                            if (Args.Count > 0 && Args[0].IsNumber())
                            {
                                len = Args[0].GetInt32();
                                if (len == 0)
                                    len = 10;
                            }

                            SendRed(string.Format("Shutting down in {0} seconds", len));

                            MasterThread.RepeatingAction.Start("Shutdown Thread",
                                a => Program.MainForm.Shutdown(),
                                TimeSpan.FromSeconds(len), TimeSpan.Zero);
                            shuttingDown = true;
                            return true;
                        }

                        SendRed("Unable to shutdown now!");

                        return true;
                    }

                #endregion

                #region Clock

                case "clock":
                    {
                        if (Args.Count > 0 && Args[0].IsNumber())
                            MapPacket.ShowMapTimerForMap(_character.Field, Args[0].GetInt32());
                        return true;
                    }

                #endregion

                #region Header

                case "header":
                    {
                        var txt = Args.Count == 0 ? "" : Args.CommandText;
                        Server.Instance.SetScrollingHeader(txt);
                        return true;
                    }

                case "headernotice":
                case "noticeheader":
                    {
                        if (Args.Count > 0)
                        {
                            var txt = Args.CommandText;
                            Server.Instance.SetScrollingHeader(txt);
                            MessagePacket.SendText(
                                MessagePacket.MessageTypes.Notice,
                                txt,
                                null,
                                MessagePacket.MessageMode.ToChannel
                            );
                        }

                        return true;
                    }

                #endregion

                #region Packet

                case "packet":
                    {
                        if (Args.Count > 0)
                        {
                            var pw = new Packet();
                            pw.WriteHexString(Args.CommandText);
                            ////Console.WriteLine(packdata);
                            _character.SendPacket(pw);
                        }

                        return true;
                    }

                case "typedpacket":
                    {
                        if (Args.Count % 2 != 0 || Args.Count == 0)
                        {
                            SendNotice("Usage: !packet <type> <value> <type> <value> ... where type is int, short, long, string, byte");
                            return true;
                        }

                        var pw = new Packet();

                        for (var i = 0; i < Args.Count; i += 2)
                        {
                            switch (Args[i].Value.ToLowerInvariant())
                            {
                                case "opcode":
                                case "op":
                                case "byte":
                                    pw.WriteByte(Args[i + 1].GetByte());
                                    break;
                                case "short":
                                    pw.WriteShort(Args[i + 1].GetInt16());
                                    break;
                                case "int":
                                    pw.WriteInt(Args[i + 1].GetInt32());
                                    break;
                                case "long":
                                    pw.WriteLong(Args[i + 1].GetInt64());
                                    break;
                                case "string":
                                    pw.WriteString(Args[i + 1].Value);
                                    break;
                                default:
                                    SendNotice("Unknown type: " + Args[i].Value);
                                    return true;
                            }
                        }

                        _character.SendPacket(pw);
                        return true;
                    }

                #endregion

                #region Drop

                case "drop":
                    {
                        try
                        {
                            if (Args.TryGetInt32(0, out var ItemID))
                            {
                                Args.TryGetInt16(1, out var Amount, 1);

                                if (!DataProvider.HasItem(ItemID))
                                {
                                    SendNotice("Item not found :(");
                                    return true;
                                }

                                var dropItem = BaseItem.CreateFromItemID(ItemID);
                                dropItem.Amount = Amount;
                                dropItem.GiveStats(ItemVariation.None);

                                _character.Field.DropPool.Create(
                                    Reward.Create(dropItem),
                                    _character.ID,
                                    0,
                                    DropOwnType.UserOwn,
                                    0,
                                    new Pos(_character.Position),
                                    _character.Position.X,
                                    0,
                                    true,
                                    0,
                                    false
                                );
                            }

                            return true;
                        }
                        catch (Exception ex)
                        {
                            _log.Error("!drop command failed", ex);
                            SendNotice("Item not found :(");
                            return true;
                        }
                    }

                case "droptext":
                    {
                        if (Args.Count < 2 || !Args[0].IsNumber())
                        {
                            SendNotice("Command syntax: !droptext [0=red, 1=green] your text");
                            return true;
                        }

                        var itemidNumber = 3990000;
                        var itemidAlphabet = 3991000;
                        var posTextNumbers = "";
                        var posTextAlphabet = "";

                        switch (Args[0].GetInt32())
                        {
                            case 0: // Red
                                posTextNumbers = "1234567890" + // red numbers
                                                 "~~~~~~~~~~" + // green numbers
                                                 "+-" +
                                                 "~~" + // green +-
                                                 "";
                                posTextAlphabet = "abcdefghijklmnopqrstuvwxyz" +
                                                  "~~~~~~~~~~~~~~~~~~~~~~~~~~" +
                                                  "";
                                break;
                            case 1: // Green
                                posTextNumbers = "~~~~~~~~~~" + // red numbers
                                                 "1234567890" + // green numbers
                                                 "~~" + // red numbers
                                                 "+-" + // green +-
                                                 "";
                                posTextAlphabet = "~~~~~~~~~~~~~~~~~~~~~~~~~~" +
                                                  "abcdefghijklmnopqrstuvwxyz" +
                                                  "";
                                break;
                            default:
                                SendNotice("Command syntax: !droptext [0=red, 1=green] your text");
                                return true;
                        }

                        var Rewards = string.Join(" ", Args.Args.Skip(1).Select(x => x.Value)).Select(x =>
                        {
                            if ((x >= '0' && x <= '9') || (x == '+' || x == '-'))
                            {
                                return itemidNumber + posTextNumbers.IndexOf(x);
                            }

                            var lowerx = char.ToLower(x);
                            if (lowerx >= 'a' && lowerx <= 'z')
                            {
                                return itemidAlphabet + posTextAlphabet.IndexOf(lowerx);
                            }

                            return 1;
                        }).Select(x => Reward.Create(BaseItem.CreateFromItemID(x))).ToList();

                        var Pos = _character.Position;

                        short Delay = 0;
                        var x2 = Pos.X + Rewards.Count * -10;
                        foreach (var Drop in Rewards)
                        {
                            if (Drop.ItemID != 1 &&
                                _character.Field.DropPool.Create(
                                    Drop,
                                    _character.ID,
                                    int.MaxValue,
                                    DropOwnType.PartyOwn,
                                    0,
                                    Pos,
                                    x2,
                                    Delay,
                                    true,
                                    0,
                                    true
                                ) == null)
                                continue;
                            Delay += 200;
                            x2 += 35;
                        }


                        return true;
                    }

#if DEBUG
                case "stacksofcash":
                    {
                        if (Args.Count == 0)
                        {
                            SendRed("Usage: !stacksofcash [stacks] {amount per stack}");
                            return true;
                        }

                        try
                        {
                            int stacks = 5;
                            int amountPerStack = 100000;

                            if (Args.Count > 0 && Args[0].TryGetInt32(out var x)) stacks = x;
                            if (Args.Count > 1 && Args[1].TryGetInt32(out var y)) amountPerStack = y;

                            for (var i = 0; i < stacks; i++)
                            {
                                _character.Field.DropPool.Create(
                                    Reward.Create(amountPerStack),
                                    _character.ID,
                                    0,
                                    DropOwnType.UserOwn,
                                    0,
                                    new Pos(_character.Position),
                                    _character.Position.X,
                                    0,
                                    true,
                                    0,
                                    false
                                );
                            }

                            return true;
                        }
                        catch
                        {
                            SendNotice("Error handling command");
                            return true;
                        }
                    }
#endif

                #endregion

                #region TogglePortal

                case "toggleportal":
                    {
                        if (_character.Field.PortalsOpen == false)
                        {
                            SendNotice("You have toggled the portal on.");
                            _character.Field.PortalsOpen = true;
                        }
                        else
                        {
                            SendNotice("You have toggled the portal off.");
                            _character.Field.PortalsOpen = false;
                        }

                        return true;
                    }

                #endregion

                #region Save

                case "save":
                    {
                        // TODO: add save for specific user
                        _character.Save();
                        SendNotice("Saved!");
                        return true;
                    }

                #endregion

                #region SaveAll

                case "saveall":
                    {
                        var tmpCharacter = _character;
                        foreach (var chr in Server.Instance.CharacterList.Values)
                        {
                            MasterThread.Instance.AddCallback(x =>
                            {
                                chr.WrappedLogging(() =>
                                {
                                    chr.Save();
                                });
                                SendNotice(chr.Name + " saved at : " + DateTime.Now + ".", tmpCharacter);
                            }, "Saving message for " + chr);
                        }

                        return true;
                    }

                #endregion

                #region PetName

                case "petname":
                    {
                        if (Args.Count > 0)
                        {
                            var newname = Args[0].Value;
                            var petItem = _character.GetSpawnedPet();
                            if (petItem == null)
                            {
                                SendRed("Spawn pet first");
                            }
                            else if (newname.Length > 13)
                            {
                                SendNotice("Cannot change the name! It's too long :(");
                            }
                            else
                            {
                                petItem.Name = newname;
                                PetsPacket.SendPetNamechange(_character);
                                SendNotice("Changed name lol");
                            }
                        }

                        return true;
                    }

                #endregion

                #region VAC

                case "vac":
                    {
                        var petLoot = false;
                        var mobLoot = false;
                        if (Args.Count > 0)
                        {
                            switch (Args[0].Value)
                            {
                                case "pet":
                                    petLoot = true;
                                    break;
                                case "mob":
                                    mobLoot = true;
                                    break;
                            }
                        }

                        var mobs = _character.Field.Mobs.Values.ToList();
                        if (mobLoot && mobs.Count == 0) mobLoot = false;

                        var dropBackup = new Dictionary<int, Drop>(_character.Field.DropPool.Drops);
                        foreach (var kvp in dropBackup)
                        {
                            if (kvp.Value == null)
                                continue;

                            var drop = kvp.Value;
                            var pickupAmount = drop.Reward.Amount;
                            if (drop.Reward.Mesos)
                            {
                                _character.AddMesos(drop.Reward.Drop);
                            }
                            else
                            {
                                if (_character.Inventory.DistributeItemInInventory(drop.Reward.GetData()) == drop.Reward.Amount)
                                {
                                    continue;
                                }
                            }

                            CharacterStatsPacket.SendGainDrop(_character, drop.Reward.Mesos, drop.Reward.Drop,
                                pickupAmount);
                            if (mobLoot)
                            {
                                var mob = mobs[(int)(Rand32.Next() % mobs.Count)];
                                _character.Field.DropPool.RemoveDrop(drop, DropLeave.PickedUpByMob, mob.SpawnID);
                            }
                            else
                            {
                                _character.Field.DropPool.RemoveDrop(drop,
                                    petLoot ? DropLeave.PickedUpByPet : DropLeave.PickedUpByUser,
                                    _character.ID);
                            }
                        }

                        return true;
                    }

                #endregion

                #region MobInfo

                case "mobinfo":
                    {
                        var Field = _character.Field;
                        var Capacity = Field.GetCapacity();
                        var boosted = Field.IsBoostedMobGen;
                        var RemainCapacity = Capacity - Field.Mobs.Count;
                        SendNotice($"Min Limit {Field.MobCapacityMin}, Max Limit {Field.MobCapacityMax}, Count {Field.Mobs.Count}");
                        SendNotice($"Capacity {Capacity}, RemainCapacity {RemainCapacity}");
                        SendNotice($"Boosted {boosted}, Boost trigger @ {Field.MobCapacityMin / 2} players (cur {Field.Characters.Count})");
                        return true;
                    }

                #endregion

                #region MobGenLimit

                case "mobgenlimit":
                    {
                        var field = _character.Field;
                        if (Args.Count > 0)
                        {
                            var newLimit = Args[0].GetInt32();
                            field.ForcedMobGenLimit = newLimit;

                            if (newLimit == -1)
                                field.MobCapacityMax = field.MobCapacityMin * 2;
                            else
                                field.MobCapacityMax = newLimit;
                        }

                        SendNotice($"Current MobGen limit: {field.ForcedMobGenLimit}. Set to -1 to disable enforced limit.");

                        return true;
                    }

                #endregion

                #region MobChase

                case "mobchase":
                    {
                        if (Args.Count > 0)
                        {
                            var victim = Args[0].Value.ToLower();
                            var who = Server.Instance.GetCharacter(victim);

                            if (who != null)
                            {
                                var allMobs = who.Field.Mobs.Values;
                                var mp = MovePath.WarpPacket(who);

                                allMobs.ForEach(x =>
                                {
                                    x.RemoveController(true);
                                    MobPacket.SendMobControlMove(who, x, false, 0, 0, mp, true);
                                    x.SetController(who, true);
                                });

                                // who.Field.Mobs.ForEach(x => x.Value.SetController(who, true));
                            }
                            else
                                SendRed("You have entered an incorrect name.");
                        }

                        return true;
                    }

                #endregion

                #region npcreload

                case "npcreload":
                case "reloadnpc":
                case "scriptreload":
                case "reloadscript":
                    {
                        if (Args.Count > 0)
                        {
                            var scriptName = Args[0];

                            var fileName = Server.Instance.GetScriptFilename(scriptName);
                            if (fileName == null)
                            {
                                SendNotice("Could not find a script with the name " + scriptName + "!");
                                return true;
                            }

                            var toAllChannels = Args.Count > 1 && Args[1].GetBool();
                            if (toAllChannels)
                            {
                                var p = new Packet(ISClientMessages.BroadcastPacketToGameservers);
                                p.WriteByte((byte)ISServerMessages.ReloadNPCScript);
                                p.WriteString(scriptName);
                                Server.Instance.CenterConnection.SendPacket(p);

                                SendNotice("Sent request to reload the script to all channels.");
                            }
                            else
                            {
                                if (Server.Instance.ForceCompileScriptfile(
                                        fileName,
                                        script =>
                                        {
                                            MessagePacket.SendNotice(_character, "Error while recompiling " + scriptName +
                                                                                ". See logs. Script: " + script);
                                        }) != null)
                                {
                                    SendNotice("Recompiled the script.");
                                }
                            }
                        }
                        else
                        {
                            SendNotice($"Usage: !{Args.Command} <script name or id> (1 here for all channels)");
                        }

                        return true;
                    }

                #endregion

                #region script update

                case "scriptupdate":
                case "updatescripts":
                case "gitupdate":
                case "gitpull":
                    {
                        var tmpCharacter = _character;
                        void asyncSendNotice(string text)
                        {
                            MasterThread.Instance.AddCallback(d =>
                            {
                                SendNotice(text, tmpCharacter);
                            }, "async sendnotice");
                        }

                        Task.Run(() =>
                        {
                            try
                            {
                                asyncSendNotice("-- starting --");
                                var appArgs = new ProcessStartInfo
                                {
                                    UseShellExecute = false,
                                    FileName = "git",
                                    CreateNoWindow = true,
                                    RedirectStandardError = true,
                                    RedirectStandardOutput = true,
                                    WorkingDirectory = Server.Instance.ScriptsDir,
                                };
                                appArgs.ArgumentList.Add("pull");
                                var proc = new Process();
                                proc.StartInfo = appArgs;
                                proc.OutputDataReceived += (sender, args) =>
                                {
                                    if (args.Data == null) return;
                                    asyncSendNotice("[git info] " + args.Data);
                                };
                                proc.ErrorDataReceived += (sender, args) =>
                                {
                                    if (args.Data == null) return;
                                    asyncSendNotice("[git error] " + args.Data);
                                };
                                proc.Start();
                                proc.BeginOutputReadLine();
                                proc.BeginErrorReadLine();
                                proc.WaitForExit(10000);
                                asyncSendNotice($"-- done, exit code {proc.ExitCode} --");
                            }
                            catch (Exception ex)
                            {
                                // fock
                                _log.Error("Unable to git pull", ex);
                                Server.Instance.ServerTraceDiscordReporter.Enqueue($"Complete disaster in git update task... {ex}");
                            }
                        });
                        return true;
                    }

                #endregion

                #region reload cashshop data

                case "csreload":
                case "cashshopreload":
                case "reloadcs":
                case "reloadcashshop":
                    {
                        var p = new Packet(ISClientMessages.BroadcastPacketToShopservers);
                        p.WriteByte((byte)ISServerMessages.ReloadCashshopData);
                        Server.Instance.CenterConnection.SendPacket(p);

                        SendNotice("Sent request to reload the cashshop data.");
                    }
                    return true;

                #endregion

                #region reload world events

                case "reloadevents":
                case "eventsreload":
                    {
                        var p = new Packet(ISClientMessages.ReloadEvents);
                        Server.Instance.CenterConnection.SendPacket(p);

                        SendNotice("Sent request to reload events.");
                        return true;
                    }

                #endregion

                #region reload credit map exclusion list

                case "creditexclreload":
                case "reloadcreditexcl":
                    {
                        RateCredits.LoadExcludedMaps();
                        SendNotice("Reloaded maps in exclusion list for credits");
                        return true;
                    }

                #endregion

                #region flush logs

                case "flushlogs":
                    SendRed("Flushing logs....");
                    LogManager.Flush(5000);
                    SendRed("Flushed!");
                    return true;

                #endregion

                #region avatartest

                case "avatartest":
                case "avatartestpacket":
                    var cid = _character.ID;
                    try
                    {
                        _character.ID = 0x1000000 + Rand32.NextBetween();
                        MapPacket.SendCharacterEnterPacket(_character, _character);
                    }
                    finally
                    {
                        _character.ID = cid;
                    }

                    return true;

                #endregion

                #region guildstuff

                #region guildrename

                case "guildrename":
                    {
                        if (Args.TryGetString(0, out var oldName) &&
                            Args.TryGetString(1, out var newName))
                        {
                            var guild = Server.Instance.GetGuild(oldName);
                            if (guild == null)
                            {
                                SendRed($"Couldn't find guild '{oldName}'");
                                return true;
                            }

                            guild.RenameGuild(_character.ID, newName);
                            SendNotice($"Requesting guild name update from '{oldName}' to '{newName}'");
                        }
                        else
                        {
                            SendRed("Usage: !guildrename oldname|id newname");
                        }

                        return true;
                    }

                #endregion

                #region guildlogo

                case "guildlogo":
                    {
                        if (Args.TryGetString(0, out var guildName) &&
                            Args.TryGetInt16(1, out var background) &&
                            Args.TryGetByte(2, out var backgroundColor) &&
                            Args.TryGetInt16(3, out var foreground) &&
                            Args.TryGetByte(4, out var foregroundColor))
                        {
                            var guild = Server.Instance.GetGuild(guildName);
                            if (guild == null)
                            {
                                SendRed($"Couldn't find guild '{guildName}'");
                                return true;
                            }

                            var logo = new GuildLogo
                            {
                                Background = background,
                                BackgroundColor = backgroundColor,
                                Foreground = foreground,
                                ForegroundColor = foregroundColor,
                            };

                            guild.ChangeLogo(_character.ID, logo);
                            SendNotice($"Requesting guild logo update for '{guildName}'");
                        }
                        else
                        {
                            SendRed("Usage: !guildlogo name|id bg bgcolor fg fgcolor");
                        }

                        return true;
                    }

                #endregion

                #region guilddisband

                case "guilddisband":
                    {
                        if (Args.TryGetString(0, out var guildName))
                        {
                            var guild = Server.Instance.GetGuild(guildName);
                            if (guild == null)
                            {
                                SendRed($"Couldn't find guild '{guildName}'");
                                return true;
                            }

                            guild.DisbandGuild(_character.ID);
                            SendNotice($"Requesting guild disband for '{guildName}'");
                        }
                        else
                        {
                            SendRed("Usage: !guilddisband name|id");
                        }

                        return true;
                    }

                #endregion

                #endregion

                #region TempStatView test

                case "tempstatviewtest":
                    {
                        var i = 3991000;
                        foreach (var buffStat in _character.PrimaryStats.GetAllBuffStats())
                        {
                            //if (buffStat.IsSet()) continue;
                            var flag = buffStat.Set(-i, 1, MasterThread.CurrentTime + (10 * 1000));
                            i++;
                            _character.Buffs.FinalizeBuff(flag, 0);
                        }

                        return true;
                    }
                case "fulldebuff":
                    {
                        BuffValueTypes flag = 0;
                        foreach (var buffStat in _character.PrimaryStats.GetAllBuffStats())
                        {
                            flag |= buffStat.Reset();
                        }
                        _character.Buffs.FinalizeDebuff(flag);
                        return true;
                    }
                #endregion

                #region Load husk

                case "loadhusk":
                    {
                        if (Args.TryGetInt32(0, out var charID))
                        {
                            var husk = Character.LoadAsHusk(charID, out var lfr);
                            if (lfr != Character.LoadFailReasons.None)
                            {
                                SendNotice($"Failed to load character, error {lfr}");
                            }
                            else
                            {
                                SendNotice($"Husk {husk.Name} loaded in {husk.Field.FullName} {husk.Field.ID}");
                            }
                        }
                        return true;
                    }

                    #endregion
            }

            return false;
        }

        public static bool HandleGMCommand(Character character, string text)
        {
            var logtext = string.Format("[{0,-9}] {1,-13}: {2}", character.MapID, character.Name, text);
            if (!Directory.Exists("Chatlogs"))
            {
                Directory.CreateDirectory("Chatlogs");
            }

            File.AppendAllText(Path.Combine("Chatlogs", "Map-" + character.MapID + ".txt"), logtext + Environment.NewLine);
            File.AppendAllText(Path.Combine("Chatlogs", character.Name + ".txt"), logtext + Environment.NewLine);

            if (text[0] != '!' && text[0] != '/') return false;

            try
            {
                CommandHandling.Args = new CommandArgs(text);
                CommandHandling._character = character;

                if (character.Field.FilterAdminCommand(character, CommandHandling.Args))
                {
                    // Handled as field command, eg /start
                    goto Cleanup;
                }

                if (character.GMLevel >= 1) //Intern commands
                {
                    if (HandleGMCommandLevel1()) goto Cleanup;
                }

                if (character.GMLevel >= 2) //Full GMs
                {
                    if (HandleGMCommandLevel2()) goto Cleanup;
                }

                if (character.GMLevel >= 3) //Admin
                {
                    if (HandleGMCommandLevel3()) goto Cleanup;
                }

                SendNotice($"Unknown command: {text}");
                goto Cleanup;
            }
            catch (Exception ex)
            {
                ////Console.WriteLine(ex.ToString());
                SendNotice("Something went wrong while processing this command.");
                if (character.IsGM)
                {
                    SendNotice(ex.ToString());
                }

                goto Cleanup;
            }

        Cleanup:
            CommandHandling.Args = null;
            CommandHandling._character = null;
            return true;
        }

        public static void HandleAdminCommand(Character chr, Packet packet)
        {
            if (chr.AssertForHack(!chr.IsGM, "Tried to use slash GM command while not GM")) return;
            //  41 12 1E 00 00 00 
            var opcode = packet.ReadByte();
            switch (opcode)
            {
                case 0x00: // /create (no idea what it does)
                    break;
                case 0x02:
                    {
                        // /exp (int amount) 
                        var exp = packet.ReadInt();
                        chr.AddEXP(exp);
                        break;
                    }

                case 0x03:
                    {
                        // /ban (user) (permanantly)
                        var name = packet.ReadString();

                        break;
                    }

                case 0x04:
                    {
                        var name = packet.ReadString();
                        var type = packet.ReadByte();
                        var duration = packet.ReadInt();
                        var comment = packet.ReadString();


                        break;
                    }

                case 0x11: //not sure what this is supposed to do. The only thing that comes after the received string is an INT(0). the format is /send (something) (something)
                    {
                        // /send (user) (mapid)
                        var To = packet.ReadString();
                        break;
                    }

                case 0x12:
                    {
                        // /snow
                        var time = new TimeSpan(0, packet.ReadInt(), 0);
                        chr.Field.MakeWeatherEffect(Constants.Items.SnowWeather, "", time, true);
                        break;
                    }

                case 0x0F:
                    {
                        // /hide 0/1
                        var doHide = packet.ReadBool();
                        //if (doHide == chr.GMHideEnabled) return;
                        chr.SetHide(doHide, false);

                        break;
                    }

                case 0x0A:
                    {
                        // /block NAME TIME REASON
                        var name = packet.ReadString();
                        var reason = packet.ReadByte();
                        var len = packet.ReadInt();
                        var reasonmsg = packet.ReadString();
                        break;
                    }
            }
        }

        public static void HandleAdminCommandLog(Character chr, Packet packet)
        {
            // 42 04 00 2F 70 6F 73 
            packet.ReadString();
        }

        public class CommandArgs
        {
            public string PlainText;
            public char Sign;
            public string Command;
            public string CommandText;
            public List<CommandArg> Args;

            public int Count => Args?.Count ?? 0;
            public CommandArg this[int Index] => GetArg(Index);

            public CommandArgs(string text)
            {
                var SplitText = text.Split(' ');
                PlainText = text;
                Sign = text[0];
                Command = SplitText[0].Remove(0, 1);
                CommandText = PlainText.Remove(0, 1).Replace($"{Command} ", "");
                SetArgs(SplitText);
            }

            public void Regenerate(string text)
            {
                SetArgs(text.Split(' '));
            }

            public CommandArg GetArg(int Index)
            {
                if (Index >= 0 && Index < Args.Count)
                    return Args[Index];
                throw new IndexOutOfRangeException($"Index must be greater then 0 and less then {Args.Count}.");
            }

            public void SetArgs(string[] Strings)
            {
                if (Args == null)
                    Args = new List<CommandArg>();
                else
                    Args.Clear();

                for (var i = 1; i < Strings.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(Strings[i]) && Strings[i] != Command)
                        Args.Add(new CommandArg(Strings[i]));
                }
            }

            private CommandArg getArgNoException(int index)
            {
                if (index >= 0 && index < Args.Count)
                    return Args[index];
                return null;
            }

            public bool TryGetString(int index, out string Result, string defaultVal = default)
            {
                Result = getArgNoException(index)?.Value;
                var found = Result != null;
                Result ??= defaultVal;
                return found;
            }

            public bool TryGetBool(int index, out bool Result, bool defaultVal = default)
            {
                Result = defaultVal;
                return getArgNoException(index)?.TryGetBool(out Result) ?? false;
            }

            public bool TryGetByte(int index, out byte Result, byte defaultVal = default)
            {
                Result = defaultVal;
                return getArgNoException(index)?.TryGetByte(out Result) ?? false;
            }

            public bool TryGetInt16(int index, out short Result, short defaultVal = default)
            {
                Result = defaultVal;
                return getArgNoException(index)?.TryGetInt16(out Result) ?? false;
            }

            public bool TryGetInt32(int index, out int Result, int defaultVal = default)
            {
                Result = defaultVal;
                return getArgNoException(index)?.TryGetInt32(out Result) ?? false;
            }
        }

        public class CommandArg
        {
            public string Value;

            public CommandArg(string Value)
            {
                this.Value = Value;
            }


            public bool IsNumber()
            {
                foreach (var Char in Value)
                {
                    if (Char < '0' || Char > '9')
                        return false;
                }

                return true;
            }

            public byte GetByte()
            {
                byte.TryParse(Value, out var Result);

                return Result;
            }

            public bool TryGetByte(out byte Result) => byte.TryParse(Value, out Result);

            public short GetInt16()
            {
                short.TryParse(Value, out var Result);

                return Result;
            }
            public bool TryGetInt16(out short Result) => short.TryParse(Value, out Result);

            public int GetInt32()
            {
                int.TryParse(Value, out var Result);

                return Result;
            }
            public bool TryGetInt32(out int Result) => int.TryParse(Value, out Result);

            public long GetInt64()
            {
                long.TryParse(Value, out var Result);

                return Result;
            }
            public bool TryGetInt64(out long Result) => long.TryParse(Value, out Result);

            public bool GetBool()
            {
                switch (Value.ToLowerInvariant())
                {
                    case "true":
                    case "t":
                    case "yes":
                    case "y":
                    case "1":
                        return true;
                    default:
                        return false;
                }
            }

            public bool TryGetBool(out bool Result)
            {
                Result = GetBool();
                return true;
            }

            public double GetDouble()
            {
                double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var Result);
                return Result;
            }


            public static implicit operator string(CommandArg Arg) => Arg.Value;

            public override string ToString()
            {
                return Value;
            }
        }
    }
}
