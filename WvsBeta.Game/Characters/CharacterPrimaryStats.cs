using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using static WvsBeta.MasterThread;

namespace WvsBeta.Game
{
    public struct PrimaryStatsAddition
    {
        public int ItemID { get; set; }
        public short Slot { get; set; }
        public short Str { get; set; }
        public short Dex { get; set; }
        public short Int { get; set; }
        public short Luk { get; set; }
        public short MaxHP { get; set; }
        public short MaxMP { get; set; }
        public short Speed { get; set; }
    }

    public class BonusSet
    {
        public short Str { get; set; } = 0;
        public short Dex { get; set; } = 0;
        public short Int { get; set; } = 0;
        public short Luk { get; set; } = 0;
        public short MaxHP { get; set; } = 0;
        public short MaxMP { get; set; } = 0;
        public short PDD { get; set; } = 0;
        public short PAD { get; set; } = 0;
        public short MAD { get; set; } = 0;
        public short MDD { get; set; } = 0;
        public short EVA { get; set; } = 0;
        public short ACC { get; set; } = 0;
        public short Hands { get; set; } = 0;
        public short Jump { get; set; } = 0;
        public short Speed { get; set; } = 0;
    }

    public class EquipBonus : BonusSet
    {
        public int ID { get; set; }
    }

    public class BuffStat
    {
        // Return the amount of milliseconds
        public static long GetTimeForBuff(long additionalMillis = 0) => MasterThread.CurrentTime + additionalMillis;

        // Number. Most of the time, this is the X or Y value of the skill/buff
        public short N { get; set; }
        // Reference ID. For Item IDs, use a negative number
        public int R { get; set; }
        // Expire Time. Extended version of T (full time in millis)
        public long TM { get; set; }
        public BuffValueTypes Flag { get; set; }

        public bool IsNewType => (ulong)Flag > uint.MaxValue;

        public bool IsSet(long? time = null)
        {
            if (N == 0) return false;
            time ??= GetTimeForBuff();
            return TM > time;
        }

        public BuffValueTypes GetState(long? time = null)
        {
            return IsSet(time) ? Flag : 0;
        }

        public bool HasReferenceId(int referenceId, long? currenTime = null)
        {
            return IsSet(currenTime) && R == referenceId;
        }

        public BuffStat(BuffValueTypes flag)
        {
            Flag = flag;
            N = 0;
            R = 0;
            TM = 0;
        }

        public BuffValueTypes Reset()
        {
            if (R == 0 && N == 0 && TM == 0) return 0;

            Trace.WriteLine($"Removing buff {Flag} {N} {R} {TM}");
            N = 0;
            R = 0;
            TM = 0;
            return Flag;
        }

        public virtual bool TryReset(long currentTime, ref BuffValueTypes flag)
        {
            if (N == 0 || TM >= currentTime) return false;

            flag |= Reset();
            return true;
        }

        public void TryResetByReference(int reference, ref BuffValueTypes flag)
        {
            if (N == 0 || R != reference) return;
            flag |= Reset();
        }

        public virtual BuffValueTypes Set(int referenceId, short nValue, long expireTime)
        {
            // Ignore 0 N-values
            if (nValue == 0) return 0;
            R = referenceId;
            N = nValue;
            TM = expireTime;
            return Flag;
        }

        public void EncodeForRemote(ref BuffValueTypes flag, long currentTime, Action<BuffStat> func, BuffValueTypes specificFlag = BuffValueTypes.ALL)
        {
            if (!IsSet(currentTime) || !specificFlag.HasFlag(Flag)) return;

            flag |= Flag;
            func?.Invoke(this);
        }

        public void EncodeForLocal(Packet pw, ref BuffValueTypes flag, long currentTime, BuffValueTypes specificFlag = BuffValueTypes.ALL)
        {
            if (!IsSet(currentTime) || !specificFlag.HasFlag(Flag)) return;

            flag |= Flag;
            pw.WriteShort(N);
            pw.WriteInt(R);
            short time = (short)((TM - currentTime) / 100);
            pw.WriteShort(time); // If its not divided, it will not flash.
        }

