using System;
using System.Collections.Generic;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.Events;
using WvsBeta.Game.Events.GMEvents;
using WvsBeta.SharedDataProvider.Templates;
using WzTools.Objects;

namespace WvsBeta.Game.GameObjects
{
    class Map_Snowball : Map_TeamBattle
    {
        /* SNOWBALL CONSTANTS */
        public static readonly int xMin = 0;
        public static readonly int xMax = 900;
        public int RecoveryAmount = 400;
        public static readonly int RecoveryDelay = 10000;
        public static readonly short DefaultSnowballMaxHp = 8999;
        public static readonly int DefaultSnowManMaxHp = 7500;
        public static readonly int SnowmanWaitDuration = 20000;
        public static readonly int Speed = 100; //gms-like is 100
        public short DamageSnowBall = 10;
        public short DamageSnowMan0 = 15;
        public short DamageSnowMan1 = 45;
        public const int FIELD_MAIN = 109060000;
        public const int FIELD_LOBBY = 109060001;
        public short SnowballMaxHp = DefaultSnowballMaxHp;
        public int SnowManMaxHp = DefaultSnowManMaxHp;

        public enum SnowballTeam
        {
            TEAM_MAPLE = 0,
            TEAM_STORY = 1
        }

        public enum SnowballEventState
        {
            NOT_STARTED = 0,
            IN_PROGRESS = 1,
            MAPLE_WIN = 2,
            STORY_WIN = 3
        }
        /**********************/

        public Portal Top => Portals["st01"];
        public Portal Bottom => Portals["st00"];
        public readonly SnowballObject MapleSnowball;
        public readonly SnowballObject StorySnowball;
        public readonly SnowmanObject MapleSnowman;
        public readonly SnowmanObject StorySnowman;
        private SnowballEventState _snowballState;

        private bool Concluded => State == States.FINISHED;

        public Map_Snowball(int id, WzProperty property) : base(id)
        {
            MapleSnowman = new SnowmanObject(this, SnowballTeam.TEAM_MAPLE);
            StorySnowman = new SnowmanObject(this, SnowballTeam.TEAM_STORY);
            MapleSnowball = new SnowballObject(this, SnowballTeam.TEAM_MAPLE);
            StorySnowball = new SnowballObject(this, SnowballTeam.TEAM_STORY);

            EffectWin = "event/coconut/victory";
            EffectLose = "event/coconut/lose";
            SoundWin = "Coconut/Victory";
            SoundLose = "Coconut/Failed";

            var snowBallProp = property["snowBall"] as WzProperty;

            DamageSnowBall = snowBallProp.GetInt16("damageSnowBall").Value;
            DamageSnowMan0 = snowBallProp.GetInt16("damageSnowMan0").Value;
            DamageSnowMan1 = snowBallProp.GetInt16("damageSnowMan1").Value;
            RecoveryAmount = snowBallProp.GetInt32("recoveryAmount").Value;

            TimeFinishProp = 15;
            TimeMessageProp = 1;
            TimeDefaultProp = 60 * 10;
            TimeExpandProp = 0;
        }

        public override void Reset(bool shuffleReactor)
        {
            SnowballMaxHp = DefaultSnowballMaxHp;
            SnowManMaxHp = DefaultSnowManMaxHp;
            if (Concluded)
            {
                base.Reset(false);
                ResetState();
            }
            else
            {
                Conclude();
            }
        }

        public override void ResetState()
        {
            base.ResetState();

            MapleSnowman.Reset();
            StorySnowman.Reset();
            SnowballState = SnowballEventState.NOT_STARTED;
            State = States.STOPPED;
            MapleSnowball.Reset();
            StorySnowball.Reset();
        }

        public override bool ShouldExtendTime()
        {
            return false;
        }

        public override void EncodeFieldSpecificData(Character chr, Packet packet)
        {
            EncodeState(packet);
        }

        public override void AddPlayer(Character chr)
        {
            if (Characters.Count == 0)
            {
                // Cleanup map on first access
                ResetState();
            }

            base.AddPlayer(chr);

            var pw = new Packet(ServerMessages.SNOWBALL_STATE);
            EncodeState(pw);
            chr.SendPacket(pw);
            
            if (State == States.RUNNING_STANDARD || State == States.RUNNING_EXTENDED)
            {
                ShowTimer(chr);
            }
        }

        public SnowballEventState SnowballState { get; set; }

        public void BroadcastState()
        {
            var pw = new Packet(ServerMessages.SNOWBALL_STATE);
            EncodeState(pw);
            SendPacket(pw);
        }

