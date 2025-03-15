using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using log4net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.GameObjects;
using WvsBeta.SharedDataProvider.Templates;

namespace WvsBeta.Game
{
    public enum MobAppear
    {
        Normal = -1,
        Regen = -2,
        Revived = -3,
        Suspended = -4,
        Delay = -5,
        Effect = 0,
    }

    public enum MobLeaveField
    {
        Etc = 0,
        RemainHP = 1,
        SelfDestruct = 2,
    }

    public enum MobAct
    {
        Move = 0,
        Stand = 1,
        Jump = 2,
        Fly = 3,
        Regen = 4,
    }

    public class Mob : MovableLife, IFieldObj
    {
        private static ILog _log = LogManager.GetLogger("MobLog");
        public int MobID { get; set; }
        public Map Field { get; set; }
        public int SpawnID { get; set; }
        public readonly short OriginalFoothold;
        public MobGenItem MobGenItem { get; set; } = null;

        public MobAppear SummonType { get; set; }
        public int SummonOption { get; set; }

        public int EXP => Template.EXP;
        public int HP { get; set; }
        public int MaxHP => Template.MaxHP;
        public int MP { get; set; }
        public int MaxMP => Template.MaxMP;
        public int Level => Template.Level;
        public bool IsBoss => Template.Boss;
        public float AllowedSpeed => (100 + Template.Speed) / 100.0f;

        private MobStatus _status;
        public bool HasAnyStatus => _status != null;
        public MobStatus Status => _status ??= new MobStatus();
        public Character Controller { get; set; } = null;
        public bool IsControlled => Controller != null;

        public MobDamageLog DamageLog { get; set; }

        public bool DeadAlreadyHandled { get; set; }
        public long LastSkillUse { get; set; }
        public Dictionary<byte, long> SkillsInUse { get; } = new Dictionary<byte, long>();

        public bool NextAttackPossible { get; set; } = false;
        public long LastAttack { get; set; } = 0;
        public byte SkillCommand { get; set; } = 0;
        public int SummonCount { get; set; } = 0;
        public int MobType { get; set; }
        public bool SelfDestructed { get; set; }

        public bool AlreadyStealed;
        public int ItemID_Stolen = -1;

        public int LastHitCharacterID { get; private set; }
        public byte HackReportCounter { get; set; } = 0;

        public MobData Template { get; }
        public Pos OriginalPosition;

        public MoveAbility MoveAbility { get; }

        public long CreateTime { get; }

        internal Mob(int spawnId, Map field, int mobid, Pos position, short foothold, bool facesLeft = false) : base(foothold, position, 0)
        {
            Template = DataProvider.Mobs[mobid];
            MoveAbility = Template.MoveAbility;

            OriginalPosition = new Pos(position);
            MobID = mobid;
            Field = field;
            SpawnID = spawnId;
            HP = MaxHP;
            MP = MaxMP;
            DamageLog = new MobDamageLog(field, MaxHP);
            CreateTime = MasterThread.CurrentTime;
            OriginalFoothold = MoveAbility == MoveAbility.Fly ? (short)0 : foothold;

            var moveAction = MoveAbility switch
            {
                MoveAbility.Fly => MoveActionType.Fly,
                MoveAbility.Jump => MoveActionType.Jump,
                MoveAbility.Stop => MoveActionType.Stand,
                MoveAbility.Walk => MoveActionType.Move,
                _ => default
            };

            SetMovePosition(OriginalPosition, moveAction, facesLeft, foothold);
        }

        public void SetMovePosition(Pos pos, MoveActionType moveAction, bool left, short SN)
        {
            Position = new Pos(pos);
            SetMoveActionType(moveAction, left);
            Foothold = MoveAbility == MoveAbility.Fly ? (short)0 : SN;
        }

        public bool IsShownTo(IFieldObj Object) => true;

        long lastHeal = MasterThread.CurrentTime;
        long lastPoisonDMG = MasterThread.CurrentTime;
        long lastStatusUpdate = MasterThread.CurrentTime;
        public long LastControllerAssignTime { get; set; }
        public int LastPoisonCharId { get; set; }

        public void UpdateDeads(long pNow)
        {
            if (DeadAlreadyHandled) return;

            // Stat updates

            if ((pNow - lastStatusUpdate) >= 1000)
            {
                lastStatusUpdate = pNow;
                if (HasAnyStatus)
                {
                    var endFlag = Status.Update(pNow);
                    if (endFlag > 0)
                    {
                        MobPacket.SendMobStatsTempReset(this, endFlag);
                    }
                }
            }

            // Poison damage

            if (HasAnyStatus &&
                Status.BuffPoison.IsSet(pNow) &&
                (pNow - lastPoisonDMG) >= 1000)
            {
                Trace.WriteLine($"Applying poison effect with latency {pNow - lastPoisonDMG}");

                var damage = (double)pNow - (double)lastPoisonDMG;
                damage /= 1000.0d; // Amount of ticks
                damage *= Status.BuffPoison.N;

                lastPoisonDMG = pNow;
                GiveDamage(null, (int)damage, AttackPacket.AttackTypes.Magic, true);
            }

            // HP/MP healing

            if (
                (
                    Template.HPRecoverAmount > 0 ||
                    Template.MPRecoverAmount > 0
                ) &&
                (pNow - lastHeal) >= 8000)
            {
                if (Template.HPRecoverAmount > 0 && HP < MaxHP)
                {
                    SetMobHP(HP + Template.HPRecoverAmount);
                }

                if (Template.MPRecoverAmount > 0 && MP < MaxMP)
                {
                    MP += Template.MPRecoverAmount;
                    if (MP > MaxMP) MP = MaxMP;
                }
                lastHeal = pNow;
            }

            if (IsTimeToRemove(pNow, MobID == 9999999))
            {
                ForceDead();
            }
        }

        public bool CheckSelfDestruct()
        {
            if (HP > Template.SelfDestructionHP) return false;
            DoSelfDestruct();
            return true;
        }

