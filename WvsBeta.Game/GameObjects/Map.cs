using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.GameObjects;
using WvsBeta.Game.Packets;
using WvsBeta.SharedDataProvider.Templates;
using WzTools.Objects;

namespace WvsBeta.Game
{
    public class Map
    {
        protected static ILog log = LogManager.GetLogger("MapLogger");

        public double m_dIncRate_Exp = 1.0;
        public double m_dIncRate_Drop = 1.0;

        public string StreetName { get; set; }
        public string Name { get; set; }

        public string FullName
        {
            get
            {
                var tmp = Name;
                if (StreetName != null) tmp += " - " + StreetName;
                return tmp;
            }
        }
        public int ID { get; }
        public bool Premium => ID / 10000000 == 19;
        public bool EventMap => ID / 1000000 % 100 == 9;
        public int ForcedReturn { get; set; }
        public int ReturnMap { get; set; }
        public bool Town { get; set; }
        public FieldLimit Limitations { get; set; }
        public double MobRate { get; set; }
        public bool HasClock { get; set; }
        public bool DisableScrolls { get; set; } = false;
        public bool AcceptPersonalShop { get; set; } = false;
        public bool DisableGoToCashShop { get; set; } = false;
        public bool DisableChangeChannel { get; set; } = false;
        public bool DisableCreditsUsage { get; set; } = false;
        public int ProtectItem { get; set; }
        public bool HideRewardInfo { get; set; }

        public bool EverlastingDrops
        {
            get => DropPool.DropEverlasting;
            set => DropPool.DropEverlasting = value;
        }

        public FieldSet ParentFieldSet { get; set; } = null;

        public short DecreaseHP { get; set; }
        public bool Swim { get; set; }
        public short TimeLimit { get; set; } = 0;

        public int WeatherID { get; set; }
        public string WeatherMessage { get; set; }
        public bool WeatherIsAdmin { get; set; }

        public long WeatherTime { get; set; }

        public int JukeboxID { get; set; } = -1;
        public string JukeboxUser { get; set; }
        public long JukeboxTime { get; set; }

        public int MobCapacityMin;
        public int MobCapacityMax;

        private static LoopingID _objectIDs { get; } = new LoopingID();

        public struct BALLOONENTRY
        {
            public Pos Host;
            public BalloonType BalloonType;
        }

        private LoopingID BalloonsSNs { get; } = new LoopingID();
        private Dictionary<int, BALLOONENTRY> Balloons { get; } = new Dictionary<int, BALLOONENTRY>();

        public Foothold[] Footholds { get; private set; }
        public List<NpcLife> NPCs { get; } = new List<NpcLife>();
        public Dictionary<string, Portal> Portals { get; } = new Dictionary<string, Portal>();
        public List<Portal> SpawnPoints { get; } = new List<Portal>();
        public List<Portal> DoorPoints { get; } = new List<Portal>();
        public Dictionary<int, Seat> Seats { get; } = new Dictionary<int, Seat>();
        public Dictionary<int, Mist> SpawnedMists { get; } = new Dictionary<int, Mist>();
        public List<MessageBox> MessageBoxes { get; } = new List<MessageBox>();
        public Dictionary<int, Reactor> Reactors { get; } = new Dictionary<int, Reactor>();
        public List<short> UsableReactorIDs { get; } = new List<short>();
        public Rectangle MBR { get; private set; }
        public Rectangle VRLimit { get; private set; }
        public Rectangle ReallyOutOfBounds { get; private set; }
        public List<MapArea> MapAreas { get; } = new List<MapArea>();

        public List<short> UsedSeats { get; } = new List<short>();

        public Dictionary<int, Mob> Mobs { get; } = new Dictionary<int, Mob>();
        public List<MobGenItem> MobGen { get; } = new List<MobGenItem>();
        private long _lastCreateMobTime;
        public HashSet<Character> Characters { get; } = new HashSet<Character>();
        public IEnumerable<Character> GetRegularPlayers => Characters.Where(x => !x.IsGM);
        public IEnumerable<Character> GetGMs => Characters.Where(x => x.IsGM);

        public bool PeopleInMap => Characters.Count > 0;

        public DropPool DropPool { get; }
        public readonly DoorManager DoorPool;
        public readonly SummonPool Summons;

        public Dictionary<string, long> PlayersThatHaveBeenHere { get; } = new Dictionary<string, long>();
        public Dictionary<int, int> MobKillCount { get; } = new Dictionary<int, int>();

        public const double MAP_PREMIUM_EXP = 1.0;
        public bool PortalsOpen { get; set; } = true;
        public Action<Character, Map> OnEnter { get; set; }
        public Action<Character, Map> OnExit { get; set; }

        public Action<Map> OnTimerEnd { get; set; }
        public long TimerEndTime { get; set; }

        public bool ChatEnabled { get; set; }

        public void StartTimer(long seconds)
        {
            TimerEndTime = MasterThread.CurrentTime + (seconds * 1000);
            SendMapTimer(null);
        }


        public Map(int id)
        {
            ID = id;

            DropPool = new DropPool(this);

            MobRate = 1.0;
            _lastCreateMobTime = MasterThread.CurrentTime;
            ChatEnabled = true;

            DoorPool = new DoorManager(this);
            Summons = new SummonPool(this);
        }

        public override string ToString()
        {
            return $"{ID} ({FullName})";
        }

        internal Mob GetMob(int SpawnID) => Mobs.TryGetValue(SpawnID, out var ret) ? ret : null;
        internal NpcLife GetNPC(int SpawnID) => NPCs.FirstOrDefault(n => n.SpawnID == SpawnID);
        public Character GetPlayer(int id) => Characters.FirstOrDefault(a => a.ID == id);
        public IEnumerable<Character> GetInParty(int ptId) => Characters.Where(c => c.PartyID == ptId);
        public List<int> GetIDsInParty(int ptId) => GetInParty(ptId).Select(x => x.ID).ToList();

        protected bool StartFieldSet(Character character)
        {
            if (ParentFieldSet == null)
            {
                MessagePacket.SendNotice(character, "This event map has no fieldset?");
                return false;
            }

            if (!ParentFieldSet.StartEvent(character))
            {
                MessagePacket.SendNotice(character, "Unable to start fieldset. Check logs.");
                return false;
            }

            MessagePacket.SendNotice(character, "Fieldset started!");
            return true;
        }

        protected bool StopFieldSet(Character character)
        {
            if (ParentFieldSet == null)
            {
                MessagePacket.SendNotice(character, "This event map has no fieldset?");
                return false;
            }

            if (!ParentFieldSet.FieldSetStart)
            {
                MessagePacket.SendNotice(character, "Unable to stop fieldset, not started");
                return false;
            }


            ParentFieldSet.End();
            MessagePacket.SendNotice(character, "Fieldset stopped!");
            return true;
        }

