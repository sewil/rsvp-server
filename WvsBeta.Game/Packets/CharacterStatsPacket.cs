using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Common.Tracking;
using WvsBeta.SharedDataProvider.Templates;

namespace WvsBeta.Game
{

    public static class CharacterStatsPacket
    {
        public static void HandleStats(Character chr, Packet packet)
        {
            chr.ExclRequestSet = true;

            var flag = packet.ReadUInt();
            if (chr.AssertForHack(chr.PrimaryStats.AP <= 0, "Trying to use AP, but nothing left."))
            {
                return;
            }

            var jobTrack = Constants.getJobTrack(chr.PrimaryStats.Job);

            switch ((StatFlags)flag)
            {
                case StatFlags.Str:
                    {
                        if (chr.PrimaryStats.Str >= Constants.MaxStat)
                        {
                            return;
                        }
                        chr.AddStr(1);
                        break;
                    }
                case StatFlags.Dex:
                    {
                        if (chr.PrimaryStats.Dex >= Constants.MaxStat)
                        {
                            return;
                        }
                        chr.AddDex(1);
                        break;
                    }
                case StatFlags.Int:
                    {
                        if (chr.PrimaryStats.Int >= Constants.MaxStat)
                        {
                            return;
                        }
                        chr.AddInt(1);
                        break;
                    }
                case StatFlags.Luk:
                    {
                        if (chr.PrimaryStats.Luk >= Constants.MaxStat)
                        {
                            return;
                        }
                        chr.AddLuk(1);
                        break;
                    }
                case StatFlags.MaxHp:
                    {
                        if (chr.PrimaryStats.MaxHP >= Constants.MaxMaxHp)
                        {
                            return;
                        }
                        short hpGain = 0;

                        hpGain += (short)Rand32.NextBetween(
                            Constants.HpMpFormulaArguments[jobTrack, 1, (int)Constants.HpMpFormulaFields.HPMin],
                            Constants.HpMpFormulaArguments[jobTrack, 1, (int)Constants.HpMpFormulaFields.HPMax]
                        );

                        var improvedMaxHpIncreaseLvl = chr.Skills.GetSkillLevel(Constants.Swordman.Skills.ImprovedMaxHpIncrease);
                        if (improvedMaxHpIncreaseLvl > 0)
                        {
                            hpGain += CharacterSkills.GetSkillLevelData(Constants.Swordman.Skills.ImprovedMaxHpIncrease, improvedMaxHpIncreaseLvl).XValue;
                        }

                        chr.ModifyMaxHP(hpGain);
                        break;
                    }
                case StatFlags.MaxMp:
                    {
                        if (chr.PrimaryStats.MaxMP >= Constants.MaxMaxMp)
                        {
                            return;
                        }
                        short mpGain = 0;
                        var intt = chr.PrimaryStats.GetIntAddition(true);

                        mpGain += (short)Rand32.NextBetween(
                            Constants.HpMpFormulaArguments[jobTrack, 1, (int)Constants.HpMpFormulaFields.MPMin],
                            Constants.HpMpFormulaArguments[jobTrack, 1, (int)Constants.HpMpFormulaFields.MPMax]
                        );

                        // Additional buffing through INT stats
                        mpGain += (short)(
                            intt *
                            Constants.HpMpFormulaArguments[jobTrack, 1, (int)Constants.HpMpFormulaFields.MPIntStatMultiplier] /
                            200
                        );

                        var improvedMaxMpIncreaseLvl = chr.Skills.GetSkillLevel(Constants.Magician.Skills.ImprovedMaxMpIncrease);
                        if (improvedMaxMpIncreaseLvl > 0)
                        {
                            mpGain += CharacterSkills.GetSkillLevelData(Constants.Magician.Skills.ImprovedMaxMpIncrease, improvedMaxMpIncreaseLvl).XValue;
                        }

                        chr.ModifyMaxMP(mpGain);
                        break;
                    }
                default:
                    {
                        Program.MainForm.LogAppend("Unknown type {0:X4}", flag);
                        break;
                    }
            }

            chr.AddAP(-1);
            chr.PrimaryStats.CalculateAdditions(false, false);
        }