        public void EncodeState(Packet pw)
        {
            pw.WriteByte(SnowballState);
            //pw.WriteInt(StorySnowman.CurHP * 100 / SnowManMaxHp); Not used in v12, used later
            //pw.WriteInt(MapleSnowman.CurHP * 100 / SnowManMaxHp); Not used in v12, used later
            MapleSnowball.Encode(pw);
            StorySnowball.Encode(pw);
            pw.WriteShort(DamageSnowBall);
            pw.WriteShort(DamageSnowMan0);
            pw.WriteShort(DamageSnowMan1);
        }

        public override bool HandlePacket(Character character, Packet packet, ClientMessages opcode)
        {
            if (SnowballState == SnowballEventState.IN_PROGRESS)
            {
                switch (opcode)
                {
                    case ClientMessages.FIELD_SNOWBALL_ATTACK:
                    {
                        var hitTarget = packet.ReadByte<HitTarget>();
                        var damage = packet.ReadShort();
                        var delay = packet.ReadShort();
                        OnSnowballHit(
                            hitTarget,
                            character,
                            damage,
                            delay
                        );
                        return true;
                    }
                }
            }

            return false;
        }

        public override void UpdateTime(TimeSpan duration)
        {
            base.UpdateTime(duration);

            ShowTimer(null);
        }

        public override int GetWinningTeam()
        {
            var curState = UpdateSnowballPositions();
            return curState switch
            {
                SnowballEventState.MAPLE_WIN => TeamA,
                SnowballEventState.STORY_WIN => TeamB,
                _ => TeamNeither,
            };
        }

        public override string GetWinMessage(int winningTeam) => null;

        public override bool FilterAdminCommand(Character character, CommandHandling.CommandArgs command)
        {
            switch (command.Command)
            {
                case "start":
                    command.TryGetInt16(0, out var snowballHP, SnowballMaxHp);
                    command.TryGetInt32(1, out var snowmanHP, SnowManMaxHp);
                    if (SnowballState == SnowballEventState.NOT_STARTED)
                    {
                        SnowballMaxHp = snowballHP;
                        SnowManMaxHp = snowmanHP;
                        MapleSnowman.Reset();
                        StorySnowman.Reset();
                        MapleSnowball.Reset();
                        StorySnowball.Reset();
                        SnowballState = SnowballEventState.IN_PROGRESS;
                        Start();
                        BroadcastState();
                        StartFieldSet(character);
                    }
                    return true;
                case "broadcaststate":
                    BroadcastState();
                    return true;
                case "dmg":
                    command.TryGetByte(0, out var hitTarget);
                    command.TryGetInt16(1, out var damage);
                    command.TryGetInt16(2, out var delay);
                    OnSnowballHit(
                        (HitTarget)hitTarget,
                        character,
                        damage,
                        delay
                    );
                    return true;
                case "skiptimer":
                    UpdateTime(TimeSpan.FromSeconds(5));
                    return true;
            }
            return base.FilterAdminCommand(character, command);
        }

        public enum HitTarget
        {
            StorySnowball = 0,
            MapleSnowball,
            StorySnowman,
            MapleSnowman,
        }

        void OnSnowballHit(HitTarget type, Character chr, short damage, short delay)
        {
            Program.MainForm.LogDebug("Type: " + type);
            switch (type)
            {
                case HitTarget.StorySnowball:
                    StorySnowball.OnHit(-damage);
                    break;
                case HitTarget.MapleSnowball:
                    MapleSnowball.OnHit(-damage);
                    break;
                case HitTarget.StorySnowman:
                    StorySnowman.OnHit(damage);
                    break;
                case HitTarget.MapleSnowman:
                    MapleSnowman.OnHit(damage);
                    break;
                default:
                    return;
            }

            var pw = new Packet(ServerMessages.SNOWBALL_HIT);
            pw.WriteByte(type);
            pw.WriteShort(damage);
            pw.WriteShort(delay);
            SendPacket(pw, chr);
        }

        public SnowballEventState UpdateSnowballPositions()
        {
            var now = MasterThread.CurrentTime;
            MapleSnowball.UpdatePosition(now);
            StorySnowball.UpdatePosition(now);

            if (MapleSnowball.XPos >= xMax)
            {
                SnowballState = SnowballEventState.MAPLE_WIN;
            }
            else if (StorySnowball.XPos >= xMax)
            {
                SnowballState = SnowballEventState.STORY_WIN;
            }
            
            if (SnowballState != SnowballEventState.IN_PROGRESS && State != States.FINISHED)
            {
                // Finish it up
                UpdateTime(TimeSpan.Zero);
            }

            return SnowballState;
        }