        public virtual bool FilterAdminCommand(Character character, CommandHandling.CommandArgs command)
        {
            switch (command.Command)
            {
                case "mute":
                    MessagePacket.SendNotice(character, "People cannot chat anymore in this map.");
                    ChatEnabled = false;
                    return true;
                case "unmute":
                    MessagePacket.SendNotice(character, "People can start chatting in this map.");
                    ChatEnabled = true;
                    return true;
            }

            if (EventMap)
            {
                if (command.Command == "start")
                {
                    StartFieldSet(character);
                    return true;
                }
                else if (command.Command == "stop" || command.Command == "end")
                {
                    StopFieldSet(character);
                    return true;
                }
            }

            return false;
        }

        public virtual void EncodeFieldSpecificData(Character chr, Packet packet) { }

        public virtual bool HandlePacket(Character character, Packet packet, ClientMessages opcode) => false;

        public enum BalloonType
        {
            Miniroom = 0,
            MessageBox = 1,
            MiniroomShop = 2,
        }

        public bool CheckBalloonAvailable(Pos host, BalloonType type)
        {
            var rect = new Rectangle(host.X, host.Y, 0, 0);
            switch (type)
            {
                case BalloonType.Miniroom: rect.Inflate(90, 60); break;
                case BalloonType.MessageBox: rect.Inflate(60, 80); break;
                case BalloonType.MiniroomShop: rect.Inflate(120, 80); break;
            }

            if (Balloons.Values.Any(x => x.BalloonType == type && rect.Contains(x.Host.X, x.Host.Y)))
                return false;

            if (NPCs.Any(x => rect.Contains(x.X, x.Y))) return false;
            if (Reactors.Values.Any(x => rect.Contains(x.X, x.Y))) return false;
            if (Portals.Values.Any(x => rect.Contains(x.X, x.Y))) return false;

            return true;
        }

        public int SetBalloon(Pos host, BalloonType type)
        {
            var sn = BalloonsSNs.NextValue();

            Balloons[sn] = new BALLOONENTRY
            {
                BalloonType = type,
                Host = new Pos(host),
            };

            return sn;
        }

        public void RemoveBalloon(int sn)
        {
            Balloons.Remove(sn);
        }

        public struct MobKillCountInfo
        {
            public int mapId;
            public int mobId;
            public int killCount;
        }


        public void FlushMobKillCount()
        {
            if (MobKillCount.Count == 0) return;
            foreach (var keyValuePair in MobKillCount)
            {
                log.Info(new MobKillCountInfo
                {
                    killCount = keyValuePair.Value,
                    mapId = ID,
                    mobId = keyValuePair.Key
                });
            }
            MobKillCount.Clear();
        }

        private long lastDamageGiven = 0;
        public virtual void MapTimer(long pNow)
        {
            DropPool.TryExpire(pNow);

            TryExpireMessageBoxes(pNow);

            if (SpawnedMists.Count > 0)
                UpdateMists(pNow);
            else
            {
                // These are already updated in UpdateMists
                Mobs.Values.ToArray().ForEach(x => x.UpdateDeads(pNow));
            }

            if (WeatherTime > 0 && pNow > WeatherTime) StopWeatherEffect();
            if (JukeboxTime > 0 && pNow > JukeboxTime) StopJukeboxEffect();

            if (PeopleInMap)
            {
                // Disconnect husks that have sold their items/closed shops
                Characters.Where(x => x.HuskMode && !x.MiniRoomBalloon && (!x.IsInMiniRoom || !x.RoomV2.DoNotRemoveMe)).ToArray().ForEach(x =>
                {
                    x.WrappedLogging(() =>
                    {
                        log.Info($"Disconnecting because no shop left (closed)");
                        x.Disconnect();
                    });
                });


                TryCreateMobs(pNow, false);

                // Toggle spawns
                NPCs.ForEach(x =>
                {
                    var shouldBeSpawned = x.IsActive();
                    var isSpawned = x.IsSpawned;
                    if (shouldBeSpawned && !isSpawned) x.Spawn();
                    else if (!shouldBeSpawned && isSpawned) x.Despawn();
                });
                
                var ft = BuffStat.GetTimeForBuff();
                ForEachCharacters(x =>
                {
                    x.PrimaryStats.CheckExpired(ft);
                    x.Summons.Update(pNow);
                });


                // Find new controllers

                Mobs.Values
                    .Where(x => x.IsControlled && (MasterThread.CurrentTime - x.LastMove) > 10000)
                    .ForEach(x => FindNewController(x, null));

                Mobs.Values
                    .Where(x => !x.IsControlled)
                    .ForEach(x => FindNewController(x, x.Controller));


                if (lastDamageGiven == 0 || (pNow - lastDamageGiven) > 10 * 1000)
                {
                    lastDamageGiven = pNow;
                    if (DecreaseHP > 0)
                    {
                        // Damage
                        ForEachCharacters(character =>
                        {
                            if (ProtectItem != 0 && character.Inventory.HasEquipped(ProtectItem))
                            {
                                // dangit.
                                return;
                            }

                            var actualDamage = DecreaseHP;
                            if (character.PrimaryStats.BuffThaw.IsSet(pNow))
                            {
                                var reduction = character.PrimaryStats.BuffThaw.N;
                                if (Swim) reduction *= -1;
                                actualDamage -= reduction;
                            }

                            // TODO: If we've got snowboots, do no damage
                            if (actualDamage > 0)
                            {
                                character.DamageHP(actualDamage);
                            }

                            // TODO: If pets and consumeHP flag set, trigger pet consume pot
                        });
                    }
                    else if (DecreaseHP < 0)
                    {
                        // Heal
                    }
                }

                Reactors.Values.ForEach(r =>
                {
                    r.UpdateOwnerInfo(pNow);
                    r.DoActionByUpdateEvent(pNow);
                    r.TryRespawn(pNow);
                });
            }

            if (MasterThread.CurrentTime >= TimerEndTime)
            {
                OnTimerEnd?.Invoke(this);
            }

            DoorPool.Update(pNow);
        }

        public void UpdateMists(long pNow)
        {
            var tmplist = SpawnedMists.Values.Where(x => x.Time < pNow).ToArray();
            foreach (var mist in tmplist)
            {
                MistPacket.SendMistDespawn(mist);
                SpawnedMists.Remove(mist.SpawnID);
            }

            var mobsCopy = Mobs.Values.ToArray();
            foreach (var mob in mobsCopy)
            {
                mob.UpdateDeads(pNow);
                if (mob.DeadAlreadyHandled) continue;
                if (mob.HP == 1) continue; // Already almost dead

                var fart = SpawnedMists.Values.FirstOrDefault((a) =>
                {
                    return
                        !a.MobMist &&
                        (
                            mob.Position.X >= a.LT_X &&
                            mob.Position.Y >= a.LT_Y &&
                            mob.Position.X <= a.RB_X &&
                            mob.Position.Y <= a.RB_Y
                        );
                });
                if (fart != null)
                {
                    var sld = DataProvider.Skills[fart.SkillID].Levels[fart.SkillLevel];
                    if (Rand32.NextBetween(0, 100) < sld.Property)
                    {
                        mob.DoPoison(fart.OwnerID, fart.SkillLevel, sld.GetExpireTime(), fart.SkillID, sld.MagicAttack, 0);
                    }
                }
            }
        }

