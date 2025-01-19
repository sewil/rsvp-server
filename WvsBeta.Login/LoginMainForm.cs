using System;
using System.Linq;
using System.Net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Login
{
    public class LoginMainForm : MainFormConsole
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
            try
            {
                Server.Init(Program.IMGFilename);

                this.Text += " (" + Program.IMGFilename + ")";
                if (Server.Tespia)
                {
                    Text += " -TESPIA MODE-";
                }


                Pinger.Init(Program.MainForm.LogAppend, Program.MainForm.LogAppend);

                MasterThread.RepeatingAction.Start(
                    "Form Updater",
                    (date) =>
                    {
                        Console.WriteLine("Ping entries: {0} | Load: {1}", Pinger.CurrentLoggingConnections, load);
                    },
                    0,
                    1500
                );

            }
            catch (Exception ex)
            {
                Program.LogFile.WriteLine("Got exception @ frmMain::InitializeServer : {0}", ex.ToString());
                throw;
            }
        }

        public override void ChangeLoad(bool up)
        {
            if (up)
            {
                ++load;
            }
            else
            {
                --load;
            }

            foreach (var kvp in Server.Instance.Worlds)
            {
                if (!kvp.Value.IsConnected) continue;
                kvp.Value.Connection.UpdateConnections(load);
            }
        }

        public override void Shutdown(ConsoleCancelEventArgs args)
        {
            if (!Server.Instance.InMigration)
                Server.Instance.RemoveOnlineServerConfig();

            if (Server.Instance.UsersDatabase != null)
                Server.Instance.UsersDatabase.Stop = true;
            MasterThread.Instance.Stop = true;

            Environment.Exit(0);
        }

        public override void HandleCommand(string name, string[] args)
        {
            switch (name)
            {
                case "update-haproxy-ips":
                    {
                        var ips = args.Select(x =>
                        {
                            return (IPAddress.TryParse(x, out var tmp), x, tmp);
                        }).ToArray();

                        var invalid = ips.Where(x => !x.Item1).ToArray();
                        if (invalid.Length > 0)
                        {
                            invalid.ForEach(x => _log.Error($"Invalid ip: {x.x}"));
                            return;
                        }

                        var p = new Packet(ISClientMessages.BroadcastPacketToAllServers);
                        p.WriteByte((byte)ISServerMessages.UpdateHaProxyIPs);
                        p.WriteShort((short)ips.Length);
                        foreach (var (_, _, ip) in ips)
                        {
                            p.WriteBytes(ip.MapToIPv4().GetAddressBytes());
                        }
                        Server.Instance.Worlds.Values.Where(x => x.IsConnected).ForEach(x => x.Connection?.SendPacket(p));

                        return;
                    }

                case "update-public-ip":
                    {
                        if (args.Length < 3)
                        {
                            _log.Info($"{name} [center] [servername] [new ip]");
                            return;
                        }

                        var centerName = args[0];
                        var serverName = args[1];
                        var newIP = args[2];

                        var center = Server.Instance.Worlds.Values.FirstOrDefault(x => x.Name == centerName);
                        if (center == null)
                        {
                            _log.Error($"Center with name {centerName} not found...");
                            return;
                        }

                        var p = new Packet(ISClientMessages.UpdatePublicIP);
                        p.WriteString(serverName);
                        p.WriteString(newIP);
                        center.Connection?.SendPacket(p);


                        return;
                    }
            }
        }
    }
}