        public void DoSelfDestruct()
        {
            DamageLog.VainDamage += HP;
            HP = 0;
            SelfDestructed = true;
        }
        
        public bool IsTimeToSelfDestruct()
        {
            // Check template ActionType & 4 and time since creation > removeAfterSeconds
            // We don't use this (yet)
            return false;
        }

        public bool IsTimeToRemove(long tCur, bool fixedMob)
        {
            // TODO: Integrate this in update loop.
            // Needs some more logic in there for updating quests and such?

            var timeSinceCreation = tCur - CreateTime;
            if (fixedMob)
            {
                return timeSinceCreation >= 30000;
            }

            if (IsTimeToSelfDestruct())
            {
                DoSelfDestruct();
                return true;
            }

            if (Template.RemoveAfterSeconds > 0)
            {
                return timeSinceCreation > (Template.RemoveAfterSeconds * 1000);
            }
            

            // Uncomment to kill/despawn mobs after event is done
            // if (MobGenItem != null && !MobGenItem.IsActive()) return true;
            

            return false;
        }

        public bool GiveDamage(Character fucker, int amount, AttackPacket.AttackTypes attackType, bool pWasPoison = false)
        {
            if (DeadAlreadyHandled || HP == 0) return false;

            if (fucker != null && pWasPoison == false)
            {
                if (!fucker.IsGM)
                {
                    if (amount >= 90000 ||
                        amount < 0)
                    {
                        fucker.PermaBan($"Impossible damage ({amount})");
                        return false;
                    }
                }
                else
                {
#if DEBUG
                    //amount += 3000000;
#endif
                    if (fucker.GMFixedDamage >= 0)
                        amount = fucker.GMFixedDamage;
                }

                // Normalize damage
                amount = Math.Min(amount, HP);

                DamageLog.AddLog(fucker.ID, amount, MasterThread.CurrentDate);

                SetMobHP(HP - amount);

                if (HP > 0)
                {
                    MobStatus.MobStatValue msv = 0;
                    if (Status.BuffWeb.IsSet())
                    {
                        msv |= Status.BuffWeb.Reset();
                    }
                    
                    if (Status.BuffStun.IsSet() && attackType == AttackPacket.AttackTypes.Melee)
                    {
                        msv |= Status.BuffStun.Reset();
                    }


                    if (msv != 0)
                    {
                        MobPacket.SendMobStatsTempReset(this, msv);
                    }
                }
            }
            else
            {
                // Normalize damage
                amount = Math.Min(amount, HP);

                // You cannot kill mobs with poison.
                if (pWasPoison)
                {
                    DamageLog.AddLog(LastPoisonCharId, amount, MasterThread.CurrentDate);
                    var newHP = Math.Max(1, HP - amount);
                    Trace.WriteLine($"Mob {this} taking poison damage {amount}, new HP {newHP}");
                    SetMobHP(newHP);
                    if (newHP == 1)
                    {
                        // Remove poison debuff
                        Trace.WriteLine("Removing poison stat.");
                        MobPacket.SendMobStatsTempReset(this, Status.BuffPoison.Reset());
                    }
                }
                else
                {
                    HP = Math.Max(0, HP - amount);
                    //DamageLog.AddLog(-1, amount, MasterThread.CurrentDate);
                }
            }


            // Switch controller if required
            if (fucker != null &&
                HP > 0 &&
                Controller != fucker &&
                (MasterThread.CurrentTime - LastAttack) > 5000 &&
                !NextAttackPossible &&
                fucker.IsShownTo(this))
            {
                Trace.WriteLine("Switching controller!");
                SetController(fucker, true);
            }

            return true;
        }


        public bool CheckDead(Pos killPos = null, short Delay = 0, int MesoUp = 0)
        {
            // Just make sure we are not trying to handle a dead body.
            if (DeadAlreadyHandled) return false;

            if (HP > 0 && CheckSelfDestruct())
            {
                // exploded!
            }

            // Just to be *really* sure
            if (HP > 0)
            {
                return false;
            }

            DeadAlreadyHandled = true;
            killPos ??= new Pos(Position);

            var OwnerID = DistributeExp(out var OwnType, out var PartyID);

            if (OwnerID > 0)
            {
                switch (OwnType)
                {
                    case DropOwnType.UserOwn:
                        {
                            var User = Field.GetPlayer(OwnerID);
                            SetMobCountQuestInfo(User);
                            break;
                        }
                    case DropOwnType.PartyOwn:
                        {
                            var Party = Field.GetInParty(PartyID);
                            Party?.ForEach(SetMobCountQuestInfo);
                            break;
                        }
                    case DropOwnType.NoOwn:
                    case DropOwnType.Explosive_NoOwn:
                        Field.ForEachCharacters(SetMobCountQuestInfo);
                        break;
                }
                GiveReward(OwnerID, PartyID, OwnType, killPos, Delay, MesoUp, false);
            }

            GiveEliminationModePoints(OwnerID);

            FinishDeath(false, killPos);

            Field.MobKillCount.TryGetValue(MobID, out var currentKillCount);
            currentKillCount += 1;
            Field.MobKillCount[MobID] = currentKillCount;

            return true;
        }

        private void GiveEliminationModePoints(int killerID)
        {
            if (!(Field is Map_Elimination field)) return;

            if (killerID == 0) return;

            var elimPoints = Template.EliminationPoints;

            if (elimPoints == 0) return;

            var chr = Server.Instance.GetCharacter(killerID);
            if (chr == null) return; // Not sure who give the points to

            var partyMembers = chr.Party?.GetAvailablePartyMembers()?.ToArray() ?? Array.Empty<int>();

            var partyMemberCount = Math.Max(partyMembers.Length, 1);

            var mapDecRate = field.DecRate;

            double actualPoints = elimPoints * (1.0 - (mapDecRate * partyMemberCount));

            field.AddPoints(actualPoints);
        }

