using System;
using System.Linq;
using System.Threading;
using log4net.Core;
using WvsBeta.Common;
using WvsBeta.Shop.Properties;

namespace WvsBeta.Shop
{
    public class ShopMainForm : MainFormConsole
    {
        int load;

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

            LogAppend("Loading data file... ", false);

            ShopProvider.Load();
            LogAppend("DONE");

            Text += " (" + Program.IMGFilename + ")";
            if (Server.Tespia)
            {
                Text += " -TESPIA MODE-";
            }

            Pinger.Init(x => Program.MainForm.LogAppend(x), x => Program.MainForm.LogAppend(x));
        }


        public override void ChangeLoad(bool up)
        {
            if (up)
                ++load;
            else
                --load;

            Server.Instance.CenterConnection.updateConnections(load);
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

            int timeout = 10;

            var startTime = MasterThread.CurrentTime;
            new MasterThread.RepeatingAction(
                "Client DC Thread",
                (date) =>
                {
                    var isTimeout = (date - startTime) > timeout * 1000;
                    if (Server.Instance.PlayerList.Count == 0 || isTimeout)
                    {
                        var queueLen = MasterThread.Instance.CurrentCallbackQueueLength;
                        var waiting = (long) Math.Max(700, Math.Min(3000, queueLen * 100));
                        LogAppend($"Preparing shutdown... Timeout? {isTimeout} Queue size: {queueLen}, waiting for {waiting} millis");

                        new MasterThread.RepeatingAction(
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
                        ).Start();
                    }
                    else
                    {
                        Server.Instance.PlayerList.ForEach(x =>
                        {
                            if (x.Value.Character == null) return;
                            try
                            {
                                x.Value.Socket.Disconnect();
                            }
                            catch { }
                        });
                    }
                },
                0,
                100
            ).Start();

            return;
        }

        public override void HandleCommand(string name, string[] args)
        {
        }
    }
}