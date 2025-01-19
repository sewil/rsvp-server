using System.Net.Sockets;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Login
{
    class LoginToLoginConnection : AbstractConnection
    {
        private bool migrated = false;

        public LoginToLoginConnection(Socket pSocket) : base(pSocket)
        {
            UseIvanPacket = true;
            SendHandshake(9994, "LoginToLogin", 99);
        }

        public LoginToLoginConnection(string pIP, ushort pPort) : base(pIP, pPort)
        {
            UseIvanPacket = true;
        }

        public override void OnDisconnect()
        {
            if (!migrated)
            {
                Program.MainForm.LogAppend("Disconnect in LTL before fully being migrated, opening LTL acceptor again");
                Server.Instance.LoginToLoginAcceptor?.Start();
                Server.Instance.InMigration = false;
                Server.Instance.StartListening();
            }
        }

        public override void AC_OnPacketInbound(Packet pPacket)
        {
            switch ((ISServerMessages)pPacket.ReadByte())
            {
                case ISServerMessages.ServerMigrationUpdate:


                    switch ((ServerMigrationStatus)pPacket.ReadByte())
                    {
                        case ServerMigrationStatus.StartMigration:
                            {
                                Program.MainForm.LogAppend("Starting migration");
                                Server.Instance.InMigration = true;
                                Server.Instance.StopListening();
                                // Tell new server to start listening
                                var pw = new Packet(ISServerMessages.ServerMigrationUpdate);
                                pw.WriteByte((byte)ServerMigrationStatus.StartListening);
                                SendPacket(pw);
                                break;
                            }
                        case ServerMigrationStatus.StartListening:
                            {
                                Server.Instance.StartListening();
                                Program.MainForm.LogAppend("Starting listening, requesting data");

                                // New server started listening, request data
                                var pw = new Packet(ISServerMessages.ServerMigrationUpdate);
                                pw.WriteByte((byte)ServerMigrationStatus.DataTransferRequest);
                                SendPacket(pw);
                                break;
                            }
                        case ServerMigrationStatus.DataTransferRequest:
                            {
                                // New server requests data, old server sends it
                                Program.MainForm.LogAppend("Sending data");
                                SendCurrentConfiguration();
                                break;
                            }
                        case ServerMigrationStatus.DataTransferResponse:
                            {
                                Program.MainForm.LogAppend("Receiving data...");

                                // Old server sent data

                                Program.MainForm.LogAppend("Finishing up migration");
                                migrated = true;
                                var pw = new Packet(ISServerMessages.ServerMigrationUpdate);
                                pw.WriteByte((byte)ServerMigrationStatus.FinishedInitialization);
                                SendPacket(pw);

                                Server.Instance.StartLTLAcceptor();

                                break;
                            }

                        case ServerMigrationStatus.FinishedInitialization:
                            {
                                Program.MainForm.LogAppend("Other login server finished.");
                                migrated = true;
                                this.Disconnect();
                                Program.MainForm.Shutdown();
                                break;
                            }
                    }

                    break;
            }
        }

        public void SendCurrentConfiguration()
        {
            var pw = new Packet(ISServerMessages.ServerMigrationUpdate);
            pw.WriteByte((byte)ServerMigrationStatus.DataTransferResponse);

            // Nothing to transfer

            SendPacket(pw);
        }
    }
}
