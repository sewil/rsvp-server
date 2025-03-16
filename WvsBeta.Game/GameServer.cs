using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using log4net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Database;
using WvsBeta.Game.GameObjects;
using WvsBeta.Game.GameObjects.MiniRooms;
using WvsBeta.Game.Handlers;
using WvsBeta.Game.Packets;
using WvsBeta.SharedDataProvider;
using WzTools.FileSystem;
using WzTools.Objects;

namespace WvsBeta.Game
{
    public class Server
    {
        private static ILog _log = LogManager.GetLogger(typeof(Server));

        public static bool Tespia { get; private set; }
        public static Server Instance { get; private set; }

        public Rand32 Randomizer { get; set; } = new Rand32();
        public double RateMobEXP = 1.0d;
        public double RateMesoAmount = 1.0d;
        public double RateDropChance = 1.0d;
        
        public string[] AllowedHaProxyIPs { get; set; }
        public byte ID { get; set; }
        public bool InMigration { get; set; }
        public bool IsNewServerInMigration { get; set; }
        public bool CenterMigration { get; set; }
        public string Name { get; set; }
        public string WorldName { get; set; }
        public byte WorldID { get; set; }

        private bool _memoryAutobanEnabled = true;

        public bool MemoryAutobanEnabled
        {
            get => _memoryAutobanEnabled;
            set
            {
                _log.Info($"Memory Autoban Enabled: {value}");
                _memoryAutobanEnabled = value;
            }
        }

        public int CenterPort { get; set; }
        public IPAddress CenterIP { get; set; }

        public ushort Port { get; set; }
        public IPAddress PublicIP { get; set; }
        public IPAddress PrivateIP { get; set; }
        public bool LazyLoadScripts { get; set; }

        public CenterSocket CenterConnection { get; set; }

        public int GetOnlineId() => RedisBackend.GetOnlineId(WorldID, ID);

        public bool Initialized { get; private set; }
        
        public GameAcceptor GameAcceptor { get; private set; }
        public GameHaProxyAcceptor GameHaProxyAcceptor { get; private set; }
        public MySQL_Connection CharacterDatabase { get; private set; }

        public Dictionary<int, Tuple<Packet, long>> CCIngPlayerList { get; } = new Dictionary<int, Tuple<Packet, long>>();
        public ConcurrentDictionary<string, Player> PlayerList { get; } = new ConcurrentDictionary<string, Player>();
        public Dictionary<int, Character> CharacterList { get; } = new Dictionary<int, Character>();
        public HashSet<Character> StaffCharacters { get; } = new HashSet<Character>();

        public HashSet<Guild> Guilds { get; } = new HashSet<Guild>();

        public Dictionary<int, (string reason, string name, byte level, BanReasons banReason, long time)> DelayedBanRecords { get; } = new Dictionary<int, (string, string, byte, BanReasons, long)>();

        public DiscordReporter BanDiscordReporter { get; private set; }
        public DiscordReporter ServerTraceDiscordReporter { get; private set; }
        public DiscordReporter PlayerLogDiscordReporter { get; private set; }
        public DiscordReporter MutebanDiscordReporter { get; private set; }
        public DiscordReporter ReportingDiscordReporter { get; private set; }

        private IDictionary<string, object> _availableNPCScripts { get; } = new ConcurrentDictionary<string, object>();

        public Dictionary<string, string> Vars { get; } = new Dictionary<string, string>();

        public string ScrollingHeader { get; private set; }
        
        public GuildManager GuildManager { get; private set; }

        public void SetScrollingHeader(string newText)
        {
            ScrollingHeader = newText;
            Program.MainForm.LogAppend("Updating scrolling header to: {0}", ScrollingHeader);
            ServerTraceDiscordReporter.Enqueue($"Updating scrolling header to: `{ScrollingHeader}`");

            MessagePacket.SendText(MessagePacket.MessageTypes.Header, ScrollingHeader, null, MessagePacket.MessageMode.ToChannel);
        }

        public Guild GetGuild(string name) => GuildManager.GetGuild(name);
        public Guild GetGuild(int id) => GuildManager.GetGuild(id);
        public Guild GetGuildForCharacterID(int characterID) => GuildManager.GetGuildForCharacterID(characterID);

