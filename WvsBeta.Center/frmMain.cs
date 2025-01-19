using System;
using log4net;
using WvsBeta.Common;

namespace WvsBeta.Center
{
    public class frmMain : MainFormConsole
    {
        private static ILog _onlinePlayerLog = LogManager.GetLogger("OnlinePlayers");
        public struct OnlinePlayerCount
        {
            public string serverName { get; set; }
            public int count { get; set; }
        }

        private string text;
        public new string Text {
            get => text; 
            set 
            {
                text = value;
                Console.Title = text;
            }
        }

        private int _totalConnections;


        public override void LogToFile(string what)
        {
            Program.LogFile.WriteLine(what);
        }

        public override void Shutdown()
        {
            Shutdown(null);
        }

        public override void InitializeServer()
        {
            try
            {
                CenterServer.Init(Program.IMGFilename);

                Text += " (" + Program.IMGFilename + ")";
                if (CenterServer.Tespia)
                {
                    Text += " -TESPIA MODE-";
                }
#if DEBUG
                Text += " -DEBUG-";
#endif

                MasterThread.RepeatingAction.Start("UpdateServerList", UpdateServerList, TimeSpan.Zero, TimeSpan.FromSeconds(5));

                MasterThread.RepeatingAction.Start(
                    "OnlinePlayerCounter",
                    date =>
                    {
                        int totalCount = 0;
                        foreach (var kvp in CenterServer.Instance.LocalServers)
                        {
                            var ls = kvp.Value;
                            if (ls.Connected == false) continue;
                            if (ls.Type != LocalServerType.Login &&
                                ls.Type != LocalServerType.Game &&
                                ls.Type != LocalServerType.Shop) continue;
                            totalCount += ls.Connections;
                            _onlinePlayerLog.Info(new OnlinePlayerCount
                            {
                                count = ls.Connections,
                                serverName = ls.Name
                            });
                        }

                        _onlinePlayerLog.Info(new OnlinePlayerCount
                        {
                            count = totalCount,
                            serverName = "TotalCount-" + CenterServer.Instance.World.ID
                        });
                    },
                    TimeSpan.FromMinutes(1),
                    TimeSpan.FromMinutes(1)
                );

                Pinger.Init(x => Program.MainForm.LogAppend(x), x=> Program.MainForm.LogAppend(x));
            }
            catch (Exception ex)
            {
                _log.Error("Got exception @ frmMain::InitServer", ex);
                Program.LogFile.WriteLine("Got exception @ frmMain::InitServer : {0}", ex.ToString());
                Console.WriteLine(ex);
                throw;
            }

            Console.WriteLine("Initialized");
        }

        public void UpdateServerList()
        {
            _totalConnections = 0;
            byte loginCount = 0;

            var lineFormat = " {0,-6} | {1,-15} | {2,-25} | {3,-15} | {4}";

            Console.WriteLine(lineFormat, "Online", "Name", "Address", "Connections", "Rates");

            foreach (var Server in CenterServer.Instance.LocalServers)
            {
                LocalServer ls = Server.Value;
                _totalConnections += ls.Connections;
                var elements = new object[]{
                    ls.Connected ? "Yes" : "No",
                    ls.Name,
                    ls.PublicIP + ":" + ls.Port,
                    ls.Connections.ToString(),
                    "N/A"
                };

                if (ls.IsGameServer)
                {
                    elements[1] = ls.Name + (ls.Connected ? " (CH. " + (ls.ChannelID + 1) + ")" : "");
                    elements[4] = $"{ls.RateMobEXP}/{ls.RateMesoAmount}/{ls.RateDropChance}";
                }

                if (ls.Connected)
                {
                    if (ls.Type == LocalServerType.Game)
                    {
                        RedisBackend.Instance.SetPlayerOnlineCount(
                            CenterServer.Instance.World.ID,
                            ls.ChannelID,
                            ls.Connections
                        );
                    }
                    else if (ls.Type == LocalServerType.Shop)
                    {
                        RedisBackend.Instance.SetPlayerOnlineCount(
                            CenterServer.Instance.World.ID,
                            50 + ls.ChannelID,
                            ls.Connections
                        );
                    }
                    else if (ls.Type == LocalServerType.Login)
                    {
                        RedisBackend.Instance.SetPlayerOnlineCount(
                            -1,
                            loginCount,
                            ls.Connections
                        );
                        loginCount++;
                    }
                }
 
                Console.WriteLine(lineFormat, elements);
            }

            Console.WriteLine();
            Console.WriteLine("Total connections: {0}", _totalConnections);
        }

        public override void ChangeLoad(bool up)
        {
            // Not used
        }


        private static bool forceShutDown = false;

        public override void Shutdown(ConsoleCancelEventArgs args)
        {
            if (forceShutDown) return;
            Console.WriteLine("Stopping...");

            if (args != null) args.Cancel = true;
            MasterThread.Instance.Stop = true;
            Environment.Exit(0);
        }

        public override void HandleCommand(string name, string[] args)
        {
        }
    }
}