        public virtual void LoadExtraData(WzProperty mapProperty, WzProperty infoProperty)
        {

        }

        public bool IsBoostedMobGen => Characters.Count > MobCapacityMin / 2;
        public int ForcedMobGenLimit = -1;

        public int GetCapacity()
        {
            if (ForcedMobGenLimit > -1)
                return ForcedMobGenLimit;

            if (Limitations.HasFlag(FieldLimit.NoMobCapacityLimit))
                return MobGen.Count;


            if (!IsBoostedMobGen) return MobCapacityMin;

            if (Characters.Count < MobCapacityMin * 2)
            {
                return (MobCapacityMin + (MobCapacityMax - MobCapacityMin) * (2 * Characters.Count - MobCapacityMin) / (3 * MobCapacityMin));
            }
            else
            {
                return MobCapacityMax;
            }
        }


        public void TryCreateMobs(long tCur, bool bReset)
        {
            if (!bReset && tCur - _lastCreateMobTime < 7000)
                return;

            // This should be done at the end, but it gives odd spawns
            _lastCreateMobTime = tCur;

            var capacity = GetCapacity();
            if (capacity < 0) return;

            var remainCapacity = capacity - Mobs.Count;

            if (remainCapacity <= 0)
                return;

            var mobGenPossible = new List<MobGenItem>(remainCapacity);
            // Fill the list with currently shown mobs
            var posList = Mobs.Values.Select(x => x.Position).ToList();

            var possibleMobsToSpawn = MobGen.Where(x => x.IsActive());

            foreach (var mgi in possibleMobsToSpawn)
            {
                // TODO: Check if we can summon this mob (MobExcept)...
                var add_in_front = true;
                var regenInterval = mgi.RegenInterval;
                if (regenInterval == 0)
                {
                    var anyMobSpawned = posList.Count != 0;
                    if (anyMobSpawned)
                    {
                        // Figure if any mob is already spawned
                        var rect = Rectangle.FromLTRB(
                            mgi.X - 100, mgi.Y - 100,
                            mgi.X + 100, mgi.Y + 100);

                        var canSpawn = !posList.Exists(vec => rect.Contains(vec.X, vec.Y));
                        if (!canSpawn) continue;
                    }
                    else
                    {
                        // Add to end
                        add_in_front = false;
                    }
                }
                else if (regenInterval < 0)
                {
                    if (!bReset) continue;
                }
                else
                {
                    if (mgi.MobCount != 0) continue;
                    if (tCur - mgi.RegenAfter < 0) continue;
                }

                if (add_in_front)
                    mobGenPossible.Insert(0, mgi);
                else
                    mobGenPossible.Add(mgi);

                posList.Add(new Pos(mgi.X, mgi.Y));
            }

            while (mobGenPossible.Count > 0 && remainCapacity != 0)
            {
                var mgi = mobGenPossible[0];

                if (mgi.RegenInterval == 0)
                {
                    // Take random
                    mgi = mobGenPossible[(int)(Rand32.Next() % mobGenPossible.Count)];
                }

                mobGenPossible.Remove(mgi);

                if (CreateMob(mgi.ID, mgi, new Pos(mgi.X, mgi.Y), mgi.Foothold, type: MobAppear.Regen) != -1)
                {
                    remainCapacity--;
                }

            }

        }

        public void AddSeat(Seat ST)
        {
            Seats.Add(ST.ID, ST);
        }

        private static IEnumerable<T> InRange<T>(IEnumerable<T> elements, Pos pAround, Pos pLeftTop, Pos pRightBottom)
            where T : MovableLife
        {
            return elements.Where(mob => MovableInRange(mob, pAround, pLeftTop, pRightBottom));
        }

        private static bool MovableInRange(MovableLife mob, Pos pAround, Pos pLeftTop, Pos pRightBottom)
        {
            return (
                (mob.Position.Y >= pAround.Y + pLeftTop.Y) && (mob.Position.Y <= pAround.Y + pRightBottom.Y) &&
                (mob.Position.X >= pAround.X + pLeftTop.X) && (mob.Position.X <= pAround.X + pRightBottom.X)
            );
        }

        public IEnumerable<Mob> GetMobsInRange(Pos pAround, Rectangle pRect) => GetMobsInRange(pAround, new Pos((short)pRect.Left, (short)pRect.Top), new Pos((short)pRect.Right, (short)pRect.Bottom));
        public IEnumerable<Mob> GetMobsInRange(Pos pAround, Pos pLeftTop, Pos pRightBottom) => InRange(Mobs.Values, pAround, pLeftTop, pRightBottom);
        public IEnumerable<Character> GetCharactersInRange(Pos pAround, Rectangle pRect) => GetCharactersInRange(pAround, new Pos((short)pRect.Left, (short)pRect.Top), new Pos((short)pRect.Right, (short)pRect.Bottom));
        public IEnumerable<Character> GetCharactersInRange(Pos pAround, Pos pLeftTop, Pos pRightBottom) => InRange(Characters, pAround, pLeftTop, pRightBottom);

        public void CreateMist(MovableLife pLife, int pSpawnID, int pSkillID, byte pSkillLevel, int pTimeMs, int pX1, int pY1, int pX2, int pY2, short delay)
        {
            int x1, x2, y1, y2;
            x1 = pX1 + pLife.Position.X;
            y1 = pY1 + pLife.Position.Y;
            x2 = pX2 + pLife.Position.X;
            y2 = pY2 + pLife.Position.Y;


            var mist = new Mist(pSkillID, pSkillLevel, this, pSpawnID, pTimeMs + delay, x1, y1, x2, y2);
            SpawnedMists.Add(mist.SpawnID, mist);

            MistPacket.SendMistSpawn(mist, null, delay);
        }

        public void SetFootholds(List<Foothold> FHs)
        {
            // Cleanup footholds:
            // If there is a vertical foothold, make footholds pointing to that as next/prev be zeroed instead.
            var verticalFHs = FHs.Where(x => x.X1 == x.X2 && Math.Abs(x.Y1 - x.Y2) > 3).Select(x => x.ID).ToList();

            Footholds = FHs.Select(x =>
            {
                var next = x.NextIdentifier;
                if (verticalFHs.Contains(next)) next = 0;
                var prev = x.PreviousIdentifier;
                if (verticalFHs.Contains(prev)) prev = 0;

                return new Foothold
                {
                    ID = x.ID,
                    NextIdentifier = next,
                    PreviousIdentifier = prev,
                    X1 = x.X1,
                    Y1 = x.Y1,
                    X2 = x.X2,
                    Y2 = x.Y2,
                };
            }).ToArray();
        }