        /// <summary>
        /// ForceDead will make sure the mob is dead and won't have stuff spawn (revive node)
        /// </summary>
        public void ForceDead()
        {
            if (DeadAlreadyHandled) return;
            DeadAlreadyHandled = true;

            FinishDeath(true, Position);
        }

        /// <summary>
        /// Kill will kill the mob, and makes it able to spawn its revive mobs
        /// </summary>
        public void Kill()
        {
            if (DeadAlreadyHandled) return;
            DeadAlreadyHandled = true;

            FinishDeath(false, Position);
        }


        private void FinishDeath(bool wasForced, Pos killPos)
        {
            RemoveController(false);

            if (!wasForced && Template.Revive != null)
            {
                // Oh damn, this mob spawns other mobs!

                foreach (var mobid in Template.Revive)
                {
                    Field.CreateMob(
                        mobid,
                        MobGenItem,
                        killPos,
                        Foothold,
                        MobAppear.Revived,
                        SpawnID,
                        IsFacingLeft(),
                        IsBoss ? 2 : 0
                    );
                }
            }

            DamageLog.Clear();

            var dieReason = MobLeaveField.Etc;
            if (HP <= 0 || IsTimeToRemove(MasterThread.CurrentTime, false)
                        || wasForced)
            {
                dieReason = MobLeaveField.RemainHP;
                if (SelfDestructed)
                    dieReason = MobLeaveField.SelfDestruct;
            }

            MobPacket.SendMobDeath(this, dieReason); // He ded.

            Field.RemoveMob(this);

            SetRemoved();

            if (MobType == 1)
            {
                Field.SubMobCount--;
                if (Field.SubMobCount == 0)
                {
                    Field.SubMobCount = -1;
                    foreach (var mob in Field.Mobs.Values.Where(x => x.MobType == 2))
                    {
                        mob.SendSuspendReset(true);
                        mob.SummonType = MobAppear.Normal;
                        mob.SummonOption = 0;
                    }
                }
            }
        }

        public void SetRemoved()
        {
            var mgi = MobGenItem;
            if (mgi == null) return;

            var regenInterval = mgi.RegenInterval;
            if (regenInterval == 0) return;

            mgi.MobCount--;
            if (mgi.MobCount != 0) return;

            mgi.RegenAfter = 0;

            var baseTime = 7 * regenInterval / 10;
            // Nexon has this set to 13
            var maxAdditionalTime = 6 * regenInterval / 10;

            mgi.RegenAfter += (baseTime + Rand32.Next() % maxAdditionalTime);
            Trace.WriteLine($"Setting regeninterval for mobid {MobID} to {mgi.RegenAfter} ({baseTime} {maxAdditionalTime})");

            mgi.RegenAfter += MasterThread.CurrentTime;

        }

        public int OnMobMPSteal(int Prop, int Percent)
        {
            if (Template.Boss)
                return 0;

            var Result = Percent * Template.MaxMP / 100;

            if (Result >= MP)
                Result = MP;

            if (Result < 0 || Rand32.Next() % 100 >= Prop)
                Result = 0;

            MP -= Result;

            return Result;
        }


