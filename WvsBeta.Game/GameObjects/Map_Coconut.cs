using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using WvsBeta.Common.Sessions;
using WzTools.Objects;

namespace WvsBeta.Game.GameObjects
{
    class Map_Coconut : Map_TeamBattle
    {
        public class COCONUT
        {
            public Types Type;
            public byte State;
            public enum Types
            {
                Falling = 0,
                Bombing = 1,
                Stopped = 2,
            }
            
            public enum MapObjStates
            {
                Shake = 1,
                Explode = 2,
                Fall = 3,
            }
        }


        public List<COCONUT> Coconuts { get; } = new List<COCONUT>();

        public int[] Count { get; } = new int[3];
        public short[] Score { get; } = new short[2];

        public int HitCount;
        
        public const int CoconutMapID = 109080000;
        public const int CokeplayMapID = 109080010;

        public string EventName;
        public string EventObjectName;

        public Map_Coconut(int id, WzProperty property) : base(id)
        {
            var nuts = property["coconut"] as WzProperty;
            EffectWin = nuts.GetString("effectWin");
            EffectLose = nuts.GetString("effectLose");
            SoundWin = nuts.GetString("soundWin");
            SoundLose = nuts.GetString("soundLose");
            TimeFinishProp = nuts.GetInt32("timeFinish").Value;
            TimeMessageProp = nuts.GetInt32("timeMessage").Value;
            TimeExpandProp = nuts.GetInt32("timeExpand").Value;
            TimeDefaultProp = nuts.GetInt32("timeDefault").Value;

            Count[0] = nuts.GetInt32("countFalling").Value;
            Count[1] = nuts.GetInt32("countBombing").Value;
            Count[2] = nuts.GetInt32("countStopped").Value;

            HitCount = nuts.GetInt32("countHit").Value;
            
            EventName = nuts.GetString("eventName");
            EventObjectName = nuts.GetString("eventObjectName");
            
            FinishMessage = $"The game of {EventName} has ended, and you'll be transported to a different map. Please wait.";

            ResetState();
        }

        public override void AddPlayer(Character chr)
        {
            base.AddPlayer(chr);

            var packet = new Packet(ServerMessages.FIELD_SPECIFIC_DATA);
            EncodeFieldSpecificData(chr, packet);
            chr.SendPacket(packet);

            chr.SendPacket(GetCoconutScorePacket());

            if (State == States.RUNNING_STANDARD || State == States.RUNNING_EXTENDED)
            {
                ShowTimer(chr);
            }
        }

        public override bool FilterAdminCommand(Character character, CommandHandling.CommandArgs command)
        {
            if (command.Command == "start")
            {
                ResetState();
                Start();

                for (var i = 0; i < Count.Length; i++)
                {
                    var nuts = Count[i];
                    if (nuts <= 0) continue;

                    for (var j = 0; j < nuts; j++)
                    {
                        Coconuts.Add(new COCONUT
                        {
                            State = (byte)HitCount,
                            Type = (COCONUT.Types)i,
                        });
                    }
                }

                Coconuts.Shuffle();

                MapPacket.ShowMapTimerForMap(this, TimeSpan.FromSeconds(TimeDefaultProp));

                SendCoconutHit(-1, 0, 0); // Make clients reset all coconuts

                // BMS sends this twice. gg
                SendMessageA($"{EventName} has started!");


                return true;
            }

            return base.FilterAdminCommand(character, command);
        }

        public override bool ShouldExtendTime() => Score[0] == Score[1];

        public override int GetWinningTeam()
        {
            int winningTeam;
            if (Score[0] > Score[1]) winningTeam = TeamA;
            else if (Score[0] < Score[1]) winningTeam = TeamB;
            else winningTeam = TeamNeither;

            return winningTeam;
        }


        public override void ResetState()
        {
            base.ResetState();
            Score[0] = 0;
            Score[1] = 0;
        }


        public override bool HandlePacket(Character character, Packet packet, ClientMessages opcode)
        {
            switch (opcode)
            {
                case ClientMessages.FIELD_COCONUT_ATTACK:
                    OnHitObject(character, packet);
                    return true;
                default:
                    return base.HandlePacket(character, packet, opcode);
            }
        }


        public void OnHitObject(Character chr, Packet packet)
        {
            CheckState(MasterThread.CurrentTime);

            if (State == States.STOPPED) return;
            if (State == States.FINISHED) return;

            var target = packet.ReadShort();
            var delay = packet.ReadShort();

            if (Coconuts.Count < target) return;
            if (target < 0) return;

            var team = GetTeam(chr.ID);
            if (team < 0 || team > 1) return;

            var coconut = Coconuts[target];

            if (coconut.State <= 0) return;


            if (coconut.Type == COCONUT.Types.Stopped)
            {
                // Static effect. Nothing
                SendCoconutHit(target, delay, (byte)COCONUT.MapObjStates.Shake);
            }
            else
            {
                coconut.State -= 1;

                if (coconut.State > 0)
                {
                    SendCoconutHit(target, delay, (byte)COCONUT.MapObjStates.Shake);
                }
                else
                {
                    var result = coconut.Type == COCONUT.Types.Bombing
                        ? COCONUT.MapObjStates.Explode 
                        : COCONUT.MapObjStates.Fall;

                    SendCoconutHit(target, delay, (byte)result);

                    if (result == COCONUT.MapObjStates.Fall)
                    {
                        Score[team]++;
                        SendCoconutScore();
                    }
                }
            }
        }


        public Packet GetCoconutScorePacket()
        {
            var packet = new Packet(ServerMessages.COCONUT_SCORE);
            packet.WriteShort(Score[0]);
            packet.WriteShort(Score[1]);
            return packet;
        }

        public void SendCoconutScore()
        {
            SendPacket(GetCoconutScorePacket());
        }

        public void SendCoconutHit(short target, short delay, byte state)
        {
            var packet = new Packet(ServerMessages.COCONUT_HIT);
            packet.WriteShort(target);
            packet.WriteShort(delay);
            packet.WriteByte(state);
            SendPacket(packet);
        }
    }
}