        private static LoopingID _npcidCounter { get; } = new LoopingID();
        public object AddLife(Life LF, WzProperty prop)
        {
            if (LF.Type == 'm')
            {
                var mgi = new MobGenItem(LF, null);
                mgi.LoadLimitedDataFromProp(prop);
                MobGen.Add(mgi);
                
                return mgi;
            }
            else if (LF.Type == 'n')
            {
                var npc = new NpcLife(LF, this)
                {
                    SpawnID = (uint)_npcidCounter.NextValue()
                };
                
                npc.LoadLimitedDataFromProp(prop);
                NPCs.Add(npc);

                if (DataProvider.NPCs.TryGetValue(LF.ID, out var npcData))
                {
                    if (npcData.Reg != null)
                        npc.Vars = new Dictionary<string, string>(npcData.Reg);
                }

                if (Server.Instance.Initialized)
                {
                    // Show new NPC to all characters
                    npc.Spawn();
                }
                else
                {
                    npc.IsSpawned = true;
                }

                return npc;
            }
            else
            {
                throw new Exception($"Unable to handle this Life type: {LF.Type}");
            }
        }

        public void GenerateMBR(Rectangle VRLimit)
        {
            var Left = int.MaxValue;
            var Top = int.MaxValue;
            var Right = int.MinValue;
            var Bottom = int.MinValue;

            foreach (var Foothold in Footholds)
            {
                var mostLeft = Math.Min(Foothold.X1, Foothold.X2);
                var mostRight = Math.Max(Foothold.X1, Foothold.X2);
                
                var mostTop = Math.Min(Foothold.Y1, Foothold.Y2);
                var mostBottom = Math.Max(Foothold.Y1, Foothold.Y2);

                Left = Math.Min(Left, mostLeft + 30);
                Right = Math.Max(Right, mostRight - 30);
                Top = Math.Min(Top, mostTop - 300);
                Bottom = Math.Max(Bottom, mostBottom + 10);
                
            }
            
            if (VRLimit != Rectangle.Empty)
            {
                this.VRLimit = VRLimit;

                if (VRLimit.Left > 0) Left = Math.Max(VRLimit.Left + 20, Left);
                if (VRLimit.Right > 0) Right = Math.Min(VRLimit.Right - 20, Right);
                if (VRLimit.Top > 0) Top = Math.Max(VRLimit.Top + 65, Top);
                if (VRLimit.Bottom > 0) Bottom = Math.Min(VRLimit.Bottom, Bottom);
            }

            MBR = Rectangle.FromLTRB(Left, Top, Right, Bottom);
            MBR = Rectangle.Inflate(MBR, 10, 10);

            ReallyOutOfBounds = Rectangle.Inflate(MBR, 60, 60);

            const bool useCorrectSpawnFormula = false;

            if (useCorrectSpawnFormula)
            {
                (MobCapacityMin, MobCapacityMax) = CalculateMobCapacity(MBR, MobRate);
            }
            else
            {
                CalculateOldCapacity(VRLimit);
            }
        }

        public static (int min, int max) CalculateMobCapacity(Rectangle MBR, double MobRate)
        {
            var minCapacity = 0;
            var maxCapacity = 0;

            var MobX = Math.Max(MBR.Width, 800);
            var MobY = Math.Max(MBR.Height - 450, 600);

            minCapacity = (int)(((MobY * MobX) * MobRate) * 0.0000078125);
            minCapacity = Math.Max(minCapacity, 1);
            minCapacity = Math.Min(40, minCapacity);
            maxCapacity = minCapacity * 2;

            return (minCapacity, maxCapacity);
        }
        
        /// <summary>
        /// This function was the original MBR implementation. When we fixed it (items dropping out of bounds),
        /// we actually reduced the mob spawns to 90% to 50% of the old spawn.
        /// </summary>
        /// <param name="VRLimit"></param>
        public void CalculateOldCapacity(Rectangle VRLimit)
        {
            int Left = int.MaxValue;
            int Top = int.MaxValue;
            int Right = int.MinValue;
            int Bottom = int.MinValue;

            foreach (var Foothold in Footholds)
            {
                if (Foothold.X1 < Left) Left = Foothold.X1;
                if (Foothold.Y1 < Top) Top = Foothold.Y1;
                if (Foothold.X2 < Left) Left = Foothold.X2;
                if (Foothold.Y2 < Top) Top = Foothold.Y2;
                if (Foothold.X1 > Right) Right = Foothold.X1;
                if (Foothold.Y1 > Bottom) Bottom = Foothold.Y1;
                if (Foothold.X2 > Right) Right = Foothold.X2;
                if (Foothold.Y2 > Bottom) Bottom = Foothold.Y2;
            }
            

            Left += 30;
            Top -= 300;
            Right -= 30;
            Bottom += 10;

            if (VRLimit != Rectangle.Empty)
            {
                if (VRLimit.Left + 20 < Left) Left = VRLimit.Left + 20;
                if (VRLimit.Top + 65 < Top) Top = VRLimit.Top + 65;
                if (VRLimit.Right - 5 > Right) Right = VRLimit.Right - 5;
                if (VRLimit.Bottom > Bottom) Bottom = VRLimit.Bottom;
            }

            Rectangle mbr;

            mbr = Rectangle.FromLTRB(Left + 10, Top - 375, Right - 10, Bottom + 60);
            mbr = Rectangle.Inflate(mbr, 10, 10);
            
            (MobCapacityMin, MobCapacityMax) = CalculateMobCapacity(mbr, MobRate);
        }

        protected bool initialSpawnDone = false;
        private void SummonAllLife()
        {
            if (initialSpawnDone) return;
            initialSpawnDone = true;
            TryCreateMobs(MasterThread.CurrentTime, true);
        }

        public void EnablePortal(string name, bool open = true)
        {
            Portals[name].Enabled = open;
        }

        public void AddPortal(Portal portal)
        {
            switch (portal.Name)
            {
                case "sp":
                    SpawnPoints.Add(portal);
                    break;
                case "tp":
                    DoorPoints.Add(portal);
                    break;
                default:
                    {
                        if (Portals.ContainsKey(portal.Name))
                        {
                            log.Warn($"Duplicate portal, Name: {portal.Name} Map: {this}");
                        }
                        Portals[portal.Name] = portal;

                        break;
                    }
            }
        }

        public Portal GetRandomStartPoint()
        {
            var spawnPortalIndex = Rand32.Next() % SpawnPoints.Count;
            return SpawnPoints[(int)spawnPortalIndex];
        }

        public Portal GetClosestStartPoint(Pos position)
        {
            return SpawnPoints.OrderBy(x => new Pos(x.X, x.Y) - position).FirstOrDefault();
        }

