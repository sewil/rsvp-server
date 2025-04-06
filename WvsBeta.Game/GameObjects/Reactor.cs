using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using log4net;
using WvsBeta.Common;
using WvsBeta.SharedDataProvider.Templates;

namespace WvsBeta.Game
{
    public class Reactor
    {
        private ILog _log = LogManager.GetLogger("Reactor");

        public readonly Map Field;

        public readonly short ID;
        public short X { get; set; }
        public short Y { get; set; }
        public readonly byte Z;
        public readonly byte ZM;
        public string Name { get; }

        // Note: reactors stay spawned if they have a RegenInterval
        private bool spawned;

        public bool Enabled { get; set; }

        public bool Done => RegenInterval != 0 && Template.States[State].Events.Length == 0;

        public int RegenInterval { get; set; }
        public long RegenAfter { get; set; }
        private long StateStart { get; set; }
        public long StateEnd { get; private set; }
        private long LastHit { get; set; }
        private int HitCount { get; set; }
        private int Timeout { get; set; }

        private int OwnerID { get; set; }
        private int LastHitCharacterID { get; set; }
        private int OwnPartyID { get; set; }
        private DropOwnType OwnOwnType { get; set; }

        public List<DropData> Rewards { get; set; } = new List<DropData>();

        public ReactorData Template { get; }

        public byte State { get; private set; }

        public int PieceID { get; set; }
        public int PageID { get; set; }

        public NpcLife NPC { get; set; }

        private string RepeatingTaskName => this.ToString();

        public Reactor(Map pField, short pID, byte pState, short pX, short pY, byte pZ, byte pZM, string l2, string name)
        {
            Field = pField;
            ID = pID;
            State = pState;
            X = pX;
            Y = pY;
            Z = pZ;
            ZM = pZM;
            Template = DataProvider.Reactors[l2];
            Name = name ?? Template.ID;
        }

        public override string ToString() => $"Reactor {Template} map {Field.ID} reactorid {ID}";

        public void Spawn()
        {
            if (spawned) return;
            spawned = true;
            ReactorPacket.SpawnReactor(this);
        }

        public void ShowTo(Character chr)
        {
            if (!spawned) return;
            ReactorPacket.SpawnReactor(this, chr);
        }

        public void Despawn()
        {
            ReactorPacket.RemoveReactor(this);
            spawned = false;
        }

        public void HitBy(Character chr, short delay, uint option)
        {
            if (StateEnd > MasterThread.CurrentTime) return;

            var currentState = Template.States[State];
            var bestEventPrio = -1;
            var bestEventIdx = -1;
            for (var i = 0; i < currentState.Events.Length; i++)
            {
                var _event = currentState.Events[i];
                var prio = _event.GetHittypePriorityLevel(option);
                if (prio != -1 && (prio < bestEventPrio || bestEventPrio == -1))
                {
                    bestEventPrio = prio;
                    bestEventIdx = i;
                }
            }

            if (bestEventPrio == -1) return;

            if (HitCount == 0 && OwnerID == 0)
            {
                OwnerID = chr.ID;

                var partyId = chr.PartyID;
                if (partyId != 0)
                {
                    OwnPartyID = partyId;
                    OwnOwnType = DropOwnType.PartyOwn;
                }
                else
                {
                    OwnPartyID = 0;
                    OwnOwnType = DropOwnType.UserOwn;
                }
            }


            var reqHitCount = Template.ReqHitCount;

            if (reqHitCount > 0)
            {
                HitCount++;
                Trace.WriteLine($"Hit reactor {Template} {HitCount} of {reqHitCount} times");
            }


            if (HitCount >= reqHitCount)
            {
                SetStateByEvent(bestEventIdx, delay);
                HitCount = 0;
                LastHitCharacterID = chr.ID;
            }

            LastHit = MasterThread.CurrentTime;
        }

        public int AnimationTime
        {
            get
            {
                var stateData = Template.States[State];
                return stateData.HitDelay + stateData.ChangeStateDelay;
            }
        }

        public void SetStateByEvent(int eventIdx, int actionDelay)
        {
            var stateData = Template.States[State];

            StateStart = MasterThread.CurrentTime;
            StateEnd = StateStart + actionDelay + AnimationTime;
            
            byte newState;

            if (eventIdx >= 0 && eventIdx < stateData.Events.Length)
            {
                var eventData = stateData.Events[eventIdx];
                newState = eventData.StateToBe;
            }
            else
            {
                newState = (byte)((State + 1) % Template.StateCount);
            }

            SetState(newState, actionDelay, (byte)eventIdx);
        }

        public void SetState(byte state, int actionDelay = 0, byte eventIdx = 0)
        {
            var animationTime = AnimationTime;

            State = state;

            var stateData = Template.States[State];

            Trace.WriteLineIf(Server.Instance.Initialized, $"New state of reactor {Template} is {State}, HitDelay {stateData.HitDelay}, animation time {animationTime}");

            FindAvailableAction();

            Timeout = stateData.Timeout;

            OnChangedState((short)actionDelay, eventIdx, animationTime);

            if (Done)
            {
                SetRemoved();
            }
        }

