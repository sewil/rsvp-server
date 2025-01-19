using System;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Center.CharacterPackets
{

    enum MessengerAction : byte
    {
        SelfEnterResult = 0,
        Enter = 1,
        Leave = 2,
        Invite = 3,
        InviteResult = 4,
        Blocked = 5,
        Chat = 6,
        Avatar = 7,
        Migrated = 8,
    }

    public static class MessengerPacket
    {
        // Used for visually displaying Characters in messenger
        public static Packet SelfEnter(Character chr)
        {
            var packet = new Packet(ServerMessages.MESSENGER);
            packet.WriteByte(MessengerAction.SelfEnterResult);
            ModifyMessengerSlot(packet, chr, true);
            return packet;
        }

        //Used to inform the client which slot it's going to enter
        public static Packet Enter(byte slot)
        {
            var packet = new Packet(ServerMessages.MESSENGER);
            packet.WriteByte(MessengerAction.Enter);
            packet.WriteByte(slot);
            return packet;
        }

        public static Packet Leave(byte slot)
        {
            var packet = new Packet(ServerMessages.MESSENGER);
            packet.WriteByte(MessengerAction.Leave);
            packet.WriteByte(slot);
            return packet;
        }

        public static Packet Invite(string sender, byte senderChannel, int messengerId, bool byAdmin)
        {
            var packet = new Packet(ServerMessages.MESSENGER);
            packet.WriteByte(MessengerAction.Invite);
            packet.WriteString(sender);
            packet.WriteByte(senderChannel);
            packet.WriteInt(messengerId);
            packet.WriteBool(byAdmin);
            return packet;
        }

        public static Packet InviteResult(String recipient, bool success)
        {
            var packet = new Packet(ServerMessages.MESSENGER);
            packet.WriteByte(MessengerAction.InviteResult);
            packet.WriteString(recipient);
            packet.WriteBool(success); // False : '%' can't be found. True : you have sent invite to '%'.
            return packet;
        }

        public static Packet Blocked(int deliverto, string receiver, byte mode)
        {
            var packet = new Packet(ServerMessages.MESSENGER);
            packet.WriteByte(MessengerFunction.Blocked);
            packet.WriteString(receiver);
            packet.WriteByte(mode); // 0 : % denied the request. 1 : '%' is currently not accepting chat.
            return packet;
        }

        public static Packet Chat(string message)
        {
            var packet = new Packet(ServerMessages.MESSENGER);
            packet.WriteByte(MessengerFunction.Chat);
            packet.WriteString(message);
            return packet;
        }

        public static Packet Avatar(Character chr)
        {
            var packet = new Packet(ServerMessages.MESSENGER);
            packet.WriteByte(MessengerFunction.Avatar);
            packet.WriteByte(chr.MessengerSlot);
            ModifyMessengerSlot(packet, chr, false);
            return packet;
        }
        
        private static void ModifyMessengerSlot(Packet packet, Character chr, bool InChat)
        {
            packet.WriteByte(chr.MessengerSlot);
            chr.WriteAvatarLook(packet);
            packet.WriteString(chr.Name);
            packet.WriteByte(chr.ChannelID);
            packet.WriteBool(InChat); //Announce in chat
        }
    }
}