        public virtual void RemovePlayer(Character chr, bool gmhide = false)
        {
            if (!gmhide && !Characters.Contains(chr)) return;

            if (!gmhide)
            {
                // Make sure we deduce for the time the user has been in this map
                chr.RateCredits.TryDeductCredits(MasterThread.CurrentTime);

                Characters.Remove(chr);
                PetsPacket.SendRemovePet(chr, PetsPacket.DespawnReason.OrderByUser);
                OnExit?.Invoke(chr, this);
            }

            if (chr.MapChair != -1)
            {
                UsedSeats.Remove(chr.MapChair);
                chr.MapChair = -1;
                MapPacket.SendCharacterSit(chr, -1);
            }

            RemoveController(chr);
            chr.Summons.RemovePuppet();

            DropPool.OnLeave(chr);

            ForEachCharacters(p =>
            {
                if (gmhide)
                {
                    if (p.IsGM) return;
                }
                else
                {
                    if (!chr.IsShownTo(p)) return;
                }
                MapPacket.SendCharacterLeavePacket(chr.ID, p);
            });
        }

        public int TownMap
        {
            get
            {
                if (ReturnMap != Constants.InvalidMap) return ReturnMap;
                if (ForcedReturn != Constants.InvalidMap) return ForcedReturn;
                return ID;
            }
        }

        public void LeavePlayer(Character chr)
        {
            // Player exits entirely
            RemovePlayer(chr);

            int newMap;
            // Make sure it isnt dead
            if (chr.PrimaryStats.HP == 0)
            {
                chr.ModifyHP(50, false);

                // Remove all buffs
                chr.PrimaryStats.Reset(false);

                newMap = TownMap;
            }
            else
            {
                newMap = ForcedReturn;
                if (newMap == Constants.InvalidMap)
                {
                    newMap = ID;
                }
            }


            var map = MapProvider.Maps[newMap];
            chr.Field = map;

            // If you did not get kicked out, this should place you on a portal near you.
            if (ForcedReturn == Constants.InvalidMap)
            {
                // Pick the one closest to the user
                chr.MapPosition = map.GetClosestStartPoint(chr.Position).ID;
            }
            else
            {
                chr.MapPosition = map.GetRandomStartPoint().ID;
            }
        }

        public virtual void AddPlayer(Character chr)
        {
            PlayersThatHaveBeenHere[chr.Name] = MasterThread.CurrentTime;

            Characters.Add(chr);

            SummonAllLife();
            ShowObjects(chr);

            if (chr.GMHideEnabled)
                AdminPacket.Hide(chr, true);

            ParentFieldSet?.OnUserEnterField(this, chr);

            OnEnter?.Invoke(chr, this);

            var shownPlayers = Characters.Where(x => !x.IsGM).ToArray();
            if (chr.IsGM && shownPlayers.Length != 0)
            {
                var playersonline = "Players in map (" + shownPlayers.Length + "): \r\n";
                playersonline += string.Join(
                    ", ",
                    shownPlayers.Select(x => x.Name + (x.IsAFK ? " (AFK)" : ""))
                );
                MessagePacket.SendNotice(chr, playersonline);
            }

            // Nuke the stats
            BuffPacket.ResetTempStats(chr, ~chr.PrimaryStats.AllActiveBuffs());
        }

        public Character FindUser(string Name)
        {
            return Characters.FirstOrDefault(x => x.Name.Equals(Name, StringComparison.CurrentCultureIgnoreCase)) ?? Server.Instance.GetCharacter(Name);
        }

        public Character FindCharacterInMap(int characterID) => Characters.FirstOrDefault(x => x.ID == characterID);

        public void ForEachCharacters(Action<Character> cb)
        {
            var characters = Characters.ToArray();
            for (var i = 0; i < characters.Length; i++)
            {
                cb(characters[i]);
            }
        }

        public void SendPacket(Packet packet, Character skipme = null, bool log = false)
        {
            if (!Server.Instance.Initialized) return;
            ForEachCharacters(p =>
            {
                if (p != skipme)
                    p.SendPacket(packet);
                else if (log) { }
            });
        }

        public void SendPacket(IFieldObj Obj, Packet packet, Character skipme = null)
        {
            ForEachCharacters(p =>
            {
                if (Obj.IsShownTo(p) && p != skipme)
                {
                    p.SendPacket(packet);
                }
            });
        }


        public void ShowPlayer(Character chr, bool gmhide)
        {
            var spawneePet = chr.GetSpawnedPet();

            // GMS actually doesn't care wether its you or not. lol.
            Characters
                .Where(x => x != chr)
                .ForEach(otherCharacter =>
                {
                    // Do not send a packet when they already know its joined
                    if (gmhide && otherCharacter.IsGM) return;

                    // Show character to P
                    if (chr.IsShownTo(otherCharacter))
                    {
                        MapPacket.SendCharacterEnterPacket(chr, otherCharacter);
                        chr.Guild?.SendGuildMemberInfoUpdate(chr, otherCharacter);
                        if (spawneePet != null) PetsPacket.SendSpawnPet(chr, spawneePet, otherCharacter);
                    }

                    // Show P to character
                    if (otherCharacter.IsShownTo(chr))
                    {
                        MapPacket.SendCharacterEnterPacket(otherCharacter, chr);
                        otherCharacter.Guild?.SendGuildMemberInfoUpdate(otherCharacter, chr);

                        var petItem = otherCharacter.GetSpawnedPet();
                        if (petItem != null) PetsPacket.SendSpawnPet(otherCharacter, petItem, chr);
                    }
                });

            RedistributeControllers();
        }

        public void ShowObjects(Character chr)
        {
            if (HasClock)
            {
                var cd = MasterThread.CurrentDate;
                MapPacket.SendMapClock(chr, cd.Hour, cd.Minute, cd.Second);
            }

            SendMapTimer(chr);

            // Reset pet position
            var petItem = chr.GetSpawnedPet();
            if (petItem != null)
            {
                var ml = petItem.MovableLife;
                ml.Position = new Pos(chr.Position);
                ml.Foothold = chr.Foothold;
                ml.MoveAction = 0;
                PetsPacket.SendSpawnPet(chr, petItem, chr);
            }

            NPCs.Where(x => x.IsSpawned).ForEach(n => NpcPacket.SendMakeEnterFieldPacket(n, chr));

            Mobs.Values.Where(x => x.HP > 0).ForEach(m =>
            {
                MobPacket.SendMobSpawn(chr, m);
            });

            DropPool.OnEnter(chr);

            // ShowPlayer also redistibutes mobs, we want this
            ShowPlayer(chr, false);

            MessageBoxes.ForEach(MapPacket.SpawnMessageBox);

            if (WeatherID != 0) MapPacket.SendWeatherEffect(this, chr);
            if (JukeboxID != -1) MapPacket.SendJukebox(this, chr);

            ShowReactorsTo(chr);

            SpawnedMists.Values.ForEach(m => MistPacket.SendMistSpawn(m, chr));

            Summons.ShowAllSummonsTo(chr);

            DoorPool.ShowAllDoorsTo(chr);
        }

        /// <summary>
        /// Send the Map Timer packet to either everybody in the map (chr == null) or the character.
        /// </summary>
        /// <param name="chr">The character to send it to. Can be null to send it to everybody in the map.</param>
        public void SendMapTimer(Character chr)
        {
            var currentTime = MasterThread.CurrentTime;
            if (currentTime < TimerEndTime)
            {
                var secondsLeft = (TimerEndTime - currentTime) / 1000;
                if (chr != null)
                    MapPacket.ShowMapTimerForCharacter(chr, (int)secondsLeft);
                else
                    MapPacket.ShowMapTimerForMap(this, (int)secondsLeft);
            }
        }

