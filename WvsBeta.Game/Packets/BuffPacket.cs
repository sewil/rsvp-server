using WvsBeta.Common.Sessions;

namespace WvsBeta.Game
{
    public static class BuffPacket
    {
        public static void AddMapBuffValues(Character chr, Packet pw, BuffValueTypes pBuffFlags = BuffValueTypes.ALL)
        {
            var ps = chr.PrimaryStats;
            var currentTime = MasterThread.CurrentTime;
            {
                BuffValueTypes added = 0;
                var tmp = pw.Position;
                pw.WriteUInt(0); // Placeholder
                ps.BuffSlow.EncodeForRemote(ref added, currentTime, stat =>
                {
                    pw.WriteShort(stat.N);
                    pw.WriteInt(stat.R);
                }, pBuffFlags);
                
                pw.SetUInt(tmp, (uint)((ulong)added >> 32));
            }

            {
                BuffValueTypes added = 0;
                var tmp = pw.Position;
                pw.WriteUInt(0); // Placeholder

                ps.BuffSpeed.EncodeForRemote(ref added, currentTime, stat => pw.WriteByte((byte)stat.N), pBuffFlags);
                if (!added.HasFlag(BuffValueTypes.Speed))
                {
                    // For V.12, remote players are set to 70% speed due to speed not being initialized by anything.
                    added |= BuffValueTypes.Speed;
                    // Set default speed
                    pw.WriteByte(100);
                }

                ps.BuffComboAttack.EncodeForRemote(ref added, currentTime, stat => pw.WriteByte((byte)stat.N), pBuffFlags);
                ps.BuffCharges.EncodeForRemote(ref added, currentTime, stat => pw.WriteInt(stat.R), pBuffFlags);
                ps.BuffStun.EncodeForRemote(ref added, currentTime, stat => pw.WriteInt(stat.R), pBuffFlags);
                ps.BuffDarkness.EncodeForRemote(ref added, currentTime, stat => pw.WriteInt(stat.R), pBuffFlags);
                ps.BuffSeal.EncodeForRemote(ref added, currentTime, stat => pw.WriteInt(stat.R), pBuffFlags);
                ps.BuffWeakness.EncodeForRemote(ref added, currentTime, stat => pw.WriteInt(stat.R), pBuffFlags);
                ps.BuffPoison.EncodeForRemote(ref added, currentTime, stat =>
                {
                    pw.WriteShort(stat.N);
                    pw.WriteInt(stat.R);
                }, pBuffFlags);
                ps.BuffSoulArrow.EncodeForRemote(ref added, currentTime, null, pBuffFlags);
                ps.BuffShadowPartner.EncodeForRemote(ref added, currentTime, null, pBuffFlags);
                ps.BuffDarkSight.EncodeForRemote(ref added, currentTime, null, pBuffFlags);

                pw.SetUInt(tmp, (uint)((ulong)added & uint.MaxValue));
            }
        }

        public static void SetTempStats(Character chr, BuffValueTypes pFlagsAdded, short pDelay = 0)
        {
            if (pFlagsAdded == 0) return;
            var pw = new Packet(ServerMessages.TEMPORARY_STAT_SET);
            chr.PrimaryStats.EncodeForLocal(pw, pFlagsAdded);
            pw.WriteShort(pDelay);
            TryAddMarker(pFlagsAdded, pw);
            chr.SendPacket(pw);
        }

        public static void ResetTempStats(Character chr, BuffValueTypes removedFlags)
        {
            if (removedFlags == 0) return;

            var pw = new Packet(ServerMessages.TEMPORARY_STAT_RESET);
            pw.WriteUInt((uint)((ulong)removedFlags >> 32));
            pw.WriteUInt((uint)((ulong)removedFlags & uint.MaxValue));
            TryAddMarker(removedFlags, pw);
            chr.SendPacket(pw);
        }

        private static void TryAddMarker(BuffValueTypes flags, Packet pw)
        {
            if ((flags & BuffValueTypes.SPEED_BUFF_ELEMENT) != 0)
            {
                pw.WriteByte(0); // This is a marker that will be pushed as entry for movepath, so that you can keep track of speed
            }
        }
    }
}