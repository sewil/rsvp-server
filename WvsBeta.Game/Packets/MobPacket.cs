using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using log4net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.Packets;
using WvsBeta.SharedDataProvider.Templates;

namespace WvsBeta.Game
{
    public static class MobPacket
    {
        private static ILog _log = LogManager.GetLogger(typeof(MobPacket));

        public static void HandleMobControl(Character victim, Packet packet)
        {
            var currentTime = MasterThread.CurrentTime;

            var mob = victim.Field.GetMob(packet.ReadInt());
            if (mob == null) return;

            // If its dead, don't care about control packets.
            if (mob.HP == 0) return;

            var moveID = packet.ReadShort();
            var x = packet.ReadByte();
            var bNextAttackPossible = (x & 0x0F) != 0;
            var action = packet.ReadSByte();

            var actualAction = action < 0 ? -1 : (action >> 1);

            var dwData = packet.ReadUInt();


            var movePath = new MovePath();
            movePath.DecodeFromPacket(packet, MovePath.MovementSource.Mob);


            if (mob.Controller != victim &&
                (!bNextAttackPossible || mob.NextAttackPossible || !mob.Field.FindNewController(mob, victim, true)))
            {
                SendMobRequestEndControl(victim, mob.SpawnID);
                return;
            }


            victim.TryTraceMovement(movePath);

            if (mob.Controller != null && victim.ID != mob.Controller.ID)
            {
                // This looks like already covered by the check above, mob.Controller != victim
                _log.Error("This should not happen? Current mob controller is not the one sending a packet.");
                return;
            }

            var lastMoveMillis = packet.PacketCreationTime - mob.LastMove;
            var justStartedControlling = (currentTime - mob.LastControllerAssignTime) < 2000;

            PacketHelper.ValidateMovePath(mob, movePath, packet.PacketCreationTime);


            // Skill related?
            if (actualAction >= 21 && actualAction <= 25)
            {
                var attackDelay = (short)(dwData >> 16);
                var level = (byte)(dwData >> 8);
                var skillId = (byte)(dwData);

                if (mob.DoSkill(skillId, level, attackDelay) == false)
                {
                    // invalid
                    _log.Warn($"mob.DoSkill failed for mob {mob.MobID}. skill {skillId} lvl {level} attackDelay {attackDelay}, packet {packet}");
                    return;
                }
            }
            else if (actualAction > 12 && actualAction < 20)
            {
                // regular attack?
                var attackIdx = (byte)(actualAction - 12);
                if (!mob.Template.Attacks.TryGetValue(attackIdx, out var attack))
                {
                    _log.Warn($"Unknown attack done by client for mob {mob.MobID}. attackIdx {attackIdx}, packet {packet}");
                    return;
                }

                mob.MP = Math.Max(0, mob.MP - attack.MPConsume);
                mob.LastAttack = currentTime;
            }


            if ((currentTime - mob.LastAttack) > 5000)
            {
                // The mob hasnt attacked for a bit.
                // Reassign controller!

                var currentDate = MasterThread.CurrentDate;
                var lastHitCharacter = mob.DamageLog.Log.FirstOrDefault(mobDamageInfo =>
                {
                    // If we already have a controller, skip that one.
                    if (mobDamageInfo.CharacterID == mob.Controller?.ID) return false;
                    if (mob.Field.GetPlayer(mobDamageInfo.CharacterID) == null) return false;

                    return (currentDate - mobDamageInfo.Time).TotalSeconds <= 5.0;
                });

                if (lastHitCharacter != null)
                {
                    mob.SetController(mob.Field.GetPlayer(lastHitCharacter.CharacterID), true, false);
                    return;
                }
            }


            mob.NextAttackPossible = bNextAttackPossible;

            // Prepare next skill
            byte forceControllerSkillLevel = 0;
            if (mob.NextAttackPossible == false ||
                mob.SkillCommand != 0 ||
                (mob.HasAnyStatus && mob.Status.BuffSealSkill.IsSet()) ||
                mob.Template.Skills == null ||
                mob.Template.Skills.Count == 0 ||
                (currentTime - mob.LastSkillUse) < 3000)
            {
                // No skill
            }
            else
            {
                var availableSkills = mob.Template.Skills.Where(skill =>
                {
                    var skillAsEnum = (Constants.MobSkills.Skills)skill.SkillID;

                    if (!DataProvider.MobSkills.TryGetValue(skill.SkillID, out var msdLevels) ||
                        !msdLevels.TryGetValue(skill.Level, out var msd)) return false;

                    // Handle HP restriction
                    if (msd.HPLimit > 0 && (mob.HP / (double)mob.MaxHP * 100.0) > msd.HPLimit)
                        return false;

                    // Skip if we already used a skill and it was not yet cooled down
                    if (mob.SkillsInUse.TryGetValue(msd.SkillID, out var lastUse) &&
                        (lastUse + (msd.Cooldown * 1000)) > currentTime)
                        return false;

                    // Skill prechecks
                    switch (skillAsEnum)
                    {
                        case Constants.MobSkills.Skills.Summon:
                            // Do not reach the summon limit
                            if (mob.SummonCount + msd.Summons.Count > msd.SummonLimit) 
                                return false;
                            break;

                        case Constants.MobSkills.Skills.MagicImmunity:
                        case Constants.MobSkills.Skills.PhysicalImmunity:
                            if (mob.Status.BuffMagicImmune.IsSet(currentTime) ||
                                mob.Status.BuffPhysicalImmune.IsSet(currentTime))
                            {
                                // We cannot have physical and magic immune active at the same time
                                // Wait until one has expired
                                return false;
                            }
                            break;
                    }



                    // Can we boost stats?
                    if (mob.HasAnyStatus)
                    {
                        var currentX = 0;
                        var maxX = Math.Abs(100 - msd.X);

                        switch (skillAsEnum)
                        {
                            case Constants.MobSkills.Skills.WeaponAttackUp:
                            case Constants.MobSkills.Skills.WeaponAttackUpAoe:
                                currentX = mob.Status.BuffPowerUp.N;
                                break;
                            case Constants.MobSkills.Skills.MagicAttackUp:
                            case Constants.MobSkills.Skills.MagicAttackUpAoe:
                                currentX = mob.Status.BuffMagicUp.N;
                                break;
                            case Constants.MobSkills.Skills.WeaponDefenseUp:
                            case Constants.MobSkills.Skills.WeaponDefenseUpAoe:
                                currentX = mob.Status.BuffPowerGuardUp.N;
                                break;
                            case Constants.MobSkills.Skills.MagicDefenseUp:
                            case Constants.MobSkills.Skills.MagicDefenseUpAoe:
                                currentX = mob.Status.BuffMagicGuardUp.N;
                                break;
                        }

                        if (currentX == 0) return true;

                        // Check if we already casted to max X stat
                        if (Math.Abs(100 - currentX) >= maxX) return false;

                    }
                    
                    return true;
                }).ToArray();

                if (availableSkills.Length > 0)
                {
                    var randomSkill = availableSkills[Rand32.Next() % availableSkills.Length];
                    mob.SkillCommand = randomSkill.SkillID;
                    forceControllerSkillLevel = randomSkill.Level;
                }
            }

            var forceControllerSkillID = mob.SkillCommand;
            // Fix crash (zero level skill)
            if (forceControllerSkillLevel == 0)
                forceControllerSkillID = 0;

            SendMobControlResponse(victim, mob.SpawnID, moveID, bNextAttackPossible, (short)mob.MP, forceControllerSkillID, forceControllerSkillLevel);

            SendMobControlMove(victim, mob, bNextAttackPossible, (byte)action, dwData, movePath);

            // Good luck on getting less.
            if (lastMoveMillis < 500 && !justStartedControlling && !victim.IsAFK)
            {
                if (victim.AssertForHack(mob.HackReportCounter++ > 5,
                    $"Mob movement speed too high! {lastMoveMillis}ms since last movement."))
                {
                    mob.HackReportCounter = 0;
                }
            }
        }