        public bool MakeJukeboxEffect(int itemID, string user, TimeSpan time)
        {
            if (JukeboxID != -1) return false;
            JukeboxID = itemID;
            JukeboxUser = user;
            JukeboxTime = MasterThread.CurrentTime + (int)time.TotalMilliseconds;

            MapPacket.SendJukebox(this, null);
            return true;
        }

        public void StopJukeboxEffect()
        {
            JukeboxID = -1;
            JukeboxUser = "";
            JukeboxTime = 0;
            MapPacket.SendJukebox(this, null);
        }

        public bool MakeWeatherEffect(int itemID, string message, TimeSpan time, bool admin = false)
        {
            if (WeatherID != 0) return false;
            WeatherID = itemID;
            WeatherMessage = message;
            WeatherIsAdmin = admin;
            if (time.TotalMilliseconds == 0 && admin)
                WeatherTime = long.MaxValue;
            else
                WeatherTime = MasterThread.CurrentTime + (int)time.TotalMilliseconds;

            MapPacket.SendWeatherEffect(this);
            return true;
        }

        public void StopWeatherEffect()
        {
            WeatherID = 0;
            WeatherMessage = "";
            WeatherIsAdmin = false;
            WeatherTime = 0;
            MapPacket.SendWeatherEffect(this);
        }

        public virtual void Reset(bool shuffleReactor)
        {
            if (Server.Instance.Initialized)
            {
                log.Info($"Resetting map {ID} (ShuffleReactors: {shuffleReactor})");
            }

            OnBanishAllUsers();

            // Reset portals
            foreach (var portal in Portals.Values)
            {
                var portalType = portal.Type;
                portal.Enabled = !(portalType == 4 || portalType == 5);
            }

            // Remove mobs
            foreach (var mob in Mobs.Values.ToArray())
                mob.ForceDead();

            if (initialSpawnDone)
            {
                initialSpawnDone = false;
                // Create mobs
                SummonAllLife();
            }

            // Remove drops
            DropPool.TryExpire(MasterThread.CurrentTime, true);

            // And get reactors updated
            ResetReactor(shuffleReactor);
        }

        public void OnBanishAllUsers()
        {
            if (ForcedReturn == Constants.InvalidMap) return;

            ForEachCharacters(p =>
            {
                if (p.IsGM) return;

                p.ChangeMap(ForcedReturn);
            });
        }

        public int KillAllMobs(int damageAmount, Character rewardCharacter, DropOwnType dropOwnType, bool forced)
        {
            var amount = 0;

            try
            {
                foreach (var mob in Mobs.Values.ToArray())
                {
                    if (damageAmount > 0)
                        MobPacket.SendMobDamageOrHeal(mob, damageAmount == 0 ? mob.HP : damageAmount, false, false);

                    if (forced) mob.ForceDead();
                    else mob.Kill();

                    if (rewardCharacter != null)
                    {
                        mob.GiveReward(
                            rewardCharacter.ID,
                            rewardCharacter.PartyID,
                            dropOwnType,
                            mob.Position,
                            0,
                            0,
                            false
                        );
                    }
                    amount++;
                }
            }
            catch (Exception ex)
            {
                log.Error("Unable to kill all mobs", ex);
            }
            return amount;
        }

        public void RemoveMob(Mob mob)
        {
            Mobs.Remove(mob.SpawnID);

            if (Mobs.Count == 0)
            {
                // ContinentMan.Instance.OnAllSummonedMobRemoved(ID);
            }
        }

        public int CreateMobWithoutMobGen(
            int mobid,
            Pos position,
            short foothold,
            MobAppear type = MobAppear.Regen,
            int option = 0,
            bool facesLeft = false,
            int mobType = 0)
        {

            return CreateMob(mobid, null, position, foothold, type, option, facesLeft, mobType);
        }

        public int SubMobCount { get; set; } = -1;

        public int CreateMob(
            int mobid,
            MobGenItem mgi,
            Pos position,
            short foothold,
            MobAppear type = MobAppear.Regen,
            int option = 0,
            bool facesLeft = false,
            int mobType = 0)
        {

            // Make sure the pos is not through the floor
            position = new Pos(position);
            position.Y -= 2;

            var id = _objectIDs.NextValue();
            if (Server.Instance.Initialized)
            {
                Trace.WriteLine($"Spawning mob {mobid} at {position.X} {position.Y}, summon type {type}.");
            }


            if (mgi != null && mgi.RegenInterval != 0)
                mgi.MobCount++;

            var mob = new Mob(id, this, mobid, position, foothold, facesLeft);
            mob.MobGenItem = mgi;

            mob.SummonType = type;
            mob.SummonOption = option;
            mob.MobType = mobType;

            mob.NextAttackPossible = false;
            mob.SkillCommand = 0;
            mob.Controller = null;
            mob.LastAttack = MasterThread.CurrentTime;
            mob.LastMove = MasterThread.CurrentTime;

            MobPacket.SendMobSpawn(mob);

            // Remove summon info right after
            if (mob.SummonType != MobAppear.Suspended)
            {
                mob.SummonType = MobAppear.Normal;
                mob.SummonOption = 0;
            }

            Mobs.Add(mob.SpawnID, mob);


            if (mob.MobType == 1)
            {
                if (SubMobCount < 0) SubMobCount = 0;
                SubMobCount++;
            }

            FindNewController(mob, null);


            return id;
        }

        /// <summary>
        /// Update controllers of mobs
        /// </summary>
        /// <param name="who">When this is NULL, it will find all uncontrolled mobs and allocate them (Same as RedistributeControllers)</param>
        public void RemoveController(Character who)
        {
            Mobs.Values.Where(x => x.Controller == who).ForEach(x => FindNewController(x, null));
        }

        public void RedistributeControllers()
        {
            Mobs.Values.Where(x => x.IsControlled == false).ForEach(x => FindNewController(x, null));
        }