        private int DistributeExp(out DropOwnType ownOwnType, out int OwnPartyID)
        {
            Trace.WriteLine($"Distributing EXP for killing mob {MobID}. EXP: {Template.EXP}");

            ownOwnType = 0;
            OwnPartyID = 0;
            var Rate = 1.0;
            //if (Stat.nShowdown_ > 0)
            //    Rate = (Stat.nShowdown_ * 100) * 0.01;

            var currentHour = MasterThread.CurrentDate.Hour;

            var MostDamage = 0;
            Character Chr = null;
            var MaxDamageCharacterID = 0;
            long DamageSum = DamageLog.VainDamage;
            var CharactersTmp = new Dictionary<int, Character>();

            var idx = 0;
            foreach (var Log in DamageLog.Log)
            {
                DamageSum += Log.Damage;

                Chr = CharactersTmp.ContainsKey(Log.CharacterID) ? CharactersTmp[Log.CharacterID] : Field.GetPlayer(Log.CharacterID);
                CharactersTmp[Log.CharacterID] = Chr;
                if (Chr == null) continue;

                if (Log.Damage > MostDamage)
                {
                    MaxDamageCharacterID = Log.CharacterID;
                    MostDamage = Log.Damage;
                }
                idx++;
            }

            if (MaxDamageCharacterID != 0)
            {
                Chr = CharactersTmp[MaxDamageCharacterID];
                Trace.WriteLine($"{Chr.Name} did most damage with {MostDamage}");
            }

            // Failsafe for when there's no damagelog
            if (Chr == null) return 0;

            if (DamageSum < DamageLog.InitHP) return 0;

            var PartyDamages = new List<PartyDamage>();

            if ((OwnPartyID = Chr.PartyID) != 0)
                ownOwnType = DropOwnType.PartyOwn;
            if (Template.PublicReward)
                ownOwnType = DropOwnType.NoOwn;
            if (Template.ExplosiveReward)
                ownOwnType = DropOwnType.Explosive_NoOwn;

            if (Template.EXP == 0 || DamageLog.Log.Count <= 0) return Chr.ID;


            var lastLogElement = idx - 1; // Because we increase _after_ the loop
            idx = 0;
            foreach (var Log in DamageLog.Log)
            {
                var bLast = lastLogElement == idx;
                idx++;
                if (Log.Damage <= 0) continue;

                var User = CharactersTmp[Log.CharacterID];
                if (User == null || User.Field.ID != Field.ID) continue;

                var PartyID = User.PartyID;
                if (PartyID == 0 || Field.GetInParty(PartyID).Count() == 1)
                {
                    var lastDamageBuff = 0.0;
                    if (bLast) lastDamageBuff = Template.EXP * 0.2;

                    Trace.WriteLine("Last damage buff: " + lastDamageBuff);
                    var expByDamage = Template.EXP * (double)Log.Damage;

                    Trace.WriteLine("expByDamage: " + expByDamage);
                    var IncEXP = (expByDamage * 0.8 / (double)DamageSum + lastDamageBuff);


                    Trace.WriteLine("IncEXP: " + IncEXP);

                    if (User.PrimaryStats.BuffHolySymbol.IsSet())
                    {
                        var hsBuff = (User.PrimaryStats.BuffHolySymbol.N * 0.2 + 100.0) * 0.01;
                        Trace.WriteLine("Holy Symbol buffed: " + hsBuff);
                        IncEXP *= hsBuff;
                    }

                    IncEXP *= User.m_dIncExpRate;
                    IncEXP = AlterEXPbyLevel(User.Level, IncEXP);
                    IncEXP *= Rate;

                    Trace.WriteLine("IncEXP: " + IncEXP);

                    if (currentHour >= 13 && currentHour <= 18)
                    {
                        // Note: this is an int, set to 100 for 1.0x
                        IncEXP = ((double)Character.ms_nIncExpRate_WSE * IncEXP * 0.01);

                        Trace.WriteLine("WS event: " + IncEXP);
                    }

                    IncEXP *= Field.m_dIncRate_Exp;
                    Trace.WriteLine("Field EXP rate IncEXP: " + IncEXP);

                    IncEXP *= User.RateCredits.GetEXPRate();
                    Trace.WriteLine("Credits rate IncEXP: " + IncEXP);

                    if (User.PrimaryStats.BuffCurse.IsSet())
                    {
                        IncEXP *= 0.5;
                        Trace.WriteLine("Curse debuffed IncEXP: " + IncEXP);
                    }

                    Trace.WriteLine("IncEXP before Max(_, 1.0) " + IncEXP);
                    IncEXP = Math.Max(IncEXP, 1.0);

                    Trace.WriteLine($"{User.Name} gets {IncEXP} EXP for {Log.Damage} damage");

                    User.WrappedLogging(() => User.AddEXP(IncEXP, true));
                }
                else
                {
                    var Damage = PartyDamages.FirstOrDefault(x => x.PartyID == PartyID);
                    if (Damage == null)
                    {
                        Damage = new PartyDamage
                        {
                            PartyID = PartyID,
                            Damage = Log.Damage,
                            MinLevel = User.Level,
                            MaxDamage = Log.Damage,
                            MaxDamageCharacter = User.ID,
                            MaxDamageLevel = User.Level
                        };
                        PartyDamages.Add(Damage);
                    }
                    else
                    {
                        Damage.Damage += Log.Damage;
                        if (Log.Damage > Damage.MaxDamage)
                        {
                            Damage.MaxDamage = Log.Damage;
                            Damage.MaxDamageCharacter = User.UserID;
                            Damage.MaxDamageLevel = User.Level;
                        }

                        if (Damage.MinLevel > User.Level)
                            Damage.MinLevel = User.Level;
                    }
                    Damage.bLast = bLast;
                }
            }

            // Distribute EXP over parties
            // Basically CMob::GiveExp
            if (PartyDamages.Count > 0)
            {
                foreach (var damage in PartyDamages)
                {
                    Trace.WriteLine($"[party {damage.PartyID}] Damage {damage.Damage}, minLevel {damage.MinLevel}, maxDamage {damage.MaxDamage} character {damage.MaxDamageCharacter}");

                    var Party = Field.GetInParty(damage.PartyID)
                        .Where(User =>
                            User != null &&
                            User.PrimaryStats.HP > 0 &&
                            User.Field.ID == Field.ID
                        );

                    damage.MinLevel = Math.Min(Template.Level, damage.MinLevel);
                    damage.MinLevel -= Constants.PartyMinLevelOffset;

                    var partyMembersHigherThanMinLevel = Party.Where(x => x.Level >= damage.MinLevel).ToArray();
                    var partyMemberCountHigherThanMinLevel = partyMembersHigherThanMinLevel.Length;
                    var partyMemberLevelSumHigherThanMinLevel = partyMembersHigherThanMinLevel.Sum(x => x.Level);


                    var MaxPossibleEXP = (double)Template.EXP; // Base EXP

                    // Not the killing party? You lose 20% of the exp
                    if (!damage.bLast) MaxPossibleEXP *= 0.8;

                    // The percentage this party has dealt to the mob
                    MaxPossibleEXP *= damage.Damage / (double)DamageSum;

                    // Make sure they get at least 1 EXP
                    if (MaxPossibleEXP < 1.0) MaxPossibleEXP = 1.0;

                    MaxPossibleEXP = AlterEXPbyLevel(damage.MaxDamageLevel, MaxPossibleEXP);

                    var partyMemberEXP = 0.0;

                    const double baseKillingBlowRate = 0.8261;
                    // Killing players get 84.6% - 97% of max exp
                    var killingBlowUserEXP = MaxPossibleEXP * baseKillingBlowRate;

                    Trace.WriteLine($"Maximum EXP {MaxPossibleEXP} from base {Template.EXP}");
                    Trace.WriteLine($"- Killer gets {killingBlowUserEXP} EXP");

                    if (partyMemberCountHigherThanMinLevel > 1)
                    {
                        var partyBonusEventRate = 1.0;
                        if (Character.ms_nPartyBonusEventRate > 0)
                        {
                            partyBonusEventRate = Character.ms_nPartyBonusEventRate * 0.01;
                        }

                        // Give some EXP because there are partymembers involved

                        var normalPartyExpBoost = (0.05 * partyMemberCountHigherThanMinLevel) * partyBonusEventRate;

                        partyMemberEXP = MaxPossibleEXP;
                        partyMemberEXP *= 1.0 + normalPartyExpBoost;
                        partyMemberEXP *= 0.5; // Only give half, max

                        Trace.WriteLine($"- Party members gets {partyMemberEXP} EXP, including {normalPartyExpBoost * 100.0}% exp boost");

                        // However, for killing blow, give an extra 2.05% per party member
                        var killingBlowUserBonusRate = baseKillingBlowRate;
                        killingBlowUserBonusRate += (0.0205 * partyBonusEventRate) * partyMemberCountHigherThanMinLevel;

                        killingBlowUserEXP = MaxPossibleEXP * killingBlowUserBonusRate;
                        Trace.WriteLine($"Making killing blow user {killingBlowUserBonusRate * 100.0}%, making it {killingBlowUserEXP} EXP");
                    }

                    foreach (var User in partyMembersHigherThanMinLevel)
                    {
                        double incExpUser;

                        if (Chr.ID == User.ID)
                        {
                            // We found the killer, give him some more exp
                            incExpUser = killingBlowUserEXP;
                        }
                        else
                        {
                            incExpUser = partyMemberEXP;

                            if (true)
                            {
                                // Prevent leeching by giving players with a lower level, less EXP
                                var leechExpRate = User.Level / (double)partyMemberLevelSumHigherThanMinLevel;
                                incExpUser *= leechExpRate;
                                Trace.WriteLine($"{User}: Leech rate {leechExpRate * 100.0}% -> {incExpUser} EXP");
                            }
                        }

                        Trace.WriteLine($"{User}: Starting with {incExpUser} EXP");

                        if (User.PrimaryStats.BuffHolySymbol.IsSet())
                        {
                            if (partyMemberCountHigherThanMinLevel == 1)
                            {
                                incExpUser *= (User.PrimaryStats.BuffHolySymbol.N * 0.2 + 100.0) * 0.01;
                            }
                            else if (partyMemberCountHigherThanMinLevel > 1)
                            {
                                incExpUser *= (User.PrimaryStats.BuffHolySymbol.N + 100.0) * 0.01;
                                var maxHSBuff = (double)Template.EXP;
                                maxHSBuff *= (User.PrimaryStats.BuffHolySymbol.N * 0.2 + 100.0) * 0.01;

                                incExpUser = Math.Min(maxHSBuff, incExpUser);
                            }
                        }

                        // EXP bonus from ticket
                        incExpUser *= User.m_dIncExpRate;
                        incExpUser *= Rate;

                        if (currentHour >= 13 && currentHour < 19)
                        {
                            incExpUser = Character.ms_nIncExpRate_WSE * incExpUser * 0.01;
                        }

                        incExpUser *= Field.m_dIncRate_Exp;

                        incExpUser *= User.RateCredits.GetEXPRate();

                        if (User.PrimaryStats.BuffCurse.IsSet())
                        {
                            incExpUser *= 0.5;
                        }

                        Trace.WriteLine($"{User}: Receives {incExpUser} EXP");

                        User.WrappedLogging(() => User.AddEXP(incExpUser, User.ID == Chr.ID));
                    }
                }
            }

            if (MobID == Constants.Zakum3)
            {
                var slayers =
                    DamageLog.Log
                        .OrderByDescending(x => x.Damage)
                        .Select(x => Server.Instance.GetCharacter(x.CharacterID)?.Name)
                        .Where(x => x != null)
                        .ToArray();


                var slayersLength = slayers.Length;

                MessagePacket.SendTextChannel(MessagePacket.MessageTypes.Notice, "The spirit of the Zakum tree has been slain by a brave group of heroes in the depths of the El Nath dungeon! The townspeople of El Nath will forever revere these brave adventurers:");
                
                const int slayersPerLine = 20;
                for (var i = 0; i < slayersLength; i += slayersPerLine)
                {
                    var chunk = Math.Min(slayersLength - i, slayersPerLine);

                    MessagePacket.SendTextChannel(MessagePacket.MessageTypes.Notice, string.Join(", ", slayers[i..(i+chunk)]));
                }
            }

            return Chr.ID;
        }