        public void AddDelayedBanRecord(Character chr, string reason, BanReasons banReason, int extraDelay)
        {
            // Only enqueue when we haven't recorded it yet, otherwise you would
            // be able to extend the A/B delay.
            if (DelayedBanRecords.ContainsKey(chr.UserID)) return;


            Character.HackLog.Info(new Character.PermaBanLogRecord
            {
                reason = reason
            });
            var seconds = Rand32.NextBetween(3, 10) + extraDelay;
            DelayedBanRecords[chr.UserID] = (reason, chr.Name, chr.Level, banReason, MasterThread.CurrentTime + (seconds * 1000));

            var str = $"Enqueued delayed permban for userid {chr.UserID}, charname {chr.Name}, level {chr.Level}, reason ({banReason}) {reason}, map {chr.MapID} in {seconds} seconds...";
            BanDiscordReporter.Enqueue(str);

            MessagePacket.SendNoticeGMs(
                str,
                MessagePacket.MessageTypes.Notice
            );
        }

        public void CheckMaps(long pNow)
        {
            MapProvider.Maps.ForEach(x => x.Value.MapTimer(pNow));
        }

        public object TryGetOrCompileScript(string scriptName, Action<string> errorHandlerFnc)
        {
            _log.Debug($"Trying to find script {scriptName} in {_availableNPCScripts.Count} elements...");
            if (_availableNPCScripts.TryGetValue(scriptName, out var ret))
            {
                _log.Debug("Found script, using it.");
                return ret;
            }
            
            var scriptUri = GetScriptFilename(scriptName);
            _log.Debug($"Script not found, trying to compile {scriptUri}");

            if (scriptUri == null)
            {
                errorHandlerFnc?.Invoke($"Unable to find the script with name {scriptName}.");
                return null;
            }

            return ForceCompileScriptfile(scriptUri, errorHandlerFnc);
        }

        public string ScriptsDir => Path.Combine(Environment.CurrentDirectory, "..", "DataSvr", "Scripts");

        public string GetScriptFilename(string scriptName)
        {
            var scriptsDir = ScriptsDir;

            string filename = Path.Combine(scriptsDir, scriptName + ".s");
            if (!File.Exists(filename)) filename = Path.Combine(scriptsDir, scriptName + ".cs");
            if (!File.Exists(filename)) return null;
            return filename;
        }

        public void CompileAllScripts()
        {
            _log.Info("Compiling all scripts");
            var sd = new DirectoryInfo(ScriptsDir);
            var scripts = sd.GetFiles("*.s", SearchOption.TopDirectoryOnly).Concat(sd.GetFiles("*.cs", SearchOption.TopDirectoryOnly));

            try
            {
                scripts.AsParallel().ForAll(x =>
                {
                    ForceCompileScriptfile(x.FullName, null);
                });
            }
            catch (Exception ex)
            {
                _log.Error("Compilation failed", ex);
            }

            _log.Info($"Compiled {_availableNPCScripts.Count} scripts!");
        }

        public object ForceCompileScriptfile(string filePath, Action<string> errorHandlerFnc)
        {
            filePath = Path.GetFullPath(filePath);
            _log.Info($"Compiling {filePath}");

            var compileLog = filePath + ".log";
            if (File.Exists(compileLog)) File.Delete(compileLog);

            var results = Scripting.CompileScriptRoslyn(filePath);
            if (results.Errors.Count > 0)
            {
                errorHandlerFnc?.Invoke(Path.GetFileName(filePath));

                _log.Warn($"Couldn't compile the file ({filePath}) correctly:");
                foreach (CompilerError error in results.Errors)
                {
                    var errorText = $"File {filePath}, Line {error.Line}, Column {error.Column}: {error.ErrorText}";
                    File.AppendAllText(compileLog, errorText + "\r\n");
                    _log.Warn(errorText);
                }
                
                _log.Error($"Compilation of {filePath} failed!");
                return null;
            }


            var filename = Path.GetFileNameWithoutExtension(filePath);

            var script = Scripting.FindInterfaceImplementor(results.CompiledAssembly, typeof(IScriptV2), typeof(INpcScript));

