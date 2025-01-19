using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Game.Packets
{
    public static class CfgPacket
    {
        public static void SetPosition(Character chr, Pos pos)
        {
            chr.Position = new Pos(pos);
            chr.Position.Y -= 3; // Put a bit higher so your client wont drop through a foothold
            var p = new Packet(CfgServerMessages.CFG_TELEPORT);
            p.WriteShort(chr.Position.X);
            p.WriteShort(chr.Position.Y);
            chr.SendPacket(p);
        }

        private enum RateCreditsOpcodes
        {
            UPDATE
        }

        public static void UpdateRateCredits(Character chr)
        {
            var rc = chr.RateCredits;
            var creditsPaused = !rc.CreditsCurrentlyUsable;

            var activeCredits = new List<RateCredits.Credit>();
            foreach (var creditType in new[] { RateCredits.Type.EXP, RateCredits.Type.Drop, RateCredits.Type.Mesos })
            {
                if (rc.TryGetCredit(creditType, out var credit)) activeCredits.Add(credit);
            }

            var p = new Packet(CfgServerMessages.CFG_RATECREDITS);
            p.WriteByte(RateCreditsOpcodes.UPDATE);
            p.WriteBool(creditsPaused);
            
            p.WriteShort((short)activeCredits.Count);
            
            foreach (var credit in activeCredits)
            {
                p.WriteLong(credit.UID);
                p.WriteByte((byte)credit.Type);
                p.WriteDouble(credit.Rate);
                p.WriteString(credit.Comment);
                p.WriteInt((int)credit.DurationLeft.TotalSeconds);
            }

            chr.SendPacket(p);
        }

        public static void ToggleUI(Character chr, Constants.UIType type, bool open)
        {
            var p = new Packet(CfgServerMessages.CFG_TOGGLE_UI);
            p.WriteByte(type);
            p.WriteBool(open);
            chr.SendPacket(p);
        }
    }
}
