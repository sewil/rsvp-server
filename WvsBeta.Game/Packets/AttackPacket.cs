using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.GameObjects;
using WvsBeta.SharedDataProvider.Templates;

namespace WvsBeta.Game
{
    public static class AttackPacket
    {
        public enum AttackTypes
        {
            Melee,
            Ranged,
            Magic,
            Summon
        }

        public static bool ParseAttackData(Character chr, Packet packet, out AttackData data, AttackTypes type)
        {
            // Don't accept zombies
            if (chr.PrimaryStats.HP == 0)
            {
                data = null;
                return false;
            }

            // Normal weapon + sword boost gives about 500, so we need to go faster
            if (false && packet.PacketCreationTime - chr.LastAttackPacket < 350)
            {
                Trace.WriteLine($"{packet.PacketCreationTime - chr.LastAttackPacket}");
                if (chr.AssertForHack(chr.FastAttackHackCount++ > 5, $"Fast attack hack, type {type}"))
                {
                    chr.FastAttackHackCount = 0;
                    data = null;
                    return false;
                }
            }
            chr.LastAttackPacket = packet.PacketCreationTime;


            var ad = new AttackData();
            byte hits;
            byte targets;
            var skillid = 0;
            data = null;

            if (type != AttackTypes.Summon)
            {
                ad.RandomNumber = chr.RndActionRandomizer.Random();

                var TByte = packet.ReadByte();
                skillid = packet.ReadInt();

                if (skillid != 0)
                {
                    if (!chr.Skills.Skills.TryGetValue(skillid, out var level))
                    {
                        return false; // Hacks
                    }

                    ad.SkillLevel = level;
                }
                else
                {
                    ad.SkillLevel = 0;
                }

                if (skillid == Constants.ChiefBandit.Skills.MesoExplosion)
                {
                    ad.IsMesoExplosion = true;
                }

                targets = (byte)((TByte >> 4) & 0x0F);
                hits = (byte)(TByte & 0x0F);

                ad.Option = packet.ReadByte();
                var b = packet.ReadByte();
                ad.Action = (byte)(b & 0x7F);
                ad.FacesLeft = (b >> 7) == 1;
                ad.AttackType = packet.ReadByte();
            }
            else
            {
                ad.SummonID = packet.ReadInt();
                ad.AttackType = packet.ReadByte();
                targets = 1;
                hits = 1;
            }

            if (type == AttackTypes.Ranged)
            {
                ad.StarItemSlot = packet.ReadShort();
                var item = chr.Inventory.GetItem(2, ad.StarItemSlot);

                if (ad.StarItemSlot == 0)
                {
                    if (!chr.PrimaryStats.BuffSoulArrow.IsSet())
                    {
                        chr.DesyncedSoulArrows++;
                        if (chr.AssertForHack(chr.DesyncedSoulArrows > 5, "Trying to use no arrow without Soul Arrow " + chr.DesyncedSoulArrows + " times.")) //Allow up to 5 arrows due to buff desync
                        {
                            return false;
                        }
                    }
                    else
                    {
                        chr.DesyncedSoulArrows = 0;
                    }
                    ad.StarID = -1;
                }
                else if (chr.AssertForHack(item == null, "Attempting to use nonexistent item for ranged attack"))
                {
                    return false;
                }
                else
                {
                    ad.StarID = item.ItemID;
                }

                ad.ShootRange = packet.ReadByte();
            }

            ad.Targets = targets;
            ad.Hits = hits;
            ad.SkillID = skillid;

            for (byte i = 0; i < targets; i++)
            {
                var ai = new AttackData.AttackInfo()
                {
                    MobMapId = packet.ReadInt(),
                    HitAction = packet.ReadByte()
                };
                var b = packet.ReadByte();
                ai.ForeAction = (byte)(b & 0x7F);
                ai.FacesLeft = b >> 7 == 1;
                ai.FrameIndex = packet.ReadByte();
                if (!ad.IsMesoExplosion && type != AttackTypes.Summon)
                {
                    ai.CalcDamageStatIndex = packet.ReadByte();
                }
                ai.HitPosition = new Pos(packet);
                ai.PreviousMobPosition = new Pos(packet);
                
                if (ad.IsMesoExplosion)
                {
                    hits = packet.ReadByte();
                }

                ai.Damages = new List<(int, bool)>(hits);
                if (ad.IsMesoExplosion)
                {
                    // Hit delay is basically set to 0, because
                    // they did not send it to the server.
                    // Technically they use the previous hit info,
                    // but that would be also set to zero... wut

                    for (byte j = 0; j < hits; j++)
                    {
                        var dmg = packet.ReadInt();
                        
                        var crit = false;
                        if ((dmg & 0x40000000) > 0)
                        {
                            crit = true;
                            dmg ^= 0x40000000;
                        }
                        
                        ad.TotalDamage += dmg;
                        ai.Damages.Add((dmg, crit));
                    }
                }
                else
                {
                    ai.HitDelay = packet.ReadShort();
                    

                    for (byte j = 0; j < hits; j++)
                    {
                        var dmg = packet.ReadInt();
                        
                        var crit = false;
                        if ((dmg & 0x40000000) > 0)
                        {
                            crit = true;
                            dmg ^= 0x40000000;
                        }
                        
                        ad.TotalDamage += dmg;
                        ai.Damages.Add((dmg, crit));
                    }
                }
                ad.Attacks.Add(ai);
            }

            ad.PlayerPosition = new Pos(packet);
            var playerPacketLastPosDistance = ad.PlayerPosition - chr.Position;
            chr.AssertForHack(playerPacketLastPosDistance > (type == AttackTypes.Ranged ? 600 : 300), $"Player position in attack packet is too far away from player movement info: {playerPacketLastPosDistance}");

            data = ad;


            if (ad.Hits != 0)
            {
                foreach (var ai in ad.Attacks)
                {
                    var mob = chr.Field.GetMob(ai.MobMapId);
                    if (mob == null) continue;

                    var mobId = mob.MobID;

                    // Make sure we update the damage log.
                    chr.UpdateDamageLog(
                        ad.SkillID,
                        ad.SkillLevel,
                        mobId,
                        ai.Damages.Min(tuple => tuple.Damage),
                        ai.Damages.Max(tuple => tuple.Damage)
                    );

                    var playerMobDistance = ad.PlayerPosition - mob.Position;

                    chr.AssertForHack(playerMobDistance > 650, $"Player position is too far from mob {playerMobDistance}");

                }
            }

            return true;
        }