            if (script is IScriptV2 v2)
            {
                v2.ScriptName = filename;
            }
            
            _log.Debug($"Compiled {filePath}, stored as {filename}");
            _availableNPCScripts[filename] = script;
            return script;
        }

        public void AddPlayer(Player player)
        {
            string hash;
            do
            {
                hash = Cryptos.GetNewSessionHash();
            } while (PlayerList.ContainsKey(hash));
            PlayerList.TryAdd(hash, player);
            player.SessionHash = hash;
        }

        public void RemovePlayer(string hash)
        {
            for (var i = 0; i < 3; i++)
            {
                if (PlayerList.TryRemove(hash, out _)) return;
            }
            _log.Error($"Unable to remove player with hash {hash}");
        }

        public Character GetCharacter(int ID)
        {
            return CharacterList.TryGetValue(ID, out var ret) ? ret : null;
        }
        
        public Character GetCharacter(string name)
        {
            return CharacterList.Values.FirstOrDefault(x => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
        }
        
        public Character GetRandomCharacter(bool checkAfk = false)
        {
            return CharacterList.Values.Where(character => !character.IsAFK || !checkAfk).FirstRandom();
        }
        
        public Player GetPlayer(string hash)
        {
            return PlayerList.TryGetValue(hash, out var ret) ? ret : null;
        }


        public static void Init(string configFile)
        {
            Instance = new Server()
            {
                Name = configFile,
                ID = 0xFF
            };
            Instance.Load();
        }
        
        public string GetConfigPath(params string[] pathElements) =>
            Path.Combine(new [] {Environment.CurrentDirectory, "..", "DataSvr"}.Concat(pathElements).ToArray());

        void Load()
        {
            var startTime = MasterThread.CurrentTime;
            Program.MainForm.LogAppend("Server starting...");

            _availableNPCScripts["samplenpc"] = new Sample();

            Omok.TestOmokLogic();

            Initialized = false;
            LoadConfig(new ConfigReader(GetConfigPath(Name + ".img")));
            LoadDBConfig(GetConfigPath("Database.img"));
            SetupGuildManager();

            MasterThread.RepeatingAction.Start("RemoveNotConnectingPlayers",
                curTime =>
                {
                    var tmp = CCIngPlayerList.ToArray();
                    foreach (var elem in tmp)
                    {
                        if ((elem.Value.Item2 - curTime) > 10000)
                        {
                            CCIngPlayerList.Remove(elem.Key);
                        }
                    }
                },
                TimeSpan.Zero, 
                TimeSpan.FromSeconds(5)
            );

            MasterThread.RepeatingAction.Start("Delayed Ban Processor",
                curTime =>
                {
                    var tmp = DelayedBanRecords.ToList();
                    foreach (var keyValuePair in tmp)
                    {
                        var value = keyValuePair.Value;
                        var userid = keyValuePair.Key;
                        if (value.time > curTime) continue;

                        CenterConnection.KickUser(userid);
                        CharacterDatabase.PermaBan(userid, (byte)value.banReason, "AB-" + Name, value.reason);

                        var (maxMachineBanCount, maxUniqueBanCount, maxIpBanCount) = CharacterDatabase.GetUserBanRecordLimit(userid);
                        var (machineBanCount, uniqueBanCount, ipBanCount) = CharacterDatabase.GetUserBanRecord(userid);

                        var str = $"Delayed permaban for userid {userid}, charname {value.name}, level {value.level}, reason {value.reason}. Ban counts: {machineBanCount}/{uniqueBanCount}/{ipBanCount} of {maxMachineBanCount}/{maxUniqueBanCount}/{maxIpBanCount}.";
                        if (uniqueBanCount >= maxUniqueBanCount ||
                            ipBanCount >= maxIpBanCount)
                        {
                            str += " Reached limits, so new accounts are useless.";
                        }

                        BanDiscordReporter.Enqueue(str);

                        MessagePacket.SendNoticeGMs(
                            str,
                            MessagePacket.MessageTypes.Notice
                        );

                        DelayedBanRecords.Remove(userid);
                    }
                },
                TimeSpan.Zero, 
                TimeSpan.FromSeconds(1)
            );
            
            MasterThread.RepeatingAction.Start("Miniroom Updater", MiniRoomBase.UpdateMiniRoom, TimeSpan.Zero, TimeSpan.FromSeconds(1));

            ContinentMan.Init();

            DiscordReporter.Username = Program.IMGFilename;
            BanDiscordReporter = new DiscordReporter(DiscordReporter.BanLogURL, "Ban");
            ServerTraceDiscordReporter = new DiscordReporter(DiscordReporter.ServerTraceURL, "ServerTrace");
            MutebanDiscordReporter = new DiscordReporter(DiscordReporter.MuteBanURL, "MuteBan");
            ReportingDiscordReporter = new DiscordReporter(DiscordReporter.ReportsURL, "Reports");
            PlayerLogDiscordReporter = new DiscordReporter(DiscordReporter.PlayerLogURL, "PlayerLog");

            if (!LazyLoadScripts)
            {
                Task.Run(() =>
                {
                    // Compile all scripts in the background
                    CompileAllScripts();

                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                });
            }

            DataProvider.Load();
            MapProvider.Load();
            QuestsProvider.Load();
            QuestNamesProvider.Load();
            CommunicatorProvider.Load();
            MapProvider.FinishLoading();
            QuestsProvider.FinishLoading();
            RateCredits.LoadExcludedMaps();

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);

            _log.Info("Connecting to center...");
            for (var reconnects = 0; reconnects < 8; reconnects++)
            {
                for (var timeouts = 0; timeouts < 10; timeouts++)
                {
                    if (ID != 0xFF) break;
                    System.Threading.Thread.Sleep(100);
                }
                if (ID != 0xFF) break;

                ConnectToCenter();
                System.Threading.Thread.Sleep(500);
            }

            if (ID == 0xFF)
            {
                _log.Error("No server to connect to?");
                Environment.Exit(1);
            }

            LoadFieldSet();
            MasterThread.RepeatingAction.Start("Map Checker", CheckMaps, TimeSpan.Zero, TimeSpan.FromSeconds(1));
            MasterThread.RepeatingAction.Start("FieldSet Checker", FieldSet.Update, TimeSpan.Zero, TimeSpan.FromSeconds(1));

            MasterThread.RepeatingAction.Start("Log killed mobs",
                _ => MapProvider.Maps.Values.ForEach(x => x.FlushMobKillCount()),
                TimeSpan.Zero, 
                TimeSpan.FromMinutes(1)
            );

            MasterThread.RepeatingAction.Start("Seed GlobalRnd", () => Rand32.Next(), 0, 1000);

            OnlineStats.StartLogger();
            EventDateMan.Init();

            Initialized = true;

            Program.MainForm.LogAppend("Server started!");
            ServerTraceDiscordReporter.Enqueue($"Server {Name} booted up! Time {MasterThread.CurrentTime - startTime}ms");
        }
        private void SetupGuildManager()
        {
            GuildManager = new GuildManager();
            GuildManager.OnGuildUpdated += guild =>
            {
                guild.SendGuildInfoUpdate();
            };

            Packet MakeGuildPacket(GuildHandler.GuildServerOpcodes opcode)
            {
                var outPacket = new Packet(CfgServerMessages.CFG_GUILD);
                outPacket.WriteByte(opcode);
                return outPacket;
            }

            void SendPlayerLeft(Guild guild, GuildCharacter character, bool kicked)
            {
                // Tell everyone of the guild the person left/is kicked
                var outPacket = MakeGuildPacket(GuildHandler.GuildServerOpcodes.PlayerLeft);
                outPacket.WriteInt(character.CharacterID);
                outPacket.WriteBool(kicked);
                guild.BroadcastPacket(outPacket);

                // Tell everyone in the map that the character is not in a guild anymore
                var chr = GetCharacter(character.CharacterID);
                chr?.Field.SendPacket(outPacket);
            }

            GuildManager.OnPlayerKicked += (guild, character) =>
            {
                SendPlayerLeft(guild, character, true);
            };
            GuildManager.OnPlayerLeft += (guild, character) =>
            {
                SendPlayerLeft(guild, character, false);
            };

            GuildManager.OnPlayerUpdated += (guild, character) =>
            {
                var pw = MakeGuildPacket(GuildHandler.GuildServerOpcodes.UpdatePlayer);
                character.Encode(pw);
                guild.BroadcastPacket(pw);
            };

            GuildManager.OnGuildDisbanded += guild =>
            {
                guild.Characters.ForEach(guildCharacter =>
                {
                    SendPlayerLeft(guild, guildCharacter, false);
                });
            };

            GuildManager.OnPlayerJoined += (guild, character) =>
            {
                // Tell all guild members that the player joined
                var pw = MakeGuildPacket(GuildHandler.GuildServerOpcodes.JoinPlayer);
                character.Encode(pw);
                guild.BroadcastPacket(pw);
                
                var chr = GetCharacter(character.CharacterID);
                if (chr != null)
                {
                    guild.SendGuildInfoUpdate(chr);

                    guild.SendGuildMemberInfoUpdate(chr);
                }
            };

            GuildManager.OnPlayerChat += (guild, character, text) =>
            {
                var pw = new Packet(ServerMessages.GROUP_MESSAGE);
                pw.WriteByte(2); // guild chat
                pw.WriteString(character.CharacterName);
                pw.WriteString(text);
                guild.BroadcastPacket(pw, character.CharacterID);
            };
            
        }


