using WvsBeta.Common.Sessions;

namespace WvsBeta.Game.GameObjects.MiniRooms
{
    public abstract class MiniRoomGame : MiniRoomBase
    {
        public enum GameResults
        {
            OnGoing = 0,
            Tie = 1,
            GiveUp = 2,
        }
        
        public enum MGChatMessage
        {
            None = -1,
            UserBan = 0,
            UserTurn = 1,
            UserGiveUp = 2,
            UserRetreatSuccess = 3,
            UserLeave = 4,
            UserLeaveEngage = 5,
            UserLeaveEngageCancel = 6,
            UserEnter = 7,
            UserNotEnoughMoney = 8,
            UserMatchCard = 9,
            Game10SecAlert = 101,
            GameStart = 102,
            TournamentMatchEnd = 103,
        }
        
        protected const int PriceOfTheGame = 100;
        
        public GameResults GameResult { get; protected set; }
        public bool[] LeaveBooked { get; protected set; } = new bool[2];
        public bool[] Retreat { get; protected set; } = new bool[2];
        public double[] PWin { get; protected set; } = new double[2];
        public byte WinnerIndex { get; protected set; }
        public int CurTurnUser { get; protected set; }
        public bool UserReady { get; protected set; }

        public int BalloonSN { get; private set; }

        protected MiniRoomGame(int maxUsers) : base(maxUsers)
        {
        }
        
        
        public override byte GetCloseType() => (byte) (Tournament ? 2 : 1);
        
        public override void EncodeEnter(Character chr, Packet packet)
        {
            EncodeMiniGameRecord(chr, packet);
        }

        public void OnChat(Character chr, MGChatMessage messageCode)
        {
            var slot = FindUserSlot(chr);
            if (CurUsers == 0 || slot < 0) return;

            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.MRP_Chat);
            p.WriteByte(Opcodes.MRP_CreateResult);
            p.WriteByte(messageCode);
            p.WriteString(chr.VisibleName);
            Broadcast(p, null);
        }
        
        public override void EncodeEnterResult(Character chr, Packet packet)
        {
            if (MaxUsers > 0)
            {
                ForEachCharacter((idx, x) =>
                {
                    packet.WriteByte((byte) idx);
                    EncodeMiniGameRecord(x, packet);
                });
            }

            packet.WriteByte(255);

            packet.WriteString(Title);
            packet.WriteByte(MiniRoomSpec);
            packet.WriteBool(Tournament);

            if (Tournament)
                packet.WriteByte(Round);

            UserReady = Tournament && CurUsers == MaxUsers;
        }
        
        public override void EndGame()
        {
            GameResult = GameResults.Tie;
            OnGameSet(0);

            ProcessLeaveRequest();
        }
        
        public override void OnPacket(Opcodes type, Character chr, Packet packet)
        {
            switch (type)
            {
                case Opcodes.MGRP_TieRequest:
                    OnTieRequest(chr, packet);
                    break;
                case Opcodes.MGRP_TieResult:
                    OnTieResult(chr, packet);
                    break;
                case Opcodes.MGRP_GiveUpRequest:
                    OnGiveUpRequest(chr, packet);
                    break;
                case Opcodes.MGRP_RetreatRequest:
                    OnRetreatRequest(chr, packet);
                    break;
                case Opcodes.MGRP_RetreatResult:
                    OnRetreatResult(chr, packet);
                    break;
                case Opcodes.MGRP_LeaveEngage:
                    OnUserLeaveEngage(chr, packet);
                    break;
                case Opcodes.MGRP_LeaveEngageCancel:
                    OnUserLeaveEngageCancel(chr, packet);
                    break;
                case Opcodes.MGRP_Ready:
                    OnUserReady(chr, packet);
                    break;
                case Opcodes.MGRP_CancelReady:
                    OnUserCancelReady(chr, packet);
                    break;
                case Opcodes.MGRP_Ban:
                    OnUserBanRequest(chr, packet);
                    break;
                case Opcodes.MGRP_Start:
                    OnUserStart(chr, packet);
                    break;
                case Opcodes.MGRP_TimeOver:
                    OnTimeOver(chr, packet);
                    break;
            }
        }


