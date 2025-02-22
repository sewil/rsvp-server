using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using log4net;
using MySqlConnector;
using WvsBeta.Common;
using WvsBeta.Common.Character;
using WvsBeta.Common.Sessions;
using System.Text;
using Bert.RateLimiters;
using OtpNet;
using WvsBeta.Common.Crypto;

namespace WvsBeta.Login
{
    public class ClientSocket : AbstractConnection
    {
        private static ILog log = LogManager.GetLogger("LoginLogic");

        private const int MaxCharacters = 60;
        public Player Player { get; private set; }
        public bool Loaded { get; set; }
        private Dictionary<string, int> checksums;
        private string machineID;
        private byte? autoSelectChar = null;

        public bool RequestOut = false;
        public int KeyUpdates = 0;
        public bool IsCCing { get; private set; }
        private EncryptedRSA.RSAAuthChallenge AuthChallenge;

        private IThrottleStrategy ThrottleStrategy = new FixedTokenBucket(50, 1, 1000); // 50 incoming packets allowed per second
        private IThrottleStrategy ThrottleWarningStrategy = new FixedTokenBucket(1, 1, 2000); // warn max once per 2 seconds

        public ClientSocket(System.Net.Sockets.Socket pSocket, IPEndPoint endPoint)
            : base(pSocket)
        {
            SetIPEndPoint(endPoint);
            Player = new Player()
            {
                LoggedOn = false,
                Socket = this
            };
            Loaded = false;
            Pinger.Add(this);
            Server.Instance.AddPlayer(Player);
            checksums = null;
            machineID = "";
            AuthChallenge = null;

            SendHandshake(Constants.MAPLE_VERSION, Constants.MAPLE_PATCH_LOCATION, Constants.MAPLE_LOCALE);
            SendMemoryRegions();
            SendLatestPatch();
        }

        public void SendLatestPatch()
        {
            short version = (short)(Constants.MAPLE_VERSION * 1000);
            version += Server.Instance.CurrentPatchVersion;

            SendPatchException(version);
        }

        public override void StartLogging()
        {
            base.StartLogging();

            log4net.ThreadContext.Properties["LoginState"] = Player.State;
            if (Loaded)
            {
                log4net.ThreadContext.Properties["UserID"] = Player.UserID;
            }
        }

        public override void EndLogging()
        {
            base.EndLogging();
            log4net.ThreadContext.Properties.Remove("UserID");
            log4net.ThreadContext.Properties.Remove("LoginState");
        }

