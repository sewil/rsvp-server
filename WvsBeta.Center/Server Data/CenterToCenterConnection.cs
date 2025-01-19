using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Center
{
    public class CenterToCenterConnection : AbstractConnection
    {
        private const ushort CTCMapleVersion = 9994;
        private const byte CTCMapleLocale = 99;
        private const string CTCMaplePatchLocation = "CenterToCenter";

        public static bool IsConnected { get; private set; } = false;
        public static bool IsDone { get; private set; } = false;

        public bool IsNewServer { get; }

        public CenterToCenterConnection(Socket pSocket) : base(pSocket)
        {
            IsDone = false;
            IsNewServer = false;
            UseIvanPacket = true;
            SendHandshake(CTCMapleVersion, CTCMaplePatchLocation, CTCMapleLocale);
        }

        public CenterToCenterConnection(string pIP, ushort pPort) : base(pIP, pPort)
        {
            UseIvanPacket = true;
            IsNewServer = true;
        }

        public override void OnHandshakeInbound(Packet pPacket)
        {
            if (MapleVersion == CTCMapleVersion &&
                MapleLocale == CTCMapleLocale &&
                MaplePatchLocation == CTCMaplePatchLocation)
            {
                IsConnected = true;
            }
        }

        public override void OnDisconnect()
        {
            base.OnDisconnect();

            if (!IsDone)
            {
                if (!IsNewServer)
                {
                    Program.MainForm.LogAppend("Something odd happened while migrating. DC! Ignoring and resetting state.");
                    CenterServer.Instance.StartCTCAcceptor();
                }
                else
                {
                    Program.MainForm.LogAppend("Something odd happened while migrating. DC! Not sure what to do now.");
                }
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
                                CenterServer.Instance.CenterToCenterAcceptor.Stop();

                                Program.MainForm.LogAppend("Starting migration");
                                CenterServer.Instance.InMigration = true;
                                CenterServer.Instance.StopListening();
                                var pw = new Packet(ISServerMessages.ServerMigrationUpdate);
                                pw.WriteByte((byte)ServerMigrationStatus.StartListening);
                                SendPacket(pw);
                                break;
                            }
                        case ServerMigrationStatus.StartListening:
                            {
                                CenterServer.Instance.StartListening();
                                Program.MainForm.LogAppend("Starting listening, requesting data");
                                var pw = new Packet(ISServerMessages.ServerMigrationUpdate);
                                pw.WriteByte((byte)ServerMigrationStatus.DataTransferRequest);
                                SendPacket(pw);
                                break;
                            }
                        case ServerMigrationStatus.DataTransferRequest:
                            {
                                Program.MainForm.LogAppend("Sending data");
                                SendCurrentConfiguration();
                                break;
                            }
                            
                        case ServerMigrationStatus.DataTransferResponseChunked:
                            {
                                var type = (ServerMigrationDataType)pPacket.ReadByte();
                                Program.MainForm.LogAppend("Receiving data... {0}, {1} bytes", type, pPacket.Length);

                                using (var deflateStream = new DeflateStream(pPacket.MemoryStream, CompressionMode.Decompress))
                                using (var decompressedPacket = new Packet(deflateStream))
                                {
                                    switch (type)
                                    {
                                        case ServerMigrationDataType.Characters:
                                            {
                                                var memberCount = decompressedPacket.ReadInt();
                                                for (var i = 0; i < memberCount; i++)
                                                {
                                                    var member = new Character(decompressedPacket);
                                                    var possibleCopy = CenterServer.Instance.FindCharacter(member.Name, false);
                                                    if (possibleCopy != null)
                                                    {
                                                        // We found a duplicate name. figure out what to do with it.
                                                        // Either ignore  (ID < one already stored)
                                                        // Or remove the previous one (ID > one already stored)
                                                        if (member.ID < possibleCopy.ID) continue;

                                                        CenterServer.Instance.CharacterStore.Remove(possibleCopy);
                                                    }

                                                    CenterServer.Instance.CharacterStore.Add(member);

                                                }
                                                break;
                                            }

                                        case ServerMigrationDataType.Messengers: Messenger.DecodeForMigration(decompressedPacket); break;
                                        case ServerMigrationDataType.Parties: Party.DecodeForMigration(decompressedPacket); break;
                                    }

                                }

                                break;
                            }

                        case ServerMigrationStatus.DataTransferResponseChunkedDone:
                            {
                                Program.MainForm.LogAppend("Finishing up migration");
                                var pw = new Packet(ISServerMessages.ServerMigrationUpdate);
                                pw.WriteByte((byte)ServerMigrationStatus.FinishedInitialization);
                                pw.WriteBytes(CenterServer.Instance.PrivateIP.GetAddressBytes());
                                pw.WriteUShort(CenterServer.Instance.Port);
                                SendPacket(pw);

                                IsDone = true;
                                CenterServer.Instance.StartCTCAcceptor();
                                break;
                            }

                        case ServerMigrationStatus.FinishedInitialization:
                            {
                                Program.MainForm.LogAppend("Other center server finished.");
                                IsDone = true;

                                var ip = pPacket.ReadBytes(4);
                                var port = pPacket.ReadUShort();

                                CenterServer.Instance.LocalServers
                                    .Where(x => x.Value.Connected)
                                    .ForEach(x =>
                                    {
                                        var pw = new Packet(ISServerMessages.ChangeCenterServer);
                                        pw.WriteBytes(ip);
                                        pw.WriteUShort(port);
                                        x.Value.Connection.SendPacket(pw);
                                    });

                                this.Disconnect();
                                int count = 0;
                                new MasterThread.RepeatingAction(
                                    "Kicker",
                                    (date) =>
                                    {
                                        if (
                                            CenterServer.Instance.LocalServers.Count(x => x.Value.Connected) == 0 ||
                                            count++ > 10
                                        )
                                        {
                                            Program.MainForm.Shutdown();
                                        }
                                    },
                                    0,
                                    1000
                                    ).Start();
                                break;
                            }
                    }

                    break;
            }
        }

        public void SendCurrentConfiguration()
        {
            // -------------- CHARACTERS

            var pw = new Packet(ISServerMessages.ServerMigrationUpdate);
            pw.WriteByte(ServerMigrationStatus.DataTransferResponseChunked);
            pw.WriteByte(ServerMigrationDataType.Characters);

            using (var uncompressedPacket = new Packet())
            {
                var members = CenterServer.Instance.CharacterStore;

                uncompressedPacket.WriteInt(members.Count);
                foreach (var character in members)
                {
                    character.EncodeForTransfer(uncompressedPacket);
                }

                uncompressedPacket.DeflateCompress(pw.MemoryStream);
                Program.MainForm.LogAppend("Compressed characters buffer from {0} to {1} bytes", uncompressedPacket.Length, pw.Length - 4);
            }
            SendPacket(pw);

            // -------------- MESSENGERS

            pw = new Packet(ISServerMessages.ServerMigrationUpdate);
            pw.WriteByte(ServerMigrationStatus.DataTransferResponseChunked);
            pw.WriteByte(ServerMigrationDataType.Messengers);

            using (var uncompressedPacket = new Packet())
            {
                Messenger.EncodeForMigration(uncompressedPacket);

                uncompressedPacket.DeflateCompress(pw.MemoryStream);
                Program.MainForm.LogAppend("Compressed messenger buffer from {0} to {1} bytes", uncompressedPacket.Length, pw.Length - 4);
            }
            SendPacket(pw);

            // -------------- PARTIES

            pw = new Packet(ISServerMessages.ServerMigrationUpdate);
            pw.WriteByte(ServerMigrationStatus.DataTransferResponseChunked);
            pw.WriteByte(ServerMigrationDataType.Parties);

            using (var uncompressedPacket = new Packet())
            {
                Party.EncodeForMigration(uncompressedPacket);

                uncompressedPacket.DeflateCompress(pw.MemoryStream);
                Program.MainForm.LogAppend("Compressed parties buffer from {0} to {1} bytes", uncompressedPacket.Length, pw.Length - 4);
            }
            SendPacket(pw);


            // -------------- DONE

            pw = new Packet(ISServerMessages.ServerMigrationUpdate);
            pw.WriteByte(ServerMigrationStatus.DataTransferResponseChunkedDone);
            SendPacket(pw);
        }
    }
}
