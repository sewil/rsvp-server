using System.Diagnostics;
using System.Drawing;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Common.Tracking;

namespace WvsBeta.Game
{
    public class Drop : IFieldObj
    {
        // Checked with BMS, they say 15000. Originally was 60000
        private const int TimeUntilFreeForPickupMillis = 30000;

        public Map Field { get; set; }
        public int DropID { get; set; }
        public bool ByPet { get; set; }
        public bool ByUser => SourceID == 0;
        public bool Everlasting { get; set; }
        public bool ConsumeOnPickup { get; set; }
        public DropOwnType OwnType { get; set; }
        public int OwnerID { get; set; }
        public int OwnPartyID { get; set; }
        public long CreateTime { get; set; }
        public bool FFA { get; set; }
        public bool ToExplode { get; set; }
        public Reward Reward { get; set; }
        public Pos Pt1 { get; set; }
        public Pos Pt2 { get; set; }
        public Rectangle MergeArea { get; set; }
        public int SourceID { get; set; }
        public long DateExpire { get; set; }
        public int QuestID { get; set; }
        public string QuestState { get; set; } = "";
        public short ShowMax { get; set; }
        public short Pos { get; set; }
        public int MaxDamageCharacterID { get; set; }

        public Drop(int DropID, Reward reward, int OwnerID, int OwnPartyID, DropOwnType ownType, int SourceID, short x1, short y1, short x2, short y2, bool ByPet)
        {
            this.DropID = DropID;
            this.Reward = reward;
            this.OwnerID = OwnerID;
            this.OwnPartyID = OwnPartyID;
            this.OwnType = ownType;
            this.SourceID = SourceID;
            this.Pt1 = new Pos(x1, y1);
            this.Pt2 = new Pos(x2, y2);
            this.MergeArea = Rectangle.FromLTRB(x2 - 50, y2 - 20, x2 + 50, y2 + 20);
            this.ByPet = ByPet;
            this.DateExpire = reward.DateExpire;
        }

        public bool CanTakeDrop(Character chr, bool ignoreMigration = false)
        {
            if (Server.Instance.InMigration && !ignoreMigration) return false;

            if (!IsShownTo(chr)) return false;

            if (IsTimeToRemove(MasterThread.CurrentTime)) return false;

            var isPartyAble = chr.PartyID != 0 && OwnPartyID == chr.PartyID;
            var isOwnerDrop = OwnerID == chr.ID;
            
            if (isOwnerDrop) return true;
            if (isPartyAble) return true;


            // Everlasting Drops cannot be picked up by anyone else
            if (Field.EverlastingDrops) return false;

            // User drops can be picked up immediately
            if (ByUser) return true;
            
            // Drops that are FFA can be picked up immediately
            if (OwnType == DropOwnType.NoOwn ||
                OwnType == DropOwnType.Explosive_NoOwn) return true;


            var ffaTimeStarted = (MasterThread.CurrentTime - CreateTime) >= TimeUntilFreeForPickupMillis;

            return ffaTimeStarted;
        }
        

        public bool IsShownTo(IFieldObj Object)
        {
            var User = Object as Character;
            if (User == null) return false;

            // Kinda weird that you don't see new drops when you are dead?
            // if (User.PrimaryStats.HP <= 0) return false;

            if (User.MapID != Field.ID) return false;

            if (ShowMax > 0)
            {
                var Count = User.Inventory.ItemCount(Reward.ItemID, true);
                
                if (Count >= ShowMax)
                    return false;
            }

            if (QuestID <= 0) return true;
            if (User.Quests.GetQuestData(QuestID) != QuestState) return false;

            if (OwnType == DropOwnType.UserOwn && User.ID == OwnerID ||
                OwnType == DropOwnType.PartyOwn && User.PartyID == OwnPartyID ||
                OwnType == DropOwnType.NoOwn ||
                OwnType == DropOwnType.Explosive_NoOwn)
            {
                return true;
            }

            return false;

        }

        public bool IsTimeToRemove(long tCur)
        {
            if (Everlasting) return false;

            if (DateExpire <= tCur) return true;
            if ((tCur - CreateTime) > DropPool.DropExpireTime) return true;

            return false;
        }

        public void EncodeForMigration(Packet pw)
        {
            pw.WriteInt(DropID);
            this.Reward.EncodeForMigration(pw);
            pw.WriteInt(OwnerID);
            pw.WriteInt(OwnPartyID);
            pw.WriteByte((byte)OwnType);
            pw.WriteInt(SourceID);
            pw.WriteShort(Pt1.X);
            pw.WriteShort(Pt1.Y);
            pw.WriteShort(Pt2.X);
            pw.WriteShort(Pt2.Y);
            pw.WriteBool(ByPet);
            pw.WriteBool(false);
            pw.WriteShort(Pos);
            pw.WriteBool(Everlasting);
            pw.WriteBool(ConsumeOnPickup);
            pw.WriteInt(QuestID);
            pw.WriteString(QuestState);
            pw.WriteShort(ShowMax);
        }

        public static Drop DecodeForMigration(Packet pr)
        {
            var DropID = pr.ReadInt();
            var reward = Game.Reward.DecodeForMigration(pr);
            var OwnerID = pr.ReadInt();
            var OwnPartyID = pr.ReadInt();
            var OwnType = pr.ReadByte();
            var SourceID = pr.ReadInt();
            var Pt1X = pr.ReadShort();
            var Pt1Y = pr.ReadShort();
            var Pt2X = pr.ReadShort();
            var Pt2Y = pr.ReadShort();
            var ByPet = pr.ReadBool();
            pr.ReadBool();
            var Pos = pr.ReadShort();
            var DropEverlasting = pr.ReadBool();
            var ConsumeOnPickup = pr.ReadBool();

            var drop = new Drop(DropID, reward, OwnerID, OwnPartyID, (DropOwnType)OwnType, SourceID, Pt1X, Pt1Y, Pt2X, Pt2Y, ByPet);

            // Drop time is reset; cannot get the datetime transfer to work
            drop.CreateTime = MasterThread.CurrentTime;

            drop.Pos = Pos;
            drop.Everlasting = DropEverlasting;
            drop.ConsumeOnPickup = ConsumeOnPickup;
            drop.QuestID = pr.ReadInt();
            drop.QuestState = pr.ReadString();
            drop.ShowMax = pr.ReadShort();
            return drop;
        }


        public DropInfo GetDropInfo()
        {
            return new DropInfo()
            {
                ownPartyID = OwnPartyID,
                ownType = OwnType.ToString(),
                ownerID = OwnerID,
                dropPos = Pt2,
                sourceID = SourceID,
                maxDamageCharacterID = MaxDamageCharacterID
            };
        }
    }
}
