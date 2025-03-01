using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using WvsBeta.Common.Sessions;
using WzTools.Objects;

namespace WvsBeta.Game.GameObjects
{
    internal class Map_Elimination : Map
    {
        public const int ReviveCoupon = 4030017; // Premium Road Ticket

        public Map_Elimination(int id) : base(id)
        {
        }

        public uint TotalPoints
        {
            get => uint.Parse(ParentFieldSet?.GetVar("points", "0") ?? "0");
            set => ParentFieldSet?.SetVar("points", value.ToString());
        }

        public byte RevivesLeft
        {
            get => byte.Parse(ParentFieldSet?.GetVar("revives", "3") ?? "3");
            set => ParentFieldSet?.SetVar("revives", value.ToString());
        }

        /// <summary>
        /// % of Point gain deduction * amount of party players.
        /// </summary>
        public double DecRate => ParentFieldSet?.DecRate ?? 0.0;

        public override void RemovePlayer(Character chr, bool gmhide = false)
        {
            base.RemovePlayer(chr, gmhide);

            // Update points in the users quest data
            chr.Quests.SetQuestData(1001304, TotalPoints.ToString());
        }

        public override void AddPlayer(Character chr)
        {
            base.AddPlayer(chr);

            SendCurrentInfo(chr);
        }


        public void AddPoints(uint amount)
        {
            if (ParentFieldSet == null)
            {
                log.Error($"No fieldset in Elimination map {ID}");
                return;
            }
            
            TotalPoints = Math.Min(9999, TotalPoints + amount);

            UpdatePoints();
        }
        
        enum FSDOpcodes
        {
            Nothing = 0,
            UpdatePoints = 1,
            UpdateRevives = 2,
            UpdateAll = 3,
        }

        public override void EncodeFieldSpecificData(Character chr, Packet packet)
        {
            packet.WriteByte(FSDOpcodes.Nothing);
        }
         
        public void SendCurrentInfo(Character chr)
        {
            var p = new Packet(ServerMessages.FIELD_SPECIFIC_DATA);
            p.WriteByte(FSDOpcodes.UpdateAll);
            p.WriteUInt(TotalPoints);
            p.WriteByte(RevivesLeft);
            SendPacket(p);
        }

        public void UpdatePoints()
        {
            var p = new Packet(ServerMessages.FIELD_SPECIFIC_DATA);
            p.WriteByte(FSDOpcodes.UpdatePoints);
            p.WriteUInt(TotalPoints);
            SendPacket(p);
        }

        public void UpdateRevives()
        {
            var p = new Packet(ServerMessages.FIELD_SPECIFIC_DATA);
            p.WriteByte(FSDOpcodes.UpdateRevives);
            p.WriteByte(RevivesLeft);
            SendPacket(p);
        }
    }
}