        public bool FindNewController(Mob mob, Character wantedCharacter, bool chase = false)
        {
            // This function is not the same as GMS
            // GMS figures out who has the lowest amount of mobs to control

            var currentController = mob.Controller;

            if (wantedCharacter != null)
            {
                // Already the same
                if (currentController == wantedCharacter) return true;
                // Not in current map O.o
                if (!Characters.Contains(wantedCharacter)) return false;

                // Cant enforce control on hidden characters
                if (!wantedCharacter.IsShownTo(mob)) return false;

                mob.SetController(wantedCharacter, chase);
                return true;
            }


            // Try to give back the control to the person that did most damage
            var lastHitCharacter = Characters.FirstOrDefault(c =>
                c != currentController &&
                !c.HuskMode &&
                c.IsShownTo(mob) &&
                c.ID == mob.LastHitCharacterID
            );
            if (lastHitCharacter != null)
            {
                mob.SetController(lastHitCharacter, chase);
                return true;
            }

            // Shuffle the characters so if there are more mobs and players, its better distributed.
            var shuffledPlayers = Characters
                .Where(c => c != currentController && c.IsShownTo(mob))
                .ToArray();

            // Take players that are closest
            Array.Sort(shuffledPlayers, (x, y) =>
            {
                var xOffset = (x.Position - mob.Position);
                var yOffset = (y.Position - mob.Position);

                return xOffset.CompareTo(yOffset);
            });

            var nonAFKPlayers = shuffledPlayers.Where(c => !c.IsAFK).ToArray();

            // Figure out a player that is in range
            var pickedPlayer = nonAFKPlayers.FirstOrDefault() ?? shuffledPlayers.FirstOrDefault();

            if (pickedPlayer != null)
            {
                mob.SetController(pickedPlayer, chase);
                return true;
            }


            // No players found, so deallocate
            mob.RemoveController(true);
            return false;
        }

        public bool IsPointInMBR(int x, int y, bool AsClient)
        {
            var checkMBR = MBR;

            if (AsClient)
                checkMBR.Inflate(9, 9);

            return checkMBR.Contains(x, y);
        }

        public IEnumerable<Foothold> SearchFootholds(int x0, int y0, int x1, int y1)
        {
            var Left = Math.Min(x0, x1);
            var Top = Math.Min(y0, y1);
            var Right = Math.Max(x0, x1);
            var Bottom = Math.Max(y0, y1);

            var Area = Rectangle.FromLTRB(Left, Top, Right, Bottom);

            return Footholds.Where(foothold => Area.IntersectsWith(foothold.Rect));
        }

        public Foothold? GetFootholdUnderneath(int X, int Y, out short IntersectY)
        {
            Foothold? ClosestFoothold = null;
            IntersectY = short.MaxValue;

            var fhs = SearchFootholds(
                X - 1,
                Y - 3,
                X + 1,
                short.MaxValue
            ).ToArray();

            if (fhs.Length == 0) return null;

            var intersectionLine = new Foothold.Line
            {
                x1 = X - 1,
                x2 = X + 1,
                y1 = Y - 3,
                y2 = short.MaxValue
            };

            var minDistance = short.MaxValue;

            foreach (var fh in fhs)
            {
                var pos = fh.Intersection(intersectionLine);
                if (pos == null)
                {
                    // huh
                    continue;
                }

                var distanceToY = (short)Math.Abs(pos.Y - Y);
                if (distanceToY > minDistance) continue;

                minDistance = distanceToY;

                IntersectY = pos.Y;
                ClosestFoothold = fh;
            }

            return ClosestFoothold;
        }

        public Foothold? GetFootholdClosest(int x, int y, ref int pcx, ref int pcy, int ptHitx)
        {
            Foothold? ClosestFoothold = null;
            var minimum = 2147483647;
            var x2 = 0;

            foreach (var Foothold in Footholds)
            {
                var xDist = 0;
                var yDist = 0;
                if (Foothold.X1 >= Foothold.X2)
                    continue;

                var HitX = ptHitx - x < 0;
                if (ptHitx > x)
                {
                    if (Foothold.X1 < x)
                        continue;
                    HitX = ptHitx - x < 0;
                }

                if (((ptHitx < 0 && HitX) || (ptHitx > 0 && !HitX)) || Foothold.X2 <= x)
                {
                    if (Foothold.Y1 >= y - 100)
                    {
                        if (Foothold.Y2 >= y - 100)
                        {
                            if (ptHitx <= x)
                            {
                                if (ptHitx < x)
                                {
                                    x2 = Foothold.X2;
                                    xDist = x2 - x;
                                }
                                else
                                    xDist = (Foothold.X1 + Foothold.X2) / 2 - x;
                            }
                            else
                            {
                                x2 = Foothold.X1;
                                xDist = x2 - x;
                            }

                            if (ptHitx <= x)
                            {
                                if (ptHitx >= x)
                                    yDist = (Foothold.Y2 + Foothold.Y1) / 2;
                                else
                                    yDist = Foothold.Y2;
                            }
                            else
                                yDist = Foothold.Y1;

                            var dist = xDist * xDist + (yDist - y) * (yDist - y);
                            if (dist < minimum)
                            {
                                var xPos = Foothold.X1;
                                if (x > xPos && (x >= Foothold.X2 || x - (xPos + Foothold.X2) / 2 >= 0))
                                    xPos = Foothold.X2;

                                var yPos = Foothold.Y1 + ((Foothold.Y2 - Foothold.Y1) * (xPos - Foothold.X1) / (Foothold.X2 - Foothold.X1));
                                x2 = MBR.Left + 10;

                                if (xPos <= x2 || xPos >= (x2 = MBR.Right - 10))
                                    xPos = (short)x2;

                                if (IsPointInMBR(xPos, yPos, true))
                                {
                                    pcx = xPos;
                                    pcy = yPos;
                                    minimum = dist;
                                    ClosestFoothold = Foothold;
                                }
                            }
                        }
                    }
                }
            }

            if (ClosestFoothold == null)
            {
                ClosestFoothold = GetFootholdUnderneath(x, y, out var pcyShort);
                pcy = pcyShort;

                x2 = MBR.Left + 10;
                if (x <= x2 || x >= (x2 = MBR.Right - 10))
                    x = x2;
                pcx = x;

                foreach (var Foothold in Footholds)
                {
                    var x1 = 0;
                    if (Foothold.X1 < Foothold.X2)
                    {
                        var x3 = Foothold.X2 + Foothold.X1;
                        long y3 = Foothold.Y1 + Foothold.Y2;
                        var MinY = (x3 / 2 - x) * (x3 / 2 - x) + (((y3 - (int)((y3 >> 32) & 0xffffffff)) >> 1) - y) * (((y3 - (int)((y3 >> 32) & 0xffffffff)) >> 1) - y);
                        if (MinY < minimum)
                        {
                            if (x > Foothold.X1)
                            {
                                if (x < Foothold.X2)
                                {
                                    x2 = Foothold.X2;
                                    if (x - x3 / 2 < 0)
                                        x2 = Foothold.X1;
                                    x1 = x2;
                                }
                                else
                                    x1 = Foothold.X2;
                            }
                            else
                                x1 = Foothold.X1;

                            var Distance = (x1 - Foothold.X1) * (Foothold.Y2 - Foothold.Y1);
                            var y2 = Foothold.Y1 + (Distance / (Foothold.X2 - Foothold.X1));
                            x2 = MBR.Left + 10;

                            if (x1 <= x2 || (x1 >= (x2 = MBR.Right - 10)))
                                x1 = x2;

                            if (IsPointInMBR(x1, y2, true))
                            {
                                pcx = x1;
                                pcy = y2;
                                minimum = (int)MinY;
                                ClosestFoothold = Foothold;
                            }
                        }
                    }
                }
            }

            return ClosestFoothold;
        }