        public static void HandleMeleeAttack(Character chr, Packet packet)
        {
            //Program.MainForm.LogAppend("Handling Melee");
            if (!ParseAttackData(chr, packet, out var ad, AttackTypes.Melee)) return;

            SendMeleeAttack(chr, ad);
            Mob mob;
            bool died;
            var TotalDamage = 0;

            if (ad.SkillID != 0)
            {
                chr.Skills.UseMeleeAttack(ad.SkillID, ad);
            }

            var pickPocketActivated = chr.PrimaryStats.HasBuff(Constants.ChiefBandit.Skills.Pickpocket);
            var pickPocketSLD = chr.Skills.GetSkillLevelData(Constants.ChiefBandit.Skills.Pickpocket, out var pickPocketSkillLevel);
            var pickOk = !ad.IsMesoExplosion && pickPocketActivated && pickPocketSkillLevel > 0 && pickPocketSLD != null;

            var StolenMP = 0;
            var MpStealSkillID = chr.Skills.GetMpStealSkillData(2, out var MpStealProp, out var MpStealPercent, out var MpStealLevel);

            List<Drop> dropsToPop = null;
            short delayForMesoExplosionKill = 0;

            if (ad.IsMesoExplosion)
            {
                var items = packet.ReadByte();
                dropsToPop = new List<Drop>(items);
                for (byte i = 0; i < items; i++)
                {
                    var objectID = packet.ReadInt();
                    packet.Skip(1);

                    if (chr.Field.DropPool.Drops.TryGetValue(objectID, out var drop) &&
                        drop.Reward.Mesos)
                    {
                        dropsToPop.Add(drop);
                    }
                }

                delayForMesoExplosionKill = packet.ReadShort();

            }


            var sld = ad.SkillID == 0 ? null : DataProvider.Skills[ad.SkillID].Levels[ad.SkillLevel];
            var buffExpireTime = sld?.GetExpireTime() ?? MasterThread.CurrentTime;
            bool IsSuccessRoll() => sld != null && (Rand32.Next() % 100) < sld.Property;


            foreach (var ai in ad.Attacks)
            {
                try
                {
                    TotalDamage = 0;
                    mob = chr.Field.GetMob(ai.MobMapId);

                    if (mob == null) continue;

                    var boss = mob.Template.Boss;

                    if (MpStealPercent > 0)
                        StolenMP += mob.OnMobMPSteal(MpStealProp, MpStealPercent / ad.Targets);
                    if (pickOk)
                        mob.GivePickpocketMoney(chr, ai, ad.Hits);

                    foreach (var amount in ai.Damages)
                    {
                        mob.GiveDamage(chr, amount.Damage, AttackTypes.Melee);
                        TotalDamage += amount.Damage;
                    }

                    if (TotalDamage == 0) continue;

                    var maxDamage = 5 + (chr.Level * 6);
                    var mainWeapon = chr.Inventory.GetEquip(Constants.EquipSlots.Slots.Weapon, false);

                    if (ad.SkillID == 0 &&
                        chr.Level < 10 && 
                        TotalDamage > maxDamage &&
                        mainWeapon?.Str < 3)
                    {
                        chr.PermaBan("Melee damage hack (low level), hit " + TotalDamage + " (max: " + maxDamage + ")");
                        return;
                    }

                    died = mob.CheckDead(ai.HitPosition, ad.IsMesoExplosion ? delayForMesoExplosionKill : ai.HitDelay, chr.PrimaryStats.BuffMesoUP.N);

                    if (died && ad.IsMesoExplosion)
                    {
                        delayForMesoExplosionKill = Math.Min((short)1000, delayForMesoExplosionKill);
                    }

                    if (!died && (chr.PrimaryStats.HasBuff(Constants.WhiteKnight.Skills.SwordIceCharge) ||
                                                chr.PrimaryStats.HasBuff(Constants.WhiteKnight.Skills.BwIceCharge)))
                    {
                        mob.Template.elemModifiers.TryGetValue(SkillElement.Ice, out var elemLevel);

                        if ((elemLevel < 1 || elemLevel > 2) && !mob.IsBoss)
                        {
                            var chargeId = chr.PrimaryStats.HasBuff(Constants.WhiteKnight.Skills.SwordIceCharge)
                                ? Constants.WhiteKnight.Skills.SwordIceCharge
                                : Constants.WhiteKnight.Skills.BwIceCharge;
                            
                            var skillLevelData = chr.Skills.GetSkillLevelData(chargeId);

                            var stat = mob.Status.BuffFreeze.Set(chargeId, (short) (Rand32.Next() % 100 < skillLevelData.ZValue ? 1 : 0), MasterThread.CurrentTime + ai.HitDelay + 3000);
                            MobPacket.SendMobStatsTempSet(mob, ai.HitDelay, stat);
                        }
                    }

                    if (ad.SkillID == Constants.DragonKnight.Skills.Sacrifice)
                    {
                        var percentSacrificed = sld.XValue / 100.0;
                        var amountSacrificed = (short)(TotalDamage * percentSacrificed);
                        chr.DamageHP(amountSacrificed);
                    }

                    //TODO sometimes when attacking without using a skill this gets triggered and throws a exception?
                    if (died || ad.SkillID <= 0) continue;

                    if (ad.SkillID != 0)
                    {

                        MobStatus.MobStatValue addedStats = 0;

                        switch (ad.SkillID)
                        {
                            case Constants.Bandit.Skills.Steal:
                                if (!boss && IsSuccessRoll())
                                    mob.GiveReward(chr.ID, 0, DropOwnType.UserOwn, ai.HitPosition, ai.HitDelay, 0, true);
                                break;

                            // Debuffs

                            case Constants.Rogue.Skills.Disorder:

                                addedStats = mob.Status.BuffPhysicalDamage.Set(ad.SkillID, (short)sld.XValue, buffExpireTime);
                                addedStats |= mob.Status.BuffPhysicalDefense.Set(ad.SkillID, (short)sld.XValue, buffExpireTime);
                                break;

                            case Constants.WhiteKnight.Skills.ChargeBlow:
                                if (!boss && IsSuccessRoll())
                                {
                                    // Charged Blow is a bit weird in that it actually applies a freeze stat instead of stun
                                    // this might be an oversight but it has some gameplay implications that it doesn't cancel on hit
                                    // Ice charges always freeze so charged blow doesn't need to do anything here
                                    if (chr.PrimaryStats.BuffCharges.R != Constants.WhiteKnight.Skills.BwIceCharge && chr.PrimaryStats.BuffCharges.R != Constants.WhiteKnight.Skills.SwordIceCharge)
                                    {
                                        addedStats = mob.Status.BuffFreeze.Set(ad.SkillID, 1, buffExpireTime);
                                    }
                                }
                                break;
                            case Constants.Crusader.Skills.AxeComa:
                            case Constants.Crusader.Skills.SwordComa:
                            case Constants.Crusader.Skills.Shout:
                            case Constants.ChiefBandit.Skills.Assaulter:
                                if (!boss && IsSuccessRoll())
                                {
                                    addedStats = mob.Status.BuffStun.Set(ad.SkillID, 1, buffExpireTime);
                                }
                                break;

                            case Constants.Crusader.Skills.AxePanic:
                            case Constants.Crusader.Skills.SwordPanic:
                                if (!boss && IsSuccessRoll())
                                {
                                    addedStats = mob.Status.BuffDarkness.Set(ad.SkillID, 1, buffExpireTime);
                                    //darkness animation doesnt show in this ver?
                                }
                                break;

                        }

                        if (addedStats != 0)
                        {
                            MobPacket.SendMobStatsTempSet(mob, ai.HitDelay, addedStats);
                        }
                    }
                }

                catch (Exception ex)
                {
                    _hackLog.Error("Unable to handle MeleeAttack", ex);
                }
            }

            if (StolenMP > 0)
            {
                chr.ModifyMP((short)StolenMP);
                MapPacket.SendPlayerSkillAnimSelf(chr, MpStealSkillID, MpStealLevel);
                MapPacket.SendPlayerSkillAnim(chr, MpStealSkillID, MpStealLevel);
            }


            if (chr.PrimaryStats.BuffComboAttack.IsSet())
            {
                if (ad.SkillID == Constants.Crusader.Skills.AxeComa ||
                    ad.SkillID == Constants.Crusader.Skills.SwordComa ||
                    ad.SkillID == Constants.Crusader.Skills.AxePanic ||
                    ad.SkillID == Constants.Crusader.Skills.SwordPanic)
                {
                    chr.PrimaryStats.BuffComboAttack.N = 1;
                    BuffPacket.SetTempStats(chr, BuffValueTypes.ComboAttack);
                    MapPacket.SendPlayerBuffed(chr, BuffValueTypes.ComboAttack);
                }
                else if (ad.SkillID != Constants.Crusader.Skills.Shout && TotalDamage > 0)
                {
                    if (chr.PrimaryStats.BuffComboAttack.N <= chr.PrimaryStats.BuffComboAttack.MaxOrbs)
                    {
                        chr.PrimaryStats.BuffComboAttack.N++;
                        BuffPacket.SetTempStats(chr, BuffValueTypes.ComboAttack);
                        MapPacket.SendPlayerBuffed(chr, BuffValueTypes.ComboAttack);
                    }
                }
            }


            switch (ad.SkillID)
            {
                case 0: // Normal wep
                    {
                        if (chr.IsAdmin && chr.Inventory.GetEquippedItemId(Constants.EquipSlots.Slots.Helm, true) == 1002258) // Blue Diamondy Bandana
                        {
                            var mobs = chr.Field.GetMobsInRange(chr.Position, new Pos(-10000, -10000), new Pos(10000, 10000));

                            foreach (var m in mobs)
                            {
                                MobPacket.SendMobDamageOrHeal(m, 1337, false, false);

                                if (m.GiveDamage(chr, 1337, AttackTypes.Melee))
                                {
                                    m.CheckDead();
                                }
                            }
                        }
                        break;
                    }

                case Constants.ChiefBandit.Skills.MesoExplosion:
                    {
                        byte i = 0;
                        foreach (var drop in dropsToPop)
                        {
                            var delay = (short)Math.Min(1000, delayForMesoExplosionKill + (100 * (i % 5)));
                            chr.Field.DropPool.RemoveDrop(drop, DropLeave.Explode, delay);
                            i++;
                        }
                        break;
                    }

                case Constants.WhiteKnight.Skills.ChargeBlow:
                    if (IsSuccessRoll())
                    {
                        // RIP. It cancels your charge
                        var removedBuffs = chr.PrimaryStats.RemoveByReference(chr.PrimaryStats.BuffCharges.R);
                        BuffPacket.SetTempStats(chr, removedBuffs);
                        MapPacket.SendPlayerBuffed(chr, removedBuffs);
                    }
                    break;

                case Constants.WhiteKnight.Skills.BwFireCharge:
                case Constants.WhiteKnight.Skills.BwIceCharge:
                case Constants.WhiteKnight.Skills.BwLitCharge:
                case Constants.WhiteKnight.Skills.SwordFireCharge:
                case Constants.WhiteKnight.Skills.SwordIceCharge:
                case Constants.WhiteKnight.Skills.SwordLitCharge:
                    {
                        var buff = chr.PrimaryStats.BuffCharges.Set(
                            ad.SkillID,
                            sld.XValue,
                            sld.GetExpireTime()
                        );
                        BuffPacket.SetTempStats(chr, buff);
                        MapPacket.SendPlayerBuffed(chr, buff);
                        break;
                    }

                case Constants.DragonKnight.Skills.DragonRoar:
                    {
                        // Apply stun
                        var buff = chr.PrimaryStats.BuffStun.Set(
                            ad.SkillID,
                            1,
                            BuffStat.GetTimeForBuff(1000 * sld.YValue)
                        );
                        BuffPacket.SetTempStats(chr, buff);
                        MapPacket.SendPlayerBuffed(chr, buff);
                        break;
                    }
            }

        }

