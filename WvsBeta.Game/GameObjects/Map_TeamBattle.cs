using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Game.GameObjects
{
    internal abstract class Map_TeamBattle : Map
    {
        public const int REWARD_MAP = 109050000;

        public string EffectWin, EffectLose;
        public string SoundWin, SoundLose;

        public const int TeamA = 0;
        public const int TeamB = 1;
        public const int TeamNeither = -1;

        public long FinishMessageTime, NextStateTime;
        /// <summary>
        /// Time until Finished finishes
        /// </summary>
        public int TimeFinishProp;
        /// <summary>
        /// Time until the finish message shows after finish. Can be zero.
        /// </summary>
        public int TimeMessageProp;
        /// <summary>
        /// Extended playtime
        /// </summary>
        public int TimeExpandProp;
        /// <summary>
        /// Event playtime
        /// </summary>
        public int TimeDefaultProp;
        public States State;

        /// <summary>
        /// The message that will be sent before people will be kicked out
        /// </summary>
        public string FinishMessage;
        public string TeamAName = "Maple";
        public string TeamBName = "Story";
        
        public enum States
        {
            STOPPED = 0,
            RUNNING_STANDARD = 1,
            RUNNING_EXTENDED = 2,
            FINISHED = 3,
        }

        public List<int>[] Members { get; } = new List<int>[2]
        {
            new List<int>(),
            new List<int>()
        };

        protected Map_TeamBattle(int id) : base(id)
        {
        }

        public abstract int GetWinningTeam();

        public override bool FilterAdminCommand(Character character, CommandHandling.CommandArgs command)
        {
            if (command.Command == "usercount")
            {
                MessagePacket.SendTextPlayer(MessagePacket.MessageTypes.Notice, $"Total:{Characters.Count} {TeamAName}:{Members[0].Count} {TeamBName}:{Members[1].Count}", character);
                return true;
            }

            return base.FilterAdminCommand(character, command);
        }

        public override void AddPlayer(Character chr)
        {
            if (Characters.Count == 0)
            {
                // Cleanup leftover member data
                Members[TeamA].Clear();
                Members[TeamB].Clear();
            }

            var playerTeam = GetNewTeamForPlayer(chr);
            if (playerTeam != TeamNeither)
            {
                Members[playerTeam].Add(chr.ID);
            }

            base.AddPlayer(chr);
        }

        public virtual int GetNewTeamForPlayer(Character chr)
        {
            if (!chr.IsGM || true)
            {
                return Members[TeamA].Count <= Members[TeamB].Count ? TeamA : TeamB;
            }

            return TeamNeither;
        }

        public override void RemovePlayer(Character chr, bool gmhide = false)
        {
            base.RemovePlayer(chr, gmhide);

            Members[TeamA].Remove(chr.ID);
            Members[TeamB].Remove(chr.ID);
        }

        public int GetTeam(int characterID)
        {
            if (Members[TeamA].Contains(characterID)) return TeamA;
            if (Members[TeamB].Contains(characterID)) return TeamB;

            return TeamNeither;
        }

        public virtual void OnTimeExpand()
        {
            SendMessageA("The game ended in a tie, so an additional 2 minutes is rewarded for overtime.");
            SendMessageA("If the game ends in a tie after 2 minutes, both teams will be deemed losers and the prizes will not be awarded.");
            MapPacket.ShowMapTimerForMap(this, TimeSpan.FromSeconds(TimeExpandProp));
        }

        public virtual void OnTimeReset()
        {
            var winningTeam = GetWinningTeam();

            for (var i = 0; i < Members.Length; i++)
            {
                // CREATE COPY
                var members = Members[i].ToArray(); 

                var returnMap = winningTeam == i ? REWARD_MAP : ReturnMap;

                foreach (var characterId in members)
                {
                    var chr = FindCharacterInMap(characterId);
                    if (chr == null) continue;

                    chr.ChangeMap(returnMap);
                }

                Members[i].Clear();
            }

            ResetState();
        }

        public virtual void ResetState()
        {
            State = States.STOPPED;
            FinishMessageTime = 0;
        }

        public void Start()
        {
            State = States.RUNNING_STANDARD;
            UpdateTime(TimeSpan.FromSeconds(TimeDefaultProp));
        }

        public void ShowTimer(Character chr = null)
        {
            var timeLeft = TimeSpan.FromMilliseconds(NextStateTime - MasterThread.CurrentTime);
            if (chr == null)
                MapPacket.ShowMapTimerForMap(this, timeLeft);
            else
                MapPacket.ShowMapTimerForCharacter(chr, timeLeft);
        }

        public virtual void UpdateTime(TimeSpan duration)
        {
            NextStateTime = MasterThread.CurrentTime + (long)duration.TotalMilliseconds;
        }

        public virtual void OnTimeFinish()
        {
            FinishGame(GetWinningTeam());
        }

        public abstract bool ShouldExtendTime();

        public void CheckState(long tCur)
        {
            if (State == States.STOPPED || NextStateTime - tCur > 0) return;


            if (State == States.FINISHED)
            {
                State = States.STOPPED;
                OnTimeReset();
            }
            else if (State == States.RUNNING_STANDARD && ShouldExtendTime())
            {
                State = States.RUNNING_EXTENDED;
                UpdateTime(TimeSpan.FromSeconds(TimeExpandProp));
                OnTimeExpand();
            }
            else if (State == States.RUNNING_STANDARD || State == States.RUNNING_EXTENDED)
            {
                if (TimeMessageProp > 0 && !string.IsNullOrEmpty(FinishMessage))
                    FinishMessageTime = tCur + (1000 * TimeMessageProp);
                State = States.FINISHED;
                UpdateTime(TimeSpan.FromSeconds(TimeFinishProp));
                OnTimeFinish();
            }
        }

        public void FinishGame(int winningTeam)
        {
            for (var i = 0; i < Members.Length; i++)
            {
                var members = Members[i];
                var winner = winningTeam == i;

                var effect = winner ? EffectWin : EffectLose;
                var sound = winner ? SoundWin : SoundLose;

                foreach (var characterId in members)
                {
                    var chr = FindCharacterInMap(characterId);
                    if (chr == null) continue;

                    if (!string.IsNullOrEmpty(effect)) MapPacket.EffectScreen(chr, effect);
                    if (!string.IsNullOrEmpty(sound)) MapPacket.EffectSound(chr, sound);
                }
            }

            var message = GetWinMessage(winningTeam);
            if (!string.IsNullOrEmpty(message)) SendMessageA(message);
        }

        public virtual string GetWinMessage(int winningTeam)
        {
            return winningTeam switch
            {
                TeamA => $"Team {TeamAName} WINS!",
                TeamB => $"Team {TeamBName} WINS!",
                _ => $"The game between Team {TeamAName} and Team {TeamBName} have resulted in a tie."
            };
        }

        public void SendMessageA(string sMsg)
        {
            MessagePacket.SendTextMap(MessagePacket.MessageTypes.RedText, sMsg, this);
        }

        public override void MapTimer(long pNow)
        {
            CheckState(pNow);

            if (FinishMessageTime != 0 && FinishMessageTime - pNow < 0)
            {
                SendMessageA(FinishMessage);
                FinishMessageTime = 0;
            }

            base.MapTimer(pNow);
        }
        
        public override void EncodeFieldSpecificData(Character chr, Packet packet)
        {
            var team = GetTeam(chr.ID);
            packet.WriteByte((byte)team);
        }
    }
}