        public bool DoSkill(byte skillId, byte level, short delay)
        {
            if ((HasAnyStatus && Status.BuffSealSkill.IsSet()))
            {
                _log.Error($"mob.DoSkill {MobID} tried to use skill {skillId} but mob is sealed. Resetting SkillCommand");
                SkillCommand = 0;
                return false;
            }

            if (SkillCommand != skillId)
            {
                _log.Error($"mob.DoSkill {MobID} tried to use skill {skillId} but command is {SkillCommand}. Resetting SkillCommand");
                SkillCommand = 0;
                return false;
            }

            var mobSkills = Template.Skills;
            if (mobSkills == null)
                return false;

            // Bug: level == 0 in packet
            var FIX_ZERO_LVL_BUG = level == 0;
            var mobSkill = mobSkills.FirstOrDefault(x => x.SkillID == skillId && (FIX_ZERO_LVL_BUG || x.Level == level));

            if (mobSkill == null ||
                !DataProvider.MobSkills.TryGetValue(skillId, out var msdLevels))
            {
                _log.Error($"mob.DoSkill {MobID} tried to use skill {skillId} but skill does not exist???");
                SkillCommand = 0;
                return false;
            }
            else if (FIX_ZERO_LVL_BUG)
            {
                level = mobSkill.Level;
            }

            if (!msdLevels.ContainsKey(level))
            {
                _log.Error($"mob.DoSkill {MobID} tried to use skill {skillId} but level {level} does not exist???");
                SkillCommand = 0;
                return false;
            }

            var actualSkill = msdLevels[level];
            // Validate skill use here? EG HPLimit and such
            MP = Math.Max(0, MP - actualSkill.MPConsume);

            LastSkillUse = MasterThread.CurrentTime;

            SkillsInUse[skillId] = LastSkillUse;

            SkillCommand = 0;


            var skillIdAsEnum = (Constants.MobSkills.Skills)skillId;

            switch (skillIdAsEnum)
            {
                case Constants.MobSkills.Skills.WeaponAttackUp:
                case Constants.MobSkills.Skills.MagicAttackUp:
                case Constants.MobSkills.Skills.WeaponDefenseUp:
                case Constants.MobSkills.Skills.MagicDefenseUp:
                case Constants.MobSkills.Skills.PhysicalImmunity:
                case Constants.MobSkills.Skills.MagicImmunity:
                    DoSkill_StatChange(skillId, level, actualSkill, delay);
                    break;

                case Constants.MobSkills.Skills.PoisonMist:
                    // Smoke them boys out!
                    Field.CreateMist(
                        this,
                        SpawnID,
                        skillId,
                        level,
                        actualSkill.Time * 1000,
                        actualSkill.LTX,
                        actualSkill.LTY,
                        actualSkill.RBX,
                        actualSkill.RBY,
                        delay
                    );
                    break;

                case Constants.MobSkills.Skills.Seal:
                case Constants.MobSkills.Skills.Darkness:
                case Constants.MobSkills.Skills.Weakness:
                case Constants.MobSkills.Skills.Stun:
                case Constants.MobSkills.Skills.Curse:
                case Constants.MobSkills.Skills.Dispell:
                case Constants.MobSkills.Skills.Poison:
                case Constants.MobSkills.Skills.Slow:
                    DoSkill_UserStatChange(skillId, level, actualSkill, delay);
                    break;

                case Constants.MobSkills.Skills.Summon:
                    DoSkill_Summon(actualSkill, delay);
                    break;

                case Constants.MobSkills.Skills.HealAoe:
                    DoSkill_PartizanOneTimeStatChange(skillId, level, actualSkill, delay);
                    break;

                case Constants.MobSkills.Skills.MagicAttackUpAoe:
                case Constants.MobSkills.Skills.MagicDefenseUpAoe:
                case Constants.MobSkills.Skills.WeaponAttackUpAoe:
                case Constants.MobSkills.Skills.WeaponDefenseUpAoe:
                    DoSkill_PartizanStatChange(skillId, level, actualSkill, delay);
                    break;
                    

                default:
                    _log.Warn($"Unhandled mob skill {skillIdAsEnum}, mob {MobID}");
                    break;
            }

            return true;
        }