        public override ErrorMessage IsAdmitted(Character chr, Packet packet, bool onCreate)
        {
            var ret = base.IsAdmitted(chr, packet, onCreate);
            if (ret != 0) return ret;

            if (Tournament)
            {
                if (onCreate) MiniRoomSpec = 2;
                return 0;
            }

            if (chr.Inventory.Mesos < PriceOfTheGame)
            {
                return ErrorMessage.NotEnoughMesos;
            }


            if (!onCreate) return 0;

            packet.ReadIntPoint();

            MiniRoomSpec = packet.ReadByte();

            if (!IsValidMiniRoomSpec())
            {
                _log.Error($"User tried to make memorygame with type {MiniRoomSpec}");
                return ErrorMessage.UnableToDoIt;
            }



            // TODO: Field::CheckBalloonAvailable() - SetBalloon
            if (false)
            {
                return ErrorMessage.OtherItemsAtPoint2;
            }

            return 0;
        }
        
        protected void OnTieRequest(Character chr, Packet packet)
        {
            if (CurUsers == 0) return;
            if (!GameOn) return;

            var slot = FindUserSlot(chr);
            var otherUser = FindUserSlot(1 - slot);
            if (otherUser == null) return;

            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.MGRP_TieRequest);
            otherUser.SendPacket(p);
        }
        
        public void OnGiveUpRequest(Character chr, Packet packet)
        {
            if (CurUsers == 0) return;
            if (!GameOn) return;

            var slot = FindUserSlot(chr);
            GameResult = GameResults.GiveUp;
            OnChat(chr, MGChatMessage.UserGiveUp);
            OnGameSet(slot ^ 1);
        }
        
        public void OnUserLeaveEngageCancel(Character chr, Packet packet)
        {
            if (CurUsers == 0) return;
            if (Tournament) return;

            var slot = FindUserSlot(chr);
            if (GameOn)
            {
                if (!LeaveBooked[slot]) return;
                LeaveBooked[slot] = false;
            }

            OnChat(chr, MGChatMessage.UserLeaveEngageCancel);
        }

        public void OnUserReady(Character chr, Packet packet)
        {
            if (CurUsers == 0) return;
            if (GameOn) return;
            if (UserReady) return;

            UserReady = true;
            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.MGRP_Ready);
            Broadcast(p, null);
        }

        public void OnUserCancelReady(Character chr, Packet packet)
        {
            if (CurUsers == 0) return;
            if (GameOn) return;
            if (!UserReady) return;
            if (Tournament) return;

            UserReady = false;
            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.MGRP_CancelReady);
            Broadcast(p, null);
        }

        public void OnUserLeaveEngage(Character chr, Packet packet)
        {
            if (CurUsers == 0) return;
            if (!GameOn) return;
            if (Tournament) return;

            var slot = FindUserSlot(chr);
            if (LeaveBooked[slot]) return;
            LeaveBooked[slot] = true;
            OnChat(chr, MGChatMessage.UserLeaveEngage);
        }
        
        public override void OnLeave(Character chr, LeaveReason leaveType)
        {
            var slot = FindUserSlot(chr);
            if (GameOn)
            {
                OnGameSet(slot ^ 1);
            }

            if (slot == 0 && !Tournament)
            {
                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.MRP_Leave);
                p.WriteByte(0);
                p.WriteByte(0);
                chr.SendPacket(p);
                // TODO: CField::RemoveBalloon
            }

            UserReady = false;
            WinnerIndex = 1;
        }

        public void OnGameSet(int slot)
        {
            WinnerIndex = (byte) slot;

            if (Tournament)
            {
                var chr = FindUserSlot(GameResult == GameResults.Tie ? 0 : WinnerIndex);
                if (GameResult == GameResults.Tie)
                {
                    if (chr.Field is Map_Tournament mt)
                    {
                        mt.SetWinner(chr.ID, true);
                    }
                }
                else
                {
                    if (chr.Field is Map_Tournament mt)
                    {
                        mt.SetWinner(chr.ID, false);
                    }
                }

                GameOn = false;
                SendResultMessage();
                DoCloseRequest(chr, LeaveReason.MRLeave_Closed, LeaveReason.MRLeave_Closed);

                return;
            }

            if (MaxUsers > 0)
            {
                // TODO: Need to do calculation here

                ForEachCharacter(UpdatePlayerScore);
            }

            UserReady = false;
            GameOn = false;
            SendResultMessage();

            if (LeaveBooked[0])
            {
                // Host quit
                DoCloseRequest(FindUserSlot(0), LeaveReason.MRLeave_HostOut, LeaveReason.MRLeave_Closed);
            }
            else if (LeaveBooked[1])
            {
                // Guy quit
                SetLeaveRequest(1, LeaveReason.MRLeave_Closed);
            }
            else
                SetBalloon(!CloseRequest);
        }



        public void OnUserBanRequest(Character chr, Packet packet)
        {
            if (CurUsers == 0) return;
            if (GameOn) return;
            if (Tournament) return;
            if (FindUserSlot(chr) != 0) return;

            OnChat(chr, MGChatMessage.UserBan);
            SetLeaveRequest(1, LeaveReason.MRLeave_Kicked);
        }
        
        public void OnTieResult(Character chr, Packet packet)
        {
            if (CurUsers == 0) return;
            if (!GameOn) return;

            var slot = FindUserSlot(chr);
            var otherUser = FindUserSlot(1 - slot);
            if (otherUser == null) return;

            if (packet.ReadBool())
            {
                // RIP. He just lost the game
                GameResult = GameResults.Tie;
                OnGameSet(0);
            }
            else
            {
                // Ha, no Tie!

                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.MGRP_TieResult);
                p.WriteByte(0);
                otherUser.SendPacket(p);
            }
        }

        public void OnRetreatRequest(Character chr, Packet packet)
        {
            if (CurUsers == 0) return;
            if (!GameOn) return;
            var slot = FindUserSlot(chr);

            if (Retreat[slot]) return;

            var otherUser = FindUserSlot(1 - slot);
            if (otherUser == null) return;

            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.MGRP_RetreatRequest);
            otherUser.SendPacket(p);
        }
        
        public void SendResultMessage()
        {
            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.MGRP_GameResult);
            p.WriteByte(GameResult);
            if (GameResult != GameResults.Tie)
                p.WriteByte(WinnerIndex);

            ForEachCharacter((idx, chr) =>
            {
                EncodeMiniGameRecord(chr, p);
            });

            Broadcast(p, null);
        }
        
        protected virtual void OnUserStart(Character chr, Packet packet)
        {
            if (GameOn) return;
            if (!UserReady) return;
            if (Tournament) return;
            if (CurUsers < 2) return;

            var slot = FindUserSlot(chr);
            if (slot != 0) return;

            if (MaxUsers > 0)
            {
                // Lets grab some cash
                // There's a bug in BMS where if you have someone in the group with not enough cash
                // the others will still lose mesars
                var notEnoughCash = false;
                ForEachCharacter((idx, chr) =>
                {
                    if (chr.Inventory.Mesos < PriceOfTheGame)
                    {
                        OnChat(chr, MGChatMessage.UserNotEnoughMoney);
                        notEnoughCash = true;
                    }
                });

                if (notEnoughCash) return;

                // Now for reals

                ForEachCharacter((idx, mesoChr) =>
                {
                    mesoChr.AddMesos(-PriceOfTheGame);
                });
            }

            // Piper is paid, lets prepare the game

            ResetMiniGameData(false);

            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.MGRP_Start);
            p.WriteByte(WinnerIndex);
            EncodeGameStart(p);
            Broadcast(p, null);
            OnChat(chr, MGChatMessage.GameStart);

            GameOn = true;

            SetBalloon(true);
        }

        protected abstract void EncodeGameStart(Packet packet);
        protected abstract void UpdatePlayerScore(int idx, Character chr);
        protected abstract void OnRetreatResult(Character chr, Packet packet);
        protected abstract void OnTimeOver(Character chr, Packet packet);
        protected abstract bool IsValidMiniRoomSpec();
        protected abstract void EncodeMiniGameRecord(Character chr, Packet packet);
        protected abstract void ResetMiniGameData(bool open);
    }
}