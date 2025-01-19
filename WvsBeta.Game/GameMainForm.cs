using System;
using System.Diagnostics;
using Newtonsoft.Json.Serialization;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.GameObjects;

namespace WvsBeta.Game
{
    class GameMainForm : MainFormConsole
    {
        private int load = 0;

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
            Server.Init(Program.IMGFilename);


            LogAppend("Server successfully booted!");

            if (Server.Instance.InMigration)
            {
                // Tell the other server to start migrating...
                var pw = new Packet(ISClientMessages.ServerMigrationUpdate);
                pw.WriteByte((byte)ServerMigrationStatus.StartMigration);
                Server.Instance.CenterConnection.SendPacket(pw);
            }

            Pinger.Init(x => Program.MainForm.LogAppend(x), x => Program.MainForm.LogAppend(x));
        }


        public override void ChangeLoad(bool up)
        {
            if (up)
            {
                ++load;
                //LogAppend(string.Format("[{0}] Received a connection! The server now has {1} connections.", DateTime.Now.ToString(), load));
            }
            else
            {
                --load;
                //LogAppend(string.Format("[{0}] Lost a connection! The server now has {1} connections.", DateTime.Now.ToString(), load));
            }

            Server.Instance.CenterConnection?.SendUpdateConnections(load);
        }


        private static bool alreadyShuttingDown = false;
        private static bool forceShutDown = false;

        public override void Shutdown(ConsoleCancelEventArgs args)
        {
            if (forceShutDown) return;

            if (args != null) args.Cancel = true;
            StartShutdown();
        }

        private void StartShutdown()
        {
            if (alreadyShuttingDown)
            {
                return;
            }
            alreadyShuttingDown = true;

            Program.MainForm.LogAppend("Getting rid of players");

            var timeoutSeconds = 10;

            var startTime = MasterThread.CurrentTime;
            MasterThread.RepeatingAction ra = null;
            ra = new MasterThread.RepeatingAction(
                "Client DC Thread",
                (date) =>
                {
                    var isTimeout = (date - startTime) > timeoutSeconds * 1000;
                    if (Server.Instance.PlayerList.Count == 0 || isTimeout)
                    {
                        var queueLen = MasterThread.Instance.CurrentCallbackQueueLength;
                        var waiting = (long)Math.Max(700, Math.Min(3000, queueLen * 10));
                        LogAppend($"Preparing shutdown... Timeout? {isTimeout} Queue size: {queueLen}, connections {Pinger.CurrentLoggingConnections}, waiting for {waiting} millis");

                        MasterThread.RepeatingAction.Start(
                            "Server finalizing and shutdown thread",
                            (date2) =>
                            {
                                forceShutDown = true;
                                Server.Instance.CenterConnection?.Disconnect();
                                MasterThread.Instance.Stop = true;
                                Environment.Exit(0);
                            },
                            waiting,
                            0
                        );

                        MasterThread.Instance.RemoveRepeatingAction(ra);
                    }
                    else
                    {
                        Server.Instance.CharacterList.Values.ForEach(x =>
                        {
                            try
                            {
                                x.DestroyAdditionalProcess();
                                x.Disconnect();
                            }
                            catch (Exception ex)
                            {
                                _log.Error($"Unable to kick {x}", ex);
                            }
                        });
                    }
                },
                0,
                100
            );

            ra.Start();
        }

        public override void HandleCommand(string name, string[] args)
        {
            switch (name)
            {
                case "saveall":
                    _log.Warn("Saving all characters...");
                    Server.Instance.CharacterList.Values.ForEach(x => x.WrappedLogging(() => x.Save()));
                    _log.Warn("Saved!");
                    break;

                case "dcall":
                    _log.Warn("Disconnecting all characters...");
                    Server.Instance.CharacterList.Values.ForEach(x => x.WrappedLogging(() => x.Disconnect()));
                    _log.Warn("Everyone should be gone.");
                    break;
            }
        }
    }
}