        public virtual bool EncodeForCC(Packet pr, ref BuffValueTypes flag, long currentTime)
        {
            if (!IsSet(currentTime)) return false;

            flag |= Flag;
            pr.WriteShort(N);
            pr.WriteInt(R);
            pr.WriteLong(TM);
            return true;
        }

        public virtual bool DecodeForCC(Packet pr, BuffValueTypes flag)
        {
            if (!flag.HasFlag(Flag))
            {
                Reset();
                return false;
            }
            else
            {
                N = pr.ReadShort();
                R = pr.ReadInt();
                TM = pr.ReadLong();
                return true;
            }
        }
    }

    public class BuffStat_DragonBlood : BuffStat
    {
        private readonly Character Owner;
        private long tLastDamaged;

        public BuffStat_DragonBlood(BuffValueTypes flag, Character own) : base(flag)
        {
            Owner = own;
        }

        public override BuffValueTypes Set(int referenceId, short nValue, long expireTime)
        {
            tLastDamaged = CurrentTime;
            return base.Set(referenceId, nValue, expireTime);
        }

        public void ApplyTemporaryStat(long currentTime)
        {
            if (CurrentTime - tLastDamaged < 5000) return;

            var debuff = false;
            var damage = N;
            var hp = Owner.PrimaryStats.HP;

            if (hp <= 1)
            {
                // Player is already dead, don't overdo it.
                damage = 0;
                debuff = true;
            }
            else if (damage > hp)
            {
                // Would otherwise kill the player
                damage = (short)(hp - 1);
                debuff = true;
            }

            if (damage > 0)
            {
                Owner.DamageHP(damage);
                MapPacket.SendPlayerSpecialSkillAnim(Owner, R);
            }

            tLastDamaged = CurrentTime;

            if (debuff)
            {
                Owner.PrimaryStats.RemoveByReference(R);
            }
        }

        public override bool TryReset(long currentTime, ref BuffValueTypes flag)
        {
            ApplyTemporaryStat(currentTime);
            return base.TryReset(currentTime, ref flag);
        }

        public override bool EncodeForCC(Packet pr, ref BuffValueTypes flag, long currentTime)
        {
            if (base.EncodeForCC(pr, ref flag, currentTime))
            {
                pr.WriteLong(tLastDamaged);
                return true;
            }
            return false;
        }

        public override bool DecodeForCC(Packet pr, BuffValueTypes flag)
        {
            if (base.DecodeForCC(pr, flag))
            {
                tLastDamaged = pr.ReadLong();
                return true;
            }
            return false;
        }
    }

    public class BuffStat_ComboAttack : BuffStat
    {
        public int MaxOrbs { get; set; }

        public BuffStat_ComboAttack(BuffValueTypes flag) : base(flag)
        {
        }

        public override BuffValueTypes Set(int referenceId, short nValue, long expireTime)
        {
            MaxOrbs = nValue;
            return base.Set(referenceId, 1, expireTime);
        }
    }

    public class CharacterPrimaryStats
    {
        private Character Char { get; }

        public byte Level
        {
            get => Char.Level;
            set => Char.Level = value;
        }
        public short Job
        {
            get => Char.Job;
            set => Char.Job = value;
        }
        public short Str { get; set; }
        public short Dex { get; set; }
        public short Int { get; set; }
        public short Luk { get; set; }
        public short MaxHP { get; set; }
        public short MP { get; set; }
        public short MaxMP { get; set; }
        public short AP { get; set; }
        public short SP { get; set; }
        public int EXP { get; set; }
        public short Fame { get; set; }

        public float speedMod => TotalSpeed + 100.0f;

        public short MAD => Int;
        public short MDD => Int;
        public int EVA
        {
            get
            {
                int eva = Luk / 2 + Dex / 4;

                var buff = Char.Skills.GetSkillLevelData(4000000, out byte lvl2);
                if (buff != null)
                {
                    eva += buff.YValue;
                }

                return eva;
            }
        }