        public void ConnectToCenter()
        {
            if (CenterConnection?.Disconnected == false) return;
            CenterConnection = new CenterSocket();
        }

        private void LoadDBConfig(string configFile)
        {
            var reader = new ConfigReader(configFile);
            CharacterDatabase = new MySQL_Connection(MasterThread.Instance, reader);
            BaseCharacterInventory.Connection = CharacterDatabase;
            CharacterCashItems.Connection = CharacterDatabase;
        }

        private void LoadConfig(ConfigReader reader)
        {
            Port = reader["port"].GetUShort();
            WorldID = reader["gameWorldId"]?.GetByte() ?? 0;

            CharacterCashItems.WorldID = WorldID;
            log4net.GlobalContext.Properties["WorldID"] = WorldID;

            PublicIP = IPAddress.Parse(reader["PublicIP"].GetString());
            PrivateIP = IPAddress.Parse(reader["PrivateIP"].GetString());

            CenterIP = IPAddress.Parse(reader["center"]["ip"].GetString());
            CenterPort = reader["center"]["port"].GetUShort();
            WorldName = reader["center"]["worldName"].GetString();

            Tespia = reader["tespia"]?.GetBool() ?? false;
            MemoryAutobanEnabled = reader["memoryAutobanEnabled"]?.GetBool() ?? true;
            LazyLoadScripts = reader["lazyLoadScripts"]?.GetBool() ?? false;
            
            DiscordReporter.LoadURLs(reader["discord"]);

            var tmpHeader = reader["scrollingHeader"]?.GetString() ?? "";
            if (tmpHeader == "EMPTY")
            {
                tmpHeader = "";
            }

            ScrollingHeader = tmpHeader;
            
            var haProxyIPs = reader["HaProxyIPs"];
            if (haProxyIPs != null)
            {
                AllowedHaProxyIPs = haProxyIPs.Select(x => x.Value).ToArray();
            }

            RedisBackend.Init(reader);
        }

        public void LoadFieldSet()
        {
			var path = GetConfigPath("Server", "FieldSet.img");

            using var fieldSet = new FSFile(path);
            
            foreach (var node in fieldSet)
            {
                var fs = new FieldSet();
                fs.Load(node);
            }
        }

        public void StartListening()
        {
            Program.MainForm.LogAppend($"Starting to listen on port {Port}");
            GameAcceptor = new GameAcceptor();
            GameHaProxyAcceptor = new GameHaProxyAcceptor(AllowedHaProxyIPs);
        }

        public void StopListening()
        {
            Program.MainForm.LogAppend($"Stopped listening on port {Port}");
            GameAcceptor?.Stop();
            GameAcceptor = null;
            GameHaProxyAcceptor?.Stop();
            GameHaProxyAcceptor = null;
        }
    }
}
