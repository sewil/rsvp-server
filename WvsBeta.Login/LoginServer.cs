using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using MySqlConnector;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Database;
using System.Linq;
using System.Security.Cryptography;
using WvsBeta.Common.Crypto;

namespace WvsBeta.Login
{
    class Server
    {
        public static Server Instance { get; private set; }
        public static bool Tespia { get; private set; }

        public string[] AllowedHaProxyIPs { get; set; }
        public ushort Port { get; set; }
        public ushort AdminPort { get; set; }
        public ushort LTLPort => (ushort)(Port + 10000);
        public IPAddress PublicIP { get; set; }
        public IPAddress PrivateIP { get; set; }
        public bool RequiresEULA { get; set; }
        public Dictionary<byte, Center> Worlds = new Dictionary<byte, Center>();
        public string Name { get; set; }
        public bool InMigration { get; set; }
        public Dictionary<short, short> PatchNextVersion { get; } = new Dictionary<short, short>();
        public int DataChecksum { get; private set; }
        public int WzMssChecksum { get; private set; }
        public short CurrentPatchVersion { get; private set; }
        public bool AdminsRequirePublicKeyAuth { get; private set; }

        private LoginAcceptor LoginAcceptor { get; set; }
        public LoginHaProxyAcceptor LoginHaProxyAcceptor { get; private set; }
        public LoginToLoginAcceptor LoginToLoginAcceptor { get; set; }
        public LoginToLoginConnection LoginToLoginConnection { get; set; }

        public DiscordReporter ServerTraceDiscordReporter { get; private set; }

        public MySQL_Connection UsersDatabase { get; private set; }

        private ConcurrentDictionary<string, Player> PlayerList { get; } = new ConcurrentDictionary<string, Player>();

        public RSACryptoServiceProvider ServerKey { get; private set; }

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
            PlayerList.TryRemove(hash, out var tmp);
        }

        public bool IsPlayer(string hash)
        {
            return PlayerList.ContainsKey(hash);
        }

        public Player GetPlayer(string hash)
        {
            if (PlayerList.TryGetValue(hash, out var player)) return player;
            return null;
        }

        public bool GetWorld(byte worldId, out Center world, bool onlyConnected = true)
        {
            if (!Worlds.TryGetValue(worldId, out var tmp) || (onlyConnected && !tmp.IsConnected))
            {
                world = null;
                return false;
            }
            world = tmp;
            return true;
        }

        public static void Init(string configFile)
        {
            Instance = new Server()
            {
                Name = configFile
            };
            Instance.Load();
        }

        private string ServerConfigFile => Path.Combine(Environment.CurrentDirectory, "..", "DataSvr", Name + ".img");

        public void Load()
        {
            Program.MainForm.LogAppend("Reading Config File... ", false);
            LoadConfig(ServerConfigFile);
            LoadClientPatchData(ServerConfigFile);
            LoadDBConfig(Path.Combine(Environment.CurrentDirectory, "..", "DataSvr", "Database.img"));
            Program.MainForm.LogAppend(" Done!", false);

            Program.MainForm.LogAppend("Starting to patch... ", false);
            DataBasePatcher.StartPatching(UsersDatabase, Path.Combine(Environment.CurrentDirectory, "evolutions", "login"), "login");

            Program.MainForm.LogAppend(" Done!", false);

            MasterThread.RepeatingAction.Start("Center Reconnect Timer", time =>
            {
                foreach (var kvp in Worlds)
                {
                    if (kvp.Value.IsConnected) continue;
                    try
                    {
                        kvp.Value.Connect();
                    }
                    catch { }
                }
            }, 0, 5000);


            using (var reader = UsersDatabase.RunQuery(
                "SELECT private_ip FROM servers WHERE configname = @configName AND world_id = 0",
                "@configName", Name
            ) as MySqlDataReader)
            {
                if (reader != null && reader.Read())
                {
                    // Server exists, try to migrate
                    var privateIp = reader.GetString("private_ip");
                    Program.MainForm.LogAppend("Starting migration... {0}:{1}", privateIp, LTLPort);
                    reader.Close();

                    try
                    {
                        var wasConnected = false;
                        LoginToLoginConnection = new LoginToLoginConnection(privateIp, LTLPort);
                        for (var i = 0; i < 10; i++)
                        {
                            System.Threading.Thread.Sleep(250);
                            if (LoginToLoginConnection.Disconnected != false) continue;
                            wasConnected = true;
                            break;
                        }

                        if (!wasConnected)
                        {
                            LoginToLoginConnection.PreventConnectFromSucceeding = true;
                            LoginToLoginConnection = null;
                            Program.MainForm.LogAppend("Not able to migrate as server is not accessible.");

                            StartLTLAcceptor();
                            StartListening();
                        }
                        else
                        {
                            Program.MainForm.LogAppend("Connected to LTL acceptor");
                            InMigration = false;
                            var pw = new Packet(ISServerMessages.ServerMigrationUpdate);
                            pw.WriteByte((byte)ServerMigrationStatus.StartMigration);
                            LoginToLoginConnection.SendPacket(pw);
                        }
                    }
                    catch (Exception ex)
                    {
                        Program.MainForm.LogAppend("Migration failed! {0}", ex);
                        // It failed.
                        StartLTLAcceptor();
                        StartListening();
                    }
                }
                else
                {
                    reader?.Close();

                    StartLTLAcceptor();
                    StartListening();
                }
            }

            DiscordReporter.Username = Name;
            ServerTraceDiscordReporter = new DiscordReporter(DiscordReporter.ServerTraceURL, "ServerTrace");

            ServerTraceDiscordReporter.Enqueue($"Server {Name} booted up!");
        }