        private void OnChangedState(short actionDelay, byte eventIdx, int animationTime)
        {
            if (!spawned)
                Spawn();
            else
                ReactorPacket.ReactorChangedState(this, actionDelay, eventIdx, animationTime);
        }

        public void FindAvailableAction()
        {
            var timeLeft = StateEnd - MasterThread.CurrentTime;
            var dropIdx = 0;
            for (var i = 0; i < Template.Actions.Length; i++)
            {
                var action = Template.Actions[i];
                if (action.State == -1) continue;
                if (action.State != State) continue;

                if (action.Type == ReactorData.ActionData.Types.Reward)
                {
                    DoAction(action, (int)timeLeft / 2, dropIdx);
                    dropIdx++;
                }
                else if (action.Type == ReactorData.ActionData.Types.RunOnGuardianDestroyed)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    MasterThread.RepeatingAction.Start($"{RepeatingTaskName} Action {i} type " + action.Type, _ =>
                    {
                        DoAction(action, 0, 0);
                    }, TimeSpan.FromMilliseconds(timeLeft), TimeSpan.Zero);
                }
            }

            Trace.WriteLineIf(Server.Instance.Initialized, $"Reactor {Name} changed to state {State}");
            Field.CheckReactorAction(Name, State, StateEnd);
        }

        public void DoAction(ReactorData.ActionData action, int delay, int dropIdx)
        {
            switch (action)
            {
                case ReactorData.RewardActionData _:
                    {
                        var possibleOwner = Field.FindCharacterInMap(OwnerID);

                        if (Rewards.Count == 0)
                        {
                            Rewards.AddRange(Template.RewardInfo);
                        }

                        Trace.WriteLine($"Trying to calculate drops of {Rewards.Count} rewards");

                        var actualRewards = Reward.GetRewards(possibleOwner, Field, Field.Premium, Rewards).ToArray();
                        Trace.WriteLine($"Trying to drop {actualRewards.Length}");
                        if (actualRewards.Length == 0) return;

                        const int dropDelay = 200;
                        const int dropDistance = 20;

                        var additionalOffset = dropDistance * dropIdx; // There's a start offset for multi-drop/actions Reactors.
                        var halfDistance = (actualRewards.Length / 2) * dropDistance;

                        foreach (var reward in Reward.ShuffleSort(actualRewards))
                        {
                            var x2 = X + additionalOffset - halfDistance;

                            Field.DropPool.Create(
                                reward,
                                OwnerID,
                                OwnPartyID,
                                OwnOwnType,
                                -1, // Newer versions have the 'template id' here, but we cannot do that
                                new Pos(X, Y),
                                x2,
                                (short)delay,
                                false,
                                0,
                                true
                            );

                            additionalOffset += dropDistance;
                            delay += dropDelay;
                        }

                        break;
                    }

                case ReactorData.SummonActionData sad:
                    {
                        var posX = X;
                        var posY = Y;

                        if (sad.HasXY)
                        {
                            posX = (short)sad.X;
                            posY = (short)sad.Y;
                        }

                        var pos = new Pos(posX, posY);
                        var fh = Field.GetFootholdUnderneath(pos.X, pos.Y - 20, out var intersectY);
                        if (fh == null)
                        {
                            _log.Error($"No foothold under {pos}, so not spawning mob...  {action} {this}");
                            return;
                        }

                        pos.Y = intersectY;

                        if (action.Message != null)
                        {
                            MessagePacket.ScriptNotice(Field, action.Message);
                        }

                        for (var i = 0; i < sad.SummonAmount; i++)
                        {
                            Field.CreateMobWithoutMobGen(
                                sad.MobID,
                                pos,
                                fh.Value.ID,
                                (MobAppear)(sbyte)sad.SummonType,
                                0,
                                false,
                                sad.SummonMobType
                            );
                        }

                        break;
                    }

                case ReactorData.SummonNpcActionData snad:
                    {
                        var posX = X;
                        var posY = Y;

                        if (snad.HasXY)
                        {
                            posX = (short)snad.X;
                            posY = (short)snad.Y;
                        }

                        var pos = new Pos(posX, posY);
                        var fh = Field.GetFootholdUnderneath(pos.X, pos.Y - 20, out var intersectY);
                        if (fh == null)
                        {
                            _log.Error($"No foothold under {pos}, so not spawning npc...  {action} {this}");
                            return;
                        }

                        pos.Y = intersectY;

                        if (action.Message != null)
                        {
                            MessagePacket.ScriptNotice(Field, action.Message);
                        }

                        // add NPC to map
                        NPC = Field.AddLife(
                            new Life
                            {
                                Cy = 0,
                                FacesLeft = false,
                                Foothold = (ushort)fh.Value.ID,
                                ID = snad.NpcID,
                                X = pos.X,
                                Y = pos.Y,
                                Type = 'n',
                                RespawnTime = -1,
                                Rx0 = (short)(pos.X - 50),
                                Rx1 = (short)(pos.X + 50)
                            },
                            null
                        ) as NpcLife;


                        break;
                    }

                case ReactorData.TransferActionData tad:
                    {
                        int mapId;
                        string portalName;
                        if (tad.MultiMap)
                        {
                            var randomMap = (int)(Rand32.Next() % tad.MapCount);
                            mapId = tad.GetMapID(randomMap);
                            portalName = tad.GetPortalName(randomMap);
                        }
                        else
                        {
                            mapId = (int)tad.Args[0];
                            portalName = null;
                        }

                        if (mapId == Constants.InvalidMap)
                        {
                            _log.Error($"Invalid map id for reactor event, ignoring. {action} {this}");
                            return;
                        }

                        if (tad.AllPlayers)
                        {
                            if (action.Message != null)
                            {
                                MessagePacket.ScriptNotice(Field, action.Message);
                            }
                            Field.ForEachCharacters(character => character.ChangeMap(mapId, portalName));
                        }
                        else
                        {
                            var owner = Field.FindCharacterInMap(LastHitCharacterID);
                            if (owner == null) return;

                            if (action.Message != null)
                            {
                                MessagePacket.ScriptNotice(owner, action.Message);
                            }

                            owner.ChangeMap(mapId, portalName);
                        }

                        break;
                    }

                case ReactorData.TogglePortalActionData tpad:
                    {
                        // This is not implemented in BMS, so we don't either.
                        break;
                    }

                default:
                    _log.Error($"Unhandled action {action} for reactor {Name} ({this})");
                    break;
            }
        }