        public int ACC
        {
            get
            {
                int acc = 0;

                if (Job / 100 == 3 || Job / 100 == 4)
                    acc = (int)((Luk * 0.3) + (Dex * 0.6));
                else
                    acc = (int)((Luk * 0.5) + (Dex * 0.8));

                var buff = Char.Skills.GetSkillLevelData(Constants.Archer.Skills.BlessingOfAmazon, out byte lvl1);
                if (buff != null)
                {
                    acc += buff.XValue;
                }

                buff = Char.Skills.GetSkillLevelData(Constants.Rogue.Skills.NimbleBody, out byte lvl2);
                if (buff != null)
                {
                    acc += buff.XValue;
                }

                // TODO: Weapon mastery buff
                /*
                buff = Char.Skills.GetSkillLevelData(Char.Skills.GetMastery(), out byte lvl3);
                if (buff != null)
                {
                    acc += buff.Accurancy;
                }
                */

                return Math.Max(0, Math.Min(acc, 999));
            }
        }

        public int Hands => Dex + Luk + Int;

        // TODO: Get this out here
        public int BuddyListCapacity { get; set; }

        private short _hp;
        public short HP
        {
            get
            {
                return _hp;
            }
            set
            {
                _hp = value;
                Char.PartyHPUpdate();
            }
        }


        private Dictionary<byte, EquipBonus> EquipStats { get; } = new Dictionary<byte, EquipBonus>();
        public BonusSet EquipBonuses = new BonusSet();
        public BonusSet BuffBonuses = new BonusSet();

        public int TotalStr => Str + EquipBonuses.Str;
        public int TotalDex => Dex + EquipBonuses.Dex;
        public int TotalInt => Int + EquipBonuses.Int;
        public int TotalLuk => Luk + EquipBonuses.Luk;
        public int TotalMaxHP => MaxHP + EquipBonuses.MaxHP + BuffBonuses.MaxHP;
        public int TotalMaxMP => MaxMP + EquipBonuses.MaxMP + BuffBonuses.MaxMP;

        public short TotalMAD => (short)Math.Max(0, Math.Min(MAD + EquipBonuses.MAD + BuffBonuses.MAD, 1999));
        public short TotalMDD => (short)Math.Max(0, Math.Min(MDD + EquipBonuses.MDD + BuffBonuses.MDD, 1999));
        public short TotalPAD => (short)Math.Max(0, Math.Min(EquipBonuses.PAD + BuffBonuses.PAD, 1999));
        public short TotalPDD => (short)Math.Max(0, Math.Min(EquipBonuses.PDD + BuffBonuses.PDD, 1999));

        public short TotalACC => (short)Math.Max(0, Math.Min(ACC + EquipBonuses.ACC + BuffBonuses.ACC, 999));
        public short TotalEVA => (short)Math.Max(0, Math.Min(EVA + EquipBonuses.EVA + BuffBonuses.EVA, 999));
        public short TotalHands => (short)Math.Max(0, Math.Min(Hands + EquipBonuses.Hands + BuffBonuses.Hands, 999));
        public short TotalJump => (short)Math.Max(100, Math.Min(EquipBonuses.Jump + BuffBonuses.Jump, 123));
        public byte TotalSpeed => (byte)Math.Max(100, Math.Min(EquipBonuses.Speed + BuffBonuses.Speed, 200));


        // Real Stats

