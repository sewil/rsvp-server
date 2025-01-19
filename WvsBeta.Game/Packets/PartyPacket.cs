using WvsBeta.Common.Sessions;

namespace WvsBeta.Game
{
    public static class PartyPacket
    {
        public static Packet GetHPUpdatePacket(Character ofCharacter)
        {
            var pw = new Packet(ServerMessages.UPDATE_PARTYMEMBER_HP);
            pw.WriteInt(ofCharacter.ID);
            pw.WriteInt(ofCharacter.PrimaryStats.HP);
            pw.WriteInt(ofCharacter.PrimaryStats.GetMaxHP());
            return pw;
        }
    }
}