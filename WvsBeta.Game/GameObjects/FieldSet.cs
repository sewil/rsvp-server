using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using log4net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.GameObjects;
using WvsBeta.SharedDataProvider.Providers;
using WvsBeta.SharedDataProvider.Templates;
using WzTools.Objects;

namespace WvsBeta.Game
{
    public class FieldSet
    {
        /// <summary>
        /// This type of timer is used as a delay.
        /// As long as its IsSet, the timer DID NOT ran out
        /// </summary>
        public class TIMER
        {
            public string Name { get; set; }

            public long StartTime { get; set; }

            public long TimeSet { get; set; }
            public bool IsCleared { get; set; }
            public bool IsTriggered { get; set; }
            public bool Log { get; set; } = true;

            public TimeSpan Time
            {
                get => TimeSpan.FromMilliseconds(TimeSet);
                set
                {
                    TimeSet = (long)value.TotalMilliseconds;
                    IsTriggered = false;
                    IsCleared = false;
                    StartTime = MasterThread.CurrentTime;

                    if (Log)
                    {
                        _log.Info($"FieldSet timer {Name} updated (estimated time to run: {Time})");
                    }
                }
            }

            public TimeSpan TimeLeft => TimeSpan.FromMilliseconds(!IsTriggered && !IsCleared ? (Time.TotalMilliseconds - (MasterThread.CurrentTime - StartTime)) : 0);

            public TimeSpan TimePassed => TimeSpan.FromMilliseconds(!IsTriggered && !IsCleared ? (MasterThread.CurrentTime - StartTime) : 0);

            public TIMER(string name, bool log = true)
            {
                Name = name;
                Log = log;
                Reset();
            }

            public void Reset()
            {
                IsCleared = true;
                IsTriggered = false;
            }

            public bool IsExpired
            {
                get
                {
                    Update();
                    return IsTriggered && !IsCleared;
                }
            }

            public bool ResetIfExpired()
            {
                if (IsSet) return false;
                Reset();
                _log.Info($"FieldSet timer {Name} triggered reset after expiration.");
                return true;
            }

            public void Update()
            {
                if (!IsTriggered)
                {
                    var msElapsed = MasterThread.CurrentTime - StartTime;
                    var msRequired = Time.TotalMilliseconds;

                    if (msElapsed > msRequired)
                    {
                        _log.Info($"FieldSet timer {Name} triggered (unset) (estimated time to run: {Time})");
                        IsTriggered = true;
                    }
                }
            }

            public bool IsSet
            {
                get
                {
                    if (IsCleared) return false;
                    Update();
                    
                    return !IsTriggered;
                }
            }

            public override string ToString()
            {
                return $"{Name}, Triggered {IsTriggered}, Cleared {IsCleared}, TimeLeft {TimeLeft}, TimePassed {TimePassed}";
            }
        }

        private static ILog _log = LogManager.GetLogger("FieldSet");

        public static Dictionary<string, FieldSet> Instances { get; } = new Dictionary<string, FieldSet>();

        public static bool DisabledForMaintenance = false;

        public Dictionary<string, string> Variables { get; } = new Dictionary<string, string>();

        public IEnumerable<Character> Characters
            => MapsAffected.Values
                .SelectMany(map => map.Characters)
                .Where(character => !character.GMHideEnabled);

        public int UserCount => Characters.Count();

        public string Name { get; private set; }
        public string InitScript { get; private set; }
        public Dictionary<int, Map> Maps { get; } = new Dictionary<int, Map>();
        public Dictionary<int, Map> MapsAffected { get; } = new Dictionary<int, Map>();
        public TimeSpan CheckTimeOut { get; private set; }

        public TIMER OccupiedTimer { get; set; } 
        public TIMER EnterTimer { get; set; }
        public TIMER TimeOutTimer { get; set; }

        public long TimeRemaining => (long)TimeOutTimer.TimeLeft.TotalMilliseconds;

        // Character ID of character that started the fieldset
        public int Leader { get; private set; }

        public bool FieldSetStart { get; private set; }
        public bool HasTimeout => CheckTimeOut.TotalMilliseconds > 0 || TimeOutTimer.IsSet || TimeOutTimer.IsTriggered;

        public bool ShuffleReactors { get; private set; }
        public int TargetFieldID { get; set; }

        public bool TryToRunInitScript { get; set; }
        public long EventStart { get; set; }
        public bool EndFieldSetAct { get; set; }

