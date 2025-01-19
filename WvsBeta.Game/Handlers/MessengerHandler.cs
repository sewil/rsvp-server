using log4net;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Game
{
    class MessengerHandler
    {
        private static ILog chatLog = LogManager.GetLogger("MessengerChatLog");
        public static void HandleMessenger(Character chr, Packet packet)
        {
            byte mode = packet.ReadByte();
            switch (mode)
            {
                case 0:
                    Server.Instance.CenterConnection.MessengerJoin(packet.ReadInt(), chr);
                    break;
                case 2:
                    Server.Instance.CenterConnection.MessengerLeave(chr.ID);
                    break;
                case 3:
                    {
                        var invited = packet.ReadString();
                        Server.Instance.CenterConnection.MessengerInvite(chr.ID, invited);
                        break;
                    }
                case 5:
                    Server.Instance.CenterConnection.MessengerBlock(chr.ID, packet.ReadString(), packet.ReadString(), packet.ReadByte());
                    break;
                case 6:
                    {
                        var message = packet.ReadLocalizedString(chr.ClientActiveCodePage);
                        if (MessagePacket.ShowMuteMessage(chr))
                        {
                            chatLog.Info($"[MUTED] {chr.Name}: {message}");
                        }
                        else
                        {
                            Server.Instance.CenterConnection.MessengerChat(chr.ID, message);
                        }
                        break;
                    }
                case 7:
                    Server.Instance.CenterConnection.MessengerAvatar(chr);
                    break;
                default:
                    Program.MainForm.LogAppend("UNKNOWN MESSENGER OP: " + mode);
                    break;
            }
        }

    }
}