        public BuffStat BuffWeaponAttack { get; } = new BuffStat(BuffValueTypes.WeaponAttack);
        public BuffStat BuffWeaponDefense { get; } = new BuffStat(BuffValueTypes.WeaponDefense);
        public BuffStat BuffMagicAttack { get; } = new BuffStat(BuffValueTypes.MagicAttack);
        public BuffStat BuffMagicDefense { get; } = new BuffStat(BuffValueTypes.MagicDefense);
        public BuffStat BuffAccurancy { get; } = new BuffStat(BuffValueTypes.Accurancy);
        public BuffStat BuffAvoidability { get; } = new BuffStat(BuffValueTypes.Avoidability);
        public BuffStat BuffHands { get; } = new BuffStat(BuffValueTypes.Hands);
        public BuffStat BuffSpeed { get; } = new BuffStat(BuffValueTypes.Speed);
        public BuffStat BuffJump { get; } = new BuffStat(BuffValueTypes.Jump);
        public BuffStat BuffMagicGuard { get; } = new BuffStat(BuffValueTypes.MagicGuard);
        public BuffStat BuffDarkSight { get; } = new BuffStat(BuffValueTypes.DarkSight);
        public BuffStat BuffBooster { get; } = new BuffStat(BuffValueTypes.Booster);
        public BuffStat BuffPowerGuard { get; } = new BuffStat(BuffValueTypes.PowerGuard);
        public BuffStat BuffMaxHP { get; } = new BuffStat(BuffValueTypes.MaxHP);
        public BuffStat BuffMaxMP { get; } = new BuffStat(BuffValueTypes.MaxMP);
        public BuffStat BuffInvincible { get; } = new BuffStat(BuffValueTypes.Invincible);
        public BuffStat BuffSoulArrow { get; } = new BuffStat(BuffValueTypes.SoulArrow);
        public BuffStat BuffStun { get; } = new BuffStat(BuffValueTypes.Stun);
        public BuffStat BuffPoison { get; } = new BuffStat(BuffValueTypes.Poison);
        public BuffStat BuffSeal { get; } = new BuffStat(BuffValueTypes.Seal);
        public BuffStat BuffDarkness { get; } = new BuffStat(BuffValueTypes.Darkness);
        public BuffStat_ComboAttack BuffComboAttack { get; } = new BuffStat_ComboAttack(BuffValueTypes.ComboAttack);
        public BuffStat BuffCharges { get; } = new BuffStat(BuffValueTypes.Charges);
        public BuffStat_DragonBlood BuffDragonBlood { get; }
        public BuffStat BuffHolySymbol { get; } = new BuffStat(BuffValueTypes.HolySymbol);
        public BuffStat BuffMesoUP { get; } = new BuffStat(BuffValueTypes.MesoUP);
        public BuffStat BuffShadowPartner { get; } = new BuffStat(BuffValueTypes.ShadowPartner);
        public BuffStat BuffPickPocket { get; } = new BuffStat(BuffValueTypes.PickPocket);
        public BuffStat BuffMesoGuard { get; } = new BuffStat(BuffValueTypes.MesoGuard);
        public BuffStat BuffThaw { get; } = new BuffStat(BuffValueTypes.Thaw);
        public BuffStat BuffWeakness { get; } = new BuffStat(BuffValueTypes.Weakness);
        public BuffStat BuffCurse { get; } = new BuffStat(BuffValueTypes.Curse);
        public BuffStat BuffSlow { get; } = new BuffStat(BuffValueTypes.Slow);


        public CharacterPrimaryStats(Character chr)
        {
            Char = chr;
            BuffDragonBlood = new BuffStat_DragonBlood(BuffValueTypes.DragonBlood, Char);
        }

        public void AddEquipStats(sbyte slot, EquipItem equip, bool isLoading)
        {
            try
            {
                var realSlot = (byte)Math.Abs(slot);
                if (equip != null)
                {
                    if (!EquipStats.TryGetValue(realSlot, out var equipBonus))
                    {
                        equipBonus = new EquipBonus();
                    }

                    equipBonus.ID = equip.ItemID;
                    equipBonus.MaxHP = equip.HP;
                    equipBonus.MaxMP = equip.MP;
                    equipBonus.Str = equip.Str;
                    equipBonus.Int = equip.Int;
                    equipBonus.Dex = equip.Dex;
                    equipBonus.Luk = equip.Luk;
                    equipBonus.Speed = equip.Speed;
                    equipBonus.PAD = equip.Watk;
                    equipBonus.PDD = equip.Wdef;
                    equipBonus.MAD = equip.Matk;
                    equipBonus.MDD = equip.Mdef;
                    equipBonus.EVA = equip.Avo;
                    equipBonus.ACC = equip.Acc;
                    equipBonus.Hands = equip.Hands;
                    equipBonus.Jump = equip.Jump;
                    EquipStats[realSlot] = equipBonus;
                }
                else
                {
                    EquipStats.Remove(realSlot);
                }
                CalculateAdditions(true, isLoading);
            }
            catch (Exception ex)
            {
                Program.MainForm.LogAppend(ex.ToString());
            }
        }

