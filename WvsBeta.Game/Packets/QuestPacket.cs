using System.Collections.Generic;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.Packets;

namespace WvsBeta.Game
{

    public static class QuestPacket
    {

        public static void SendQuestDataUpdate(Character chr, int QuestID, string Data)
        {
            var pw = new Packet(ServerMessages.SHOW_STATUS_INFO);
            pw.WriteByte(0x01);
            pw.WriteBool(true);
            pw.WriteInt(QuestID);
            pw.WriteString(Data);
            chr.SendPacket(pw);
        }

        public static void SendQuestRemove(Character chr, int QuestID)
        {
            var pw = new Packet(ServerMessages.SHOW_STATUS_INFO);
            pw.WriteByte(0x01);
            pw.WriteBool(false);
            pw.WriteInt(QuestID);
            chr.SendPacket(pw);
        }

        public static void SendGainItemChat(Character chr, params (int ItemID, int Amount)[] pItems)
        {
            var pw = new Packet(ServerMessages.LOCAL_USER_EFFECT);
            pw.WriteByte(UserEffect.Quest);
            pw.WriteByte((byte)pItems.Length);
            foreach (var kvp in pItems)
            {
                pw.WriteInt(kvp.ItemID);
                pw.WriteInt(kvp.Amount);
            }
            chr.SendPacket(pw);
        }
    }
}