        public Dictionary<string, List<Character>> GetCharactersInMapAreas()
        {
            return MapAreas.Select(area =>
            {
                return (Name: area.Name, Characters: Characters.Where(character => area.Area.Contains((Point)character.Position) && !character.GMHideEnabled).ToList());
            }).ToDictionary(tuple => tuple.Name, tuple => tuple.Characters);
        }

        public List<Character> GetCharactersInMapArea(string name)
        {
            if (!GetCharactersInMapAreas().TryGetValue(name, out var characters))
            {
                return new List<Character>();
            }
            return characters;
        }


        public Dictionary<string, int> CharactersInAreas()
        {
            return GetCharactersInMapAreas().Select(x => (x.Key, x.Value.Count)).ToDictionary(x => x.Key, x => x.Count);
        }

        public bool CharacterInArea(Character chr, string areaName)
        {
            return MapAreas.First(area => area.Name == areaName)?.Area.Contains(chr.Position) ?? false;
        }


        public void AddReactor(Reactor r)
        {
            Reactors[r.ID] = r;
        }

        private void ShowReactorsTo(Character chr)
        {
            Reactors.Values.ForEach(r => r.ShowTo(chr));
        }

        public void RemoveReactor(short rid)
        {
            if (!Reactors.TryGetValue(rid, out var r)) return;
            Reactors.Remove(rid);
            r.Despawn();
        }

        public void PlayerHitReactor(Character chr, int rid, short delay, uint option)
        {
            if (!Reactors.TryGetValue(rid, out var r)) return;
            r.HitBy(chr, delay, option);
        }

        public Reactor GetReactor(string name) => Reactors.Values.FirstOrDefault(x => x.Name == name);

        public void ResetReactor(bool shuffle)
        {
            var posQueue = new Queue<(short X, short Y)>();
            if (shuffle)
            {
                var allPositions = Reactors.Values.Select(x => (x.X, x.Y)).ToList();
                allPositions.Shuffle();
                allPositions.ForEach(posQueue.Enqueue);
            }


            foreach (var reactor in Reactors.Values)
            {
                if (shuffle)
                {
                    (reactor.X, reactor.Y) = posQueue.Dequeue();
                }

                if (reactor.NPC != null)
                {
                    reactor.NPC.Despawn();
                    NPCs.Remove(reactor.NPC);
                    reactor.NPC = null;
                }

                reactor.Despawn();
                reactor.Enabled = true;
                reactor.Respawn();
            }
        }

        public void SetReactorState(string name, byte state)
        {
            var reactor = GetReactor(name);
            reactor.SetState(state);
            var stateInfo = reactor.GetCurStateInfo();

        }

        public void CheckReactorAction(string name, int eventState, long eventTime)
        {
            if (ParentFieldSet == null) return;
            ParentFieldSet.CheckReactorAction(this, name, eventState, eventTime);
        }

        /// <summary>
        /// Build an array of random unique numbers from start - (range + start), and return <para>count</para>.
        /// </summary>
        /// <param name="start">Start number</param>
        /// <param name="range">Amount of numbers to generate</param>
        /// <param name="count">Amount to return</param>
        /// <returns>Enumerable of unique indices</returns>
        internal static IEnumerable<int> get_random_unique_array(int start, int range, int count)
        {
            var l = new List<int>(range);
            for (var i = 0; i < range; i++) l.Add(start + i);
            l.Shuffle();
            return l.Take(count);
        }

        /// <summary>
        /// Gets all intersections of footholds between x,y1 and x,y2
        /// </summary>
        /// <param name="x">X axis</param>
        /// <param name="y1">Top</param>
        /// <param name="y2">Bottom</param>
        /// <returns>All footholds that match</returns>
        public IEnumerable<(Foothold fh, Pos intersection)> GetFootholdRange(int x, int y1, int y2)
        {
            var intersectionLine = new Foothold.Line
            {
                x1 = x - 1,
                x2 = x + 1,
                y1 = y1,
                y2 = y2
            };


            // Go through all footholds and find ones that intersect in this line
            foreach (var foothold in SearchFootholds(x, y1, x, y2))
            {
                var intersection = foothold.Intersection(intersectionLine);
                if (intersection == null)
                {
                    // Weird!
                    continue;
                }

                yield return (foothold, intersection);
            }
        }

        /// <summary>
        /// Get a list of x,intersectY of footholds that can be used.
        /// </summary>
        /// <param name="count">Amount of points to return</param>
        /// <param name="range">Area that should contain these footholds</param>
        /// <returns>List of footholds and the position, up to <para>count</para></returns>
        public IEnumerable<(Foothold fh, Pos intersection)> GetFootholdRandom(int count, Rectangle range)
        {
            if (count == 0) yield break;

            var rcArea = range;
            rcArea.Intersect(MBR);

            var elementsToPlace = count * 2;
            var randArray = get_random_unique_array(0, elementsToPlace, elementsToPlace).ToArray();

            var nStart = rcArea.Left;
            var nGrid = (rcArea.Right - rcArea.Left + 1) / elementsToPlace;

            var returned = 0;

            foreach (var elem in randArray)
            {
                var x = (int)(nStart + (Rand32.Next() % nGrid) + nGrid * elem);
                var lYPos = GetFootholdRange(x, rcArea.Top, rcArea.Bottom).ToArray();

                if (lYPos.Length <= 0)
                    continue;

                var randomPoint = lYPos[Rand32.Next() % lYPos.Length];
                yield return randomPoint;

                returned++;
                if (returned >= count) yield break;
            }
        }


        public long LastTryExpireMessageBoxes = 0;
        public void TryExpireMessageBoxes(long time)
        {
            if (time - LastTryExpireMessageBoxes < 60 * 1000) return;
            LastTryExpireMessageBoxes = time;

            MessageBoxes
                .Where(x => time - x.CreateTime >= (60 * 60 * 1000))
                // make a copy (!)
                .ToArray()
                .ForEach(x => x.Remove());
        }
        

        public NpcLife GetNpcByTemplate(int templateID)
        {
            var npc = NPCs.FirstOrDefault(x => x.ID == templateID);
            if (npc == null)
            {
                log.Error($"Unable to find npc with template {templateID} in map {ID}");
            }
            return npc;
        }
        
        public string GetNpcVar(int templateID, string varName, string defaultValue = "")
        {
            GetNpcVar(templateID, varName, out var value);

            return value ?? defaultValue;
        }

        public bool GetNpcVar(int templateID, string varName, out string value)
        {
            value = null;
            return GetNpcByTemplate(templateID)?.Vars?.TryGetValue(varName, out value) ?? false;
        }
        
        public bool SetNpcVar(int templateID, string varName, string value)
        {
            var npc = GetNpcByTemplate(templateID);
            if (npc == null) return false;

            npc.Vars ??= new Dictionary<string, string>();
            log.Debug($"Set {varName} for NPC {templateID} in {ID} to {value}");
            npc.Vars[varName] = value;

            return true;
        }
    }
}