        public void DoSkill_UserStatChange(short skillId, byte level, MobSkillLevelData msld, short delay)
        {
            int left = msld.LTX;
            int right = msld.RBX;

            if (IsFacingRight())
            {
                left *= -1;
                right *= -1;
            }
            
            Field.GetCharactersInRange(
                Position,
                new Pos((short)left, msld.LTY),
                new Pos((short)right, msld.RBY)
            ).ForEach(character =>
            {
                if (character.GMHideEnabled) return;

                CharacterStatsPacket.OnStatChangeByMobSkill(character, msld, delay);
            });
        }

        public void DoSkill_Summon(MobSkillLevelData msld, short delay)
        {
            // Cant have more than 50 mobs in the map.
            if (Field.Mobs.Count >= 50) return;
            
            var summons = msld.Summons;

            SummonCount += summons.Count;


            var rect = msld.GetAffectedArea(false);
            if (rect.IsEmpty)
            {
                rect = Rectangle.FromLTRB(
                    -150,
                    -100,
                    100,
                    150
                );
            }

            rect.Offset(Position.X, Position.Y);

            var randomFootholds = Field.GetFootholdRandom(summons.Count, rect);

            var summonIndex = 0;
            foreach (var randomFoothold in randomFootholds)
            {

                Field.CreateMobWithoutMobGen(
                    summons[summonIndex],
                    randomFoothold.intersection,
                    randomFoothold.fh.ID,
                    (MobAppear)(sbyte)msld.SummonEffect,
                    delay,
                    IsFacingLeft()
                );

                summonIndex++;
            }
        }

        public void DoSkill_PartizanStatChange(int skillID, byte level, MobSkillLevelData msld, short delay)
        {
            var affectedArea = msld.GetAffectedArea(IsFacingLeft());
            if (affectedArea.IsEmpty) return;

            foreach (var mob in Field.GetMobsInRange(Position, affectedArea))
            {
                if (mob.MobID == Constants.InvisibleMob) continue;

                mob.DoSkill_StatChange(skillID, level, msld, delay);
            }
        }


        public void DoSkill_StatChange(int skillID, byte level, MobSkillLevelData msld, short delay)
        {
            var rValue = skillID | (level << 16);

            MobStatus.MobBuffStat buffStat = null;
            var skillIdAsEnum = (Constants.MobSkills.Skills)skillID;
            switch (skillIdAsEnum)
            {
                case Constants.MobSkills.Skills.WeaponAttackUpAoe:
                case Constants.MobSkills.Skills.WeaponAttackUp: buffStat = Status.BuffPowerUp; break;

                case Constants.MobSkills.Skills.MagicAttackUpAoe:
                case Constants.MobSkills.Skills.MagicAttackUp: buffStat = Status.BuffMagicUp; break;

                case Constants.MobSkills.Skills.WeaponDefenseUpAoe:
                case Constants.MobSkills.Skills.WeaponDefenseUp: buffStat = Status.BuffPowerGuardUp; break;

                case Constants.MobSkills.Skills.MagicDefenseUpAoe:
                case Constants.MobSkills.Skills.MagicDefenseUp: buffStat = Status.BuffMagicGuardUp; break;

                case Constants.MobSkills.Skills.PhysicalImmunity: buffStat = Status.BuffPhysicalImmune; break;
                case Constants.MobSkills.Skills.MagicImmunity: buffStat = Status.BuffMagicImmune; break;

                default:
                    _log.Warn($"DoSkill_StatChange({skillID}): no skill matched?");
                    return;

            }

            var stat = buffStat.Set(rValue, (short)msld.X, MasterThread.CurrentTime + (msld.Time * 1000));

            MobPacket.SendMobStatsTempSet(this, delay, stat);
        }

        public void DoSkill_PartizanOneTimeStatChange(int skillID, byte level, MobSkillLevelData msld, short delay)
        {
            var affectedArea = msld.GetAffectedArea(IsFacingLeft());
            if (affectedArea.IsEmpty) return;

            foreach (var mob in Field.GetMobsInRange(Position, affectedArea))
            {
                if (mob.MobID == Constants.InvisibleMob) continue;

                mob.DoSkill_OneTimeStatChange(skillID, level, msld, delay);
            }
        }