        public BonusSet CalculateBonusSet(Constants.EquipSlots.Slots excludingSlot)
        {
            var bonusSet = new BonusSet();
            foreach (var data in EquipStats.Where(x => x.Key != (byte)excludingSlot))
            {
                var item = data.Value;
                if (bonusSet.Dex + item.Dex > short.MaxValue) bonusSet.Dex = short.MaxValue;
                else bonusSet.Dex += item.Dex;
                if (bonusSet.Int + item.Int > short.MaxValue) bonusSet.Int = short.MaxValue;
                else bonusSet.Int += item.Int;
                if (bonusSet.Luk + item.Luk > short.MaxValue) bonusSet.Luk = short.MaxValue;
                else bonusSet.Luk += item.Luk;
                if (bonusSet.Str + item.Str > short.MaxValue) bonusSet.Str = short.MaxValue;
                else bonusSet.Str += item.Str;
                if (bonusSet.MaxMP + item.MaxMP > short.MaxValue) bonusSet.MaxMP = short.MaxValue;
                else bonusSet.MaxMP += item.MaxMP;
                if (bonusSet.MaxHP + item.MaxHP > short.MaxValue) bonusSet.MaxHP = short.MaxValue;
                else bonusSet.MaxHP += item.MaxHP;

                bonusSet.PAD += item.PAD;

                // TODO: Shield mastery buff
                if (data.Key == (byte)Constants.EquipSlots.Slots.Shield)
                {

                }

                bonusSet.PDD += item.PDD;
                bonusSet.MAD += item.MAD;
                bonusSet.MDD += item.MDD;
                bonusSet.ACC += item.ACC;
                bonusSet.EVA += item.EVA;
                bonusSet.Speed += item.Speed;
                bonusSet.Jump += item.Jump;
                bonusSet.Hands += item.Hands;

                bonusSet.PAD = (short)Math.Max(0, Math.Min((int)bonusSet.PAD, 1999));
                bonusSet.PDD = (short)Math.Max(0, Math.Min((int)bonusSet.PDD, 1999));
                bonusSet.MAD = (short)Math.Max(0, Math.Min((int)bonusSet.MAD, 1999));
                bonusSet.MDD = (short)Math.Max(0, Math.Min((int)bonusSet.MDD, 1999));
                bonusSet.ACC = (short)Math.Max(0, Math.Min((int)bonusSet.ACC, 999));
                bonusSet.EVA = (short)Math.Max(0, Math.Min((int)bonusSet.EVA, 999));
                bonusSet.Hands = (short)Math.Max(0, Math.Min((int)bonusSet.Hands, 999));
                bonusSet.Speed = (short)Math.Max(100, Math.Min((int)bonusSet.Speed, 200));
                bonusSet.Jump = (short)Math.Max(100, Math.Min((int)bonusSet.Jump, 123));
            }

            return bonusSet;
        }

        public void CalculateAdditions(bool updateEquips, bool isLoading)
        {
            if (updateEquips)
            {
                EquipBonuses = CalculateBonusSet(Constants.EquipSlots.Slots.Invalid);
            }

            if (!isLoading)
            {
                CheckHPMP(isLoading);
                Char.FlushDamageLog();
            }
        }

        public void CheckHPMP(bool isLoading)
        {
            var mhp = GetMaxHP(false);
            var mmp = GetMaxMP(false);
            if (HP > mhp)
            {
                Char.ModifyHP(mhp, !isLoading);
            }
            if (MP > mmp)
            {
                Char.ModifyMP(mmp, !isLoading);
            }
        }

        public void CheckWeaponBuffs(int oldItem)
        {
            var equippedId = Char.Inventory.GetEquippedItemId(Constants.EquipSlots.Slots.Weapon, false);

            if (equippedId != oldItem || equippedId == 0)
            {
                Char.Buffs.ClearWeaponBuffs();
            }
        }

