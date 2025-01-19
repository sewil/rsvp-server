using System.Collections.Generic;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Center
{
    public class Character : Common.CharacterBase
    {
        public byte ChannelID { get; set; }
        public bool isCCing { get; set; }
        public bool isConnectingFromLogin { get; set; }
        public bool InCashShop { get; set; }
        public byte LastChannel { get; set; }
        public BuddyList FriendsList => BuddyList.Get(ID);

        public Messenger Messenger { get; set; }

        public Party Party
        {
            get
            {
                Center.Party.Parties.TryGetValue(PartyID, out var party);
                return party;
            }
        }
        public byte MessengerSlot { get; set; }

        public int WeaponStickerID { get; set; }

        public Dictionary<byte, int> Equips { get; set; }

        private int _PartyID;
        public override int PartyID
        {
            get
            {
                return _PartyID;
            }
            set
            {
                _PartyID = value;
                if (IsOnline)
                {
                    var packet = new Packet(ISServerMessages.ChangeParty);
                    packet.WriteInt(ID);
                    packet.WriteInt(_PartyID);
                    CenterServer.Instance.SendPacketToServer(packet, ChannelID);
                }
            }
        }

        public Character() { }

        public Character(Packet pr)
        {
            ChannelID = pr.ReadByte();
            LastChannel = pr.ReadByte();
            new BuddyList(pr);
            base.DecodeForTransfer(pr);
        }

        public new void EncodeForTransfer(Packet pw)
        {
            pw.WriteByte(ChannelID);
            pw.WriteByte(LastChannel);
            FriendsList.EncodeForTransfer(pw);

            base.EncodeForTransfer(pw);
        }

        public void SendPacket(Packet pPacket)
        {
            var toserver = new Packet(ISServerMessages.PlayerSendPacket);
            toserver.WriteInt(base.ID);
            toserver.WriteBytes(pPacket.ToArray());
            CenterServer.Instance.SendPacketToServer(toserver, ChannelID);
        }

        public void UpdateFromAvatarLook(Packet packet)
        {
            Gender = packet.ReadByte();
            Skin = packet.ReadByte();
            Face = packet.ReadInt();
            packet.ReadByte();
            Hair = packet.ReadInt();

            var equips = new Dictionary<byte, int>();
            while (true)
            {
                var slot = packet.ReadByte();
                if (slot == 0xFF) break;

                var itemid = packet.ReadInt();
                equips[slot] = itemid;
            }
            Equips = equips;

            WeaponStickerID = packet.ReadInt();

            // Eventually this will contain pet item ID
        }

        public void WriteAvatarLook(Packet packet)
        {
            packet.WriteByte(Gender);
            packet.WriteByte(Skin);
            packet.WriteInt(Face);
            packet.WriteByte(0); // Part of equips lol
            packet.WriteInt(Hair);
            foreach (var kvp in Equips)
            {
                packet.WriteByte(kvp.Key);
                packet.WriteInt(kvp.Value);
            }
            packet.WriteByte(0xFF); // Equips shown end
            packet.WriteInt(WeaponStickerID);
            // Eventually this will contain pet item ID
        }
    }
}