        public void DoSkill_OneTimeStatChange(int skillID, byte level, MobSkillLevelData msld, short delay)
        {
            if (skillID == (int)Constants.MobSkills.Skills.HealAoe)
            {
                var rValue = skillID | (level << 16);

                var min = msld.X;
                var additional = (int)(Rand32.Next() % msld.Y);

                var healedHP = min + additional;
                SetMobHP(HP + healedHP);
                SendDamagePacket(false, -healedHP);
                SendMobAffected(rValue, 0);
            }
        }

        /// <summary>
        /// Sends the DamageOrHeal packet. Note that we always assume negative damage, so don't overcorrect.
        /// </summary>
        /// <param name="web">Wether or not this was caused by the Web skill</param>
        /// <param name="decHP">Send negative damage for heal, positive for damage.</param>
        public void SendDamagePacket(bool web, int decHP)
        {
            MobPacket.SendMobDamageOrHeal(this, decHP, false, web);
        }

        public void SendMobAffected(int skillID, short delay)
        {
            MobPacket.SendMobAffected(this, skillID, delay);
        }

        public double AlterEXPbyLevel(int Level, double IncEXP) => IncEXP;

        // This function only gives (extra) money when doing Pickpocket
        public void GivePickpocketMoney(Character User, AttackData.AttackInfo Attack, int AttackCount)
        {
            if (User.Skills.GetSkillLevel(Constants.ChiefBandit.Skills.Pickpocket, out var SkillData) <= 0) return;
            if (AttackCount <= 0) return;

            var Rewards = new List<Reward>();
            var Rate = User.Level / (double)Template.Level;
            double DamageRate = 0;
            if (Rate > 1.0) Rate = 1.0;

            foreach (var Damage in Attack.Damages)
            {
                if (Damage.Damage != 0 && SkillData.Property * Rate >= Rand32.Next() % 100)
                {
                    DamageRate = Damage.Damage / Template.MaxHP;
                    if (DamageRate > 1.0) DamageRate = 1.0;
                    if (DamageRate < 0.5) DamageRate = 0.5;
                    DamageRate = Template.Level * SkillData.XValue * DamageRate * 0.006666666666666667;
                    if (DamageRate < 1.0) DamageRate = 1.0;
                    var Mesos = Convert.ToInt32(Rand32.NextBetween(1, int.MaxValue) % DamageRate);
                    Rewards.Add(Reward.Create(Mesos));
                }
            }

            var x2 = Position.X - 10 * Rewards.Count + 10;
            var Delay = 0;
            foreach (var Reward in Rewards)
            {
                var drop = Field.DropPool.Create(
                    Reward,
                    User.ID,
                    0,
                    DropOwnType.UserOwn,
                    SpawnID,
                    Position,
                    x2,
                    (short)(Attack.HitDelay + Delay),
                    false,
                    0,
                    false
                );
                if (drop != null)
                {
                    Delay += 120;
                    x2 += 20;
                }
            }
        }

        public void GiveReward(int OwnerID, int OwnPartyID, DropOwnType ownOwnType, Pos Pos, short Delay, int MesoUp, bool Steal)
        {
            if (Steal && AlreadyStealed) return;

            // if (FieldType == MonsterCarnival) Field.GetMCRewardRate()

            var User = Server.Instance.GetCharacter(OwnerID);

            var Rewards = Reward.ShuffleSort(Reward.GetRewards(
                User,
                Field,
                MobID,
                Field.Premium,
                !Template.NoGlobalReward
            )).ToArray();
            if (Rewards.Length == 0) return;

            OwnerID = (Template.PublicReward ? 0 : OwnerID);
            OwnPartyID = (Template.PublicReward ? 0 : OwnPartyID);

            if (Steal && Rewards.Length > 0)
            {
                Reward StolenDrop = null;
                var Limit = 0;
                
                // Find an item that is not a quest item
                while (StolenDrop == null || DataProvider.QuestItems.Contains(StolenDrop.ItemID))
                {
                    StolenDrop = Rewards[(int)(Rand32.Next() % Rewards.Length)];
                    if (Limit++ > 100)
                    {
                        StolenDrop = null;
                        break;
                    }
                }

                if (StolenDrop != null)
                {
                    if (StolenDrop.Mesos)
                    {
                        // NOTE: if its money it should drop half.
                        StolenDrop = Reward.Create(StolenDrop.Drop / 2.0d);
                    }

                    var drop = Field.DropPool.Create(
                        StolenDrop,
                        OwnerID,
                        OwnPartyID,
                        ownOwnType,
                        SpawnID,
                        Pos,
                        Pos.X,
                        Delay,
                        false,
                        0,
                        true
                    );
                    ItemID_Stolen = StolenDrop.ItemID;
                    AlreadyStealed = true;
                }
            }
            else
            {
                var i = 0;
                var x2 = Pos.X + Rewards.Length * (Template.ExplosiveReward ? -20 : -10);

                Trace.WriteLine($"Drops to do {Rewards.Length}");

                var maxDamageCharacter = DamageLog?.Log?.OrderByDescending(x => x.Damage).FirstOrDefault()?.CharacterID ?? 0;

                foreach (var Reward in Rewards)
                {
                    if (/*(DataProvider.QuestItems.Contains(Reward.ItemID) && !User.Quests.ItemCheck(Reward.ItemID)) || */
                        (ItemID_Stolen == Reward.ItemID && !Reward.Mesos))
                        continue;
                    if (Reward.Mesos)
                    {
                        if (MesoUp > 0)
                        {
                            Reward.Drop = (int)((float)Reward.Drop * MesoUp / 100.0);
                        }
                    }

                    var drop = Field.DropPool.Create(Reward, OwnerID, OwnPartyID, ownOwnType, SpawnID, Pos, x2, Delay, false, 0, true);
                    if (drop != null)
                    {
                        drop.MaxDamageCharacterID = maxDamageCharacter;
                        i++;
                        // Delay += 200;
                        x2 += Template.ExplosiveReward ? 40 : 20;
                    }
                }
            }

        }