        public static void HandleHeal(Character chr, Packet packet)
        {
            chr.ExclRequestSet = true;

            // 2B 00 14 00 00 00 00 03 00 00
            var flag = packet.ReadInt();


            var hp = (flag & 0x0400) != 0 ? packet.ReadShort() : (short)0;
            var mp = (flag & 0x1000) != 0 ? packet.ReadShort() : (short)0;

            var extraHealEffect = packet.ReadByte();

            if (chr.PrimaryStats.HP == 0) return;

            if (hp > 0)
            {
                var timeSinceLastHeal = packet.PacketCreationTime - chr.LastHealHPPacket;
                chr.LastHealHPPacket = packet.PacketCreationTime;

                if (chr.AssertForHack(timeSinceLastHeal < 5000, $"heal HP packet received in {timeSinceLastHeal / 1000.0}s since last one"))
                    return;
            }

            if (mp > 0)
            {
                var timeSinceLastHeal = packet.PacketCreationTime - chr.LastHealMPPacket;
                chr.LastHealMPPacket = packet.PacketCreationTime;

                if (chr.AssertForHack(timeSinceLastHeal < 5000, $"heal MP packet received in {timeSinceLastHeal / 1000.0}s since last one"))
                    return;
            }


            if (hp > 400 ||
                mp > 1000 ||
                (hp > 0 && mp > 0))
            {
                return;
            }

            if (hp > 0)
            {
                // Check endure and stuff here...
                chr.ModifyHP(hp);
            }

            if (mp > 0)
            {
                chr.ModifyMP(mp);
            }

        }

        public static void SendUpdateStat(Character chr, StatFlags StatFlag)
        {
            if (StatFlag <= 0) return;

            var byExclRequest = chr.ExclRequestSet;
            chr.ExclRequestSet = false;

            var pw = new Packet(ServerMessages.STAT_CHANGED);
            pw.WriteBool(byExclRequest);
            pw.WriteUInt((uint)StatFlag);

            if ((StatFlag & StatFlags.Skin) == StatFlags.Skin)
                pw.WriteByte(chr.Skin);
            if ((StatFlag & StatFlags.Eyes) == StatFlags.Eyes)
                pw.WriteInt(chr.Face);
            if ((StatFlag & StatFlags.Hair) == StatFlags.Hair)
                pw.WriteInt(chr.Hair);

            if ((StatFlag & StatFlags.Pet) == StatFlags.Pet)
                pw.WriteLong(chr.PetCashId);

            if ((StatFlag & StatFlags.Level) == StatFlags.Level)
                pw.WriteByte(chr.Level);
            if ((StatFlag & StatFlags.Job) == StatFlags.Job)
                pw.WriteShort(chr.PrimaryStats.Job);
            if ((StatFlag & StatFlags.Str) == StatFlags.Str)
                pw.WriteShort(chr.PrimaryStats.Str);
            if ((StatFlag & StatFlags.Dex) == StatFlags.Dex)
                pw.WriteShort(chr.PrimaryStats.Dex);
            if ((StatFlag & StatFlags.Int) == StatFlags.Int)
                pw.WriteShort(chr.PrimaryStats.Int);
            if ((StatFlag & StatFlags.Luk) == StatFlags.Luk)
                pw.WriteShort(chr.PrimaryStats.Luk);

            if ((StatFlag & StatFlags.Hp) == StatFlags.Hp)
                pw.WriteShort(chr.PrimaryStats.HP);
            if ((StatFlag & StatFlags.MaxHp) == StatFlags.MaxHp)
                pw.WriteShort(chr.PrimaryStats.MaxHP);
            if ((StatFlag & StatFlags.Mp) == StatFlags.Mp)
                pw.WriteShort(chr.PrimaryStats.MP);
            if ((StatFlag & StatFlags.MaxMp) == StatFlags.MaxMp)
                pw.WriteShort(chr.PrimaryStats.MaxMP);

            if ((StatFlag & StatFlags.Ap) == StatFlags.Ap)
                pw.WriteShort(chr.PrimaryStats.AP);
            if ((StatFlag & StatFlags.Sp) == StatFlags.Sp)
                pw.WriteShort(chr.PrimaryStats.SP);

            if ((StatFlag & StatFlags.Exp) == StatFlags.Exp)
                pw.WriteInt(chr.PrimaryStats.EXP);

            if ((StatFlag & StatFlags.Fame) == StatFlags.Fame)
                pw.WriteShort(chr.PrimaryStats.Fame);

            if ((StatFlag & StatFlags.Mesos) == StatFlags.Mesos)
                pw.WriteInt(chr.Inventory.Mesos);

            chr.SendPacket(pw);
        }

