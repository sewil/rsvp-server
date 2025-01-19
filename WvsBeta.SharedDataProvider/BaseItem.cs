using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using log4net;
using MySql.Data.MySqlClient;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Game
{
    public abstract class BaseItem
    {
        protected static ILog _log = LogManager.GetLogger(typeof(BaseItem));

        public int ItemID { get; set; } = 0;

        private short _amount { get; set; }

        public short Amount
        {
            get => _amount;
            set
            {
                if (Constants.isEquip(ItemID) && value != 1)
                {
                    _log.Error($"Trying to set equip {ItemID} CashId {CashId} AlreadyInDatabase {AlreadyInDatabase} InventorySlot {InventorySlot} Amount to weird value like {value}. AS THERE IS NO FIX FOR THIS, PROCEEDING");
                }

                if (value < 0)
                {
                    throw new Exception($"Trying to set item {ItemID} to {value} amount!!!!!!!!");
                }

                _amount = value;
            }
        }
        public short InventorySlot { get; set; } = 0;
        public long CashId { get; set; }
        public long Expiration { get; set; } = NoItemExpiration;
        public bool AlreadyInDatabase { get; set; } = false;

        public bool IsTreatSingly
        {
            get
            {
                var inventory = Constants.getInventory(ItemID);
                if (CashId != 0) return true;
                if (Expiration < NoItemExpiration) return true;
                if (Constants.isRechargeable(ItemID)) return true;

                if (inventory != 2 &&
                    inventory != 3 &&
                    inventory != 4) return true;

                return false;
            }
        }

        /// <summary>
        /// This is actually 2079-01-01 00:00:00, as a FILETIME field
        /// </summary>
        public const long NoItemExpiration = 150842304000000000L;

        protected BaseItem()
        {
        }

        protected BaseItem(BaseItem itemBase)
        {
            ItemID = itemBase.ItemID;
            Amount = itemBase.Amount;
            CashId = itemBase.CashId;
            Expiration = itemBase.Expiration;
        }

        public BaseItem Duplicate()
        {
            if (this is EquipItem ei) return new EquipItem(ei);
            if (this is PetItem pi) return new PetItem(pi);
            if (this is BundleItem bi) return new BundleItem(bi);
            throw new Exception($"Unable to duplicate item {GetType()}");
        }

        public BaseItem SplitInTwo(short secondPairAmount)
        {
            if (!Constants.isStackable(ItemID) || IsTreatSingly)
            {
                _log.Error($"Trying to split a singly item in two!!! ItemID: {ItemID} amount {secondPairAmount}");
                return null;
            }

            if (secondPairAmount < 0)
            {
                _log.Error($"Trying to split item with a negative value!!!! ItemID: {ItemID} amount {secondPairAmount}");
                return null;
            }

            if (this.Amount < secondPairAmount)
            {
                return null;
            }

            var dupe = Duplicate();
            this.Amount -= secondPairAmount;

            dupe.Amount = secondPairAmount;
            return dupe;
        }

        public static IEnumerable<BaseItem> CreateMultipleFromItemID(int itemId, short amount = 1)
        {
            short max = 1;
            if (!Constants.isEquip(itemId) && !Constants.isPet(itemId))
            {
                max = (short)DataProvider.Items[itemId].MaxSlot;
                if (max == 0)
                {
                    max = 100;
                }
            }

            if (amount <= max)
            {
                var item = CreateFromItemID(itemId, amount);
                yield return item;
            }
            else
            {
                // split amount up in multiple bundles depending on maxSlot
                short leftOver = amount;
                while (leftOver > 0)
                {
                    short newQuantity = Math.Min(leftOver, max);
                    leftOver -= newQuantity;
                    yield return CreateFromItemID(itemId, newQuantity);
                }
            }
        }

        public static BaseItem CreateFromItemID(int itemId, short amount = 1)
        {
            if (itemId == 0) throw new Exception("Invalid ItemID in CreateFromItemID");

            BaseItem ret;
            if (Constants.isEquip(itemId)) ret = new EquipItem();
            else if (Constants.isPet(itemId)) ret = new PetItem(itemId);
            else ret = new BundleItem();

            ret.ItemID = itemId;
            ret.Amount = amount;

            return ret;
        }

        public virtual void GiveStats(ItemVariation enOption)
        {
        }

        public virtual void Load(MySqlDataReader data)
        {
            // Load ItemID manually

            if (ItemID == 0) throw new Exception("Tried to Load() an item while CreateFromItemID was not used.");

            AlreadyInDatabase = true;
            CashId = data.GetInt64("cashid");
            Expiration = data.GetInt64("expiration");
        }

        public void EncodeForMigration(Packet pw)
        {
            pw.WriteInt(ItemID);

            pw.WriteShort(Amount);

            if (this is EquipItem equipItem)
            {
                pw.WriteByte(equipItem.Slots);
                pw.WriteByte(equipItem.Scrolls);
                pw.WriteShort(equipItem.Str);
                pw.WriteShort(equipItem.Dex);
                pw.WriteShort(equipItem.Int);
                pw.WriteShort(equipItem.Luk);
                pw.WriteShort(equipItem.HP);
                pw.WriteShort(equipItem.MP);
                pw.WriteShort(equipItem.Watk);
                pw.WriteShort(equipItem.Matk);
                pw.WriteShort(equipItem.Wdef);
                pw.WriteShort(equipItem.Mdef);
                pw.WriteShort(equipItem.Acc);
                pw.WriteShort(equipItem.Avo);
                pw.WriteShort(equipItem.Hands);
                pw.WriteShort(equipItem.Jump);
                pw.WriteShort(equipItem.Speed);
            }
            else
            {
                pw.WriteByte(0);
                pw.WriteByte(0);
                pw.WriteShort(0);
                pw.WriteShort(0);
                pw.WriteShort(0);
                pw.WriteShort(0);
                pw.WriteShort(0);
                pw.WriteShort(0);
                pw.WriteShort(0);
                pw.WriteShort(0);
                pw.WriteShort(0);
                pw.WriteShort(0);
                pw.WriteShort(0);
                pw.WriteShort(0);
                pw.WriteShort(0);
                pw.WriteShort(0);
                pw.WriteShort(0);
            }

            pw.WriteLong(CashId);
            pw.WriteLong(Expiration);

            pw.WriteString("");
        }

        public static BaseItem DecodeForMigration(Packet pr)
        {
            var itemId = pr.ReadInt();

            var item = CreateFromItemID(itemId);
            item.ItemID = itemId;

            item.Amount = pr.ReadShort();

            if (item is EquipItem equipItem)
            {
                equipItem.Slots = pr.ReadByte();
                equipItem.Scrolls = pr.ReadByte();
                equipItem.Str = pr.ReadShort();
                equipItem.Dex = pr.ReadShort();
                equipItem.Int = pr.ReadShort();
                equipItem.Luk = pr.ReadShort();
                equipItem.HP = pr.ReadShort();
                equipItem.MP = pr.ReadShort();
                equipItem.Watk = pr.ReadShort();
                equipItem.Matk = pr.ReadShort();
                equipItem.Wdef = pr.ReadShort();
                equipItem.Mdef = pr.ReadShort();
                equipItem.Acc = pr.ReadShort();
                equipItem.Avo = pr.ReadShort();
                equipItem.Hands = pr.ReadShort();
                equipItem.Jump = pr.ReadShort();
                equipItem.Speed = pr.ReadShort();
            }
            else
            {
                pr.ReadByte();
                pr.ReadByte();
                pr.ReadShort();
                pr.ReadShort();
                pr.ReadShort();
                pr.ReadShort();
                pr.ReadShort();
                pr.ReadShort();
                pr.ReadShort();
                pr.ReadShort();
                pr.ReadShort();
                pr.ReadShort();
                pr.ReadShort();
                pr.ReadShort();
                pr.ReadShort();
                pr.ReadShort();
                pr.ReadShort();
            }

            item.CashId = pr.ReadLong();
            item.Expiration = pr.ReadLong();

            pr.ReadString();

            return item;
        }

        public virtual void Encode(Packet packet)
        {
            packet.WriteInt(ItemID);

            packet.WriteBool(CashId != 0);
            if (CashId != 0)
                packet.WriteLong(CashId);

            packet.WriteLong(Expiration);

        }

        /// <summary>
        /// Build a full insert statement that is not optimized.
        /// </summary>
        /// <returns>A comma delimited set of fields</returns>
        public virtual string GetFullSaveColumns()
        {
            throw new NotImplementedException();
        }

        public virtual string GetFullUpdateColumns()
        {
            throw new NotImplementedException();
        }
    }

    public class BundleItem : BaseItem
    {
        public BundleItem() { }

        public BundleItem(BundleItem itemBase) : base(itemBase) { }

        public ItemData Template => DataProvider.Items[ItemID];

        public override void Load(MySqlDataReader data)
        {
            base.Load(data);
            Amount = data.GetInt16("amount");
        }


        public override void Encode(Packet packet)
        {
            base.Encode(packet);
            packet.WriteShort(Amount);
        }

        public override string GetFullSaveColumns()
        {
            return
                ItemID + ", " +
                Amount + ", " +
                CashId + ", " +
                Expiration;
        }

        public override string GetFullUpdateColumns()
        {
            return
                "itemid = " + ItemID + ", " +
                "amount = " + Amount + ", " +
                "cashid = " + CashId + ", " +
                "expiration = " + Expiration;
        }
    }

    public enum ItemVariation
    {
        None = 0,
        Better = 1,
        Normal = 2,
        Great = 3,
        Gachapon = 4,
    }

    public class EquipItem : BaseItem
    {
        public byte Slots { get; set; } = 7;
        public byte Scrolls { get; set; } = 0;
        public short Str { get; set; } = 0;
        public short Dex { get; set; } = 0;
        public short Int { get; set; } = 0;
        public short Luk { get; set; } = 0;
        public short HP { get; set; } = 0;
        public short MP { get; set; } = 0;
        public short Watk { get; set; } = 0;
        public short Matk { get; set; } = 0;
        public short Wdef { get; set; } = 0;
        public short Mdef { get; set; } = 0;
        public short Acc { get; set; } = 0;
        public short Avo { get; set; } = 0;
        public short Hands { get; set; } = 0;
        public short Jump { get; set; } = 0;
        public short Speed { get; set; } = 0;

        public EquipData Template => DataProvider.Equips[ItemID];

        public int Quality => Template.CalculateEquipQuality(this);

        public EquipItem() { }

        public EquipItem(EquipItem itemBase) : base(itemBase)
        {
            Amount = 1;
            Slots = itemBase.Slots;
            Scrolls = itemBase.Scrolls;
            Str = itemBase.Str;
            Dex = itemBase.Dex;
            Int = itemBase.Int;
            Luk = itemBase.Luk;
            HP = itemBase.HP;
            MP = itemBase.MP;
            Watk = itemBase.Watk;
            Matk = itemBase.Matk;
            Wdef = itemBase.Wdef;
            Mdef = itemBase.Mdef;
            Acc = itemBase.Acc;
            Avo = itemBase.Avo;
            Hands = itemBase.Hands;
            Jump = itemBase.Jump;
            Speed = itemBase.Speed;
        }

        public override void GiveStats(ItemVariation enOption)
        {
            var data = Template;

            Slots = data.Slots;
            Amount = 1; // Force it to be 1.

            Str = GetVariation(data.Strength, enOption);
            Dex = GetVariation(data.Dexterity, enOption);
            Int = GetVariation(data.Intellect, enOption);
            Luk = GetVariation(data.Luck, enOption);
            HP = GetVariation(data.HP, enOption);
            MP = GetVariation(data.MP, enOption);
            Watk = GetVariation(data.WeaponAttack, enOption);
            Wdef = GetVariation(data.WeaponDefense, enOption);
            Matk = GetVariation(data.MagicAttack, enOption);
            Mdef = GetVariation(data.MagicDefense, enOption);
            Acc = GetVariation(data.Accuracy, enOption);
            Avo = GetVariation(data.Avoidance, enOption);
            Hands = GetVariation(data.Hands, enOption);
            Speed = GetVariation(data.Speed, enOption);
            Jump = GetVariation(data.Jump, enOption);

        }


        public override void Load(MySqlDataReader data)
        {
            base.Load(data);

            Slots = (byte)data.GetInt16("slots");
            Scrolls = (byte)data.GetInt16("scrolls");
            Str = data.GetInt16("istr");
            Dex = data.GetInt16("idex");
            Int = data.GetInt16("iint");
            Luk = data.GetInt16("iluk");
            HP = data.GetInt16("ihp");
            MP = data.GetInt16("imp");
            Watk = data.GetInt16("iwatk");
            Matk = data.GetInt16("imatk");
            Wdef = data.GetInt16("iwdef");
            Mdef = data.GetInt16("imdef");
            Acc = data.GetInt16("iacc");
            Avo = data.GetInt16("iavo");
            Hands = data.GetInt16("ihand");
            Speed = data.GetInt16("ispeed");
            Jump = data.GetInt16("ijump");
        }

        public override void Encode(Packet packet)
        {
            base.Encode(packet);

            packet.WriteByte(Slots);
            packet.WriteByte(Scrolls);
            packet.WriteShort(Str);
            packet.WriteShort(Dex);
            packet.WriteShort(Int);
            packet.WriteShort(Luk);
            packet.WriteShort(HP);
            packet.WriteShort(MP);
            packet.WriteShort(Watk);
            packet.WriteShort(Matk);
            packet.WriteShort(Wdef);
            packet.WriteShort(Mdef);
            packet.WriteShort(Acc);
            packet.WriteShort(Avo);
            packet.WriteShort(Hands);
            packet.WriteShort(Speed);
            packet.WriteShort(Jump);
        }

        public override string GetFullSaveColumns()
        {
            return (
                ItemID + ", " +
                Slots + ", " +
                Scrolls + ", " +
                Str + ", " +
                Dex + ", " +
                Int + ", " +
                Luk + ", " +
                HP + ", " +
                MP + ", " +
                Watk + ", " +
                Matk + ", " +
                Wdef + ", " +
                Mdef + ", " +
                Acc + ", " +
                Avo + ", " +
                Hands + ", " +
                Speed + ", " +
                Jump + ", " +
                CashId + ", " +
                Expiration
            );
        }

        public override string GetFullUpdateColumns()
        {
            return (
                "itemid = " + ItemID + ", " +
                "slots = " + Slots + ", " +
                "scrolls = " + Scrolls + ", " +
                "istr = " + Str + ", " +
                "idex = " + Dex + ", " +
                "iint = " + Int + ", " +
                "iluk = " + Luk + ", " +
                "ihp = " + HP + ", " +
                "imp = " + MP + ", " +
                "iwatk = " + Watk + ", " +
                "imatk = " + Matk + ", " +
                "iwdef = " + Wdef + ", " +
                "imdef = " + Mdef + ", " +
                "iacc = " + Acc + ", " +
                "iavo = " + Avo + ", " +
                "ihand = " + Hands + ", " +
                "ispeed = " + Speed + ", " +
                "ijump = " + Jump + ", " +
                "cashid = " + CashId + ", " +
                "expiration = " + Expiration
            );
        }

        public static int GetMaxDistribution(int stat, ItemVariation enOption)
        {
            if (enOption == ItemVariation.Gachapon)
            {
                // Gachapon returns up to +7 stat items.
                // You start having 7 stat from 30 basestat.
                return Math.Min((stat / 5) + 1, 7);
            }
            else
            {
                // The rest returns up to +5 stat items.
                // You start having 5 stat from 40 basestat.
                return Math.Min((stat / 10) + 1, 5);
            }
        }

        /// <summary>
        /// Generate a stat based on a particular ItemVariation.
        /// Gachapon variation gives up to 7 additional stat points, the rest up to 5.
        ///
        /// Non-gachapon variations cannot double-negative and give a lucky 2 point stat (max).
        /// </summary>
        /// <param name="v">base stat</param>
        /// <param name="enOption">variation</param>
        /// <returns>new stat</returns>
        /// <exception cref="Exception"></exception>
        public static short GetVariation(short v, ItemVariation enOption)
        {
            // This is a bit nasty from Wizet:
            // For non-gacha:
            // - You need 2 extra set bits to get a stat
            // - If <= 2 bits set, you don't get anything
            // For gacha:
            // - You have the chance to roll a 'subtract',
            //   but if you were unlucky before, it would give you points instead (v - -x)
            const int minimumPointsNeeded = 2;

            if (v == 0) return 0;
            if (enOption == ItemVariation.None) return v;

            // maximum amount of points to give
            var maxDiff = GetMaxDistribution(v, enOption);


            var maxBits = (uint)(1 << (maxDiff + minimumPointsNeeded));
            
            var calculatedBoost = 0;
            
            // See how many bits (= points) were 1
            calculatedBoost += BitOperations.PopCount(Rand32.Next() % maxBits);
            // Correct for the difficulty
            calculatedBoost -= minimumPointsNeeded;

            // Trace.WriteLine($"Boost w/ bonus: {calculatedBoost}");

            if (enOption != ItemVariation.Gachapon)
            {
                // In the original code, calculatedBoost would be reset to 0,
                // but this would then not really affect the addition or subtraction of
                // points in the last part of the code, so I removed it.
                // This does affect Rand32's seed, however.
                if (calculatedBoost < 0) return v;
            }

            //Trace.WriteLine($"Actual boost: {calculatedBoost}");

            static short GetRate(int v, int chanceToGoDown, int pointsDown, int pointsUp)
            {
                if ((Rand32.Next() % 100) < chanceToGoDown)
                    return (short)(v - pointsDown);
                else
                    return (short)(v + pointsUp);
            }

            return enOption switch
            {
                // Both gacha and normal drops have 50% chance to lose points.

                ItemVariation.Normal => GetRate(v, 50, calculatedBoost, calculatedBoost),
                ItemVariation.Gachapon => GetRate(v, 50, calculatedBoost, calculatedBoost),

                // Better and great can only gain points
                ItemVariation.Better => GetRate(v, 30, 0, calculatedBoost),
                ItemVariation.Great => GetRate(v, 10, 0, calculatedBoost),
                _ => throw new Exception($"Invalid ItemVariation {enOption}"),
            };
        }
        
        public string GetStatDescription()
        {
            var template = Template;
            var sb = new StringBuilder();

            void AddStat(string name, int itemValue, int templateValue)
            {
                if (itemValue == 0) return;
                var diff = itemValue - templateValue;
                string diffText;
                if (diff < 0) diffText = diff.ToString();
                else if (diff > 0) diffText = "+" + diff.ToString();
                else diffText = "0";
                sb.Append($"{name} : {itemValue} ({diffText})\r\n");
            }

            sb.Append($"Quality : {Quality}\r\n");
            AddStat("STR", Str, template.Strength);
            AddStat("DEX", Dex, template.Dexterity);
            AddStat("INT", Int, template.Intellect);
            AddStat("LUK", Luk, template.Luck);
            AddStat("HP", HP, template.HP);
            AddStat("MP", MP, template.MP);
            AddStat("Weapon Attack", Watk, template.WeaponAttack);
            AddStat("Weapon Defense", Wdef, template.WeaponDefense);
            AddStat("Magic Attack", Matk, template.MagicAttack);
            AddStat("Magic Defense", Mdef, template.MagicDefense);
            AddStat("Accuracy", Acc, template.Accuracy);
            AddStat("Avoidability", Avo, template.Avoidance);
            AddStat("Speed", Speed, template.Speed);
            AddStat("Jump", Jump, template.Jump);

            return sb.ToString().Trim();
        }
    }

    public class PetItem : BaseItem
    {
        public bool IsDead => DeadDate >= NoItemExpiration;

        public string Name { get; set; }
        public byte Level { get; set; } = 1;
        /// <summary>
        /// Also known as tameness
        /// </summary>
        public short Closeness { get; set; } = 0;
        /// <summary>
        /// Also known as repleteness
        /// </summary>
        public byte Fullness { get; set; } = 100;
        /// <summary>
        /// The date the pet died (RIP)
        /// </summary>
        public long DeadDate { get; set; } = NoItemExpiration;

        [Flags]
        public enum PetFlags
        {
            CanLootMesos = 1,
            CanLootItems = 2,
        }

        public PetFlags Flags
            => PetFlags.CanLootMesos | (Level >= 5 ? PetFlags.CanLootItems : 0);
        //{ get; set; } = (PetFlags)0xff;

        public TimeSpan RemainHungriness { get; set; } = TimeSpan.FromMilliseconds(36000);
        public int OvereatTimes { get; set; }
        public long LastUpdated { get; set; } = MasterThread.CurrentTime;
        public long LastInteraction { get; set; } = MasterThread.CurrentTime;

        public MovableLife MovableLife { get; } = new MovableLife();

        public PetData Template => DataProvider.Pets[ItemID];

        public PetItem(PetItem itemBase) : base(itemBase)
        {
            Name = itemBase.Name;
            Level = itemBase.Level;
            Closeness = itemBase.Closeness;
            Fullness = itemBase.Fullness;
            DeadDate = itemBase.DeadDate;
            RemainHungriness = itemBase.RemainHungriness;
            OvereatTimes = itemBase.OvereatTimes;
            LastUpdated = itemBase.LastUpdated;
            LastInteraction = itemBase.LastInteraction;
            MovableLife = new MovableLife(itemBase.MovableLife);
        }

        public PetItem(int itemID)
        {
            ItemID = itemID;
            Amount = 1;
            Name = Template.Name;
            DeadDate = Tools.GetDateExpireFromPeriodDays(Template.Life);
        }

        public override void Load(MySqlDataReader data)
        {
            base.Load(data);

            Name = data.GetString("name");
            Level = data.GetByte("level");
            Closeness = data.GetInt16("closeness");
            Fullness = data.GetByte("fullness");
            DeadDate = data.GetInt64("deaddate");
        }

        public override void Encode(Packet packet)
        {
            base.Encode(packet);

            packet.WriteString(Name, 13);
            packet.WriteByte(Level);
            packet.WriteShort(Closeness);
            packet.WriteByte(Fullness);
            packet.WriteLong(DeadDate);
            packet.WriteByte(Flags);
        }

        public override string GetFullSaveColumns()
        {
            return
                CashId + "," +
                ItemID + "," +
                "'" + MySqlHelper.EscapeString(Name) + "'," +
                Level + "," +
                Closeness + "," +
                Fullness + "," +
                Expiration + "," +
                DeadDate + "";
        }

        public override string GetFullUpdateColumns()
        {
            return
                "cashid = " + CashId + "," +
                "itemid = " + ItemID + "," +
                "name = '" + MySqlHelper.EscapeString(Name) + "'," +
                "level = " + Level + "," +
                "closeness = " + Closeness + "," +
                "fullness = " + Fullness + "," +
                "expiration = " + Expiration + "," +
                "deaddate = " + DeadDate + "";
        }

        /// <summary>
        /// Update pet data based on time elapsed
        /// </summary>
        /// <param name="nowMS">current time in milliseconds</param>
        /// <param name="remove">if this is true, the pet should be unequipped/despawned.</param>
        /// <returns>True when the stats are updated</returns>
        public bool Update(long nowMS, out bool remove)
        {
            var statsUpdated = false;
            remove = false;

            RemainHungriness -= TimeSpan.FromMilliseconds(nowMS - LastUpdated);
            LastUpdated = nowMS;

            if (RemainHungriness.TotalMilliseconds < 0)
            {
                var newHungriness = (long)Template.Hungry * 6;
                newHungriness = Rand32.Next() % (36 - newHungriness) + 60;

                RemainHungriness = TimeSpan.FromSeconds(newHungriness);

                if (Fullness > 0)
                    Fullness--;

                statsUpdated = true;
            }

            if (Fullness == 0)
            {
                remove = true;
                if (Closeness > 0) Closeness--;
                Fullness = 5;

                statsUpdated = true;
            }

            return statsUpdated;
        }
    }
}