        public override void OnDisconnect()
        {
            try
            {
                StartLogging();
                checksums = null;
                try
                {
                    if (crashLogTmp != null)
                    {
                        FileWriter.WriteLine(Path.Combine("ClientCrashes", base.IP + "-unknown_username.txt"),
                            crashLogTmp);
                        crashLogTmp = null;
                    }
                }
                catch { }

                if (Player != null)
                {
                    Server.Instance.RemovePlayer(Player.SessionHash);
                    if (Player.LoggedOn)
                    {
                        Program.MainForm.ChangeLoad(false);

                        Player.Characters.Clear();

                        if (!IsCCing)
                            RedisBackend.Instance.RemovePlayerOnline(Player.UserID);

                        Player.Socket = null;
                        Player = null;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                EndLogging();
            }

            Pinger.Remove(this);
        }

        private string crashLogTmp = null;

        private static HashSet<ClientMessages> logIgnore = new HashSet<ClientMessages>
        {
            ClientMessages.CLIENT_HASH, ClientMessages.PONG,
            ClientMessages.LOGIN_CHECK_PASSWORD, ClientMessages.LOGIN_CHECK_PIN,
            ClientMessages.LOGIN_WORLD_INFO_REQUEST, ClientMessages.LOGIN_SELECT_CHANNEL,
            ClientMessages.CFG
        };

        private static PacketTimingTracker<ClientMessages> ptt = new PacketTimingTracker<ClientMessages>();

        static ClientSocket()
        {
            MasterThread.RepeatingAction.Start("PacketTimingTracker Flush ClientSocket", ptt.Flush, 0, 60 * 1000);
        }

        public override void AC_OnPacketInbound(Packet packet)
        {
            ptt.StartMeasurement();
            var header = (ClientMessages)packet.ReadByte();
            try
            {
                if (ThrottleStrategy.ShouldThrottle())
                {
                    if (!ThrottleWarningStrategy.ShouldThrottle())
                    {
                        var throttleWarning = $"Packet {header} hit throttle limit from {Player?.Username ?? IP}";
                        Server.Instance.ServerTraceDiscordReporter.Enqueue(throttleWarning);
                        Program.MainForm.LogAppend(throttleWarning);
                    }

                    // Uncomment to drop packets on throttle. currently not enabled to keep an eye on warnings
                    // return;
                }

                if (!logIgnore.Contains(header))
                    Common.Tracking.PacketLog.ReceivedPacket(packet, (byte)header, Server.Instance.Name, this.IP);

                if (!Loaded)
                {
                    switch (header)
                    {
                        case ClientMessages.LOGIN_CHECK_PASSWORD:
                            OnCheckPassword(packet);
                            break;
                        case ClientMessages.CLIENT_CRASH_REPORT:
                            OnCrashLog(packet);

                            break;
                        case ClientMessages.LOGIN_EULA:
                            OnConfirmEULA(packet);
                            break;
                    }
                }
                else if (!IsCCing)
                {
                    switch (header)
                    {
                        // Ignore this one
                        case ClientMessages.LOGIN_CHECK_PASSWORD: break;

                        case ClientMessages.LOGIN_SELECT_CHANNEL:
                            OnChannelSelect(packet);
                            break;
                        case ClientMessages.LOGIN_WORLD_INFO_REQUEST:
                            OnWorldInfoRequest(packet);
                            break;
                        case ClientMessages.LOGIN_WORLD_SELECT:
                            OnWorldSelect(packet);
                            break;
                        case ClientMessages.LOGIN_CHECK_CHARACTER_NAME:
                            OnCharNamecheck(packet);
                            break;
                        case ClientMessages.LOGIN_SELECT_CHARACTER:
                            OnSelectCharacter(packet);
                            break;
                        case ClientMessages.LOGIN_SET_GENDER:
                            OnSetGender(packet);
                            break;
                        case ClientMessages.LOGIN_CHECK_PIN:
                            OnPinCheck(packet);
                            break;
                        case ClientMessages.LOGIN_CREATE_CHARACTER:
                            OnCharCreation(packet);
                            break;
                        case ClientMessages.LOGIN_DELETE_CHARACTER:
                            OnCharDeletion(packet);
                            break;
                        case ClientMessages.PONG:
                            RedisBackend.Instance.SetPlayerOnline(Player.UserID, 1);
                            break;
                        case ClientMessages.CLIENT_HASH: break;
                        case ClientMessages.CFG: HandleCfgPacket(packet); break;
                        default:
                            {
                                var errorText = "Unknown packet found " + packet;
                                Server.Instance.ServerTraceDiscordReporter.Enqueue(errorText);
                                Program.MainForm.LogAppend(errorText);

                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                var errorText = "Exception caught: " + ex;
                Server.Instance.ServerTraceDiscordReporter.Enqueue(errorText);
                Program.MainForm.LogAppend(errorText);
                log.Error(ex);
                Disconnect();
            }
            finally
            {
                ptt.EndMeasurement((byte)header);
            }
        }


        public void HandleCfgPacket(Packet packet)
        {
            switch (packet.ReadByte<CfgClientMessages>())
            {
                case CfgClientMessages.CFG_MEMORY_EDIT_DETECTED: break;
                case CfgClientMessages.CFG_TOTP:
                    OnTOTP(packet);
                    break;
                case CfgClientMessages.CFG_FILE_CHECKSUM:
                    Program.MainForm.LogAppend(HandleChecksum(packet));
                    break;
            }
        }

        public override void OnHackDetected(List<MemoryEdit> memEdits)
        {
            TryRegisterHackDetection();
        }

        public void TryRegisterHackDetection()
        {
            if (!Loaded) return;
            TryRegisterHackDetection(Player.UserID);
        }

        private void OnCrashLog(Packet packet)
        {
            // SendPatchException

            crashLogTmp = packet.ReadString();
            if (crashLogTmp.Contains("LdrShutdownProcess") ||
                (crashLogTmp.Contains("005FEC22") && crashLogTmp.Contains("0019F7B")))
            {
                // Ignore
                crashLogTmp = null;
                return;
            }

            Program.MainForm.LogAppend("Received a crashlog!!!");

            if (Constants.MAPLE_VERSION == 4)
            {
                if (crashLogTmp.Contains("Exception code: E0434352") && crashLogTmp.Contains("005D7DF1"))
                {
                    // Exception caused by locale issue w/ double.Parse in monsterbook rewards processing
                    Program.MainForm.LogAppend("Found set_stage v4 exception, patch to 4001");
                    SendPatchException(4001);
                    return;
                }
            }
        }

        private string HandleChecksum(Packet packet)
        {
            checksums = new Dictionary<string, int>();
            try
            {
                while (packet.Length - packet.Position > 0)
                {
                    string file = packet.ReadString();
                    int checksum = 0;
                    var b = packet.ReadBytes(4);
                    // Try to cover an error in the encoder, where no checksum is set.
                    if (b[1] == 0x00 &&
                        char.IsLetterOrDigit((char)b[2]) &&
                        char.IsLetterOrDigit((char)b[3]))
                    {
                        // This is an empty/broken file!
                        checksum = 0;
                        packet.Position -= 4;
                    }
                    else
                    {
                        checksum = BitConverter.ToInt32(b, 0);
                    }

                    checksums.Add(file, checksum);
                }
            }
            catch
            {
            }

            return "Got Checksum from IP: " + IP;
        }

        private void LogFileChecksum()
        {
            if (checksums == null)
            {
                Program.MainForm.LogAppend($"WARNING: User {Player.UserID} / {Player.Username} from IP {IP} did not send a checksum packet.");
                Common.Tracking.ClientChecksum.NoChecksum(machineID, IP);
                return;
            }

            void hackDisconnect(string reason)
            {
                AssertError(true, $"Disconnecting user {Player.UserID} / {Player.Username} from IP {IP}. {reason}");
                Disconnect();
            }

            if (Player.IsGM)
            {
                var s = new StringBuilder();
                s.AppendLine("Received GM checksum. Values are as follows:");
                checksums.ForEach(c =>
                {
                    s.AppendLine($"{c.Key}: {c.Value} {c.Value:X8}");
                });
                Program.MainForm.LogAppend(s.ToString());
            }
            else if (checksums.Count > 45)
            {
                hackDisconnect("Too many files in directory: " + checksums.Count);
            }
            else if (checksums.ContainsKey("Data.wz") && checksums["Data.wz"] != Server.Instance.DataChecksum)
            {
                if (Server.Instance.DataChecksum == 0)
                {
                    Program.MainForm.LogAppend("No data checksum set! User {0} sent {1:X8}", Player.UserID,
                        checksums["Data.wz"]);
                }
                else
                {
                    hackDisconnect(
                        $"Data.wz checksum invalid: 0x{checksums["Data.wz"]:X8} != 0x{Server.Instance.DataChecksum:X8}");
                }
            }

            checksums.ForEach(c => Common.Tracking.ClientChecksum.LogFileChecksum(machineID, IP, c.Key, "" + c.Value));
        }

        private bool AssertWarning(bool assertion, string msg)
        {
            if (assertion)
            {
                log.Warn(msg);
                Server.Instance.ServerTraceDiscordReporter.Enqueue($"AssertWarning: {msg}");
            }

            return assertion;
        }

        private bool AssertError(bool assertion, string msg)
        {
            if (assertion)
            {
                log.Error(msg);
                Program.MainForm.LogAppend(msg);
                Server.Instance.ServerTraceDiscordReporter.Enqueue($"AssertError: {msg}");
            }

            return assertion;
        }

        public bool IsValidName(string pName)
        {
            if (AssertWarning(Player.Characters.Count >= MaxCharacters, "Reached maximum amount of characters and still did a namecheck.")) return false;

            var checkResult = NameCheck.Check(pName);
            
            switch (checkResult)
            {
                case NameCheck.Result.OK:
                    // Quick OK
                    return true;

                case NameCheck.Result.Forbidden:
                    AssertWarning(true, "Charactername matched a ForbiddenName item. " + pName);
                    break;
                case NameCheck.Result.InvalidCharacter:
                    AssertWarning(true, "Name had invalid characters: " + pName);
                    break;
                case NameCheck.Result.InvalidLength:
                    AssertWarning(true, "Name length invalid! " + pName);
                    break;
            }

            return false;
        }
        
        public void ConnectToServer(int charid, byte[] ipAddr, ushort port)
        {
            byte bit = 0, goPremium = 0;

            bit |= (byte)(goPremium << 1);

            if (!Player.Characters.TryGetValue(charid, out var characterName))
            {
                if (charid != 0)
                {
                    log.Error("Unable to load character name in ConnectToServer");
                    return;
                }
            }

            if (port != 0 && IP == "127.0.0.1")
            {
                // Use local address for local connections
                ipAddr = new byte[] { 127, 0, 0, 1 };
            }

            log.Info($"Connecting to {ipAddr[0]}.{ipAddr[1]}.{ipAddr[2]}.{ipAddr[3]}:{port} world {Player.World} channel {Player.Channel} with charid {charid} name {characterName ?? "[unknown]"}");

            IsCCing = true;

            var pw = new Packet(ServerMessages.SELECT_CHARACTER_RESULT);
            pw.WriteByte(0);
            pw.WriteByte(0);
            pw.WriteBytes(ipAddr);
            pw.WriteUShort(port);
            pw.WriteInt(charid);
            pw.WriteByte(bit);

            var ccToken = new byte[16];
            for (var i = 0; i < ccToken.Length; i++)
            {
                ccToken[i] = (byte)Rand32.Next();
            }

            RedisBackend.Instance.SetCCToken(charid, ccToken);
            pw.WriteBytes(ccToken);

            SendPacket(pw);
        }

        public void OnSelectCharacter(Packet packet)
        {
            if (AssertWarning(
                Player.State != Player.LoginState.CharacterSelect &&
                Player.State != Player.LoginState.CharacterCreation, "Trying to select character while not in character select screen.")) return;

            if (AssertWarning(RequestOut, "Trying to select a character while already having a request out."))
            {
                return;
            }

            var charid = packet.ReadInt();

            if (AssertWarning(Player.HasCharacterWithID(charid) == false, "Trying to select a character that the player doesnt have. ID: " + charid)) return;

            LogFileChecksum();

            if (!Server.Instance.GetWorld(Player.World, out Center center) || center.Connection == null)
            {
                // Server is offline
                var pw = new Packet(ServerMessages.SELECT_CHARACTER_RESULT);
                pw.WriteByte(LoginResCode.SystemError);
                pw.WriteByte(0);
                SendPacket(pw);

                return;
            }

            RequestOut = true;
            center.Connection.RequestCharacterConnectToWorld(Player.SessionHash, charid, Player.World, Player.Channel);
        }

        public void OnCharNamecheck(Packet packet)
        {
            if (AssertWarning(Player.State != Player.LoginState.CharacterSelect && Player.State != Player.LoginState.CharacterCreation, "Trying to check character name while not in character select or creation screen.")) return;

            if (AssertWarning(RequestOut, "Trying to check charname state while already having a request out."))
            {
                return;
            }

            Player.State = Player.LoginState.CharacterCreation;

            var name = packet.ReadString();

            if (!IsValidName(name))
            {
                var pack = new Packet(ServerMessages.CHECK_CHARACTER_NAME_AVAILABLE);
                pack.WriteString(name);
                pack.WriteBool(true);
                SendPacket(pack);
                return;
            }

            if (AssertWarning(!Server.Instance.GetWorld(Player.World, out Center center) || center.Connection == null, "Server was offline while checking for duplicate charname"))
            {
                var pack = new Packet(ServerMessages.CHECK_CHARACTER_NAME_AVAILABLE);
                pack.WriteString(name);
                pack.WriteBool(true);
                SendPacket(pack);
                return;
            }


            RequestOut = true;
            center.Connection.CheckCharacternameTaken(Player.SessionHash, name);
        }

        public void OnCharDeletion(Packet packet)
        {
            if (AssertWarning(
                Player.State != Player.LoginState.CharacterSelect &&
                Player.State != Player.LoginState.CharacterCreation,
                "Trying to delete character while not in character select or create screen.")) return;


            if (AssertWarning(RequestOut, "Trying to delete character while already having a request out."))
            {
                return;
            }

            var DoB = packet.ReadInt();
            var charid = packet.ReadInt();

            if (AssertWarning(Player.HasCharacterWithID(charid) == false, "Trying to delete a character that the player doesnt have. ID: " + charid)) return;

            if (Player.DateOfBirth != DoB)
            {
                log.Warn("Invalid DoB entered when trying to delete character.");

                var pack = new Packet(ServerMessages.DELETE_CHARACTER_RESULT);
                pack.WriteInt(charid);
                pack.WriteByte(LoginResCode.InvalidDoB);
                SendPacket(pack);
                return;
            }

            if (!Server.Instance.GetWorld(Player.World, out var center) || center.Connection == null)
            {
                log.Error("Unable to connect to center server?");
                var pack = new Packet(ServerMessages.DELETE_CHARACTER_RESULT);
                pack.WriteInt(charid);
                pack.WriteByte(LoginResCode.Timeout);
                SendPacket(pack);
                return;
            }


            center.Connection?.RequestDeleteCharacter(Player.SessionHash, Player.UserID, charid);
        }

        public void HandleCharacterDeletionResult(int characterId, byte result)
        {
            if (result == 0)
            {
                log.Info($"User deleted a character, called '{Player.Characters[characterId]}'");
                // Alright!
                Player.Characters.Remove(characterId);
            }

            var pack = new Packet(ServerMessages.DELETE_CHARACTER_RESULT);
            pack.WriteInt(characterId);
            pack.WriteByte(result);
            SendPacket(pack);
        }

        private bool IsValidCreationId(IEnumerable<int> validIds, int inputId, string name)
        {
            if (validIds.Contains(inputId)) return true;
            AssertError(true, $"[CharCreation] Invalid {name}: {inputId}");
            return false;
        }

        enum CharacterCreationErrorCode : byte
        {
            Invalid = 0xff,
            OK = 0,
            NameNotAvailable = 1,
            UnknownError = 2,
        }

        public void InstantBan(string reason)
        {
            Server.Instance.ServerTraceDiscordReporter.Enqueue($"Banning userid {Player.UserID} for reason: {reason}");

            Server.Instance.UsersDatabase.PermaBan(
                Player.UserID,
                (byte)BanReasons.Hack,
                "AB-" + Server.Instance.Name,
                reason
            );

            // Try to make the client connect to a server that doesnt exist (goes back to login)
            // TODO: there must be a better way for this

            ConnectToServer(0, new byte[] { 0, 0, 0, 0 }, 0);
        }

        public void OnCharCreation(Packet packet)
        {
            var errorCode = CharacterCreationErrorCode.Invalid;
            if (AssertWarning(Player.State != Player.LoginState.CharacterCreation, "Trying to create character while not in character creation screen (skipped namecheck?).")) return;

            if (AssertWarning(RequestOut, "Trying to create character while already having a request out."))
            {
                errorCode = CharacterCreationErrorCode.UnknownError;
                goto not_available;
            }

            if (!Server.Instance.GetWorld(Player.World, out Center center))
            {
                log.Error("Unable to connect to center server?");
                errorCode = CharacterCreationErrorCode.NameNotAvailable;
                goto not_available;
            }

            if (center.BlockCharacterCreation)
            {
                log.Error("Character creation blocked!");
                errorCode = CharacterCreationErrorCode.NameNotAvailable;
                goto not_available;
            }

            Packet pack;
            string charname = packet.ReadString();

            if (!IsValidName(charname))
            {
                goto not_available;
            }

            int face = packet.ReadInt();
            int hair = packet.ReadInt();
            int haircolor = packet.ReadInt();
            int skin = packet.ReadInt();

            int top = packet.ReadInt();
            int bottom = packet.ReadInt();
            int shoes = packet.ReadInt();
            int weapon = packet.ReadInt();
            byte str = packet.ReadByte();
            byte dex = packet.ReadByte();
            byte intt = packet.ReadByte();
            byte luk = packet.ReadByte();

            if (str >= 13 || dex >= 13 || intt >= 13 || luk >= 13)
            {
                InstantBan($"Character stats are hacked of {charname}: {str}/{dex}/{intt}/{luk}");
                return;
            }

            if (!(str >= 4 && dex >= 4 && intt >= 4 && luk >= 4 && (str + dex + intt + luk) <= 25))
            {
                log.Error($"Invalid stats for character creation: {str} {dex} {intt} {luk}");
                goto not_available;
            }

            var cci = Player.Gender == 0 ? CreateCharacterInfo.Male : CreateCharacterInfo.Female;

            if (!IsValidCreationId(cci.Face, face, "face") ||
                !IsValidCreationId(cci.Hair, hair, "hair") ||
                !IsValidCreationId(cci.HairColor, haircolor, "haircolor") ||
                !IsValidCreationId(cci.Skin, skin, "skin") ||
                !IsValidCreationId(cci.Coat, top, "top") ||
                !IsValidCreationId(cci.Pants, bottom, "bottom") ||
                !IsValidCreationId(cci.Shoes, shoes, "shoes") ||
                !IsValidCreationId(cci.Weapon, weapon, "weapon"))
            {
                InstantBan($"User tried to create account with wrong starter equips. {face} {hair} {haircolor} {skin} {top} {bottom} {shoes} {weapon}");
                return;
            }

            // Create packet for Center
            RequestOut = true;

            pack = new Packet(ISClientMessages.PlayerCreateCharacter);
            pack.WriteString(Player.SessionHash);
            pack.WriteInt(Player.UserID);
            pack.WriteByte(Player.Gender);

            pack.WriteString(charname);

            pack.WriteInt(face);
            pack.WriteInt(hair);
            pack.WriteInt(haircolor);
            pack.WriteInt(skin);

            pack.WriteInt(top);
            pack.WriteInt(bottom);
            pack.WriteInt(shoes);
            pack.WriteInt(weapon);

            pack.WriteByte(str);
            pack.WriteByte(dex);
            pack.WriteByte(intt);
            pack.WriteByte(luk);

            center.Connection.SendPacket(pack);

            return;

        not_available:
            pack = new Packet(ServerMessages.CREATE_NEW_CHARACTER_RESULT);
            pack.WriteByte(errorCode);
            SendPacket(pack);
        }

        public void HandleCreateNewCharacterResult(Packet packet)
        {
            RequestOut = false;

            var pack = new Packet(ServerMessages.CREATE_NEW_CHARACTER_RESULT);
            if (packet.ReadBool())
            {
                // Succeeded
                pack.WriteByte(CharacterCreationErrorCode.OK);
                var ad = new AvatarData();
                ad.Decode(packet);
                ad.Encode(pack);

                Server.Instance.ServerTraceDiscordReporter.Enqueue($"New character created: {ad.CharacterStat.Name} (id {ad.CharacterStat.ID}) on account {Player.UserID}");
                log.Info($"User created a new character, called '{ad.CharacterStat.Name}'");
                Player.Characters.Add(ad.CharacterStat.ID, ad.CharacterStat.Name);
                Player.State = Player.LoginState.CharacterSelect;
            }
            else
            {
                pack.WriteByte(CharacterCreationErrorCode.NameNotAvailable);
            }

            Player.Socket.SendPacket(pack);
        }

        public void OnChannelSelect(Packet packet)
        {
            var worldId = packet.ReadByte();
            var channelId = packet.ReadByte();

            if (AssertWarning(Player.State != Player.LoginState.WorldSelect,
                "Tried to select channel while not in channel select.")) return;

            Player.World = worldId;
            if (!Server.Instance.GetWorld(worldId, out var center) ||
                channelId >= center.Channels)
            {
                var p = new Packet(ServerMessages.SELECT_WORLD_RESULT);
                p.WriteByte(LoginResCode.NotConnectableWorld);
                SendPacket(p);
                return;
            }

            center.Connection?.RequestCharacterIsChannelOnline(Player.SessionHash, Player.World, channelId, Player.UserID);
        }


        public void HandleChannelSelectResult(Packet packet)
        {
            // Packet received from the center server
            RequestOut = false;

            var resultCode = packet.ReadByte<LoginResCode>();

            var pack = new Packet(ServerMessages.SELECT_WORLD_RESULT);
            pack.WriteByte(resultCode);

            if (resultCode != LoginResCode.SuccessChannelSelect)
            {
                SendPacket(pack);
                return;
            }


            Player.Channel = packet.ReadByte();

            var characters = packet.ReadByte();

            pack.WriteByte(characters);

            for (var index = 0; index < characters; index++)
            {
                var ad = new AvatarData();
                ad.Decode(packet);
                ad.Encode(pack);

                var hasRanking = packet.ReadBool();
                pack.WriteBool(hasRanking);
                if (hasRanking)
                {
                    pack.WriteInt(packet.ReadInt());
                    pack.WriteInt(packet.ReadInt());
                    pack.WriteInt(packet.ReadInt());
                    pack.WriteInt(packet.ReadInt());
                }

                Player.Characters[ad.CharacterStat.ID] = ad.CharacterStat.Name;
            }

            SendPacket(pack);

            Player.State = Player.LoginState.CharacterSelect;

            if (autoSelectChar.HasValue &&
                autoSelectChar.Value < Player.Characters.Count &&
                Server.Instance.GetWorld(Player.World, out Center center))
            {
                var charid = Player.Characters.ElementAt(autoSelectChar.Value).Key;
                center.Connection.RequestCharacterConnectToWorld(
                    Player.SessionHash,
                    charid,
                    Player.World,
                    Player.Channel
                );
            }
        }


        public void OnWorldInfoRequest(Packet packet)
        {
            if (AssertWarning(Player.State != Player.LoginState.WorldSelect,
                "Tried to get the world information while not in worldselect")) return;

            foreach (var kvp in Server.Instance.Worlds)
            {
                var world = kvp.Value;

                var worldInfo = new Packet(ServerMessages.WORLD_INFORMATION);
                worldInfo.WriteByte(world.ID);
                worldInfo.WriteString(world.Name);
                worldInfo.WriteByte(world.Channels);

                for (byte i = 0; i < world.Channels; i++)
                {
                    worldInfo.WriteString(world.Name + "-" + (i + 1));
                    worldInfo.WriteInt((int)(world.UserNo[i] * world.UserNoMultiplier));
                    worldInfo.WriteByte(world.ID);
                    worldInfo.WriteByte(i);
                    worldInfo.WriteBool(world.BlockCharacterCreation);
                }

                SendPacket(worldInfo);
            }

            var endWorldInfo = new Packet(ServerMessages.WORLD_INFORMATION);
            endWorldInfo.WriteSByte(-1);

            SendPacket(endWorldInfo);
        }

        public void OnWorldSelect(Packet packet)
        {
            if (AssertWarning(Player.State != Player.LoginState.WorldSelect,
                "Player tried to select world while not in worldselect")) return;

            if (AssertWarning(RequestOut, "Not yet ack-ed world select packet")) return;

            var worldId = packet.ReadByte();


            if (!Server.Instance.GetWorld(worldId, out var center))
            {
                var p = new Packet(ServerMessages.CHECK_USER_LIMIT_RESULT);
                p.WriteByte(WorldLoadState.Full);
                SendPacket(p);
                return;
            }

            Player.World = worldId;
            if (center.Connection == null)
            {
                log.Warn("World is not connected...?");
                var p = new Packet(ServerMessages.CHECK_USER_LIMIT_RESULT);
                p.WriteByte(WorldLoadState.Full);
                SendPacket(p);
                return;
            }

            RequestOut = true;
            center.Connection.RequestCharacterGetWorldLoad(Player.SessionHash, worldId);
        }

        public void HandleWorldLoadResult(Packet packet)
        {
            RequestOut = false;

            var state = packet.ReadByte<WorldLoadState>();

            var pack = new Packet(ServerMessages.CHECK_USER_LIMIT_RESULT);
            pack.WriteByte(state);
            SendPacket(pack);
        }

        public void SendPatchException(short toVersion)
        {
            var p = new Packet(CfgServerMessages.CFG_INVOKE_PATCHER);
            p.WriteShort(toVersion);
            SendPacket(p);
        }

        private int LoginErrorCount = 0;

        public struct LoginLoggingStruct
        {
            public string localUserId { get; set; }
            public string uniqueId { get; set; }
            public string osVersion { get; set; }
            public string cultureName { get; set; }
            public string uiCultureName { get; set; }
            public bool adminClient { get; set; }
            public bool possibleUniqueIdBypass { get; set; }
            public string username { get; set; }

            public string windowsUsername { get; set; }
            public string windowsMachineName { get; set; }
            public int dotNetInstallRelease { get; set; }
            public string dotNetInstallVersion { get; set; }
        }

        public void RegisterLoginInfo(int userID, LoginLoggingStruct lls)
        {
            Server.Instance.UsersDatabase.RunQuery(
                @"INSERT INTO login_records 
SET 
id = NULL,
userid = @userid,
uniqueid = @uniqueid,
windows_username = @windows_username,
windows_machine_name = @windows_machine_name,
local_userid = @local_userid,
first_login = NOW(),
last_login = NOW(),
login_count = 1
ON DUPLICATE KEY UPDATE
last_login = NOW(),
login_count = login_count + 1
",
                "@userid", userID,
                "@uniqueid", lls.uniqueId,
                "@windows_username", lls.windowsUsername,
                "@windows_machine_name", lls.windowsMachineName,
                "@local_userid", lls.localUserId
            );
        }


        public void OnCheckPassword(Packet packet)
        {
            if (AssertWarning(Player.State != Player.LoginState.LoginScreen, "Player tried to login while not in loginscreen."))
            {
                Program.MainForm.LogAppend("Disconnected client (4)");
                Disconnect();
                return;
            }

            var requestedRSAChallenge = packet.ReadBool();

            if (requestedRSAChallenge)
            {
                try
                {
                    var usern = packet.ReadString();
                    AuthChallenge = new EncryptedRSA.RSAAuthChallenge(EncryptedRSA.ReadPublicKeyFromFile(Path.Combine("..", "DataSvr", "keys", usern)));
                    var encrypted = AuthChallenge.GetEncryptedChallenge();
                    var resp = new Packet(CfgServerMessages.CFG_RSA_CHALLENGE);
                    resp.WriteInt(encrypted.Length);
                    resp.WriteBytes(encrypted);
                    SendPacket(resp);
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    var err = new Packet(ServerMessages.CHECK_PASSWORD_RESULT);
                    err.WriteByte(LoginResCode.AccountDoesNotExist); //Login State
                    err.WriteByte(0); // nRegStatID
                    err.WriteInt(0); // nUseDay
                    SendPacket(err);
                }

                return;
            }

            var challengeResult = packet.ReadBool();
            var challengeLength = packet.ReadInt();
            byte[] response = packet.ReadBytes(challengeLength);

            var username = packet.ReadString();
            var password = packet.ReadString();

            if (AssertWarning(username.Length < 4 || username.Length > 12, "Username length wrong (len: " + username.Length + "): " + username) ||
                AssertWarning(password.Length < 4 || password.Length > 12, "Password length wrong (len: " + password.Length + ")"))
            {
                Disconnect();
                return;
            }

            var lastBit = username.Substring(username.Length - 2);
            if (lastBit[0] == ':' && byte.TryParse("" + lastBit[1], out var b))
            {
                autoSelectChar = b;
                username = username.Remove(username.Length - 2);
            }

            var machineID = string.Join("", packet.ReadBytes(16).Select(x => x.ToString("X2")));
            this.machineID = machineID;
            var startupThingy = packet.ReadInt();

            int localUserIdLength = packet.ReadShort();
            var localUserId = string.Join("", packet.ReadBytes(localUserIdLength).Select(x => x.ToString("X2")));


            var possibleHack = packet.ReadBool();


            int uniqueIDLength = packet.ReadShort();
            var uniqueID = string.Join("", packet.ReadBytes(uniqueIDLength).Select(x => x.ToString("X2")));

            var adminClient = packet.ReadBool();

            var magicWord = 0;

            if (adminClient)
            {
                magicWord = packet.ReadInt();
            }

            var osVersionString = packet.ReadString();

            var cultureName = packet.ReadString();
            var uiCultureName = packet.ReadString();

            var patchVersion = packet.ReadShort();

            var windowsUsername = packet.ReadString();
            var windowsMachineName = packet.ReadString();
            var dotNetInstallRelease = packet.ReadInt();
            var dotNetInstallVersion = packet.ReadString();
            var lls = new LoginLoggingStruct
            {
                adminClient = adminClient,
                localUserId = localUserId,
                osVersion = osVersionString,
                cultureName = cultureName,
                uiCultureName = uiCultureName,
                possibleUniqueIdBypass = possibleHack,
                uniqueId = uniqueID,
                username = username,
                dotNetInstallRelease = dotNetInstallRelease,
                dotNetInstallVersion = dotNetInstallVersion,
                windowsMachineName = windowsMachineName,
                windowsUsername = windowsUsername
            };
            void writeLoginInfo()
            {
                log.Info(lls);
            }


            if (Server.Instance.CurrentPatchVersion > patchVersion)
            {
                writeLoginInfo();

                var nextVersion = (short)(patchVersion + 1);

                // Figure out how to patch
                if (Server.Instance.PatchNextVersion.TryGetValue(nextVersion, out var usingVersion))
                {
                    SendPatchException(usingVersion);
                    log.Info($"Sent patchexception packet ({Constants.MAPLE_VERSION:D5}to{usingVersion:D5})");
                    return;
                }
                else
                {
                    AssertError(true, $"No patch strategy to go from {patchVersion} to {Server.Instance.CurrentPatchVersion}");
                    Disconnect();
                    return;
                }
            }

            // Okay, packet parsed
            if (adminClient && IP != "127.0.0.1")
            {
                if (AssertError(magicWord != 0x31333337, $"Account '{username}' tried to login with an admin client! Magic word: {magicWord:X8}, IP: {IP}. Disconnecting."))
                {
                    writeLoginInfo();
                    Disconnect();
                    return;
                }
            }


            var result = LoginResCode.Unknown;

            var dbpass = "";
            var updateDBPass = false;
            byte banReason = 0;
            long banExpire = 0;
            var userId = 0;

            using (var data = Server.Instance.UsersDatabase.RunQuery(
                "SELECT * FROM users WHERE username = @username",
                "@username", username
            ) as MySqlDataReader)
            {
                if (!data.Read())
                {
                    log.Warn($"[{username}] account does not exist");
                    result = LoginResCode.AccountDoesNotExist;
                }
                else
                {
                    username = data.GetString("username");
                    userId = data.GetInt32("ID");
                    dbpass = data.GetString("password");
                    banReason = data.GetByte("ban_reason");
                    banExpire = ((DateTime)data.GetMySqlDateTime("ban_expire")).ToFileTimeUtc();
                    Player.GMLevel = data.GetByte("admin");
                    Player.PinSecret = data.GetString("pin_secret");

                    // To fix the debug thing
                    if (false) { }
#if DEBUG
                    // Bypass for local testing
                    else if (IP == "127.0.0.1" && password == "imup2nogood")
                    {
                        result = LoginResCode.SuccessLogin;
                    }
#endif
                    else if (banExpire > MasterThread.CurrentDate.ToUniversalTime().ToFileTimeUtc())
                    {
                        AssertWarning(true, $"[{username}][{userId}] banned until " + data.GetDateTime("ban_expire"));
                        result = LoginResCode.Banned;
                    }
                    else if (Server.Instance.AdminsRequirePublicKeyAuth && Player.GMLevel > 0 && AuthChallenge != null && AuthChallenge.CheckChallengeResponse(response))
                    {
                        result = LoginResCode.SuccessLogin;
                    }
                    else if (dbpass.Length > 1 && dbpass[0] != '$')
                    {
                        if (dbpass == password) // Unencrypted
                        {
                            result = LoginResCode.SuccessLogin;
                            dbpass = BCrypt.HashPassword(password, BCrypt.GenerateSalt());
                            updateDBPass = true;
                        }
                        else
                        {
                            result = LoginResCode.InvalidPassword;
                        }
                    }
                    else if (BCrypt.CheckPassword(password, dbpass))
                    {
                        result = LoginResCode.SuccessLogin;
                    }
                    else
                    {
                        result = LoginResCode.InvalidPassword;
                    }

                    if (result == LoginResCode.SuccessLogin && RedisBackend.Instance.IsPlayerOnline(userId))

                    {
                        AssertWarning(true, $"[{username}][{userId}] already online");
                        result = LoginResCode.AlreadyOnline;
                    }

                    if (result == LoginResCode.SuccessLogin)
                    {
                        Player.UserID = userId;
                        if (Server.Instance.RequiresEULA && data.GetBoolean("confirmed_eula") == false)
                        {
                            result = LoginResCode.ConfirmEULA;
                        }
                        else
                        {
                            Player.Gender = data.GetByte("gender");
                            Player.DateOfBirth = data.GetInt32("char_delete_password");

                            Player.Username = username;
                        }
                    }
                    else if (result == LoginResCode.InvalidPassword)
                    {
                        log.Warn($"[{username}][{userId}] invalid password");
                    }
                }
            }

            RegisterLoginInfo(userId, lls);

            var isLoginOK = result == LoginResCode.SuccessLogin;
            int machineBanCount = 0, uniqueBanCount = 0, ipBanCount = 0;

            if (isLoginOK)
            {
                Loaded = true;
                StartLogging();

                writeLoginInfo();

                var macBanned = false;
                using (var mdr = Server.Instance.UsersDatabase.RunQuery(
                    "SELECT 1 FROM machine_ban WHERE machineid = @machineId OR machineid = @uniqueId",
                    "@machineId", machineID,
                    "@uniqueId", uniqueID) as MySqlDataReader)
                {
                    if (mdr.HasRows)
                    {
                        macBanned = true;
                    }
                }

                // Outside of using statement because of secondary query
                if (AssertWarning(macBanned,
                    $"[{username}][{userId}] tried to login on a machine-banned account for machineid {machineID}."))
                {
                    Disconnect();

                    Server.Instance.UsersDatabase.RunQuery(
                        "UPDATE machine_ban SET last_try = CURRENT_TIMESTAMP, last_username = @username, last_unique_id = @uniqueId, last_ip = @ip WHERE machineid = @machineId OR machineid = @uniqueId",
                        "@ip", IP,
                        "@username", username,
                        "@machineId", machineID,
                        "@uniqueId", uniqueID
                    );
                    return;
                }

                using (var mdr =
                    Server.Instance.UsersDatabase.RunQuery("SELECT 1 FROM ipbans WHERE ip = @ip", "@ip", this.IP) as
                        MySqlDataReader)
                {
                    if (mdr.HasRows)
                    {
                        AssertError(true, $"[{username}][{userId}] tried to login on a ip-banned account for ip {IP}.");
                        Disconnect();
                        return;
                    }
                }

                var (maxMachineBanCount, maxUniqueBanCount, maxIpBanCount) =
                    Server.Instance.UsersDatabase.GetUserBanRecordLimit(Player.UserID);
                (machineBanCount, uniqueBanCount, ipBanCount) =
                    Server.Instance.UsersDatabase.GetBanRecord(machineID, uniqueID, IP);

                // Do not use MachineID banning, as its not unique enough
                if (ipBanCount >= maxIpBanCount ||
                    uniqueBanCount >= maxUniqueBanCount)
                {
                    AssertError(true,
                        $"[{username}][{userId}] tried to log in an account where a machineid, uniqueid and/or ip has already been banned for " +
                        $"{machineBanCount}/{uniqueBanCount}/{ipBanCount} times. " +
                        $"(Max values: {maxMachineBanCount}/{maxUniqueBanCount}/{maxIpBanCount})");

                    if (ipBanCount >= maxIpBanCount)
                    {
                        result = LoginResCode.MasterCannotLoginOnThisIP; // rip.
                    }
                    else
                    {
                        Disconnect();
                        return;
                    }
                }


                if (!Player.IsGM && IP != "127.0.0.1")
                {
                    // Try to find accounts that are logged in already, with the same unique info
                    using (var mdr = Server.Instance.UsersDatabase.RunQuery(
                        "SELECT ID FROM users WHERE last_unique_id = @uniqueId AND admin = 0",
                        "@uniqueId", uniqueID) as MySqlDataReader)
                    {
                        while (mdr.Read())
                        {
                            var userID = mdr.GetInt32("ID");
                            if (RedisBackend.Instance.IsPlayerOnline(userID))
                            {
                                result = LoginResCode.WrongGatewayOrChangeInfo;
                                AssertError(true, $"[{username}][{userId}] tried to log in an account that has a uniqueid that is already logged in ({userID}).");
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                writeLoginInfo();
            }

            // Refresh the value
            isLoginOK = result == LoginResCode.SuccessLogin;

            if (!isLoginOK)
            {
                LoginErrorCount++;
                if (LoginErrorCount > 10)
                {
                    Disconnect();
                    return;
                }
            }

            var pack = new Packet(ServerMessages.CHECK_PASSWORD_RESULT);
            pack.WriteByte(result); //Login State
            pack.WriteByte(0); // nRegStatID
            pack.WriteInt(0); // nUseDay
            if (isLoginOK)
            {
                pack.WriteInt(Player.UserID);
                pack.WriteByte(Player.Gender);
                pack.WriteByte((byte)(Player.IsGM ? 1 : 0)); // Check more flags, 0x40/0x80?
                pack.WriteByte(0x01); //Country ID
                pack.WriteString(username);
                pack.WriteByte(0); // Purchase EXP
                // Wait is this actually here
                pack.WriteByte(0); // Chatblock Reason (1-5)
                pack.WriteLong(0); // Chat Unlock Date
            }
            else if (result == LoginResCode.Banned)
            {
                pack.WriteByte(banReason);
                pack.WriteLong(banExpire);
            }

            SendPacket(pack);

            if (!isLoginOK) Loaded = false;

            if (isLoginOK)
            {
                TryRegisterHackDetection();

                // Set online.

                RedisBackend.Instance.SetPlayerOnline(Player.UserID, 1);

                if (crashLogTmp != null)
                {
                    var crashlogName = IP + "-" + username + ".txt";
                    FileWriter.WriteLine(Path.Combine("ClientCrashes", crashlogName), crashLogTmp);
                    crashLogTmp = null;
                    Server.Instance.ServerTraceDiscordReporter.Enqueue($"Saving crashlog to {crashlogName}");
                }

                AssertWarning(Player.IsGM == false && adminClient, $"[{username}] Logged in on an admin client while not being admin!!");

                Player.LoggedOn = true;
                Player.State = Player.Gender == 10 ? Player.LoginState.SetupGender : Player.LoginState.PinCheck;

                Program.MainForm.LogAppend($"Account {username} ({Player.UserID}) logged on. Machine ID: {machineID}, Unique ID: {uniqueID}, IP: {IP}, Ban counts: {machineBanCount}/{uniqueBanCount}/{ipBanCount}");
                Program.MainForm.ChangeLoad(true);

                // Update database
                Server.Instance.UsersDatabase.RunQuery(
                    @"
                    UPDATE users SET 
                    last_login = NOW(), 
                    last_ip = @ip, 
                    last_machine_id = @machineId, 
                    last_unique_id = @uniqueId 
                    WHERE ID = @id",
                    "@id", Player.UserID,
                    "@ip", IP,
                    "@machineId", machineID,
                    "@uniqueId", uniqueID
                );

                if (updateDBPass)
                {
                    Server.Instance.UsersDatabase.RunQuery(
                        "UPDATE users SET password = @password WHERE ID = @id",
                        "@id", Player.UserID,
                        "@password", dbpass
                    );
                }
            }
            else if (result == LoginResCode.ConfirmEULA)
            {
                Player.State = Player.LoginState.ConfirmEULA;
            }
        }

        public void OnSetGender(Packet packet)
        {
            if (AssertWarning(Player.State != Player.LoginState.SetupGender,
                "Tried to set gender while not in setup gender state")) return;

            if (packet.ReadBool() == false)
            {
                // 'back' to login
                BackToLogin();
                return;
            }

            var isFemale = packet.ReadBool();

            Server.Instance.UsersDatabase.RunQuery(
                "UPDATE users SET gender = @gender WHERE ID = @id",
                "@id", Player.UserID,
                "@gender", isFemale ? 1 : 0
            );

            Player.Gender = (byte)(isFemale ? 1 : 0);
            Player.State = Player.LoginState.PinCheck;

            var pack = new Packet(ServerMessages.SET_ACCOUNT_RESULT);
            pack.WriteBool(isFemale);
            pack.WriteByte(1);

            SendPacket(pack);
        }

        enum PinCodeResCode
        {
            Success = 0x0,
            NotAssigned = 0x1,
            Incorrect = 0x2,
            Assigned = 0x4,
            DBFail = 0x3,
            AlreadyConnected = 0x7,
        }

        public bool ValidateTOTP(string pin, string secret = null)
        {
            while (pin.Length < 6) pin = "0" + pin;

            var totp = new Totp(Base32Encoding.ToBytes((secret ?? Player.PinSecret).Replace(" ", "")));

            if (!totp.VerifyTotp(pin, out var timeStepMatched, VerificationWindow.RfcSpecifiedNetworkDelay))
            {
                return false;
            }

            return true;
        }

        public void OnPinCheck(Packet packet)
        {
            if (AssertWarning(Player.State != Player.LoginState.PinCheck,
                "Tried to do a pin check while not in pin check state")) return;

            var state = packet.ReadByte();
            if (state == 0)
            {
                BackToLogin();
                return;
            }

            PinCodeResCode result;

            var isEmpty = packet.ReadBool();
            var userID = packet.ReadInt();

            if (AssertError(userID != Player.UserID, "Invalid AccountID in PinCode request"))
            {
                Disconnect();
                return;
            }

            if (Player.PinSecret == "")
            {
                result = PinCodeResCode.Success;
                goto SendPacket;
            }


            if (isEmpty)
            {
                result = PinCodeResCode.Assigned;
                goto SendPacket;
            }

            var pin = packet.ReadString();

            if (!ValidateTOTP(pin))
            {
                if (AssertWarning(++(Player.FailCountbyPinCode) > 4, "Too many wrong pincodes entered"))
                {
                    Disconnect();
                    return;
                }

                result = PinCodeResCode.Incorrect;
            }
            else
            {
                result = PinCodeResCode.Success;
            }

        SendPacket:
            var pack = new Packet(ServerMessages.PIN_OPERATION);
            pack.WriteByte(result);

            SendPacket(pack);

            if (result == PinCodeResCode.Success)
                Player.State = Player.LoginState.WorldSelect;
        }

        public void OnConfirmEULA(Packet packet)
        {
            if (AssertWarning(Player.State != Player.LoginState.ConfirmEULA, "Tried to confirm EULA while not in dialog")) return;

            if (packet.ReadBool())
            {
                Server.Instance.UsersDatabase.RunQuery(
                    "UPDATE users SET confirmed_eula = 1 WHERE ID = @id",
                    "@id", Player.UserID
                );

                Packet pack = new Packet(ServerMessages.CONFIRM_EULA_RESULT);
                pack.WriteBool(true);
                SendPacket(pack);
            }

            BackToLogin();
        }

        public void OnTOTP(Packet packet)
        {
            if (AssertWarning(
                Player.State != Player.LoginState.WorldSelect &&
                Player.State != Player.LoginState.CharacterCreation &&
                Player.State != Player.LoginState.CharacterSelect,
                $"Tried to do TOTP stuff while in {Player.State}")) return;

            switch (packet.ReadByte())
            {
                case 0:
                    {
                        var key = packet.ReadString();
                        var pin = packet.ReadString();
                        var save = packet.ReadBool();

                        var p = new Packet(CfgServerMessages.CFG_TOTP);
                        p.WriteByte(0);
                        p.WriteBool(save);
                        if (ValidateTOTP(pin, key))
                        {
                            log.Info("User is setting up TOTP, passed check.");
                            p.WriteBool(true);

                            if (save)
                            {
                                Player.PinSecret = key;

                                if (AssertWarning(KeyUpdates++ > 5, $"User updated the TOTP key {KeyUpdates} times.. Kicking."))
                                {
                                    Disconnect();
                                    return;
                                }

                                Server.Instance.UsersDatabase.RunQuery(
                                    "UPDATE users SET pin_secret = @key WHERE ID = @id",
                                    "@id", Player.UserID,
                                    "@key", key
                                );
                            }
                        }
                        else
                        {
                            log.Warn("User is setting up TOTP, but check failed.");
                            p.WriteBool(false);
                        }

                        SendPacket(p);
                        break;
                    }

            }
        }

        private void BackToLogin()
        {
            Player.State = Player.LoginState.LoginScreen;
            Program.MainForm.ChangeLoad(false);

            Loaded = false;
            Player.LoggedOn = false;
            RedisBackend.Instance.RemovePlayerOnline(Player.UserID);
        }
    }
}