        public short getTotalStr() => (short)(Str + EquipBonuses.Str);
        public short getTotalDex() => (short)(Dex + EquipBonuses.Dex);
        public short getTotalInt() => (short)(Int + EquipBonuses.Int);
        public short getTotalLuk() => (short)(Luk + EquipBonuses.Luk);
        public short getTotalMagicAttack() => (short)(Int + EquipBonuses.MAD);
        public short getTotalMagicDef() => (short)(Int + EquipBonuses.MDD);

        public short GetStrAddition(bool nobonus = false)
        {
            if (!nobonus)
            {
                return (short)((Str + EquipBonuses.Str + BuffBonuses.Str) > short.MaxValue ? short.MaxValue : (Str + EquipBonuses.Str + BuffBonuses.Str));
            }
            return Str;
        }
        public short GetDexAddition(bool nobonus = false)
        {
            if (!nobonus)
            {
                return (short)((Dex + EquipBonuses.Dex + BuffBonuses.Dex) > short.MaxValue ? short.MaxValue : (Dex + EquipBonuses.Dex + BuffBonuses.Dex));
            }
            return Dex;
        }
        public short GetIntAddition(bool nobonus = false)
        {
            if (!nobonus)
            {
                return (short)((Int + EquipBonuses.Int + BuffBonuses.Int) > short.MaxValue ? short.MaxValue : (Int + EquipBonuses.Int + BuffBonuses.Int));
            }
            return Int;
        }
        public short GetLukAddition(bool nobonus = false)
        {
            if (!nobonus)
            {
                return (short)((Luk + EquipBonuses.Luk + BuffBonuses.Luk) > short.MaxValue ? short.MaxValue : (Luk + EquipBonuses.Luk + BuffBonuses.Luk));
            }
            return Luk;
        }
        public short GetMaxHP(bool nobonus = false)
        {
            if (!nobonus)
            {
                return (short)((MaxHP + EquipBonuses.MaxHP + BuffBonuses.MaxHP) > short.MaxValue ? short.MaxValue : (MaxHP + EquipBonuses.MaxHP + BuffBonuses.MaxHP));
            }
            return MaxHP;
        }
        public short GetMaxMP(bool nobonus = false)
        {
            if (!nobonus)
            {
                return (short)((MaxMP + EquipBonuses.MaxMP + BuffBonuses.MaxMP) > short.MaxValue ? short.MaxValue : (MaxMP + EquipBonuses.MaxMP + BuffBonuses.MaxMP));
            }
            return MaxMP;
        }

        public void ProcessHyperBodyReset(BuffValueTypes flag)
        {
            if (flag.HasFlag(BuffValueTypes.MaxHP) &&
                flag.HasFlag(BuffValueTypes.MaxMP))
            {
                Char.Buffs.CancelHyperBody();
            }
        }

        /// <summary>
        /// Returns all buffstat instances in order of encoding
        /// </summary>
        /// <returns></returns>
        public BuffStat[] GetAllBuffStats()
        {
            return new[]
            {
                BuffWeaponAttack,
                BuffWeaponDefense,
                BuffMagicAttack,
                BuffMagicDefense,
                BuffAccurancy,
                BuffAvoidability,
                BuffHands,
                BuffSpeed,
                BuffJump,
                BuffMagicGuard,
                BuffDarkSight,
                BuffBooster,
                BuffPowerGuard,
                BuffMaxHP,
                BuffMaxMP,
                BuffInvincible,
                BuffSoulArrow,
                BuffStun,
                BuffPoison,
                BuffSeal,
                BuffDarkness,
                BuffComboAttack,
                BuffCharges,
                BuffDragonBlood,
                BuffHolySymbol,
                BuffMesoUP,
                BuffShadowPartner,
                BuffPickPocket,
                BuffMesoGuard,
                BuffThaw,
                BuffWeakness,
                BuffCurse,
                BuffSlow,
            };
        }
        