        public FieldSetData.EventData[] Events { get; private set; } = new FieldSetData.EventData[0];
        public FieldSetData.ReactorActionInfo[] ReactorActions { get; private set; } = new FieldSetData.ReactorActionInfo[0];
        public bool[] EventsRan { get; private set; }

        public bool Load(WzProperty property)
        {
            Name = property.Name;
            Instances[Name] = this;

            var unaffectedProp = property.GetProperty("unaffected");
            var affectedCount = 0;

            TemplateProvider.LoadArgs<int>(property).ForEach((mapid, index) =>
            {
                if (!MapProvider.Maps.TryGetValue(mapid, out var map))
                {
                    throw new Exception($"Found FieldSet map that doesn't exist: {mapid} (fs {Name})");
                }

                Maps[index] = map;

                if ((unaffectedProp?.GetBool(index.ToString()) ?? false) == false)
                {
                    MapsAffected[affectedCount++] = map;
                }
            });

            InitScript = property.GetString("script");
            ShuffleReactors = property.GetBool("shuffle") ?? false;
            CheckTimeOut = TimeSpan.FromSeconds(property.GetInt32("timeOut") ?? 0);
            FieldSetStart = (property.GetBool("manualstart") ?? false) == false;
            EndFieldSetAct = property.GetBool("endfieldset") ?? false;

            if (property["event"] is WzProperty evt) LoadEvents(evt);
            if (property["action"] is WzProperty act) LoadReactorActions(act);
            
            OccupiedTimer = new TIMER($"{Name} occupied timer", false);
            EnterTimer = new TIMER($"{Name} enter timer");
            TimeOutTimer = new TIMER($"{Name} timeout timer");
            // if all maps are loaded correctly, set required props and return successfully

            Maps.Values.ForEach(x => x.ParentFieldSet = this);

            _log.Info($"Loaded fieldset '{Name}' with maps {string.Join(", ", Maps.Values.Select(x => x.ID))}");
            return true;
        }

        private void LoadEvents(WzProperty eventNode)
        {
            Events = TemplateProvider.SelectOverIndexed(eventNode, (index, node) =>
            {
                var action = (FieldSetData.EventData.Actions)node.GetInt32("action");
                FieldSetData.EventData ed = action switch
                {
                    FieldSetData.EventData.Actions.ShowDesc => new FieldSetData.ShowDescEventData(),
                    FieldSetData.EventData.Actions.UpdateClock => new FieldSetData.UpdateClockEventData(),
                    FieldSetData.EventData.Actions.TogglePortal => new FieldSetData.TogglePortalEventData(),
                    FieldSetData.EventData.Actions.BroadcastMsg => new FieldSetData.BroadcastMsgEventData(),
                    FieldSetData.EventData.Actions.ResetAllMaps => new FieldSetData.ResetAllMapsEventData(),
                    FieldSetData.EventData.Actions.SnowballConclude => new FieldSetData.SnowballConcludeEventData(),
                    FieldSetData.EventData.Actions.SendChannelRedText => new FieldSetData.SendChannelRedTextEventData(),
                    FieldSetData.EventData.Actions.SpawnMob => new FieldSetData.SpawnMobEventData(),
                    _ => throw new NotImplementedException(),
                };

                ed.Index = index;
                ed.Action = action;
                ed.TimeAfter = node.GetInt32("timeAfter") ?? 0;
                ed.Args = TemplateProvider.LoadArgs(node).ToArray();

                return ed;
            }).ToArray();
        }

