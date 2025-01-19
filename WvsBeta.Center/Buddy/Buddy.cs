using System;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Center
{
    public class BuddyData
    {
        public int CharacterID { get; }
        public string CharacterName { get; set; }

        public BuddyData(int i, string n)
        {
            CharacterID = i;
            CharacterName = n;
        }

        public BuddyData(Packet pr)
        {
            CharacterID = pr.ReadInt();
            CharacterName = pr.ReadString();
        }

        public void EncodeForTransfer(Packet pw)
        {
            pw.WriteInt(CharacterID);
            pw.WriteString(CharacterName);
        }

        public Character GetChar() => CenterServer.Instance.FindCharacter(CharacterID);

        public bool IsOnline() => GetChar() != null;

        public int GetChannel() => GetChar()?.ChannelID ?? -1;

        public bool InCashShop() => GetChar()?.InCashShop ?? false;

        public BuddyList GetBuddyList() => BuddyList.Get(CharacterID);

        public bool CanChatTo(BuddyData other) => other.IsOnline() && other.GetBuddyList().HasBuddy(this);

        public void SendPacket(Packet pPacket)
        {
            GetChar()?.SendPacket(pPacket);
        }
    }
}