        public void RemoveOnlineServerConfig()
        {
            UsersDatabase.RunQuery("DELETE FROM servers WHERE configname = @configName AND world_id = 0",
                "@configName", Name);
        }

        public void StartLTLAcceptor()
        {
            Program.MainForm.LogAppend("Starting LTL acceptor on port {0}", LTLPort);
            LoginToLoginAcceptor = new LoginToLoginAcceptor(LTLPort);
            RemoveOnlineServerConfig();
            UsersDatabase.RunQuery(
                "INSERT INTO servers VALUES (@configName, 0, @privateIp)",
                "@configName", Name,
                "@privateIp", PrivateIP
            );
        }

        public void StopListening()
        {
            LoginAcceptor?.Stop();
            LoginHaProxyAcceptor?.Stop();
            LoginAcceptor = null;
            LoginHaProxyAcceptor = null;
        }

        public void StartListening()
        {
            if (LoginAcceptor != null) return;
            LoginHaProxyAcceptor = new LoginHaProxyAcceptor(AllowedHaProxyIPs);
            LoginAcceptor = new LoginAcceptor();
        }


        private void LoadDBConfig(string configFile)
        {
            var reader = new ConfigReader(configFile);
            UsersDatabase = new MySQL_Connection(MasterThread.Instance, reader);
        }

        private void LoadConfig(string configFile)
        {
            var reader = new ConfigReader(configFile);
            Port = reader["port"].GetUShort();
            AdminPort = reader["adminPort"].GetUShort();
            PublicIP = IPAddress.Parse(reader["PublicIP"].GetString());
            PrivateIP = IPAddress.Parse(reader["PrivateIP"].GetString());

            RequiresEULA = reader["requiresEULA"]?.GetBool() ?? false;
            Tespia = reader["tespia"]?.GetBool() ?? false;
            AdminsRequirePublicKeyAuth = reader["adminsRequirePublicKeyAuth"]?.GetBool() ?? false;

            DiscordReporter.LoadURLs(reader["discord"]);

            foreach (var worldConfig in reader["center"])
            {
                var center = new Center
                {
                    Channels = worldConfig["channelNo"].GetByte(),
                    ID = worldConfig["world"].GetByte(),
                    Port = worldConfig["port"].GetUShort(),
                    IP = IPAddress.Parse(worldConfig["ip"].GetString()),
                    AdultWorld = worldConfig["adult"]?.GetBool() ?? false,
                    EventDescription = worldConfig["eventDesc"]?.GetString() ?? "",
                    BlockCharacterCreation = worldConfig["BlockCharCreation"]?.GetBool() ?? false,
                    State = worldConfig["worldState"]?.GetByte() ?? 0,
                    Name = worldConfig.Name,
                    UserNoMultiplier = worldConfig["userNoMultiplier"]?.GetFloat() ?? 8.0f,
                };
                center.UserNo = new int[center.Channels];

                Worlds.Add(center.ID, center);
            }

            var haProxyIPs = reader["HaProxyIPs"];
            if (haProxyIPs != null)
            {
                AllowedHaProxyIPs = haProxyIPs.Select(x => x.Value).ToArray();
            }

            LoadPrivateKey(reader["privateKey"]);

            RedisBackend.Init(reader);
            LoginDataProvider.Load();
        }

        private void LoadPrivateKey(Node config)
        {
            var path = config?["path"]?.GetString();
            var password = config?["password"]?.GetString();

            if (path != null && password != null)
            {
                if (!File.Exists(path))
                {
                    Program.MainForm.LogAppend("Unable to load private key, path does not exist: {0}", path);
                    return;
                }

                Program.MainForm.LogAppend("Loading Private Key: {0}", path);
                ServerKey = EncryptedRSA.ReadPrivateKeyFromFile(path, password);
            }
            else
            {
                Program.MainForm.LogAppend("Unable to load private key. Either missing 'password' or 'path' in config under 'privateKey'");
            }
        }

        private void LoadClientPatchData(string configFile)
        {
            var reader = new ConfigReader(configFile);
            PatchNextVersion.Clear();
            CurrentPatchVersion = 0;
            DataChecksum = reader["dataChecksum"]?.GetInt() ?? 0;
            WzMssChecksum = reader["wzmssChecksum"]?.GetInt() ?? 0;

            var versionAndChecksumNode = reader["versionUpdates"];
            if (versionAndChecksumNode != null)
            {
                foreach (var node in versionAndChecksumNode)
                {
                    var fromVersion = short.Parse(node.Name);
                    var usingVersion = node.GetShort();
                    PatchNextVersion.Add(fromVersion, usingVersion);
                    CurrentPatchVersion = Math.Max(CurrentPatchVersion, fromVersion);
                    Program.MainForm.LogAppend("Loaded patch {0:D5}to{1:D5} for v{0}.{2}", Constants.MAPLE_VERSION, usingVersion, fromVersion);
                }
            }

            Program.MainForm.LogAppend("Latest subpatch version: {0}", CurrentPatchVersion);
            Program.MainForm.LogAppend("Data checksum: {0} 0x{0:X8}", DataChecksum);
        }
    }
}