        private void LoadReactorActions(WzProperty actionsNode)
        {
            ReactorActions = actionsNode.Select(kvp =>
                {
                    var mapIndex = int.Parse(kvp.Key);
                    var map = Maps[mapIndex];
                    var mapInfoNode = (WzProperty)kvp.Value;
                    return TemplateProvider.SelectOverIndexed(mapInfoNode, (index, node) =>
                    {
                        var info = node.GetProperty("info");
                        var type = (FieldSetData.ReactorActionInfo.Types)info.GetInt32("type");
                        FieldSetData.ReactorActionInfo ed = type switch
                        {
                            FieldSetData.ReactorActionInfo.Types.ChangeMusic => new FieldSetData.ChangeMusicReactorActionInfo(),
                            FieldSetData.ReactorActionInfo.Types.ChangeReactorState => new FieldSetData.ChangeReactorStateReactorActionInfo(),
                            FieldSetData.ReactorActionInfo.Types.SetFieldsetVariable => new FieldSetData.SetFieldsetVariableReactorActionInfo(),
                            _ => throw new NotImplementedException(),
                        };

                        ed.Index = index;
                        ed.Args = TemplateProvider.LoadArgs(info).ToArray();
                        ed.DefinedMapIndex = mapIndex;

                        // Load all reactor name and event state data
                        foreach (var kvp in node)
                        {
                            var reactorName = kvp.Key;
                            if (kvp.Key == "info") continue;
                            var eventState = (int)kvp.Value;

                            if (!map.Reactors.Any(x => x.Value.Template.Name == reactorName))
                            {
                                _log.Error($"Did not find reactor {reactorName} in map {map} for fieldset {Name}");
                            }

                            ed.ReactorInfo.Add((reactorName, eventState));
                        }


                        return ed;
                    }).ToArray();
                })
                // Flatten the data
                .SelectMany(x => x)
                .ToArray();

        }

        public bool TryGetVar(string key, out string ret) => Variables.TryGetValue(key, out ret);
        public string GetVar(string key, string defaultValue = null) => Variables.TryGetValue(key, out var value) ? value : defaultValue;

        public string SetVar(string key, string value)
        {
            _log.Info($"Setting variable {key} to {value} in FieldSet {Name}");
            return Variables[key] = value;
        }

        public void StartFieldSetManually()
        {
            if (HasTimeout)
            {
                TimeOutTimer.Time = CheckTimeOut;
            }

            FieldSetStart = true;
        }

        public static bool Enter(string name, Character[] characters, Character leader)
        {
            return Enter(name, leader, characters: characters) == EnterError.NoError;
        }

        /// <summary>
        /// Try to enter a FieldSet as a character. 
        /// </summary>
        /// <param name="name">FieldSet name</param>
        /// <param name="leader">Person that tries to enter</param>
        /// <param name="fieldInfo">Map index of which to enter, usually 0</param>
        /// <param name="characters">All characters that should join, or null for default logic.</param>
        /// <returns></returns>
        public static EnterError Enter(string name, Character leader, int fieldInfo = 0, Character[] characters = null)
        {
            if (!Instances.TryGetValue(name, out var fs)) return EnterError.FieldNull;
            if (DisabledForMaintenance)
            {
                _log.Warn($"Unable to start {name}, disabled for maintenance");
                return EnterError.FieldSetAlreadyRunning;
            }

            var enterError = fs.Enter(leader, fieldInfo, characters);
            if (enterError != EnterError.NoError)
            {
                _log.Warn($"Unable to enter {name} with reason: {enterError}");
            }

            return enterError;
        }

        public bool StartEvent(Character initiator)
        {
            if (DisabledForMaintenance)
            {
                _log.Warn("Unable to start event, disabled for maintenance");
                return false;
            }

            EventStart = MasterThread.CurrentTime;

            RunScript(initiator, InitScript);

            EventsRan = new bool[Events.Length];

            return true;
        }

        public void End() => BanishUser(true);

        public static FieldSet Get(string name)
        {
            Instances.TryGetValue(name, out var fs);
            return fs;
        }

        public void BanishUser(bool exceptAdmin)
        {
            CastOut(Characters.Where(x => !exceptAdmin || !x.IsGM).ToArray(), 0, null, "Banished");
        }


        public static void Update(long currentTime)
        {
            foreach (var fs in Instances.Values)
            {
                if (!fs.FieldSetStart) continue;

                fs.Update();
            }
        }