        public static void HandleDistanceFromBoss(Character chr, Packet packet)
        {
            int mapmobid = packet.ReadInt();
            int distance = packet.ReadInt();
            // Do something with it :P
        }

        public static void HandleApplyControl(Character chr, Packet packet)
        {
            // Also know as 'Snatching'
            var mob = chr.Field.GetMob(packet.ReadInt());
            if (mob == null) return;
            if (mob.HP == 0) return;

            // Only certain mobs can do this.
            // Nependeaths seem to be one of them. Map: disposed flower garden

            if (!mob.Template.FirstAttack)
            {
                _log.Error($"User tried to snatch mob, even though mob doesn't have FirstAttack. Mobid: {mob.MobID}");
                return;
            }

            var priority = packet.ReadInt();

            // if (map == MonsterCarnival && mob->mobgen->teamformc == user->teamformc) priority = 1000;

            Trace.WriteLine($"Getting HandleApplyControl with prio {priority} for mob {mob.MobID} on map {chr.MapID}");


            if (chr.ID == mob.Controller?.ID)
            {
                // Already controlled
                return;
            }

            if (!mob.NextAttackPossible)
            {
                Trace.WriteLine("Trying to snatch while NextAttack is not possible");
                return;
            }

            // BMS would use the provided priority field. We are using our own based on distance.

            if (mob.Controller != null)
            {
                var controllerDistance = mob.Controller.Position - mob.Position;
                var characterDistance = chr.Position - mob.Position;
                if (controllerDistance <= characterDistance)
                {
                    // Controller is closer, ignore request.
                    return;
                }
            }

            mob.Field.FindNewController(mob, chr);
        }

