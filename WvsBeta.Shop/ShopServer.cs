using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Database;
using WvsBeta.Game;
using WvsBeta.SharedDataProvider;
using WzTools.FileSystem;

namespace WvsBeta.Shop
{
    class Server
    {
        public static Server Instance { get; private set; }
        public static bool Tespia { get; private set; }
        public string Name { get; private set; }
        public string WorldName { get; private set; }
        public byte WorldID { get; private set; }

        public int SlotIncreasePrice { get; private set; }
        public int TrunkIncreasePrice { get; private set; }
        
        public string[] AllowedHaProxyIPs { get; set; }
        public int CenterPort { get; set; }
        public IPAddress CenterIP { get; set; }
        public bool CenterMigration { get; set; }

        public ushort Port { get; private set; }
        public IPAddress PublicIP { get; set; }
        public IPAddress PrivateIP { get; private set; }

        public CenterSocket CenterConnection { get; set; }

        private ShopAcceptor ShopAcceptor { get; set; }
        public ShopHaProxyAcceptor HaProxyAcceptor { get; private set; }
        public MySQL_Connection CharacterDatabase { get; private set; }

        public Dictionary<string, Player> PlayerList { get; } = new Dictionary<string, Player>();
        public Dictionary<int, Character> CharacterList { get; } = new Dictionary<int, Character>();
        public Dictionary<int, Packet> CCIngPlayerList { get; } = new Dictionary<int, Packet>();

        public int GetOnlineId() => RedisBackend.GetOnlineId(WorldID, 50);

        private string ConfigFilePath { get; set; }

        public Dictionary<(byte category, byte gender, byte idx), int> BestItems { get; } = new Dictionary<(byte category, byte gender, byte idx), int>();
        public DiscordReporter ServerTraceDiscordReporter { get; private set; }

        public GuildManager GuildManager { get; private set; }

        public void AddPlayer(Player player)
        {
            var hash = Cryptos.GetNewSessionHash();
            while (PlayerList.ContainsKey(hash))
            {
                hash = Cryptos.GetNewSessionHash();
            }
            PlayerList[hash] = player;
            player.SessionHash = hash;
        }

        public void RemovePlayer(string hash)
        {
            PlayerList.Remove(hash);
        }


        public Player GetPlayer(string hash)
        {
            if (PlayerList.TryGetValue(hash, out var player)) return player;
            return null;
        }

        public static void Init(string configFile)
        {
            Instance = new Server()
            {
                Name = configFile
            };
            Instance.Load();
        }

        public void ConnectToCenter()
        {
            if (CenterConnection?.Disconnected == false) return;
            CenterConnection = new CenterSocket();
        }

        public void Load()
        {
            ConfigFilePath = Path.Combine(Environment.CurrentDirectory, "..", "DataSvr", Name + ".img");
            LoadConfig(ConfigFilePath);
            LoadDBConfig(Path.Combine(Environment.CurrentDirectory, "..", "DataSvr", "Database.img"));

            DataProvider.Load(DataProvider.LoadCategories.Equips | DataProvider.LoadCategories.Items | DataProvider.LoadCategories.Pets);

            LoadCashshopData();
            SetupGuildManager();

            ConnectToCenter();
            
            ServerTraceDiscordReporter = new DiscordReporter(DiscordReporter.ServerTraceURL, "ServerTrace");
            ServerTraceDiscordReporter.Enqueue($"Server {Name} booted up!");
        }

        private void SetupGuildManager()
        {
            GuildManager = new GuildManager();
        }

        private void LoadDBConfig(string configFile)
        {
            var reader = new ConfigReader(configFile);
            CharacterDatabase = new MySQL_Connection(MasterThread.Instance, reader);

            CharacterCashItems.Connection = CharacterDatabase;
            BaseCharacterInventory.Connection = CharacterDatabase;
        }

        private void LoadConfig(string configFile)
        {
            var reader = new ConfigReader(configFile);

            Port = reader["port"].GetUShort();
            WorldID = reader["gameWorldId"].GetByte();

            CharacterCashItems.WorldID = WorldID;
            log4net.GlobalContext.Properties["WorldID"] = WorldID;

            Tespia = reader["tespia"]?.GetBool() ?? false;

            PublicIP = IPAddress.Parse(reader["PublicIP"].GetString());
            PrivateIP = IPAddress.Parse(reader["PrivateIP"].GetString());

            CenterIP = IPAddress.Parse(reader["center"]["ip"].GetString());
            CenterPort = reader["center"]["port"].GetUShort();
            WorldName = reader["center"]["worldName"].GetString();


            SlotIncreasePrice = reader["slotIncreasePrice"]?.GetInt() ?? 2000;
            TrunkIncreasePrice = reader["trunkIncreasePrice"]?.GetInt() ?? 2000;
            
            var haProxyIPs = reader["HaProxyIPs"];
            if (haProxyIPs != null)
            {
                AllowedHaProxyIPs = haProxyIPs.Select(x => x.Value).ToArray();
            }

            RedisBackend.Init(reader);
        }

        public void LoadCashshopData()
        {
            BestItems.Clear();

            var fileSystem = new WzFileSystem();
            fileSystem.Init(Path.Combine(Environment.CurrentDirectory, "..", "DataSvr"));

            var bestItem = fileSystem.GetProperty("Server/BestItem.img");
            
            foreach (var categoryNode in bestItem.PropertyChildren)
            {
                if (!byte.TryParse(categoryNode.Name, out var category)) continue;

                foreach (var genderNode in categoryNode.PropertyChildren)
                {
                    if (!byte.TryParse(genderNode.Name, out var gender)) continue;

                    foreach (var (indexText, val) in genderNode)
                    {
                        if (!byte.TryParse(indexText, out var index)) continue;

                        BestItems[(category, gender, index)] = (int)val;
                    }
                }
            }
            
            CouponInfo.Load();
        }

        public void StartListening()
        {
            Program.MainForm.LogAppend($"Starting to listen on port {Port}");
            ShopAcceptor = new ShopAcceptor();
            HaProxyAcceptor = new ShopHaProxyAcceptor(AllowedHaProxyIPs);
        }

        public void StopListening()
        {
            Program.MainForm.LogAppend($"Stopped listening on port {Port}");
            ShopAcceptor?.Stop();
            ShopAcceptor = null;
            HaProxyAcceptor?.Stop();
            HaProxyAcceptor = null;
        }
    }
}