        private void Update()
        {
            var characters = Characters.ToArray();

            if (Name == "ZakumBoss")
                CheckBossMap(0);

            if (Name == "Populatus")
                CheckBossMap(1);

            if (Name == "Guild1" /* && m_nGulidIDCanEnterQuest < 0 && !CastOut*/)
            {
                // TODO (one day)
            }

            if ((Name == "Wedding1" || Name == "Wedding2")
                /* && CurrentWeddingState == 1 */)
            {
                // TODO
            }

            if (Name == "Wedding4"
                /* && CurrentWeddingState == 4 */)
            {
                // TODO
            }

            if (Name == "shouwaBoss")
            {
                // CheckShouwaBossMap()
            }

            if (!EndFieldSetAct && characters.Length == 0)
            {
                return;
            }

            OccupiedTimer.Time = TimeSpan.FromSeconds(20);

            if (Name.StartsWith("Party") || Name == "MoonRabbit")
            {
                if (CheckParty(characters)) return;
            }


            if (HasTimeout && TimeOutTimer.IsExpired)
            {
                if (EndFieldSetAct)
                {
                    // TODO: DestroyClock  (does not exist in client)
                    EndFieldSetAct = false;
                }
                else
                {
                    CastOut(characters, TargetFieldID, null, "Timeout Expired");
                }
                TimeOutTimer.Reset();
            }

            if (TryToRunInitScript)
            {
                TryToRunInitScript = RunScript(characters[0], InitScript) == false;
            }

            // BMS has multiple event calls, we'll just do it in the main update loop

            DoEventActions();
        }