        public void RemoveController(bool sendPacket)
        {
            if (!IsControlled) return;
            // Make sure we are not bugging people
            if (Controller.Field == Field && sendPacket)
            {
                MobPacket.SendMobRequestEndControl(Controller, SpawnID);
            }
            Controller = null;
        }

        public void SetMobCountQuestInfo(Character User)
        {
            if (User != null && User.PrimaryStats.HP > 0 && User.Field.ID == Field.ID)
            {
                if (!QuestsProvider.MobsToQuestDemands.TryGetValue(MobID, out var quests)) return;

                User.Quests.SetMobCountQuestInfo(MobID, quests);
            }
        }

        public void SetController(Character controller, bool chasing = false, bool sendStopControlPacket = true)
        {
            if (HP == 0) return;
            RemoveController(sendStopControlPacket);

            HackReportCounter = 0;
            NextAttackPossible = false;
            SkillCommand = 0;

            var currentTime = MasterThread.CurrentTime;
            LastAttack = currentTime;
            LastMove = currentTime;
            LastControllerAssignTime = currentTime;

            Controller = controller;
            MobPacket.SendMobRequestControl(Controller, this, chasing);
        }

        public void DoPoison(int charid, int poisonSLV, long expireTime, int skillId, short magicAttack, short delay)
        {
            if (IsBoss) return;

            if (Template.elemModifiers.TryGetValue(SkillElement.Poison, out var resistance) && !(resistance < 1 || resistance > 2)) return;

            var mob = this;
            mob.LastPoisonCharId = charid;
            mob.lastPoisonDMG = MasterThread.CurrentTime;
            var stat = mob.Status.BuffPoison.Set(
                skillId,
                Math.Max((short)(mob.MaxHP / (70 - poisonSLV)), magicAttack),
                expireTime
            );

            MobPacket.SendMobStatsTempSet(mob, delay, stat);

            Trace.WriteLine($"Mob {this} is poisoned with stat {mob.Status.BuffPoison}");
        }

        public bool IsMortalBlowEffective(Character chr)
        {
            SkillLevelData sld = null;

            var rangerMortalBlow = chr.Skills.GetSkillLevelData(Constants.Ranger.Skills.MortalBlow, out var lvl);
            if (lvl > 0) sld = rangerMortalBlow;

            var sniperMortalBlow = chr.Skills.GetSkillLevelData(Constants.Sniper.Skills.MortalBlow, out lvl);
            if (lvl > 0) sld = sniperMortalBlow;

            if (sld == null || IsBoss)
                return false;

            if (HP > Template.MaxHP * sld.XValue / 100 || Rand32.Next() % 100 >= sld.YValue)
                return false;

            MobPacket.SendSpecialEffectBySkill(this, sld.SkillID);

            return true;
        }

        public void SetMobHP(int hp)
        {
            if (hp < 0) hp = 0;
            if (hp > MaxHP) hp = MaxHP;

            if (hp == HP) return;

            HP = hp;

            SendMobHPChange(false);
        }

        private long lastSendMobHP = 0;
        public void SendMobHPChange(bool enforce)
        {
            var currentTime = MasterThread.CurrentTime;
            if (currentTime - lastSendMobHP < 500 && !enforce) return;

            lastSendMobHP = currentTime;

            if (Template.HPTagBgColor != 0 &&
                Template.HPTagColor != 0)
            {
                MapPacket.SendBossHPBar(Field, HP, MaxHP, Template.HPTagBgColor, Template.HPTagColor);
            }
        }

        public void SendSuspendReset(bool suspendReset)
        {
            var pw = new Packet(ServerMessages.MOB_SUSPEND_RESET);
            pw.WriteInt(SpawnID);
            pw.WriteBool(suspendReset);
            Field.SendPacket(pw);
        }

        public override string ToString()
        {
            return $"Mob {MobID} ({Template.Name}, spawn {SpawnID}, map {Field.FullName}) HP {HP}/{MaxHP}, MP {MP}/{MaxMP}, Controlled by {Controller?.Name}";
        }
    }

    public class MobDamageLog
    {
        public Map Field;
        public int InitHP;
        public int VainDamage;
        public List<MobDamageInfo> Log;

        public MobDamageLog(Map Map, int HP)
        {
            Field = Map;
            InitHP = HP;
            Log = new List<MobDamageInfo>();
        }

        private int GetNextDamageValue(int currentDamage, int extraDamage)
        {
            var newDamage = (long)currentDamage + extraDamage;
            if (newDamage > int.MaxValue) newDamage = int.MaxValue;

            return (int)newDamage;
        }

        public void AddLog(int CharacterID, int Damage, DateTime tCur)
        {
            var existingItem = Log.FirstOrDefault(x => x.CharacterID == CharacterID);

            if (existingItem != null)
            {
                existingItem.Damage = GetNextDamageValue(existingItem.Damage, Damage);
            }
            else
            {

                if (Log.Count >= 32)
                {
                    var firstDamageElem = Log.First();
                    VainDamage = GetNextDamageValue(VainDamage, firstDamageElem.Damage);
                    Log.Remove(firstDamageElem);
                }

                Log.Add(new MobDamageInfo
                {
                    CharacterID = CharacterID,
                    Damage = Damage,
                    Time = tCur
                });
            }
        }

        public void Clear()
        {
            VainDamage = 0;
            Log.Clear();
        }
    }

    public class MobDamageInfo
    {
        public int CharacterID;
        public int Damage;
        public DateTime Time;
    }

    public class PartyDamage
    {
        public int PartyID;
        public int Damage;
        public int MinLevel;
        public int MaxDamage;
        public int MaxDamageCharacter;
        public int MaxDamageLevel;
        public bool bLast;
    }
}