        public void DoActionByUpdateEvent(long now)
        {
            var stateData = Template.States[State];
            foreach (var stateDataEvent in stateData.Events)
            {
                if (!(stateDataEvent is ReactorData.FindItemUpdateEventData fiued) || fiued.CheckArea == null) continue;
                var checkArea = fiued.CheckArea.Value;
                checkArea.Offset(X, Y);

                if (Field.DropPool.Drops.Count == 0) continue;

                var drops = Field.DropPool.FindDropInRect(checkArea, TimeSpan.FromSeconds(3)).ToArray();


                var dropsMatching = fiued.Items.Select(entry =>
                {
                    var (itemid, amount) = entry;
                    return drops.FirstOrDefault(x => !x.Reward.Mesos &&
                                                     x.Reward.ItemID == itemid &&
                                                     x.Reward.Amount == amount);
                }).Where(x => x != null).ToArray();

                if (dropsMatching.Length != fiued.Items.Length)
                {
                    Trace.WriteLine("Did not find enough drops for reactor.");
                    continue;
                }

                if (fiued.RemoveDrops)
                {
                    dropsMatching.ForEach(x => Field.DropPool.RemoveDrop(x));
                }

                OwnPartyID = 0;
                OwnerID = 0;
                OwnOwnType = DropOwnType.NoOwn;
                SetStateByEvent(stateDataEvent.ID, 0);
                break;
            }
        }

        public void UpdateOwnerInfo(long now)
        {
            if (LastHit != 0 && (now - LastHit) > 15000)
            {
                OwnerID = 0;
                OwnPartyID = 0;
            }

            if (Timeout != 0 && (now - StateStart) > Timeout)
            {
                // Find a TimeoutReset event
                var stateData = Template.States[State];
                for (var i = 0; i < stateData.Events.Length; i++)
                {
                    var _event = stateData.Events[i];
                    if (_event.Type != ReactorData.EventData.Types.TimeoutReset) continue;

                    SetStateByEvent(i, 0);
                }
            }
        }

        public void TryRespawn(long now)
        {
            if (now < RegenAfter) return;
            if (spawned && !Done) return;
            if (!Enabled) return;
            Respawn();
        }

        public void Respawn()
        {
            OwnerID = 0;
            LastHitCharacterID = 0;
            OwnPartyID = 0;
            OwnOwnType = DropOwnType.UserOwn;
            
            HitCount = 0;
            StateEnd = MasterThread.CurrentTime;
            SetState(0);
        }

        private void SetRemoved()
        {
            if (RegenInterval > 0)
            {
                var baseTime = RegenInterval / 10;
                RegenAfter = (baseTime * 7) + Rand32.Next() % (baseTime * 6);

                Trace.WriteLine($"Reactor {Template} will respawn in {TimeSpan.FromMilliseconds(RegenAfter)}");
                RegenAfter += MasterThread.CurrentTime;
            }
            else
            {
                // This reactor is finished and won't spawn until reset
                Enabled = false;
            }
        }
        

        public ReactorData.StateData GetCurStateInfo() => Template.States[State];
    }
}