        private void CastOut(Character[] characters, int targetFieldID, string portal, string reason)
        {
            _log.Info($"Kicking people {characters.Length} out of {Name} because of {reason}");

            foreach (var character in characters)
            {
                var destMap = targetFieldID;
                if (destMap == 0) destMap = character.Field.ForcedReturn;
                character.ChangeMap(destMap, portal);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="characters"></param>
        /// <returns>true when people got kicked out</returns>
        private bool CheckParty(params Character[] characters)
        {
            if ((Name == "Party1" || Name == "Party3") &&
                characters.Length <= 1 &&
                GetVar("nokick") != "1")
            {
                CastOut(characters, 0, null, "Party FieldSet minimum player amount passed.");
                return true;
            }

            var partyLeader = characters[0].Party?.Leader;
            if (partyLeader != null) return false;

            // Ignore fieldsets where there's a zakum prop
            if (GetVar("zakum") != null) return false;

            CastOut(characters, 0, null, "No party");
            return true;
        }

        private static bool RunScript(Character user, string script)
        {
            if (string.IsNullOrEmpty(script)) return true;

            // Try to start the script

            if (!NpcPacket.StartScript(user, script))
            {
                _log.Error($"Unable to start script of the event, {script}");
                return false;
            }

            return true;
        }

        private void CheckBossMap(int index)
        {
            // This is not a fluke, BMS just checks the first map in the fieldset
            var doorMap = MapsAffected[0];
            var reactorName = "";

            if (index == 0)
                reactorName = "boss";

            else if (index == 1)
                reactorName = "boss2";

            var reactorState = doorMap.GetReactor(reactorName)?.State;

            var dropActivatedReactorDisabled = (reactorState ?? 0) == 0;
            if (dropActivatedReactorDisabled) return;

            if (UserCount > 0) return;

            Variables.Clear();

            foreach (var map in MapsAffected.Values)
            {
                map.Reset(ShuffleReactors);
            }
        }

        private void DoEventActions()
        {
            if (EventsRan == null) return;

            var secondsInEvent = (MasterThread.CurrentTime - EventStart) / 1000;
            for (var i = 0; i < EventsRan.Length; i++)
            {
                if (EventsRan[i]) continue;

                var ed = Events[i];

                if (secondsInEvent < ed.TimeAfter) continue;

                EventsRan[i] = true;
                DoEventAction(ed);
            }
        }

        private void DoEventAction(FieldSetData.EventData ed)
        {
            _log.Info($"Running EventAction {ed} in FieldSet {Name}");

            switch (ed)
            {
                case FieldSetData.ShowDescEventData sded:
                    MapsAffected.Values.ForEach(MapPacket.SendGMEventInstructions);
                    break;

                case FieldSetData.UpdateClockEventData { TimeLeft: 0 }:
                    _log.Warn("Ignoring timer being set to 0");
                    return;

                case FieldSetData.UpdateClockEventData uced:
                    // Original code just does the following:
                    // MapsAffected.Values.ForEach(m => MapPacket.ShowMapTimerForMap(m, TimeSpan.FromSeconds(uced.TimeLeft)));
                    ResetTimeOut(TimeSpan.FromSeconds(uced.TimeLeft));
                    break;

                case FieldSetData.TogglePortalEventData tped when !Maps.ContainsKey(tped.MapIndex):
                    _log.Error($"Found invalid {ed} for {Name}, wrong map idx");
                    return;

                case FieldSetData.TogglePortalEventData tped:
                    Maps[tped.MapIndex].EnablePortal(tped.PortalName, tped.Enable);
                    break;

                case FieldSetData.ResetAllMapsEventData ramed:
                    MapsAffected.Values.ForEach(x => x.Reset(false));
                    break;

                case FieldSetData.SendChannelRedTextEventData scrted:
                    MessagePacket.SendText(MessagePacket.MessageTypes.RedText, scrted.Message, null, MessagePacket.MessageMode.ToChannel);
                    break;

                case FieldSetData.SpawnMobEventData smed when !Maps.ContainsKey(smed.MapIndex):
                    _log.Error($"Found invalid {ed} for {Name}, wrong map idx");
                    return;

                case FieldSetData.SpawnMobEventData smed:
                    {
                        var map = Maps[smed.MapIndex];

                        var fhUnderneath = map.GetFootholdUnderneath(smed.X, smed.Y, out var intersectY);

                        Maps[smed.MapIndex].CreateMobWithoutMobGen(
                            smed.TemplateID,
                            new Pos((short)smed.X, intersectY),
                            fhUnderneath?.ID ?? 0,
                            type: MobAppear.Normal
                        );
                        break;
                    }

                case FieldSetData.BroadcastMsgEventData bmed:
                    BroadcastMsg((MessagePacket.MessageTypes)bmed.Type, bmed.Message, bmed.TemplateID);
                    break;

                default:
                    _log.Error($"Unhandled event {ed} in fieldset {Name}!!!");
                    break;
            }
        }

        public void DoReactorAction(FieldSetData.ReactorActionInfo rai)
        {
            _log.Info($"Running ReactorAction {rai} in FieldSet {Name}");
            switch (rai)
            {
                case FieldSetData.ChangeReactorStateReactorActionInfo crsrai when !Maps.ContainsKey(crsrai.MapIndex):
                    _log.Error($"Found invalid {rai} for {Name}, wrong map idx");
                    return;

                case FieldSetData.ChangeReactorStateReactorActionInfo crsrai:
                    {
                        var map = Maps[crsrai.MapIndex];

                        var reactor = map.GetReactor(crsrai.ReactorName);
                        if (reactor == null)
                        {
                            _log.Error($"Did not find rector {crsrai.ReactorName} in reactor action info!");
                            return;
                        }

                        var newReactorState = crsrai.State ?? (byte)(reactor.State + 1);

                        map.SetReactorState(crsrai.ReactorName, newReactorState);

                        // There's some extra logic here for guild quest (statueQuestion, statueAnswer)

                        break;
                    }

                case FieldSetData.ChangeMusicReactorActionInfo cmrai when !Maps.ContainsKey(cmrai.MapIndex):
                    _log.Error($"Found invalid {rai} for {Name}, wrong map idx");
                    return;


                case FieldSetData.ChangeMusicReactorActionInfo cmrai:
                    {
                        var map = Maps[cmrai.MapIndex];
                        MapPacket.EffectChangeBGM(map, cmrai.Song);
                        break;
                    }

                case FieldSetData.SetFieldsetVariableReactorActionInfo sfvrai:
                    {
                        if (
                            Name == "ZakumBoss" ||
                            sfvrai.VariableName == "boss" ||
                            sfvrai.VariableName == "boss2" ||
                            UserCount > 0
                        )
                        {
                            SetVar(sfvrai.VariableName, sfvrai.Value);
                        }

                        break;
                    }
            }

        }

        public int GetFieldIndex(Map field)
        {
            foreach (var kvp in Maps)
            {
                if (kvp.Value == field) return kvp.Key;
            }

            return -1;
        }

        public void CheckReactorAction(Map field, string reactorName, int eventState, long eventTime)
        {
            var fieldIndex = GetFieldIndex(field);

            if (fieldIndex < 0) return;

            var timeLeftUntilEvent = eventTime - MasterThread.CurrentTime;

            foreach (var reactorActionInfo in ReactorActions.Where(x => x.DefinedMapIndex == fieldIndex))
            {
                if (!reactorActionInfo.ReactorInfo.Any(x => x.ReactorName == reactorName && x.EventState == eventState))
                {
                    // No reactor state found.
                    continue;
                }

                MasterThread.RepeatingAction.Start(
                    $"Reactor {reactorName} FieldSet Action for event {eventState}",
                    () => DoReactorAction(reactorActionInfo),
                    timeLeftUntilEvent,
                    0
                );
            }
        }

        public void BroadcastClock()
        {
            if (FieldSetStart && HasTimeout)
            {
                MapPacket.ShowMapTimerForCharacters(Characters, TimeOutTimer.TimeLeft);
            }
        }

        public void BroadcastMsg(MessagePacket.MessageTypes type, string msg, int npcTemplateID)
        {
            // NOTE: npcTemplateID is not used
            MessagePacket.SendTextMaps(type, msg, MapsAffected.Values.ToArray());
        }

        public enum EnterError
        {
            FieldNull = -1,
            NoError = 0,
            NeedsParty = 1,
            WrongNumberOfPartyMembers = 2,
            PartyMemberLevelRequirementWrong = 3,

            FieldSetAlreadyRunning = 4,

            SomeoneHasItemHeSheShouldntHave = 6,

            CharacterDataInvalid = 9,
        }

        public EnterError Enter(Character chr, int fieldInfo, Character[] characters = null)
        {
            var party = chr.Party;

            if (characters == null)
            {
                if (party != null)
                {
                    characters = party.GetAvailablePartyMembers().Select(Server.Instance.GetCharacter).Where(x => x != null).ToArray();
                }
                else
                {
                    characters = new[] { chr };
                }
            }
            else
            {
                // Make sure we add the leader, if it wasn't there yet.
                characters = characters.Append(chr).Distinct().ToArray();
            }

            // If there is no party, don't count any party members
            var partyMemberCount = party != null ? characters.Length : 0;

            bool checkLevels(byte? minLevel, byte? maxLevel)
            {
                return characters
                    .Select(x => x.Level)
                    .All(x => x >= (minLevel ?? 0) && x <= (maxLevel ?? 255));
            }

            switch (Name)
            {
                case "Party1":
                    if (!chr.IsGM)
                    {
                        if (party == null) return EnterError.NeedsParty;
                        if (partyMemberCount < 3 || partyMemberCount > 4) return EnterError.WrongNumberOfPartyMembers;
                        if (!checkLevels(20, 30)) return EnterError.PartyMemberLevelRequirementWrong;
                    }

                    break;

                case "Party2":
                    if (!chr.IsGM)
                    {
                        if (party == null) return EnterError.NeedsParty;

                        if (!characters.All(x => x.Quests.HasQuest(7000000))) return EnterError.PartyMemberLevelRequirementWrong;
                    }

                    break;
            }

            if (!TryEnter(characters, fieldInfo, chr.ID)) return EnterError.FieldSetAlreadyRunning;

            StartEvent(chr);
            TryToRunInitScript = true;

            return EnterError.NoError;
        }

        public bool CantEnter()
        {
            OccupiedTimer.ResetIfExpired();
            EnterTimer.ResetIfExpired();

            return OccupiedTimer.IsSet || EnterTimer.IsSet;
        }

        public bool TryEnter(Character[] characters, int fieldIdx, int enterChar)
        {
            if (CantEnter())
            {
                return false;
            }

            // Block access for 20 seconds
            EnterTimer.Time = TimeSpan.FromSeconds(20);

            OnEnter(characters);

            var destMap = Maps[fieldIdx];
            foreach (var character in characters)
            {
                character.ChangeMap(destMap.ID);
            }

            return true;
        }

        public void OnEnter(params Character[] characters)
        {
            Variables.Clear();

            MapsAffected.Values.ForEach(x => x.Reset(ShuffleReactors));

            if (HasTimeout)
            {
                TimeOutTimer.Time = CheckTimeOut;
            }

            TargetFieldID = 0;
        }

        public void OnUserEnterField(Map map, Character chr)
        {
            if (FieldSetStart && HasTimeout)
            {
                MapPacket.ShowMapTimerForCharacter(chr, TimeOutTimer.TimeLeft);
            }
        }

        public void ResetTimeOut(TimeSpan ts)
        {
            if (!FieldSetStart) return;

            // if (!TimeOutTimer.IsWaiting) return;

            TimeOutTimer.Time = ts;
            BroadcastClock();
        }

        public static bool IsAvailable(string key)
        {
            return !DisabledForMaintenance && Instances.TryGetValue(key, out var fs) && !fs.CantEnter();
        }

        public void SendPacket(Packet packet, Character except = null)
        {
            Characters.Where(x => x != except).ForEach(x => x.SendPacket(packet));
        }
    }
}
