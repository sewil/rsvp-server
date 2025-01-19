using System.Linq;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Center
{
    public enum PartyFunction : byte
    {
        INVITE_DONE = 0x04,
        LOAD_DONE = 0x06,
        CREATE_NEW_DONE = 0x07,
        CREATE_NEW_ALREADY_JOINED = 0x08,
        CREATE_NEW_BEGINNER_DISALLOWED = 0x09,
        CREATE_NEW_UNK_ERR = 0xA,
        WITHDRAW_DONE = 0xB,
        WITHDRAW_NOT_JOINED = 0xC,
        WITHDRAW_UNK = 0xD,
        JOIN_DONE = 0xE,
        JOIN_ALREADY_JOINED = 0xF,
        JOIN_ALREADY_FULL = 0x10,
        JOIN_PARTY_UNK_USER = 0x11,
        INVITE_BLOCKED = 0x13,
        INVITE_USER_ALREADY_HAS_INVITE = 0x14,
        INVITE_REJECTED = 0x15,
        ADMIN_CANNOT_INVITE = 0x17,
        ADMIN_CANNOT_CREATE = 0x18,
        UNABLE_TO_FIND_PLAYER = 0x19,
        TOWN_PORTAL_CHANGED = 0x1A,
        CHANGE_LEVEL_OR_JOB = 0x1B,
        TOWN_PORTAL_CHANGED_UNK = 0x1C,
    }

    public static class PartyPacket
    {
        public const int CHANNEL_ID_OFFLINE = -2;


        public static Packet PartyCreated(Party party)
        {
            var pw = new Packet(ServerMessages.PARTY_RESULT);
            pw.WriteByte(PartyFunction.CREATE_NEW_DONE);
            pw.WriteInt(party.partyId);
            party.members[0].Door.Encode(pw);
            return pw;
        }

        public static Packet PartyError(PartyFunction Message)
        {
            var pw = new Packet(ServerMessages.PARTY_RESULT);
            pw.WriteByte(Message);
            return pw;
        }

        public static Packet PartyErrorWithName(PartyFunction Message, string name)
        {
            var pw = new Packet(ServerMessages.PARTY_RESULT);
            pw.WriteByte(Message);
            pw.WriteString(name);
            return pw;
        }

        public static Packet JoinParty(PartyMember joined, Party pt)
        {
            var pw = new Packet(ServerMessages.PARTY_RESULT);
            pw.WriteByte(PartyFunction.JOIN_DONE);
            pw.WriteInt(pt.partyId); //pid? charid?
            pw.WriteString(joined.CharacterName);
            AddPartyData(pw, joined, pt);
            return pw;
        }

        public static Packet SilentUpdate(PartyMember update, Party pt, int disconnecting = -1)
        {
            var pw = new Packet(ServerMessages.PARTY_RESULT);
            pw.WriteByte(PartyFunction.LOAD_DONE);
            pw.WriteInt(pt.partyId); //pid? charid?
            AddPartyData(pw, update, pt, null, disconnecting);
            return pw;
        }

        public static Packet MemberLeft(PartyMember sendTo, PartyMember leaving, Party pt, bool disband, bool expel)
        {
            var pw = new Packet(ServerMessages.PARTY_RESULT);
            pw.WriteByte(PartyFunction.WITHDRAW_DONE);
            pw.WriteInt(pt.partyId);
            pw.WriteInt(leaving.CharacterID);
            pw.WriteBool(!disband); //disband ? 0 : 1
            if (!disband)
            {
                pw.WriteBool(expel);
                pw.WriteString(leaving.CharacterName);
                AddPartyData(pw, sendTo, pt, leaving);
            }

            return pw;
        }

        public static Packet PartyInvite(Party pt)
        {
            var pw = new Packet(ServerMessages.PARTY_RESULT);
            pw.WriteByte(PartyFunction.INVITE_DONE);
            pw.WriteInt(pt.partyId);
            pw.WriteString(pt.leader.CharacterName);
            return pw;
        }

        public static Packet UpdateDoor(DoorInformation door, byte ownerIdIdx)
        {
            Program.MainForm.LogDebug("Updating door at index: " + ownerIdIdx);
            var pw = new Packet(ServerMessages.PARTY_RESULT);
            pw.WriteByte(PartyFunction.TOWN_PORTAL_CHANGED);
            pw.WriteByte(ownerIdIdx);
            door.Encode(pw);
            return pw;
        }

        public static void AddPartyData(Packet packet, PartyMember member, Party pt, PartyMember remove = null, int disconnect = -1)
        {
            var ids = pt.members.Select(e => e?.CharacterID ?? 0).ToArray();
            var names = pt.members.Select(e => e?.CharacterName ?? "").ToArray();
            var maps = pt.members.Select(e => (e == null || e.CharacterID == disconnect || e.GetChannel() != member.GetChannel()) ? Constants.InvalidMap : e.GetMap()).ToArray();
            var doors = pt.members.Select(e => e?.Door ?? DoorInformation.DefaultNoDoor).ToArray();

            ids.ForEach(packet.WriteInt);
            names.ForEach(packet.WriteString13);
            maps.ForEach(packet.WriteInt);
            packet.WriteInt(pt.leader.CharacterID);
            doors.ForEach(d => d.EncodeWithInts(packet)); // Encoding with ints, as this is memcpy'd
        }

        // Actual hack; this should be client-sided
        public static Packet NoneOnline()
        {
            // Red text packet
            var pw = new Packet(ServerMessages.BROADCAST_MSG);
            pw.WriteByte(0x05);
            pw.WriteString("Either the party doesn't exist or no member of your party is logged on.");
            return pw;
        }

        public static Packet PartyChat(string fromName, string text, byte group)
        {
            var pw = new Packet(ServerMessages.GROUP_MESSAGE);
            pw.WriteByte(group);
            pw.WriteString(fromName);
            pw.WriteString(text);
            return pw;
        }

        public static Packet RequestHpUpdate(int id)
        {
            var pw = new Packet(ISServerMessages.UpdateHpParty);
            pw.WriteInt(id);
            return pw;
        }
    }
}