        private static void MobData(Packet pw, Mob mob)
        {
            pw.WriteInt(mob.SpawnID);
            pw.WriteInt(mob.MobID);
            pw.WriteShort(mob.Position.X);
            pw.WriteShort(mob.Position.Y);
            pw.WriteByte(mob.MoveAction);
            pw.WriteShort(mob.Foothold);
            pw.WriteShort(mob.OriginalFoothold);

            pw.WriteSByte(mob.SummonType);
            if (mob.SummonType == MobAppear.Revived || mob.SummonType >= 0)
                pw.WriteInt(mob.SummonOption);

            if (mob.HasAnyStatus)
                mob.Status.Encode(pw, MobStatus.MobStatValue.ALL);
            else
                pw.WriteInt(0);
        }

        public static void SendMobSpawn(Character victim, Mob mob)
        {
            var pw = new Packet(ServerMessages.MOB_ENTER_FIELD);
            MobData(pw, mob);

            victim.SendPacket(pw);
        }

        public static void SendMobSpawn(Mob mob)
        {
            var pw = new Packet(ServerMessages.MOB_ENTER_FIELD);
            MobData(pw, mob);

            mob.Field.SendPacket(mob, pw);
        }

        public static void SendMobDeath(Mob mob, MobLeaveField how)
        {
            var pw = new Packet(ServerMessages.MOB_LEAVE_FIELD);
            pw.WriteInt(mob.SpawnID);
            pw.WriteByte(how);
            mob.Field.SendPacket(mob, pw);
        }

        public static void SendMobRequestControl(Character currentController, Mob mob, bool chasing)
        {
            var pw = new Packet(ServerMessages.MOB_CHANGE_CONTROLLER);
            pw.WriteByte((byte)(chasing ? 2 : 1));
            MobData(pw, mob);

            currentController.SendPacket(pw);
        }

        public static void SendMobRequestEndControl(Character currentController, int spawnId)
        {
            var pw = new Packet(ServerMessages.MOB_CHANGE_CONTROLLER);
            pw.WriteByte(0);
            pw.WriteInt(spawnId);
            currentController.SendPacket(pw);
        }

        public static void SendMobControlResponse(Character victim, int mobid, short moveid, bool bNextAttackPossible, short MP, byte skillCommand, byte level)
        {
            var pw = new Packet(ServerMessages.MOB_MOVE_RESPONSE);
            pw.WriteInt(mobid);
            pw.WriteShort(moveid);
            pw.WriteBool(bNextAttackPossible);
            pw.WriteShort(MP);
            pw.WriteByte(skillCommand);
            pw.WriteByte(level);

            victim.SendPacket(pw);
        }

        public static void SendMobControlMove(Character victim, Mob mob, bool bNextAttackPossible, byte action, uint dwData, MovePath movePath, bool everyone = false)
        {
            var pw = new Packet(ServerMessages.MOB_MOVE);
            pw.WriteInt(mob.SpawnID);
            pw.WriteBool(bNextAttackPossible);
            pw.WriteByte(action);
            pw.WriteUInt(dwData); // Unknown

            movePath.EncodeToPacket(pw);

            victim.Field.SendPacket(mob, pw, everyone ? null : victim);
        }

        public static void SendMobDamageOrHeal(Mob mob, int amount, bool isHeal, bool web)
        {
            var pw = new Packet(ServerMessages.MOB_DAMAGED);
            pw.WriteInt(mob.SpawnID);
            pw.WriteBool(!web); // 0 = caused by web, 1 = caused by obstacle, heal, skill or more?
            pw.WriteInt(isHeal ? -amount : amount);
            // if damagedByMob mob, write 2 ints (HP, Max HP). Not sure if this version contains this

            pw.WriteLong(0);
            pw.WriteLong(0);
            mob.Field.SendPacket(mob, pw);
        }

        public static void SendMobAffected(Mob mob, int skillID, short delay)
        {
            var pw = new Packet(ServerMessages.MOB_AFFECTED);
            pw.WriteInt(mob.SpawnID);
            pw.WriteInt(skillID);
            pw.WriteShort(delay);
            mob.Field.SendPacket(mob, pw);
        }

        public static void SendMobStatsTempSet(Mob pMob, short pDelay, MobStatus.MobStatValue pSpecificFlag = MobStatus.MobStatValue.ALL)
        {
            var pw = new Packet(ServerMessages.MOB_STAT_SET);
            pw.WriteInt(pMob.SpawnID);
            if (pMob.HasAnyStatus)
                pMob.Status.Encode(pw, pSpecificFlag);
            else
                pw.WriteInt(0);
            pw.WriteShort(pDelay);

            pMob.Field.SendPacket(pMob, pw);
        }

        public static void SendMobStatsTempReset(Mob pMob, MobStatus.MobStatValue pFlags)
        {
            if (pFlags == 0) return;
            var pw = new Packet(ServerMessages.MOB_STAT_RESET);
            pw.WriteInt(pMob.SpawnID);

            pw.WriteUInt((uint)pFlags);

            pMob.Field.SendPacket(pMob, pw);
        }

        public static void SendSpecialEffectBySkill(Mob mob, int skillID)
        {
            var pw = new Packet(ServerMessages.MOB_EFFECT_BY_SKILL);
            pw.WriteInt(mob.SpawnID);

            pw.WriteInt(skillID);

            mob.Field.SendPacket(mob, pw);

        }
    }
}