        public void Reset(bool sendPacket)
        {
            BuffValueTypes flags = 0;

            foreach (var stat in GetAllBuffStats())
                flags |= stat.Reset();

            Char.Buffs.FinalizeDebuff(flags, sendPacket);
        }

        public void DecodeForCC(Packet packet)
        {
            var flags = (BuffValueTypes)packet.ReadULong();

            foreach (var stat in GetAllBuffStats())
                stat.DecodeForCC(packet, flags);

            if (BuffMaxHP.IsSet())
            {
                short hpmpBonus = (short)((double)Char.PrimaryStats.MaxHP * ((double)BuffMaxHP.N / 100.0d));
                Char.PrimaryStats.BuffBonuses.MaxHP = hpmpBonus;
                hpmpBonus = (short)((double)Char.PrimaryStats.MaxMP * ((double)BuffMaxMP.N / 100.0d));
                Char.PrimaryStats.BuffBonuses.MaxMP = hpmpBonus;
            }

            if (BuffComboAttack.IsSet())
            {
                var sld = Char.Skills.GetSkillLevelData(BuffComboAttack.R);
                if (sld != null)
                {
                    BuffComboAttack.MaxOrbs = sld.XValue;
                }
            }
        }

        public void EncodeForCC(Packet packet)
        {
            var currentTime = BuffStat.GetTimeForBuff();
            var offset = packet.Position;
            packet.WriteULong(0);
            BuffValueTypes flags = 0;

            foreach (var stat in GetAllBuffStats())
                stat.EncodeForCC(packet, ref flags, currentTime);

            packet.SetULong(offset, (uint)flags);
        }

        public void CheckExpired(long currentTime)
        {
            BuffValueTypes endFlag = 0;

            foreach (var stat in GetAllBuffStats())
                stat.TryReset(currentTime, ref endFlag);

            Char.Buffs.FinalizeDebuff(endFlag);
        }

        public BuffValueTypes AllActiveBuffs()
        {
            var currentTime = BuffStat.GetTimeForBuff();
            BuffValueTypes flags = 0;

            foreach (var stat in GetAllBuffStats())
                flags |= stat.GetState(currentTime);

            return flags;
        }

        public BuffValueTypes RemoveByReference(int pBuffValue)
        {
            if (pBuffValue == 0) return 0;

            BuffValueTypes endFlag = 0;

            foreach (var stat in GetAllBuffStats())
                stat.TryResetByReference(pBuffValue, ref endFlag);

            Char.Buffs.FinalizeDebuff(endFlag);

            return endFlag;
        }


        public void EncodeForLocal(Packet pw, BuffValueTypes pSpecificFlag = BuffValueTypes.ALL)
        {
            var currentTime = BuffStat.GetTimeForBuff();

            {
                var tmpBuffPos = pw.Position;
                BuffValueTypes endFlag = 0;
                pw.WriteUInt((uint)endFlag);

                foreach (var stat in GetAllBuffStats().Where(x => x.IsNewType))
                {
                    stat.EncodeForLocal(pw, ref endFlag, currentTime, pSpecificFlag);
                }
                
                pw.SetUInt(tmpBuffPos, (uint)((ulong)endFlag >> 32));
            }

            {
                var tmpBuffPos = pw.Position;
                BuffValueTypes endFlag = 0;
                pw.WriteUInt((uint)endFlag);


                foreach (var stat in GetAllBuffStats().Where(x => !x.IsNewType))
                {
                    // Do not encode new flags
                    if ((ulong)stat.Flag > uint.MaxValue) continue;

                    if (stat == BuffDarkSight && stat.HasReferenceId(Constants.Gm.Skills.Hide)) continue;

                    stat.EncodeForLocal(pw, ref endFlag, currentTime, pSpecificFlag);
                }

                pw.SetUInt(tmpBuffPos, (uint)((ulong)endFlag & uint.MaxValue));
            }
        }

        public bool HasBuff(int skillOrItemID)
        {
            var currentTime = BuffStat.GetTimeForBuff();

            return GetAllBuffStats().Any(stat => stat.HasReferenceId(skillOrItemID, currentTime));
        }
    }
}