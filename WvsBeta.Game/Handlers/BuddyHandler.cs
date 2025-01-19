using WvsBeta.Common;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Game.Handlers
{
    class BuddyHandler
    {
        public static void HandleBuddy(Character chr, Packet packet)
        {
            var opcode = packet.ReadByte<FriendResReq>();
            switch (opcode)
            {
                case FriendResReq.FriendReq_LoadFriend:
                {
                    Server.Instance.CenterConnection.BuddyUpdate(chr);
                    break;
                }
                case FriendResReq.FriendReq_SetFriend:
                {
                    string Victim = packet.ReadString();
                    Server.Instance.CenterConnection.BuddyRequest(chr, Victim);
                    break;
                }
                case FriendResReq.FriendReq_AcceptFriend:
                {
                    Server.Instance.CenterConnection.BuddyAccept(chr);
                    break;
                }
                case FriendResReq.FriendReq_RefuseDeleteFriend:
                {
                    int Victim = packet.ReadInt();
                    Server.Instance.CenterConnection.BuddyDecline(chr, Victim);
                    break;
                }
                default:
                {
                    Program.MainForm.LogAppend("wat buddy op is diz: " + opcode);
                    break;
                }
            }

        }
    }
}