        public static void HandleRangedAttack(Character chr, Packet packet)
        {
            //Program.MainForm.LogAppend("Handling Ranged");
            if (!ParseAttackData(chr, packet, out var ad, AttackTypes.Ranged)) return;

            int TotalDamage;
            bool died;
            Mob mob;

            SendRangedAttack(chr, ad);

            if (ad.SkillID != 0 || ad.StarID != 0)
            {
                chr.Skills.UseRangedAttack(ad.SkillID, ad.StarItemSlot);
            }

            foreach (var ai in ad.Attacks)
            {
                try
                {
                    TotalDamage = 0;
                    mob = chr.Field.GetMob(ai.MobMapId);
                    if (mob == null) continue;
                    
                    if (mob.HP > 0 && ai.Damages.Sum(tuple => tuple.Damage) > 0 && (ad.Option & 4) != 0 && mob.IsMortalBlowEffective(chr))
                    {
                        mob.GiveDamage(chr, mob.HP, AttackTypes.Ranged);
                        mob.CheckDead(ai.HitPosition, ai.HitDelay, chr.PrimaryStats.BuffMesoUP.N);
                        continue;
                    }
                        
                    foreach (var amount in ai.Damages)
                    {
                        mob.GiveDamage(chr, amount.Damage, AttackTypes.Ranged);
                        TotalDamage += amount.Damage;
                    }

                    if (TotalDamage == 0) continue;
                    
                    died = mob.CheckDead(ai.HitPosition, ai.HitDelay, chr.PrimaryStats.BuffMesoUP.N);
                            
                    var sld = chr.Skills.GetSkillLevelData(ad.SkillID, out var skillLevel);
                    if (sld != null && skillLevel == ad.SkillLevel)
                    {
                        var expireTime = sld.GetExpireTime();

                        if (!died && !mob.IsBoss)
                        {
                            switch (ad.SkillID)
                            {
                                case Constants.Hunter.Skills.ArrowBomb:
                                {
                                    var chance = Rand32.Next() % 100;

                                    if (chance < sld.Property)
                                    {
                                        var stat = mob.Status.BuffStun.Set(ad.SkillID, 1, expireTime);
                                        MobPacket.SendMobStatsTempSet(mob, ai.HitDelay, stat);
                                    }

                                    break;
                                }
                                case Constants.Sniper.Skills.Iceshot:
                                    if ((sld.ElementFlags == SkillElement.Ice || ad.SkillID == Constants.ILMage.Skills.ElementComposition) && 
                                        // Check if mob is ice resistant
                                        (mob.Template.elemModifiers.TryGetValue(SkillElement.Ice, out var iceDamageValue) == false || (iceDamageValue < 1 || iceDamageValue > 2)))
                                    {
                                        var stat = mob.Status.BuffFreeze.Set(ad.SkillID, 1, MasterThread.CurrentTime + ai.HitDelay + 3000);
                                        MobPacket.SendMobStatsTempSet(mob, ai.HitDelay, stat);
                                    }
                                    break;
                            }
                        }

                        if (ad.SkillID == Constants.Assassin.Skills.Drain)
                        {
                            var hp = Math.Min(TotalDamage * sld.XValue * 0.01, mob.MaxHP);
                            hp = Math.Min(hp, chr.PrimaryStats.MaxHP / 2);
                            chr.ModifyHP((short)(hp));
                        }
                    }
                }

                catch (Exception ex)
                {
                    _hackLog.Error("Unable to handle RangedAttack", ex);
                }
            }
        }