        public void Conclude()
        {
            if (SnowballState != SnowballEventState.IN_PROGRESS) return;

            var posA = MapleSnowball.XPos;
            var posB = StorySnowball.XPos;
            if (posA < posB)
                SnowballState = SnowballEventState.STORY_WIN;
            else
                SnowballState = SnowballEventState.MAPLE_WIN;
        }

        public class SnowmanObject
        {
            public int CurHP { get; set; }
            public readonly Map_Snowball Field;
            public readonly SnowballTeam Team;

            public SnowmanObject(Map_Snowball snowballmap, SnowballTeam team)
            {
                Field = snowballmap;
                Team = team;
            }

            public void Reset()
            {
                CurHP = Field.SnowManMaxHp;
            }

            public void OnHit(int damage)
            {
                if (Field.UpdateSnowballPositions() != SnowballEventState.IN_PROGRESS) return;

                var oldHp = CurHP;
                CurHP -= damage;

                if (CurHP <= 0)
                {
                    CurHP = Field.SnowManMaxHp;
                    var AltBall = Team == SnowballTeam.TEAM_MAPLE ? Field.StorySnowball : Field.MapleSnowball;
                    AltBall.WaitTime = MasterThread.CurrentTime;
                    AltBall.HP = Field.SnowballMaxHp;
                    AltBall.Stopped = true;
                    //onsnowballmsg
                }

                Field.BroadcastState();
            }
        }

        public class SnowballObject
        {
            public static readonly int[] Delay = { 220, 260, 300, 340, 380, 420, 460, 500, 0, -500 };
            public short HP { get; set; }
            public bool Stopped { get; set; }
            public long WaitTime { get; set; }
            public short XPos { get; private set; }
            public long LastSpeedChanged { get; private set; }
            public long LastRecovery { get; private set; }
            public readonly Map_Snowball Field;
            public readonly SnowballTeam Team;

            public SnowballObject(Map_Snowball snowballmap, SnowballTeam team)
            {
                Field = snowballmap;
                Team = team;
            }

            public void Reset()
            {
                Stopped = false;
                XPos = 0;
                HP = Field.SnowballMaxHp;
            }

            public void OnHit(int damage)
            {
                Program.MainForm.LogDebug("Damage: " + damage);
                void CheckSnowmanWait()
                {
                    if (MasterThread.CurrentTime - WaitTime > SnowmanWaitDuration)
                    {
                        if (Stopped)
                        {
                            Stopped = false;
                            //onsnowballmsg?
                        }
                    }
                    else
                    {
                        Program.MainForm.LogDebug("Damage 0 wait");
                        damage = 0;
                    }
                }

                void UpdateHP()
                {
                    var OldHP = HP / 1000;
                    var nhp = HP + damage * Speed / 100;
                    nhp = nhp <= 0 ? 0 : nhp >= Field.SnowballMaxHp ? Field.SnowballMaxHp : nhp;
                    HP = (short)nhp;
                    Program.MainForm.LogDebug("New HP " + HP);
                    Field.BroadcastState();
                }

                CheckSnowmanWait();

                if (Field.UpdateSnowballPositions() == SnowballEventState.IN_PROGRESS)
                    UpdateHP();

                //section update thingy?

            }

            public void UpdatePosition(long tCur)
            {
                Program.MainForm.LogDebug("XPos: " + XPos);
                var SpeedUpdate = (int)(tCur - LastSpeedChanged);
                var curDelay = Delay[HP / 1000];

                if (curDelay != 0)
                {
                    var Pos = xMin;
                    if (xMin < XPos + SpeedUpdate / curDelay)
                        Pos = XPos + SpeedUpdate / curDelay;
                    if (Pos > xMax)
                        Pos = xMax;
                    this.XPos = (short)Pos;
                    this.LastSpeedChanged = SpeedUpdate + LastSpeedChanged - SpeedUpdate % curDelay;
                }
                else
                    LastSpeedChanged = tCur;

                if (tCur - LastRecovery <= RecoveryDelay)
                {
                    Program.MainForm.LogDebug("recov diff: " + (tCur - LastRecovery));
                    return;
                }
                else
                {
                    var NewHP = HP + Field.RecoveryAmount;
                    this.HP = (short)(NewHP < 0 ? 0 : NewHP > Field.SnowballMaxHp ? Field.SnowballMaxHp : NewHP);
                    this.LastRecovery = tCur;

                    return;
                }
            }

            public void Encode(Packet pw)
            {
                pw.WriteShort(XPos);
                pw.WriteByte((byte)(HP / 1000));
            }
        }
    }
}
