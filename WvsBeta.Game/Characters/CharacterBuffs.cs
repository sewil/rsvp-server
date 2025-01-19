using System.Diagnostics;
using System.Linq;
using WvsBeta.Common;

namespace WvsBeta.Game
{
    public class CharacterBuffs
    {
        public Character Character { get; set; }

        public CharacterBuffs(Character chr)
        {
            Character = chr;
        }

        public void RemoveItemBuff(int itemid)
        {
            Character.PrimaryStats.RemoveByReference(-itemid);
        }

        public void AddItemBuff(int itemid)
        {
            var alch = Character.Skills.GetSkillLevelData(Constants.Hermit.Skills.Alchemist, out _);
            var data = DataProvider.Items[itemid];
            long buffTime = data.BuffTime;

            if (alch != null)
            {
                buffTime = (long)((double)buffTime * alch.XValue / 100.0);
            }

            var expireTime = BuffStat.GetTimeForBuff(buffTime);

            var ps = Character.PrimaryStats;
            var value = -itemid;
            BuffValueTypes added = 0;

            void applyBuff(BuffStat bs, short statValue)
            {
                if (statValue == 0) return;
                added |= bs.Set(value, statValue, expireTime);
            }
            
            // buffs
            applyBuff(ps.BuffAccurancy, data.Accuracy);
            applyBuff(ps.BuffAvoidability, data.Avoidance);
            applyBuff(ps.BuffSpeed, data.Speed);
            applyBuff(ps.BuffMagicAttack, data.MagicAttack);
            applyBuff(ps.BuffWeaponAttack, data.WeaponAttack);
            applyBuff(ps.BuffWeaponDefense, data.WeaponDefense);
            applyBuff(ps.BuffThaw, data.Thaw);
            applyBuff(ps.BuffJump, data.Jump);

            // debuffs
            applyBuff(ps.BuffStun, data.Stun);
            applyBuff(ps.BuffPoison, data.Poison);
            applyBuff(ps.BuffSeal, data.Seal);
            applyBuff(ps.BuffDarkness, data.Darkness);
            applyBuff(ps.BuffWeakness, data.Weakness);
            applyBuff(ps.BuffCurse, data.Curse);

            FinalizeBuff(added, 0);

            BuffValueTypes removed = 0;

            if (data.Cures.HasFlag(ItemData.CureFlags.Weakness))
                removed |= ps.BuffWeakness.Reset();

            if (data.Cures.HasFlag(ItemData.CureFlags.Poison))
                removed |= ps.BuffPoison.Reset();

            if (data.Cures.HasFlag(ItemData.CureFlags.Curse))
                removed |= ps.BuffCurse.Reset();

            if (data.Cures.HasFlag(ItemData.CureFlags.Darkness))
                removed |= ps.BuffDarkness.Reset();

            if (data.Cures.HasFlag(ItemData.CureFlags.Seal))
                removed |= ps.BuffSeal.Reset();
            
            // No slow cureflag?
            FinalizeDebuff(removed);
        }

        public void ClearWeaponBuffs()
        {
            var ps = Character.PrimaryStats;
            BuffValueTypes removed = 0;

            removed |= ps.BuffBooster.Reset();
            removed |= ps.BuffCharges.Reset();
            removed |= ps.BuffComboAttack.Reset();
            removed |= ps.BuffSoulArrow.Reset();

            FinalizeDebuff(removed);
        }

        public void Dispel()
        {
            var ps = Character.PrimaryStats;
            BuffValueTypes removed = 0;

            removed |= ps.BuffWeakness.Reset();
            removed |= ps.BuffPoison.Reset();
            removed |= ps.BuffCurse.Reset();
            removed |= ps.BuffDarkness.Reset();
            removed |= ps.BuffSeal.Reset();
            removed |= ps.BuffSlow.Reset();

            FinalizeDebuff(removed);
        }
        
        public void ResetByUserSkill()
        {
            var ps = Character.PrimaryStats;
            BuffValueTypes removed = 0;

            ps.GetAllBuffStats()
                // Only take ones that are set by skills...
                .Where(x => x.R / 1000000 > 0)
                .ForEach(stat => removed |= stat.Reset());

            FinalizeDebuff(removed);
        }

        public void CancelHyperBody()
        {
            var primaryStats = Character.PrimaryStats;
            primaryStats.BuffBonuses.MaxHP = 0;
            primaryStats.BuffBonuses.MaxMP = 0;


            if (primaryStats.HP > primaryStats.GetMaxHP(false))
            {
                Character.ModifyHP(primaryStats.GetMaxHP(false));
            }

            if (primaryStats.MP > primaryStats.GetMaxMP(false))
            {
                Character.ModifyMP(primaryStats.GetMaxMP(false));
            }
        }