        public static void HandleMagicAttack(Character chr, Packet packet)
        {
            //Program.MainForm.LogAppend("Handling Magic");
            if (!ParseAttackData(chr, packet, out var ad, AttackTypes.Magic)) return;

            int TotalDamage;
            bool died;
            Mob mob;

            SendMagicAttack(chr, ad);


            if (ad.SkillID != 0)
            {
                if (!chr.Skills.UseMeleeAttack(ad.SkillID, ad))
                {
                    Program.MainForm.LogAppend("User tried to use a magic attack, but Melee Attack code failed?");
                    return;
                }
            }

            var StolenMP = 0;
            var MpStealSkillID = chr.Skills.GetMpStealSkillData(2, out var MpStealProp, out var MpStealPercent, out var MpStealLevel);

            var sld = DataProvider.Skills[ad.SkillID].Levels[ad.SkillLevel];

            foreach (var ai in ad.Attacks)
            {
                try
                {
                    TotalDamage = 0;
                    mob = chr.Field.GetMob(ai.MobMapId);

                    if (mob == null) continue;

                    foreach (var amount in ai.Damages)
                    {
                        mob.GiveDamage(chr, amount.Damage, AttackTypes.Magic);
                        TotalDamage += amount.Damage;
                    }

                    if (MpStealPercent > 0)
                        StolenMP += mob.OnMobMPSteal(MpStealProp, MpStealPercent / ad.Targets);

                    if (TotalDamage == 0) continue;

                    died = mob.CheckDead(ai.HitPosition, ai.HitDelay, chr.PrimaryStats.BuffMesoUP.N);

                    if (!died)
                    {
                        var expireTime = sld.GetExpireTime();

                        //TODO refactor element code when we get the proper element loading with calcdamage branch
                        if ((sld.ElementFlags == SkillElement.Ice || ad.SkillID == Constants.ILMage.Skills.ElementComposition) && 
                            !mob.IsBoss &&
                            // Check if mob is ice resistant
                            (mob.Template.elemModifiers.TryGetValue(SkillElement.Ice, out var iceDamageValue) == false || (iceDamageValue < 1 || iceDamageValue > 2)))
                        {
                            var stat = mob.Status.BuffFreeze.Set(ad.SkillID, 1, MasterThread.CurrentTime + ai.HitDelay + 3000);
                            MobPacket.SendMobStatsTempSet(mob, ai.HitDelay, stat);
                        }

                        if (
                                ad.SkillID == Constants.FPMage.Skills.ElementComposition ||
                                ad.SkillID == Constants.FPWizard.Skills.PoisonBreath
                            )
                        {
                            if (Rand32.Next() % 100 < sld.Property)
                            {
                                mob.DoPoison(chr.ID, ad.SkillLevel, expireTime, ad.SkillID, sld.MagicAttack, ai.HitDelay);
                            }
                        }
                    }

                    if (ad.SkillID == Constants.Cleric.Skills.Heal && mob.Template.Undead == false)
                    {
                        chr.PermaBan("Heal damaging regular mobs exploit");
                        return;
                    }
                }

                catch (Exception ex)
                {
                    _hackLog.Error("Unable to handle MagicAttack", ex);
                }
            }

            if (StolenMP > 0)
            {
                chr.ModifyMP((short)StolenMP);
                MapPacket.SendPlayerSkillAnimSelf(chr, MpStealSkillID, MpStealLevel);
                MapPacket.SendPlayerSkillAnim(chr, MpStealSkillID, MpStealLevel);
            }
        }


