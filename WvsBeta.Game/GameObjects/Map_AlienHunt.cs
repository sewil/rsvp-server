using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Game.GameObjects
{
    internal class Map_AlienHunt : Map_TeamBattle
    {
        public int? TeamWon = null;
        
        public const int WaitingRoom = 109100001;
        public const int EventRoom = 109100000;

        public short MobCountTeamA;
        public short MobCountTeamB;
        
        public short MobCountTeamA_Prev;
        public short MobCountTeamB_Prev;
        
        public Map_AlienHunt(int id) : base(id)
        {
            EffectWin = "event/coconut/victory";
            EffectLose = "event/coconut/lose";
            SoundWin = "Coconut/Victory";
            SoundLose = "Coconut/Failed";

            FinishMessage = "The game of Alien Hunt has ended, and you'll be transported to a different map. Please wait.";
            
            TimeFinishProp = 12;
            TimeMessageProp = 6;
            TimeExpandProp = 120;
            TimeDefaultProp = 300;

            TeamAName = "Gray";
            TeamBName = "Matian";

            // Prevent spawn on map access
            initialSpawnDone = true;
        }

        public override bool FilterAdminCommand(Character character, CommandHandling.CommandArgs command)
        {
            if (command.Command == "start")
            {
                ResetState();
                Start();

                TryCreateMobs(MasterThread.CurrentTime, true);
                
                SendMessageA("Alien Hunt has started!");

                MapPacket.ShowMapTimerForMap(this, TimeSpan.FromSeconds(TimeDefaultProp));

                return true;
            }

            return base.FilterAdminCommand(character, command);
        }

        public override bool ShouldExtendTime()
        {
            // Do not extend the moment someone already won
            if (GetTeamWithMostKills().GetValueOrDefault(TeamNeither) != TeamNeither) return false;

            // Go straight to regular finish when all mobs are dead.
            if (MobCountTeamA == 0 || MobCountTeamB == 0) return false;
            
            return true;
        }

        public int? CheckWinningTeam()
        {
            CalculateMobCount();
            return TeamWon;
        }

        public override int GetWinningTeam()
        {
            return CheckWinningTeam().GetValueOrDefault(TeamNeither);
        }

        public override void ResetState()
        {
            base.ResetState();
            KillAllMobs(0, null, DropOwnType.UserOwn, true);
            MobCountTeamA = 0;
            MobCountTeamB = 0;
            MobCountTeamA_Prev = 0;
            MobCountTeamB_Prev = 0;
            TeamWon = null;
        }

        public override void MapTimer(long pNow)
        {
            base.MapTimer(pNow);

            if (State == States.RUNNING_STANDARD || State == States.RUNNING_EXTENDED)
            {
                if (CheckWinningTeam().HasValue)
                {
                    // Skip timer
                    UpdateTime(TimeSpan.Zero);
                }
            }
        }

        public int? GetTeamWithMostKills()
        {
            // Decide winner based on kill count
                
            int winningTeam;
            if (MobCountTeamA < MobCountTeamB) winningTeam = TeamA;
            else if (MobCountTeamB < MobCountTeamA) winningTeam = TeamB;
            else winningTeam = TeamNeither;

            return winningTeam;
        }

        public void CalculateMobCount()
        {
            if (TeamWon != null) return;

            MobCountTeamA = 0;
            MobCountTeamB = 0;
            
            foreach (var templateID in Mobs.Select(x => x.Value.MobID))
            {
                switch (templateID)
                {
                    case 9400009:
                    case 9400010:
                        MobCountTeamA++;
                        break;

                    case 9400011:
                    case 9400012:
                        MobCountTeamB++;
                        break;
                }
            }
            
            if (MobCountTeamA == 0 || MobCountTeamB == 0 || State == States.FINISHED)
            {
                // Decide winner based on kill count
                TeamWon = GetTeamWithMostKills();

                if (State != States.FINISHED)
                {
                    // Finish it up
                    UpdateTime(TimeSpan.Zero);
                }
            }
            else
            {
                // Cannot decide yet
                TeamWon = null;
            }


            if (MobCountTeamA_Prev == MobCountTeamA && 
                MobCountTeamB_Prev == MobCountTeamB)
            {
                return;
            }
            
            MobCountTeamA_Prev = MobCountTeamA;
            MobCountTeamB_Prev = MobCountTeamB;
            
            SendPacket(GetCoconutScorePacket());
        }

        public Packet GetCoconutScorePacket()
        {
            var packet = new Packet(ServerMessages.COCONUT_SCORE);
            packet.WriteShort(MobCountTeamA);
            packet.WriteShort(MobCountTeamB);
            return packet;
        }

        public override void AddPlayer(Character chr)
        {
            if (Characters.Count == 0)
            {
                // Cleanup map on first access
                ResetState();
            }

            base.AddPlayer(chr);
            
            var packet = new Packet(ServerMessages.FIELD_SPECIFIC_DATA);
            EncodeFieldSpecificData(chr, packet);
            chr.SendPacket(packet);

            chr.SendPacket(GetCoconutScorePacket());

            var team = GetTeam(chr.ID);
            if (team != TeamNeither)
            {
                var teamName = team == TeamA ? TeamAName : TeamBName;
                MessagePacket.SendTextPlayer(MessagePacket.MessageTypes.RedText, $"You have been assigned to Team {teamName}", chr, true);
            }

            if (State == States.RUNNING_STANDARD || State == States.RUNNING_EXTENDED)
            {
                ShowTimer(chr);
            }
        }

        public override int GetNewTeamForPlayer(Character chr)
        {
            var portal1 = Portals["st01"]; // left
            var portal2 = Portals["st00"]; // right

            var chrX = chr.Position.X;

            // TeamB is left, TeamA is right
            
            if (chrX == portal1.X) return TeamB;
            if (chrX == portal2.X) return TeamA;

            // Detect where you spawned

            var mapWidth = MBR.Width;
            var center = MBR.Right - (mapWidth / 2);
            if (chrX < center) return TeamB;
            else return TeamA;
            
        }
    }
}
