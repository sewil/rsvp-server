using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;


namespace WvsBeta.Game.GameObjects.MiniRooms
{
    class MemoryGame : MiniRoomGame
    {
        public int CardCount => MiniRoomSpec switch
        {
            2 => 30,
            1 => 20,
            _ => 12
        };

        public float Alpha => MiniRoomSpec switch
        {
            2 => 1.2f,
            1 => 1.0f,
            _ => 0.5f
        };

        private int[] Cards;
        public long LastCardChecker { get; private set; }
        private byte FirstPick;
        public int[] Score;
        
        public MemoryGame() : base(2)
        {
            
        }

        public override E_MINI_ROOM_TYPE GetTypeNumber() => E_MINI_ROOM_TYPE.MR_MemoryGameRoom;

        protected override bool IsValidMiniRoomSpec()
            => MiniRoomSpec <= 2;

        public override void OnPacket(Opcodes type, Character chr, Packet packet)
        {
            switch (type)
            {
                case Opcodes.MGP_TurnUpCard:
                    OnTurnUpCard(chr, packet);
                    break;
                case Opcodes.MGP_MatchCard:
                    // OnMatchCard(chr, packet);
                    break;
                default:
                    base.OnPacket(type, chr, packet);
                    break;
            }
        }

        protected override void OnUserStart(Character chr, Packet packet)
        {
            ArrangeCard();
            
            base.OnUserStart(chr, packet);

            LastCardChecker = MasterThread.CurrentTime;
            
            var stats0 = FindUserSlot(0).GameStats;
            var stats1 = FindUserSlot(1).GameStats;

            var statsLoser = FindUserSlot(1 - WinnerIndex).GameStats;

            {
                var x = Math.Pow(-1.0, -WinnerIndex);
                var y = Math.Pow(10.0, (x * statsLoser.MatchCardScore * 0.05 + (stats1.MatchCardScore - stats0.MatchCardScore)) * 0.00125);

                PWin[0] = 1.0 / (y + 1.0);
            }
            {
                var x = Math.Pow(-1.0, -(1 - WinnerIndex));
                var y = Math.Pow(10.0, (x * statsLoser.MatchCardScore * 0.05 + (stats0.MatchCardScore - stats1.MatchCardScore)) * 0.00125);

                PWin[1] = 1.0 / (y + 1.0);
            }
            
            
        }

        private void ArrangeCard()
        {
            Cards = new int[CardCount];

            for (byte i = 0, j = 0; i < CardCount; i += 2, j++)
            {
                Cards[i] = j;
                Cards[i + 1] = j;
            }
            
            Cards.Shuffle();
        }

        protected override void EncodeGameStart(Packet packet)
        {
            packet.WriteByte((byte) Cards.Length);
            Cards.ForEach(packet.WriteInt);
        }

        protected override void OnRetreatResult(Character chr, Packet packet)
        {
            // Not possible in MemoryGame
            _log.Error($"{chr.Name} tried to retreat in MemoryGame");
        }

        private void OnTurnUpCard(Character chr, Packet packet)
        {
            if (CurUsers == 0 || !GameOn)
                return;
            
            LastCardChecker = MasterThread.CurrentTime;
            bool firstPick = packet.ReadBool();
            byte cardSlot = packet.ReadByte();
            var Slot = FindUserSlot(chr);

            if (CurTurnUser != Slot || cardSlot >= CardCount)
                return;

            if (firstPick)
            {
                TurnUpCard(true, cardSlot, 0);
                FirstPick = cardSlot;

                return;
            }
            
            if (Cards[cardSlot] == Cards[FirstPick])
            {
                Cards[FirstPick] = 0xFF;
                Cards[cardSlot] = 0xFF;
                Score[Slot]++;

                if (Score.Sum() == CardCount / 2)
                {
                    if (Score[0] == Score[1])
                    {
                        GameResult = GameResults.Tie;
                        OnGameSet(0);
                    }
                    else
                    {
                        OnGameSet(Score[0] < Score[1] ? 1 : 0);
                    }
                }
                else
                {
                    OnChat(chr, MGChatMessage.UserMatchCard);
                }

                TurnUpCard(false, cardSlot, Convert.ToByte(Slot + MaxUsers));
            }
            else
            {
                CurTurnUser ^= 1;
                FirstPick = 0xFF;
                TurnUpCard(false, cardSlot, (byte) Slot);
            }
        }

        private void TurnUpCard(bool firstPick, byte cardSlot, byte nextTurn)
        {
            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte((byte) Opcodes.MGP_TurnUpCard);
            
            p.WriteBool(firstPick);
            p.WriteByte(cardSlot);
            
            if (!firstPick)
            {
                p.WriteByte(FirstPick);
                p.WriteByte(nextTurn);
            }
            
            Broadcast(p, null);
        }

        protected override void UpdatePlayerScore(int idx, Character chr)
        {
            var key = ((Convert.ToByte(chr.GameStats.MatchCardWins + chr.GameStats.MatchCardTies + chr.GameStats.MatchCardLosses > 50) - 1) & 20) + 30;

            if (chr.GameStats.MatchCardScore > 3000)
                key = 20;

            if (GameResult == GameResults.Tie)
            {
                chr.GameStats.MatchCardScore += Convert.ToInt32((0.5 - PWin[idx]) * Alpha * key);
                chr.GameStats.MatchCardTies++;

                return;
            }

            if (WinnerIndex == idx)
            {
                if (GameResult == GameResults.GiveUp)
                    chr.GameStats.MatchCardScore += Convert.ToInt32(((1.0 - PWin[idx]) * Alpha * key) * ((Score[idx] < CardCount / 10) ? 0.1 : 1));
                else
                    chr.GameStats.MatchCardScore += Convert.ToInt32((1.0 - PWin[idx]) * Alpha * key);
                chr.GameStats.MatchCardWins++;
            }
            else
            {
                chr.GameStats.MatchCardScore -= Convert.ToInt32(key * Alpha * PWin[idx]);
                chr.GameStats.MatchCardLosses++;
            }
        }

        protected override void OnTimeOver(Character chr, Packet packet)
        {
            if (CurUsers == 0) return;
            if (MasterThread.CurrentTime - LastCardChecker < 9000) return;

            CurTurnUser ^= 1;

            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte((byte) Opcodes.MGRP_TimeOver);
            p.WriteByte((byte) CurTurnUser);
            Broadcast(p, null);
        }

        protected override void ResetMiniGameData(bool open)
        {
            if (open)
                WinnerIndex = 1;

            LeaveBooked = new bool[2];
            Retreat = new bool[2];
            GameResult = GameResults.OnGoing;
            UserReady = false;
            GameOn = false;
            CurTurnUser = 1 - WinnerIndex;
            Score = new int[2];
        }

        protected override void EncodeMiniGameRecord(Character chr, Packet packet)
        {
            packet.WriteInt((int) GetTypeNumber());
            packet.WriteInt(chr.GameStats.MatchCardWins);
            packet.WriteInt(chr.GameStats.MatchCardTies);
            packet.WriteInt(chr.GameStats.MatchCardLosses);
            packet.WriteInt(chr.GameStats.MatchCardScore);
        }
    }
}