        public static void HandleSummonAttack(Character chr, Packet packet)
        {
            if (!ParseAttackData(chr, packet, out var ad, AttackTypes.Summon)) return;

            var summonId = ad.SummonID;

            if (!chr.Summons.GetSummon(summonId, out var summon)) return;

            SendSummonAttack(chr, summon, ad);

            var totalDamage = 0;
            foreach (var ai in ad.Attacks)
            {
                try
                {
                    var mob = chr.Field.GetMob(ai.MobMapId);
                    if (mob == null) continue;

                    foreach (var amount in ai.Damages)
                    {
                        mob.GiveDamage(chr, amount.Damage, AttackTypes.Summon);
                        totalDamage += amount.Damage;
                    }

                    var dead = mob.CheckDead(ai.HitPosition, ai.HitDelay, chr.PrimaryStats.BuffMesoUP.N);

                    if (!dead && (summon.SkillId == Constants.Ranger.Skills.SilverHawk || summon.SkillId == Constants.Sniper.Skills.GoldenEagle))
                    {
                        var sld = CharacterSkills.GetSkillLevelData(summon.SkillId, summon.SkillLevel);
                        if (!mob.IsBoss && totalDamage > 0 && Rand32.Next() % 100 < sld.Property)
                        {
                            var stunTime = ai.HitDelay + 4000;
                            var expireTime = MasterThread.CurrentTime + stunTime;
                            var stat = mob.Status.BuffStun.Set(summon.SkillId, -1, expireTime);
                            MobPacket.SendMobStatsTempSet(mob, ai.HitDelay, stat);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Program.MainForm.LogAppend(ex.ToString());
                }
            }
        }

        public static void SendSummonAttack(Character chr, Summon summon, AttackData ad)
        {
            var pw = new Packet(ServerMessages.SPAWN_ATTACK);
            pw.WriteInt(chr.ID);
            pw.WriteInt(summon.SkillId);
            pw.WriteByte(ad.AttackType);
            pw.WriteByte(ad.Targets);
            foreach (var kvp in ad.Attacks)
            {
                pw.WriteInt(kvp.MobMapId);
                pw.WriteByte(kvp.HitAction);
                foreach (var dmg in kvp.Damages)
                {
                    pw.WriteInt(dmg.Damage);
                }
            }
            chr.Field.SendPacket(pw, chr);
        }

        private static int? GetFixedDamage(Character chr)
        {
            int? fixedDamage = null;

            /*switch (chr.Name)
            {
                case "Exile": fixedDamage = 2147483647; break;
                case "Diamondo25": fixedDamage = 25252525; break;
                case "wackyracer": fixedDamage = 13371337; break;
            }*/

            return fixedDamage;
        }

        public static void SendMeleeAttack(Character chr, AttackData data)
        {
            var fixedDamage = GetFixedDamage(chr);
            var tbyte = (byte)((data.Targets * 0x10) + data.Hits);

            var pw = new Packet(ServerMessages.CLOSE_RANGE_ATTACK);
            pw.WriteInt(chr.ID);
            pw.WriteByte(tbyte);
            pw.WriteByte(data.SkillLevel);

            if (data.SkillLevel != 0)
            {
                pw.WriteInt(data.SkillID);
            }

            pw.WriteByte((byte)(data.Action | (data.FacesLeft ? 1 << 7 : 0)));
            pw.WriteByte(data.AttackType);

            pw.WriteByte(chr.Skills.GetDisplayedMastery());

            pw.WriteInt(data.StarID);

            foreach (var ai in data.Attacks)
            {
                pw.WriteInt(ai.MobMapId);
                pw.WriteByte(ai.HitAction);

                if (data.IsMesoExplosion)
                {
                    pw.WriteByte((byte)ai.Damages.Count);
                }

                foreach (var dmg in ai.Damages)
                {
                    // add crit bit
                    var damage = dmg.Damage + (dmg.IsCrit ? 0x40000000 : 0);
                    
                    pw.WriteInt(fixedDamage.GetValueOrDefault(damage));
                }
            }

            chr.Field.SendPacket(chr, pw, chr);
        }

        public static void SendRangedAttack(Character chr, AttackData data)
        {
            var fixedDamage = GetFixedDamage(chr);

            var tbyte = (byte)((data.Targets * 0x10) + data.Hits);

            var pw = new Packet(ServerMessages.RANGED_ATTACK);
            pw.WriteInt(chr.ID);
            pw.WriteByte(tbyte);
            pw.WriteByte(data.SkillLevel);

            if (data.SkillLevel != 0)
            {
                pw.WriteInt(data.SkillID);
            }

            pw.WriteByte((byte)(data.Action | (data.FacesLeft ? 1 << 7 : 0)));
            pw.WriteByte(data.AttackType);

            pw.WriteByte(chr.Skills.GetDisplayedMastery());
            pw.WriteInt(data.StarID);

            foreach (var ai in data.Attacks)
            {
                pw.WriteInt(ai.MobMapId);
                pw.WriteByte(ai.HitAction);

                foreach (var dmg in ai.Damages)
                {
                    // add crit bit
                    var damage = dmg.Damage + (dmg.IsCrit ? 0x40000000 : 0);
                    
                    pw.WriteInt(fixedDamage.GetValueOrDefault(damage));
                }
            }

            chr.Field.SendPacket(chr, pw, chr);
        }

        public static void SendMagicAttack(Character chr, AttackData data)
        {
            var fixedDamage = GetFixedDamage(chr);
            var tbyte = (byte)((data.Targets * 0x10) + data.Hits);

            var pw = new Packet(ServerMessages.MAGIC_ATTACK);
            pw.WriteInt(chr.ID);
            pw.WriteByte(tbyte);
            pw.WriteByte(data.SkillLevel);

            if (data.SkillLevel != 0)
            {
                pw.WriteInt(data.SkillID);
            }

            pw.WriteByte((byte)(data.Action | (data.FacesLeft ? 1 << 7 : 0)));
            pw.WriteByte(data.AttackType);

            pw.WriteByte(chr.Skills.GetDisplayedMastery());

            pw.WriteInt(data.StarID);

            foreach (var ai in data.Attacks)
            {
                pw.WriteInt(ai.MobMapId);
                pw.WriteByte(ai.HitAction);

                foreach (var dmg in ai.Damages)
                {
                    pw.WriteInt(fixedDamage.GetValueOrDefault(dmg.Damage));
                }
            }

            chr.Field.SendPacket(chr, pw, chr);
        }

        private static ILog _hackLog = LogManager.GetLogger("HackLog");

        public struct DamageHackLog
        {
            public int skillId { get; set; }
            public byte skillLevel { get; set; }
            public int damage { get; set; }
            public int maxDamage { get; set; }
            public float percentage { get; set; }
            public short posX { get; set; }
            public short posY { get; set; }
        }

        private static bool ReportDamagehack(Character chr, AttackData ad, int TotalDamage, int MaxDamage)
        {
            // Broken. don't care.
            return false;


            var percentage = (TotalDamage * 100.0f / MaxDamage);
            _hackLog.Info(new DamageHackLog
            {
                damage = TotalDamage,
                maxDamage = MaxDamage,
                skillId = ad.SkillID,
                skillLevel = ad.SkillLevel,
                percentage = percentage,
                posX = chr.Position.X,
                posY = chr.Position.Y,
            });

            // Ignore broken damage
            if (percentage < 1.0 || TotalDamage < 100 || MaxDamage == 0) return false;

            if (chr.HacklogMuted >= MasterThread.CurrentDate) return false;

            //var msgType = DamageFormula.GetGMNoticeType(TotalDamage, MaxDamage);
            //MessagePacket.SendNoticeGMs("Name: '" + chr.Name + "', dmg: " + TotalDamage + " (" + percentage.ToString("0.00") + "% of max). " + "Skill: " + ad.SkillID + ", lvl " + ad.SkillLevel + ", damage " + TotalDamage + " > " + MaxDamage, msgType);

            if (percentage > 400.0)
            {
                chr.PermaBan($"Automatically banned for damage hacks ({percentage:0.00}%) skill {ad.SkillID}", BanReasons.Hack);
                return true;
            }

            return false;
        }
    }
}