        public void AddBuff(int SkillID, byte level, short delayMs = 0)
        {
            if (!BuffDataProvider.SkillBuffValues.TryGetValue(SkillID, out var flags))
            {
                return;
            }

            
            if (level == 0xFF)
            {
                level = Character.Skills.Skills[SkillID];
            }
            var data = DataProvider.Skills[SkillID].Levels[level];


            long time = data.BuffTime * 1000;
            time += delayMs;

            Trace.WriteLine($"Adding buff from skill {SkillID} lvl {level}: {time}. Flags {flags}");

            var expireTime = BuffStat.GetTimeForBuff(time);
            var ps = Character.PrimaryStats;
            BuffValueTypes added = 0;

            if (flags.HasFlag(BuffValueTypes.WeaponAttack)) added |= ps.BuffWeaponAttack.Set(SkillID, data.WeaponAttack, expireTime);
            if (flags.HasFlag(BuffValueTypes.WeaponDefense)) added |= ps.BuffWeaponDefense.Set(SkillID, data.WeaponDefense, expireTime);
            if (flags.HasFlag(BuffValueTypes.MagicAttack)) added |= ps.BuffMagicAttack.Set(SkillID, data.MagicAttack, expireTime);
            if (flags.HasFlag(BuffValueTypes.MagicDefense)) added |= ps.BuffMagicDefense.Set(SkillID, data.MagicDefense, expireTime);
            if (flags.HasFlag(BuffValueTypes.Accurancy)) added |= ps.BuffAccurancy.Set(SkillID, data.Accurancy, expireTime);
            if (flags.HasFlag(BuffValueTypes.Avoidability)) added |= ps.BuffAvoidability.Set(SkillID, data.Avoidability, expireTime);
            //if (flags.HasFlag(BuffValueTypes.Hands)) added |= ps.BuffHands.Set(SkillID, data.Hands, expireTime);
            if (flags.HasFlag(BuffValueTypes.Speed)) added |= ps.BuffSpeed.Set(SkillID, data.Speed, expireTime);
            if (flags.HasFlag(BuffValueTypes.Jump)) added |= ps.BuffJump.Set(SkillID, data.Jump, expireTime);
            if (flags.HasFlag(BuffValueTypes.MagicGuard)) added |= ps.BuffMagicGuard.Set(SkillID, data.XValue, expireTime);
            if (flags.HasFlag(BuffValueTypes.DarkSight)) added |= ps.BuffDarkSight.Set(SkillID, data.XValue, expireTime);
            if (flags.HasFlag(BuffValueTypes.Booster)) added |= ps.BuffBooster.Set(SkillID, data.XValue, expireTime);
            if (flags.HasFlag(BuffValueTypes.PowerGuard)) added |= ps.BuffPowerGuard.Set(SkillID, data.XValue, expireTime);
            if (flags.HasFlag(BuffValueTypes.MaxHP)) added |= ps.BuffMaxHP.Set(SkillID, data.XValue, expireTime);
            if (flags.HasFlag(BuffValueTypes.MaxMP)) added |= ps.BuffMaxMP.Set(SkillID, data.YValue, expireTime);
            if (flags.HasFlag(BuffValueTypes.Invincible)) added |= ps.BuffInvincible.Set(SkillID, data.XValue, expireTime);
            if (flags.HasFlag(BuffValueTypes.SoulArrow)) added |= ps.BuffSoulArrow.Set(SkillID, data.XValue, expireTime);
            if (flags.HasFlag(BuffValueTypes.ComboAttack)) added |= ps.BuffComboAttack.Set(SkillID, data.XValue, expireTime);
            if (flags.HasFlag(BuffValueTypes.Charges)) added |= ps.BuffCharges.Set(SkillID, data.XValue, expireTime);
            if (flags.HasFlag(BuffValueTypes.DragonBlood)) added |= ps.BuffDragonBlood.Set(SkillID, data.XValue, expireTime);
            if (flags.HasFlag(BuffValueTypes.HolySymbol)) added |= ps.BuffHolySymbol.Set(SkillID, data.XValue, expireTime);
            if (flags.HasFlag(BuffValueTypes.MesoUP)) added |= ps.BuffMesoUP.Set(SkillID, data.XValue, expireTime);
            if (flags.HasFlag(BuffValueTypes.ShadowPartner)) added |= ps.BuffShadowPartner.Set(SkillID, data.XValue, expireTime);
            if (flags.HasFlag(BuffValueTypes.PickPocket)) added |= ps.BuffPickPocket.Set(SkillID, data.XValue, expireTime);
            if (flags.HasFlag(BuffValueTypes.MesoGuard)) added |= ps.BuffMesoGuard.Set(SkillID, data.XValue, expireTime);

            FinalizeBuff(added, delayMs);
        }

        public void FinalizeBuff(BuffValueTypes added, short delay, bool sendPacket = true)
        {
            if (added == 0) return;
            Trace.WriteLine($"Added buffs {added}");

            Character.FlushDamageLog();

            if (!sendPacket) return;
            BuffPacket.SetTempStats(Character, added, delay);
            MapPacket.SendPlayerBuffed(Character, added, delay);
        }

        public void FinalizeDebuff(BuffValueTypes removed, bool sendPacket = true)
        {
            if (removed == 0) return;
            Trace.WriteLine($"Removed buffs {removed}");

            Character.FlushDamageLog();
            Character.PrimaryStats.ProcessHyperBodyReset(removed);

            if (!sendPacket) return;
            BuffPacket.ResetTempStats(Character, removed);
            MapPacket.SendPlayerDebuffed(Character, removed);
        }
        
    }
}