        public static bool IsCounterAttackPossible(Character chr, int mobTemplateId, int mobId, sbyte mobAttackIdx, bool reflect, bool powerGuard, bool manaReflect, Pos hit, Pos userPos)
        {
            if (reflect == false) return false;
            if (!DataProvider.Mobs.TryGetValue(mobTemplateId, out var md)) return false;

            var mob = chr.Field.GetMob(mobId);
            if (mob == null) return false;

            // PowerGuard is always OK
            if (powerGuard)
            {
                return true;
            }

            if (manaReflect)
            {
                // Can only return damage on magic attacks! you bumping into a mob is not magical
                if (mobAttackIdx < 0 ||
                    !md.Attacks.TryGetValue((byte)mobAttackIdx, out var mad) ||
                    mad.Magic == false)
                    return false;


                if (Math.Abs(userPos.X - hit.X) > 1200 ||
                    Math.Abs(userPos.Y - hit.Y) > 900)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        public static void HandleCharacterDamage(Character chr, Packet pr)
        {
            for (var i = 0; i < 4; i++)
                chr.CalcDamageRandomizer.NextSeedINT();

            var attack = pr.ReadSByte();
            var damage = pr.ReadInt();


            var reducedDamage = damage;
            var actualHPEffect = -damage;
            var actualMPEffect = 0;
            var healSkillId = 0;
            Mob mob = null;

            if (chr.AssertForHack(damage < -1, "Less than -1 (" + damage + ") damage in HandleCharacterDamage"))
            {
                return;
            }

            if (chr.PrimaryStats.HP == 0) return;

            byte mobSkillId = 0, mobSkillLevel = 0;

            if (attack <= -2)
            {
                mobSkillLevel = pr.ReadByte();
                mobSkillId = pr.ReadByte(); // (short >> 8)

                Trace.WriteLine($"Got a hit with {attack} attack, mobSkillLevel {mobSkillLevel}, mobSkillId {mobSkillId}");

            }
            else
            {
                var magicAttackElement = -1;
                if (pr.ReadBool())
                {
                    magicAttackElement = pr.ReadInt();
                    // 0 = no ElemAttr? (Grendel the Really Old, 9001001)
                    // 1 = Ice (Celion? blue, 5120003)
                    // 2 = Lightning (Regular big Sentinel, 3000000)
                    // 3 = Fire (Fire sentinel, 5200002)
                }

                var mobMapId = pr.ReadInt();
                var mobTemplateId = pr.ReadInt(); // We should probably check if the mob is transformed here

                mob = chr.Field.GetMob(mobMapId);
                if (mob == null ||
                    mobTemplateId != mob.MobID)
                {
                    return;
                }

                MobAttackData mad = null;
                if (attack >= 0 && !mob.Template.Attacks.TryGetValue((byte)attack, out mad))
                {
                    chr.AssertForHack(true, $"Trying to use mob attack {attack} for mob {mobTemplateId}, which doesn't exist.", seriousHack: false);
                    return;
                }

                // Newer ver: int nCalcDamageMobStatIndex
                var stance = pr.ReadByte();
                var reflectPercentage = pr.ReadByte();
                var isReflected = reflectPercentage > 0;

                byte reflectHitAction = 0;
                short reflectX = 0, reflectY = 0;
                int reflectedTo = 0;
                if (isReflected)
                {
                    // Not sure what current maple does, but it seems to have [bool] [int, reflectedTo] here
                    reflectedTo = mobMapId;

                    reflectHitAction = pr.ReadByte();
                    reflectX = pr.ReadShort();
                    reflectY = pr.ReadShort();
                    // Newer version has user pos here
                }

                if (isReflected)
                {
                    if (damage == 0)
                    {
                        Trace.WriteLine("Ignoring isReflected for 0 damage");
                        isReflected = false;
                    }
                    else if (magicAttackElement >= 0)
                    {
                        Trace.WriteLine("Ignoring isReflected for magic skill");
                        isReflected = false;
                    }
                }

                if (!isReflected)
                    reflectPercentage = 0;

                var magicGuard = chr.PrimaryStats.BuffMagicGuard;
                var powerGuard = chr.PrimaryStats.BuffPowerGuard;


                var counterAttack = IsCounterAttackPossible(
                    chr,
                    mobTemplateId,
                    mobMapId,
                    attack,
                    isReflected,
                    powerGuard.IsSet(),
                    // chr.PrimaryStats.BuffManaReflect.IsSet()
                    false,
                    new Pos(reflectX, reflectY),
                    chr.Position
                );


                if (magicGuard.IsSet() &&
                    chr.PrimaryStats.MP > 0)
                {
                    // Absorbs X amount of damage. :)
                    var xValue = magicGuard.N;

                    var damageEaten = (damage * xValue) / 100;
                    damageEaten = Math.Min(chr.PrimaryStats.MP, damageEaten);

                    // MagicGuard doesn't show reduced damage, because you still get hit.

                    Trace.WriteLine($"Reducing damage by MG. Reflected {damageEaten}");

                    //Program.MainForm.LogAppend("MG Damage before change: " + actualHPEffect);
                    actualHPEffect += damageEaten;
                    //Program.MainForm.LogAppend("MG Damage after change: " + actualHPEffect);
                    actualMPEffect = -damageEaten;

                    healSkillId = magicGuard.R;
                }

                if (mad != null)
                {
                    if (mad.DeadlyAttack)
                    {
                        var hpDamage = chr.PrimaryStats.HP - 1;
                        var mpDamage = chr.PrimaryStats.MP - 1;
                        actualHPEffect = -hpDamage;
                        actualMPEffect = -mpDamage;
                        // Mark user DeadlyAttack = 0?
                    }

                    if (mad.MPBurn > 0)
                    {
                        var mpDamage = Math.Min(mad.MPBurn, chr.PrimaryStats.MP);
                        actualMPEffect = -mpDamage;
                    }
                }

                if (isReflected && !counterAttack)
                {
                    Trace.WriteLine("Reflection has been ignored, counter attack was false!");
                    isReflected = false;
                }
                else if (isReflected && counterAttack)
                {
                    if (powerGuard.IsSet())
                    {
                        var xValue = powerGuard.N;

                        var currentHPDamage = -actualHPEffect;

                        currentHPDamage = Math.Min(currentHPDamage, chr.PrimaryStats.HP);

                        var damageReflectedBack = (currentHPDamage * xValue) / 100;

                        // We can only damage up to 10% of mob max hp
                        damageReflectedBack = Math.Min(mob.MaxHP / 10, damageReflectedBack);

                        // Bosses cause lower amount of damage to be reflected
                        if (mob.IsBoss)
                            damageReflectedBack /= 2;

                        if (damageReflectedBack > 0)
                        {
                            if (mob.Template.FixedDamage > 0)
                                damageReflectedBack = mob.Template.FixedDamage;

                            if (mob.Template.Invincible)
                                damageReflectedBack = 0;
                        }

                        // Now we would actually reflect X amount of damage to the right mob (= mob that got reflected back to)

                        mob.GiveDamage(chr, damageReflectedBack, AttackPacket.AttackTypes.Melee);

                        mob.CheckDead(mob.Position);

                        Trace.WriteLine($"Reducing damage by PG. Reflected {damageReflectedBack}");

                        // Calculate new percentage of damage
                        var newPercentage = (byte)Math.Ceiling(damageReflectedBack * 100.0d / damage);

                        Trace.WriteLine($"Percent damage: {reflectPercentage} {newPercentage}");
                        reflectPercentage = newPercentage;

                        actualHPEffect += damageReflectedBack; // Buff 'damaged' hp, so its less
                        reducedDamage -= damageReflectedBack;
                        healSkillId = powerGuard.R;
                    }
                    else if (false /* ManaReflect */)
                    {
                        // Blah blah, when it actually reflected
                        // MapPacket.SendPlayerSpecialSkillAnim(chr, manaReflectSkillId);
                    }
                }

                var mesoGuard = chr.PrimaryStats.BuffMesoGuard;

                if (mesoGuard.IsSet())
                {
                    var mesoGuardSkillId = mesoGuard.R;
                    var percentage = mesoGuard.N;

                    var damageReduction = reducedDamage / 2;
                    short mesoLoss = (short)(damageReduction * percentage / 100);
                    if (damageReduction != 0 && mesoLoss <= chr.Inventory.Mesos)
                    {
                        if (mesoLoss > 0)
                        {
                            MesosTransfer.PlayerUsedSkill(chr.ID, mesoLoss, mesoGuardSkillId);
                            chr.AddMesos(-mesoLoss);
                            actualHPEffect += damageReduction;
                            damage = -actualHPEffect;
                            reducedDamage -= damageReduction;
                        }

                        MapPacket.SendPlayerSpecialSkillAnim(chr, mesoGuardSkillId);
                    }

                    if (mesoLoss > 0 && mesoLoss > chr.Inventory.Mesos)
                    {
                        // Debuff when out of mesos
                        chr.PrimaryStats.RemoveByReference(mesoGuardSkillId);
                    }
                }

                SendCharacterDamageByMob(
                    chr,
                    attack,
                    damage,
                    reducedDamage,
                    healSkillId,
                    mobMapId,
                    mobTemplateId,
                    stance,
                    reflectPercentage,
                    reflectHitAction,
                    reflectX,
                    reflectY
                );

            }

            Trace.WriteLine($"Showing damage: {reducedDamage}, {damage}");
            Trace.WriteLine($"Applying damage: HP {actualHPEffect}, MP: {actualMPEffect}");
            // Normalize HP/MP damage
            actualHPEffect = Math.Max(actualHPEffect, -chr.PrimaryStats.HP);
            actualMPEffect = Math.Max(actualMPEffect, -chr.PrimaryStats.MP);
            Trace.WriteLine($"Applying damage: HP {actualHPEffect}, MP: {actualMPEffect}");

            if (!chr.GMGodMode)
            {
                if (actualHPEffect < 0) chr.ModifyHP((short)actualHPEffect);
                if (actualMPEffect < 0) chr.ModifyMP((short)actualMPEffect);
            }

            if (actualHPEffect < 0 && chr.PrimaryStats.HP > 0)
            {
                if (mobSkillLevel != 0 && mobSkillId != 0)
                {
                    // Check if the skill exists and has any extra effect.

                    if (!DataProvider.MobSkills.TryGetValue(mobSkillId, out var skillLevels)) return;

                    // Still going strong
                    if (!skillLevels.TryGetValue(mobSkillLevel, out var msld)) return;
                    OnStatChangeByMobSkill(chr, msld);
                }
                else if (mob != null)
                {
                    // CUser::OnStatChangeByMobAttack
                    if (mob.Template.Attacks == null ||
                        !mob.Template.Attacks.TryGetValue((byte)attack, out var mad)) return;
                    // Okay, we've got an attack...
                    if (mad.Disease <= 0) return;

                    // Shit's poisonous!
                    // Hmm... We could actually make snails give buffs... hurr

                    if (!DataProvider.MobSkills.TryGetValue(mad.Disease, out var skillLevels)) return;

                    // Still going strong
                    if (!skillLevels.TryGetValue(mad.SkillLevel, out var msld)) return;
                    OnStatChangeByMobSkill(chr, msld);
                }
            }
            else
            {
                // BMS seems to handle dead here...?
            }
        }

        public static void OnStatChangeByMobSkill(Character chr, MobSkillLevelData msld, short delay = 0)
        {
            var skill = (Constants.MobSkills.Skills)msld.SkillID;

            // See if we can actually set the effect...
            var prop = 100;
            if (msld.Prop != 0)
                prop = msld.Prop;

            if (Rand32.Next() % 100 >= prop) return; // Luck.

            if (msld.Time <= 0)
            {
                
                if (skill == Constants.MobSkills.Skills.Dispell)
                {
                    chr.Buffs.ResetByUserSkill();
                }

                return;
            }

            BuffStat setStat = null;
            var rValue = msld.SkillID | (msld.Level << 16);
            var ps = chr.PrimaryStats;
            var nValue = 1;
            switch (skill)
            {
                case Constants.MobSkills.Skills.Seal: setStat = ps.BuffSeal; break;
                case Constants.MobSkills.Skills.Darkness: setStat = ps.BuffDarkness; break;
                case Constants.MobSkills.Skills.Weakness: setStat = ps.BuffWeakness; break;
                case Constants.MobSkills.Skills.Stun: setStat = ps.BuffStun; break;
                case Constants.MobSkills.Skills.Curse: setStat = ps.BuffCurse; break;
                case Constants.MobSkills.Skills.Slow:
                    setStat = ps.BuffSlow;
                    nValue = msld.X;
                    break;
                case Constants.MobSkills.Skills.Poison:
                    setStat = ps.BuffPoison;
                    nValue = msld.X;
                    break;
            }

            if (setStat != null && !setStat.IsSet())
            {
                var buffTime = msld.Time * 1000;
                var stat = setStat.Set(rValue, (short)nValue, BuffStat.GetTimeForBuff(buffTime + delay));

                if (stat != 0)
                {
                    chr.Buffs.FinalizeBuff(stat, delay);
                }
            }
        }

        public static void SendCharacterDamageByMob(
            Character chr,
            sbyte attack,
            int initialDamage,
            int reducedDamage,
            int healSkillId,
            int mobMapId,
            int mobId,
            byte stance,
            byte reflectPercentage,
            byte reflectHitAction,
            short reflectX,
            short reflectY)
        {
            var pw = new Packet(ServerMessages.DAMAGE_PLAYER);
            pw.WriteInt(chr.ID);
            pw.WriteSByte(attack);
            pw.WriteInt(initialDamage);

            if (attack > -2)
            {
                pw.WriteInt(mobMapId);
                pw.WriteInt(mobId);
                pw.WriteByte(stance);
                pw.WriteByte(reflectPercentage);
                if (reflectPercentage > 0)
                {
                    pw.WriteByte(reflectHitAction);
                    pw.WriteShort(reflectX);
                    pw.WriteShort(reflectY);
                }
            }


            pw.WriteInt(reducedDamage);
            // Not used in client
            // if (reducedDamage < 0) pw.WriteInt(healSkillId);

            chr.Field.SendPacket(chr, pw);
        }

        public static void SendCharacterDamage(Character chr, sbyte attack, int initialDamage, int reducedDamage, int healSkillId)
        {
            var pw = new Packet(ServerMessages.DAMAGE_PLAYER);
            pw.WriteInt(chr.ID);
            pw.WriteSByte(attack);
            pw.WriteInt(initialDamage);

            pw.WriteInt(reducedDamage);
            // Not used in client
            // if (reducedDamage < 0) pw.WriteInt(healSkillId);

            chr.Field.SendPacket(chr, pw);
        }

        public static void SendGainEXP(Character chr, int amount, bool IsLastHit, bool Quest = false)
        {
            var pw = new Packet(ServerMessages.SHOW_STATUS_INFO);
            pw.WriteByte(3);
            pw.WriteBool(IsLastHit);
            pw.WriteInt(amount);
            pw.WriteBool(Quest);
            chr.SendPacket(pw);
        }

        public static void SendGainDrop(Character chr, bool isMesos, int idOrMesosAmount, short amount)
        {
            var pw = new Packet(ServerMessages.SHOW_STATUS_INFO);
            pw.WriteByte(0x00);
            pw.WriteBool(isMesos);
            pw.WriteInt(idOrMesosAmount);

            if (!isMesos)
            {
                var inv = (byte)(idOrMesosAmount / 1000000);
                pw.WriteInt(inv == 1 ? 1 : amount);
            }
            chr.SendPacket(pw);
        }

        public static void SendMonsterBook(Character chr, CharacterMonsterBook monsterBook)
        {
            var pw = new Packet(CfgServerMessages.CFG_MONSTER_BOOK);
            pw.WriteInt(monsterBook.Cards.Count);
            foreach (var card in monsterBook.Cards)
            {
                pw.WriteInt(card.Key);
                pw.WriteByte(card.Value);
            }
            chr.SendPacket(pw);
        }

    }

    [Flags]
    public enum StatFlags : uint
    {
        Skin = 0x01,
        Eyes = 0x02,
        Hair = 0x04,
        Pet = 0x08,
        Level = 0x10,
        Job = 0x20,
        Str = 0x40,
        Dex = 0x80,
        Int = 0x100,
        Luk = 0x200,
        Hp = 0x400,
        MaxHp = 0x800,
        Mp = 0x1000,
        MaxMp = 0x2000,
        Ap = 0x4000,
        Sp = 0x8000,
        Exp = 0x10000,
        Fame = 0x20000,
        Mesos = 0x40000
    };
}