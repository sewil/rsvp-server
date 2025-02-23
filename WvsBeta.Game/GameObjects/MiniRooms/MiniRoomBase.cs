using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using log4net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Game.GameObjects.MiniRooms
{
    public abstract class MiniRoomBase
    {
        public enum E_MINI_ROOM_TYPE
        {
            MR_NOT_DEFINED = 0,
            MR_OmokRoom = 1,
            MR_MemoryGameRoom = 2,
            MR_TradingRoom = 3,
            MR_PersonalShop = 4,
            MR_EntrustedShop = 5, // Not supported in this version!
            MR_TypeNo,
        }

        public static bool IsValidType(E_MINI_ROOM_TYPE t)
        {
            return t == E_MINI_ROOM_TYPE.MR_OmokRoom ||
                   t == E_MINI_ROOM_TYPE.MR_TradingRoom ||
                   t == E_MINI_ROOM_TYPE.MR_MemoryGameRoom ||
                   t == E_MINI_ROOM_TYPE.MR_PersonalShop;
        }

        public class ReservedSlot
        {
            public long ReservedSince;
            public int CharacterID;
        }
        public bool DoNotRemoveMe { get; set; }

        public string TransactionID = Cryptos.GetNewSessionHash();

        public static ILog _log = LogManager.GetLogger(typeof(MiniRoomBase));
        public static ILog _chatLog = LogManager.GetLogger("MiniRoomChatLog");

        private static Dictionary<int, MiniRoomBase> _miniRooms = new Dictionary<int, MiniRoomBase>();

        private static LoopingID _snCounter { get; } = new LoopingID();

        public int MiniRoomSN { get; protected set; }
        public string Title { get; protected set; }
        public string Password { get; protected set; }
        public byte MaxUsers => (byte) Users.Length;
        public byte CurUsers => (byte) Users.Count(x => x != 0);
        public int[] Users { get; }
        public ReservedSlot[] ReservedTime { get; }
        public LeaveReason[] LeaveRequest { get; }
        public bool Opened { get; protected set; }
        public bool CloseRequest { get; protected set; }
        public bool Tournament { get; protected set; }
        public bool GameOn { get; protected set; }
        public bool Private { get; protected set; }
        public byte Round { get; protected set; }
        public byte MiniRoomSpec { get; protected set; }
        public long OpenTime { get; protected set; }

        public List<string> ChatLog { get; } = new List<string>();

        public Pos Host { get; protected set; }

        protected MiniRoomBase(int maxUsers)
        {
            MiniRoomSN = _snCounter.NextValue();
            Users = new int[maxUsers];
            LeaveRequest = new LeaveReason[maxUsers];
            ReservedTime = new ReservedSlot[maxUsers];
            OpenTime = MasterThread.CurrentTime;

            for (var i = 0; i < maxUsers; i++)
            {
                ReservedTime[i] = new ReservedSlot();
            }
        }

        public bool CheckPassword(string password) => password == Password;

        public void ForEachCharacter(Action<int, Character> action)
        {
            for (var i = 0; i < Users.Length; i++)
            {
                var id = Users[i];

                if (id == 0) continue;

                var ch = Server.Instance.GetCharacter(id);
                if (ch == null)
                {
                    _log.Warn($"Dangling user in ForEachCharacter: {id}");
                    continue;
                }

                action(i, ch);
            }
        }

        public void Broadcast(Packet packet, Character except)
        {
            ForEachCharacter((idx, ch) =>
            {
                if (ch == except) return;
                ch.SendPacket(packet);
            });
        }

        public virtual bool IsEmployer(Character chr) => false;
        public virtual bool IsEntrusted() => false;
        public virtual bool IsManaging() => false;

        public abstract E_MINI_ROOM_TYPE GetTypeNumber();
        public virtual byte GetCloseType() => 254;

        public virtual void SetBalloon(bool open)
        {
            var chr = FindUserSlot(0);
            if (chr == null) return;

            Opened = open;
            chr.SetMiniRoomBalloon(open);
        }

        public static void Enter(Character chr, int serial, Packet packet, bool tournament)
        {
            ErrorMessage result;

            var mr = GetMiniRoom(serial);
            if (mr == null)
            {
                result = ErrorMessage.RoomAlreadyClosed;
            }
            else
            {
                if (mr.Tournament == tournament)
                {
                    result = mr.OnEnterBase(chr, packet);
                    if (result == 0) return;
                }
                else
                {
                    result = ErrorMessage.UnableToEnterTournament;
                }
            }

            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.MRP_EnterResult);
            p.WriteByte(0);
            p.WriteByte(result);
            chr.SendPacket(p);
        }

        public static void InviteResult(Character chr, int serial, InviteResults results)
        {
            var mr = GetMiniRoom(serial);
            if (mr == null) return;

            // Check if user was actually invited
            var slot = mr.FindEmptySlot(chr.ID);
            if (slot == -1)
            {
                _log.Warn("User tried to enter room that he was not invited for (or invite expired)");
                return;
            }

            var owner = mr.FindUserSlot(0);
            if (owner == null)
            {
                _log.Error($"Room exists without an owner? SN {serial}");
                return;
            }

            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.MRP_InviteResult);
            p.WriteByte(results); // TODO: Validate input
            p.WriteString(chr.Name);
            owner.SendPacket(p);
        }

        public virtual ErrorMessage IsAdmitted(Character chr, Packet packet, bool onCreate)
        {
            if (chr.PrimaryStats.HP == 0) return ErrorMessage.CantWhileDead;
            var map = chr.Field;
            if (Tournament)
            {
                if (onCreate)
                {
                    Private = false;
                }

                return ErrorMessage.IsAdmitted;
            }

            if (map.ID / 1000000 % 100 == 9) return ErrorMessage.CantInMiddleOfEvent;

            var type = GetTypeNumber();

            if (false && (type == E_MINI_ROOM_TYPE.MR_PersonalShop || type == E_MINI_ROOM_TYPE.MR_EntrustedShop) && !map.AcceptPersonalShop)
            {
                return ErrorMessage.BuiltAtMainTown;
            }

            // Not sure what this flag is: SLOBYTE(field->Option) < 0
            if ((type == E_MINI_ROOM_TYPE.MR_OmokRoom || type == E_MINI_ROOM_TYPE.MR_MemoryGameRoom) && false)
            {
                return ErrorMessage.CantStartGameHere;
            }

            var password = "";
            if (packet != null)
                password = packet.ReadBool() ? packet.ReadString() : "";

            if (onCreate)
            {
                Password = password;
                Private = Password != "";
                return ErrorMessage.IsAdmitted;
            }

            if (!CheckPassword(password)) return ErrorMessage.IncorrectPassword;

            return ErrorMessage.IsAdmitted;
        }

        public ErrorMessage OnEnterBase(Character chr, Packet packet)
        {
            var slot = 0;
            if (IsEmployer(chr))
                slot = 0;
            else
                slot = FindEmptySlot(chr.ID);

            if (!IsEntrusted() && (CurUsers == 0 || Users[0] == 0))
            {
                _log.Warn($"{chr.Name} trying to enter miniroom that has no owner");
                return ErrorMessage.Etc;
            }

            if (FindUserSlot(chr) != -1)
            {
                _log.Warn($"{chr.Name} already joined miniroom");
                return ErrorMessage.Etc;
            }

            if (slot < 0)
            {
                _log.Warn($"{chr.Name} tried to join full miniroom");
                return ErrorMessage.FullCapacity;
            }

            if (!IsEmployer(chr) && IsManaging())
            {
                _log.Warn($"{chr.Name} tried to enter miniroom that is being managed");
                return ErrorMessage.IsManaging;
            }

            if (!chr.CanAttachAdditionalProcess)
            {
                _log.Warn($"{chr.Name} tried to enter miniroom because no addition processes left");
                return ErrorMessage.OtherRequests;
            }

            // BMS sets miniroom here, and then removes it in error handler

            var errorCode = IsAdmitted(chr, packet, false);
            if (errorCode > 0)
            {
                return errorCode;
            }

            chr.SetMiniRoom(this);

            Users[slot] = chr.ID;
            ReservedTime[slot].CharacterID = 0;
            LeaveRequest[slot] = LeaveReason.Invalid;

            if (IsEmployer(chr))
            {
                ForEachCharacter((idx, chr) =>
                {
                    if (idx == 0) return;
                    SetLeaveRequest(idx, LeaveReason.ESLeave_StartManage);
                });
                ProcessLeaveRequest();
            }
            else
            {
                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.MRP_Enter);
                EncodeAvatar(slot, p);
                EncodeEnter(chr, p);
                Broadcast(p, chr);
            }

            SendEnterPacket(chr);

            if (Tournament)
            {
                var first = FindUserSlot(0);
                var second = FindUserSlot(1);
                Title = $"{first.VisibleName} VS {second.VisibleName}";
            }

            if ((GetTypeNumber() != E_MINI_ROOM_TYPE.MR_PersonalShop && GetTypeNumber() != E_MINI_ROOM_TYPE.MR_EntrustedShop) ||
                (GetTypeNumber() == E_MINI_ROOM_TYPE.MR_PersonalShop && CurUsers == MaxUsers) ||
                (GetTypeNumber() == E_MINI_ROOM_TYPE.MR_EntrustedShop && CurUsers == MaxUsers - 1))
            {
                SetBalloon(true);
            }

            var owner = FindUserSlot(0);
            if (owner.HuskMode)
            {
                ShowMessage(0, $"{owner.VisibleName} is currently offline. You can keep shopping in this store.", chr);
            }

            return 0;
        }

        public virtual bool CanEnterHuskMode(Character chr)
        {
            return false;

        }

        public virtual void EnterHuskMode(Character chr)
        {
            if (FindUserSlot(chr) != 0)
            {
                return;
            }

            if (!CanEnterHuskMode(chr)) return;

            ShowMessage(0, $"{chr.VisibleName} went offline. You can keep shopping in this store.", null);
            chr.EnterHuskMode();
        }

        public virtual void ResumeFromHuskMode(Character chr)
        {
            SendEnterPacket(chr);
            ChatLog.ForEach(x => ShowMessage(0, x, chr));
            ShowMessage(0, $"{chr.VisibleName} has returned to the store.", null);
        }

        public void SendEnterPacket(Character chr)
        {
            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.MRP_EnterResult);
            p.WriteByte(GetTypeNumber());
            p.WriteByte(MaxUsers);
            p.WriteByte((byte)FindUserSlot(chr));
            for (var i = 0; i < MaxUsers; i++)
                EncodeAvatar(i, p);
            p.WriteByte(255);

            EncodeEnterResult(chr, p);
            chr.SendPacket(p);
        }

        public virtual void OnAvatarChanged(Character chr)
        {
            var slot = FindUserSlot(chr);
            if (CurUsers == 0 || slot < 0) return;

            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.MRP_Avatar);
            p.WriteByte((byte) slot);
            PacketHelper.AddAvatar(p, chr);
            EncodeEnter(chr, p);
            Broadcast(p, chr);
        }

        public virtual void EncodeAvatar(int idx, Packet packet)
        {
            if (idx == 0 && IsEntrusted())
            {
                packet.WriteByte((byte) idx);
                packet.WriteInt(GetEmployeeTemplateID());
                packet.WriteString("Vendor");
            }
            else
            {
                var chr = FindUserSlot(idx);
                if (chr != null)
                {
                    packet.WriteByte((byte) idx);
                    PacketHelper.AddAvatar(packet, chr);
                    packet.WriteString(chr.VisibleName);
                }
            }
        }


        public virtual void EncodeEnter(Character chr, Packet packet)
        {
        }

        public virtual void EncodeEnterResult(Character chr, Packet packet)
        {
        }


        public virtual void OnPacket(Opcodes type, Character chr, Packet packet)
        {
        }

        public void OnInviteBase(Character chr, Packet packet)
        {
            var inviteCharacterId = packet.ReadInt();
            var inviteCharacter = Server.Instance.GetCharacter(inviteCharacterId);

            if (inviteCharacter == null || inviteCharacter == chr || (inviteCharacter.IsGM && !chr.IsGM))
            {
                _log.Info("Tried to invite character that does not exist, is himself or is an admin");
                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.MRP_InviteResult);
                p.WriteByte(InviteResults.UnableToFindCharacter);
                chr.SendPacket(p);
                return;
            }

            if (!inviteCharacter.CanAttachAdditionalProcess || CurUsers == MaxUsers)
            {
                _log.Info("Tried to invite character while that character has no additional processes left, or room is full");
                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.MRP_InviteResult);
                p.WriteByte(InviteResults.DoingSomethingElseRightNow);
                p.WriteString(inviteCharacter.VisibleName);
                chr.SendPacket(p);
                return;
            }

            // Register invitation
            var isRegistered = false;
            for (var slot = 0; slot < Users.Length; slot++)
            {
                if (Users[slot] != 0) continue;

                var rsvp = ReservedTime[slot];
                if (rsvp.CharacterID != 0) continue;

                rsvp.CharacterID = inviteCharacterId;
                rsvp.ReservedSince = long.MaxValue;
                isRegistered = true;
                break;
            }

            if (!isRegistered)
            {
                _log.Error("Tried to invite someone but there are no slots free to register in???");

                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.MRP_InviteResult);
                p.WriteByte(InviteResults.CurrentlyNotAcceptingAnyInvitation);
                p.WriteString(inviteCharacter.Name);
                chr.SendPacket(p);
                return;
            }

            {
                _log.Info($"Inviting {chr.Name} for miniroom SN {MiniRoomSN}");
                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.MRP_Invite);
                p.WriteByte(GetTypeNumber());
                p.WriteString(chr.VisibleName);
                p.WriteInt(MiniRoomSN);
                inviteCharacter.SendPacket(p);
                return;
            }
        }

        public virtual bool ProcessChat(Character chr, string message)
        {
            return true;
        }

        public void OnChat(Character chr, Packet packet)
        {
            var slot = FindUserSlot(chr);
            if (CurUsers == 0 || slot < 0) return;
            var text = packet.ReadString();

            if (!ProcessChat(chr, text)) return;

            if (MessagePacket.GetMuteMessage(chr, out var muteMessage))
            {
                ShowMessage((byte) slot, muteMessage, chr);
            }
            else
            {
                var fullMessage = chr.VisibleName + " : " + text;
                ChatLog.Add(fullMessage);
                ShowMessage((byte) slot, fullMessage, null);
            }
        }

        public void ShowMessage(byte slot, string message, Character receiver)
        {
            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.MRP_Chat);
            p.WriteByte(Opcodes.MRP_GameMessage);
            p.WriteByte((byte) slot);
            p.WriteString(message);
            SaveChat(message, (byte) slot);

            if (receiver == null)
                Broadcast(p, null);
            else
                receiver.SendPacket(p);
        }


        public void SaveChat(string text, byte slot)
        {
            _chatLog.Info(text);
        }

        public enum LeaveReason
        {
            Invalid = -1,

            // NOTE: UserRequest should only be used when the dialog on the user side is not visible!
            MRLeave_UserRequest = 0,
            MRLeave_OpenTimeOver,
            MRLeave_Closed,
            MRLeave_Kicked,
            MRLeave_HostOut,

            // Trades
            TRLeave_TradeDone,
            TRLeave_TradeFail,
            TRLeave_FieldError,

            // Personal Shop
            PSLeave_NoMoreItem,
            PSLeave_KickedTimeOver,

            // Entrusted Shop
            ESLeave_Open,
            ESLeave_StartManage,
            ESLeave_ClosedTimeOver,
            ESLeave_EndManage,
            ESLeave_DestoryByAdmin,
        }

        public void OnLeaveBase(Character chr, Packet packet)
        {
            var slot = FindUserSlot(chr);
            if (CurUsers == 0 || slot < 0) return;

            _log.Info($"{chr.Name} is leaving miniroom SN {MiniRoomSN}");

            var typeNumber = GetTypeNumber();


            if (typeNumber == E_MINI_ROOM_TYPE.MR_MemoryGameRoom)
            {
                DoCloseRequest(chr, LeaveReason.MRLeave_Closed, LeaveReason.MRLeave_UserRequest);
            }
            else if (typeNumber == E_MINI_ROOM_TYPE.MR_TradingRoom)
            {
                DoCloseRequest(chr, LeaveReason.MRLeave_OpenTimeOver, LeaveReason.MRLeave_UserRequest);
            }
            else if (slot == 0)
            {
                if (typeNumber == E_MINI_ROOM_TYPE.MR_OmokRoom)
                {
                    DoCloseRequest(chr, LeaveReason.MRLeave_Kicked, LeaveReason.MRLeave_Kicked);
                }
                else
                {
                    DoCloseRequest(chr, LeaveReason.MRLeave_Closed, LeaveReason.MRLeave_UserRequest);
                }
            }
            else
            {
                DoLeave((byte) slot, LeaveReason.MRLeave_UserRequest, true);

                if (Opened &&
                    (
                        typeNumber != E_MINI_ROOM_TYPE.MR_PersonalShop && typeNumber != E_MINI_ROOM_TYPE.MR_EntrustedShop ||
                        typeNumber == E_MINI_ROOM_TYPE.MR_PersonalShop && CurUsers == MaxUsers - 1 ||
                        typeNumber == E_MINI_ROOM_TYPE.MR_EntrustedShop && CurUsers == MaxUsers - 2
                    )
                )
                {
                    SetBalloon(true);
                }
            }

            if (false)
            {
                if (typeNumber == E_MINI_ROOM_TYPE.MR_MemoryGameRoom)
                {
                    DoCloseRequest(chr, LeaveReason.MRLeave_Closed, LeaveReason.MRLeave_UserRequest);
                }
                else if (typeNumber == E_MINI_ROOM_TYPE.MR_TradingRoom)
                {
                    DoCloseRequest(chr, LeaveReason.MRLeave_OpenTimeOver, LeaveReason.MRLeave_UserRequest);
                }
                else if (typeNumber == E_MINI_ROOM_TYPE.MR_OmokRoom && slot == 0)
                {
                    DoCloseRequest(chr, LeaveReason.MRLeave_Kicked, LeaveReason.MRLeave_Kicked);
                }
                else
                {
                    DoLeave((byte) slot, LeaveReason.MRLeave_UserRequest, true);

                    if (Opened &&
                        (
                            typeNumber != E_MINI_ROOM_TYPE.MR_PersonalShop && typeNumber != E_MINI_ROOM_TYPE.MR_EntrustedShop ||
                            typeNumber == E_MINI_ROOM_TYPE.MR_PersonalShop && CurUsers == MaxUsers - 1 ||
                            typeNumber == E_MINI_ROOM_TYPE.MR_EntrustedShop && CurUsers == MaxUsers - 2
                        )
                    )
                    {
                        SetBalloon(true);
                    }
                }
            }
        }

        public void OnBalloonBase(Character chr, Packet packet)
        {
            if (Tournament) return;

            var slot = FindUserSlot(chr);
            if (slot != 0 && !IsEmployer(chr)) return;


            var typeNumber = GetTypeNumber();

            if ((typeNumber == E_MINI_ROOM_TYPE.MR_PersonalShop || typeNumber == E_MINI_ROOM_TYPE.MR_EntrustedShop) && (Host - chr.Position) > 10)
            {
                _log.Error("Player moved, so closing shop.");

                OnLeave(chr, LeaveReason.MRLeave_Closed);

                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.MRP_Leave);
                p.WriteByte(0);
                p.WriteByte(1);
                chr.SendPacket(p);

                Users[slot] = 0;
                chr.SetMiniRoom(null);

                RemoveMiniRoom();

                return;
            }

            Opened = true;
            var open = packet.ReadBool();
            if (IsEntrusted())
            {
                // TODO: CreateEmployee(open)
                RegisterOpenTime();
                SetLeaveRequest(0, LeaveReason.ESLeave_EndManage);
            }
            else
            {
                chr.SetMiniRoomBalloon(open);
            }

            SendItemList(chr);
        }

        public virtual void SendItemList(Character chr)
        {
            // THIS IS ACTUALLY TO SEND ITEMS TO CENTER.
            // WE DONT CARE
        }

        public virtual void RegisterOpenTime()
        {
            // THIS IS ACTUALLY TO SEND DATA TO CENTER.
            // WE DONT CARE
        }

        public virtual void EndGame()
        {
        }

        /// <param name="leaveType2">Leave message for 'chr', if chr != null</param>
        public void DoCloseRequest(Character chr, LeaveReason leaveType, LeaveReason leaveType2)
        {
            CloseRequest = true;
            if (MaxUsers <= 0) return;

            for (var i = 0; i < LeaveRequest.Length; i++)
            {
                LeaveRequest[i] = Users[i] != chr?.ID ? leaveType : leaveType2;
            }
        }

        public virtual void OnLeave(Character chr, LeaveReason leaveType)
        {
        }

        public virtual void EncodeLeave(LeaveReason leaveType, Packet packet) {}

        public void OnUserLeave(Character chr)
        {
            if (IsEmployer(chr) && Opened)
            {
                // OnPacketBase(Opcodes. OnWithdrawAll);
            }
            else
            {
                OnPacketBase(Opcodes.MRP_Leave, chr, null);
            }
        }

        public void DoLeave(int idx, LeaveReason leaveType, bool broadCast)
        {
            var character = FindUserSlot(idx);
            if (character == null) return;

            _log.Info($"{character.Name} leaves miniroom {leaveType}");

            OnLeave(character, leaveType);

            if (leaveType > 0)
            {
                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.MRP_Leave);
                p.WriteByte((byte) idx);
                p.WriteByte(leaveType);
                EncodeLeave(leaveType, p);
                character.SendPacket(p);
            }

            character.SetMiniRoom(null);
            Users[idx] = 0;

            if (broadCast)
            {
                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.MRP_Leave);
                p.WriteByte((byte) idx);
                // Missing in BMS?
                p.WriteByte(leaveType);
                EncodeLeave(leaveType, p);
                Broadcast(p, character);
            }

            if (IsEntrusted() && leaveType == LeaveReason.MRLeave_Kicked ||
                !IsEntrusted() && CurUsers == 0)
            {
                RemoveMiniRoom();
            }
            

            if (character.InvokeForcedReturnOnShopExit)
            {
                character.InvokeForcedReturnOnShopExit = false;
                var forcedReturn = character.Field.ForcedReturn;
                    
                if (forcedReturn != Constants.InvalidMap)
                {
                    _log.Info($"Moving player to {forcedReturn} because person returned in ForcedReturn map");

                    character.ChangeMap(forcedReturn);
                }
            }
        }

        public void SetLeaveRequest(int idx, LeaveReason leaveType)
        {
            LeaveRequest[idx] = leaveType;
        }

        public void ProcessLeaveRequest()
        {
            var typeNumber = GetTypeNumber();

            ForEachCharacter((idx, chr) =>
            {
                if (LeaveRequest[idx] == LeaveReason.Invalid) return;

                if (idx == 0 && CloseRequest)
                    SetBalloon(false);

                DoLeave((byte) idx, LeaveRequest[idx], !IsManaging());

                if (Opened &&
                    (
                        typeNumber != E_MINI_ROOM_TYPE.MR_PersonalShop && typeNumber != E_MINI_ROOM_TYPE.MR_EntrustedShop ||
                        typeNumber == E_MINI_ROOM_TYPE.MR_PersonalShop && CurUsers == MaxUsers - 1 ||
                        typeNumber == E_MINI_ROOM_TYPE.MR_EntrustedShop && CurUsers == MaxUsers - 2
                    )
                )
                {
                    SetBalloon(true);
                }
            });
        }

        public void OnPacketBase(Opcodes type, Character chr, Packet packet)
        {
            switch (type)
            {
                case Opcodes.MRP_Invite:
                    OnInviteBase(chr, packet);
                    break;
                case Opcodes.MRP_Chat:
                    OnChat(chr, packet);
                    break;
                case Opcodes.MRP_Leave:
                    OnLeaveBase(chr, packet);
                    break;
                case Opcodes.MRP_Balloon:
                    OnBalloonBase(chr, packet);
                    break;
                default:
                    OnPacket(type, chr, packet);
                    break;
            }

            ProcessLeaveRequest();
        }

        public int FindEmptySlot(int characterID)
        {
            foreach (var vt in ReservedTime)
            {
                if (vt == null) continue;
                if (MasterThread.CurrentTime - 30000 < vt.ReservedSince) continue;
                vt.ReservedSince = 0;
                vt.CharacterID = 0;
            }

            if (MaxUsers <= 1) return -1;

            for (var i = 1; i < MaxUsers; i++)
            {
                var reserveInfo = ReservedTime[i];
                var slotInfo = Users[i];

                if (characterID != 0 && reserveInfo.CharacterID == characterID)
                {
                    return i;
                }

                if (slotInfo == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        public int FindUserSlot(Character chr)
        {
            var slot = -1;
            ForEachCharacter((i, x) =>
            {
                if (x == chr) slot = i;
            });

            return slot;
        }

        public Character FindUserSlot(int slot)
        {
            Character chr = null;
            ForEachCharacter((i, x) =>
            {
                if (i == slot) chr = x;
            });

            return chr;
        }


        public virtual void Update(long cur)
        {
        }

        public static void UpdateMiniRoom(long cur) => _miniRooms.Values.ForEach(x => x.Update(cur));

        public static MiniRoomBase GetMiniRoom(int serial)
        {
            _miniRooms.TryGetValue(serial, out var mr);
            return mr;
        }

        public virtual void StartGame()
        {
        }

        public virtual int GetEmployeeFieldID() => 0;
        public virtual int GetEmployeeTemplate() => 0;
        public virtual int GetEmployeeTemplateID() => 0;
        public virtual int GetEmployerID() => 0;
        public virtual string GetEmployerName() => "";

        public static void Create(Character chr, E_MINI_ROOM_TYPE type, Packet packet, bool tournament, byte round)
        {
            // There's an error in BMS here: tournament get passed for rounds.
            // This will not create an error in their case, as only tournaments have rounds
            // but it is confusing.
            var mr = MiniRoomFactory(type, packet, round);
            if (mr == null) return;


            var ret = mr.OnCreateBase(chr, packet, round);

            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.MRP_EnterResult);
            if (ret != 0)
            {
                _log.Info($"Opening {type} miniroom returned error {ret}");
                p.WriteByte(E_MINI_ROOM_TYPE.MR_NOT_DEFINED);
                p.WriteByte((byte) ret);
            }
            else
            {
                _log.Info($"Opened miniroom {type} SN {mr.MiniRoomSN}");
                p.WriteByte((byte) mr.GetTypeNumber());
                p.WriteByte(mr.MaxUsers);
                p.WriteByte(0);
                mr.EncodeAvatar(0, p);
                p.WriteByte(255);
                mr.EncodeEnterResult(chr, p);
            }

            chr.SendPacket(p);
        }

        public ErrorMessage OnCreateBase(Character chr, Packet packet, byte round)
        {
            if (!chr.CanAttachAdditionalProcess) return ErrorMessage.OtherRequests;

            // BMS oddity here: SetMiniRoom would be enabled here, and in error case removed.

            Host = chr.Position.Clone();
            var ret = IsAdmitted(chr, packet, true);
            if (ret != 0)
            {
                return ret;
            }

            chr.SetMiniRoom(this);

            Users[0] = chr.ID;
            // ReservedTime is already set on creation
            LeaveRequest[0] = LeaveReason.Invalid;
            Round = round;
            _miniRooms[MiniRoomSN] = this;


            return 0;
        }

        public static MiniRoomBase MiniRoomFactory(E_MINI_ROOM_TYPE type, Packet packet, byte round)
        {
            MiniRoomBase mrb;
            switch (type)
            {
                case E_MINI_ROOM_TYPE.MR_TradingRoom:
                    mrb = new TradingRoom();
                    break;

                case E_MINI_ROOM_TYPE.MR_OmokRoom:
                    mrb = new Omok();
                    if (round == 0)
                        mrb.Title = packet.ReadString();

                    mrb.Tournament = round > 0;
                    break;
                case E_MINI_ROOM_TYPE.MR_MemoryGameRoom:
                    mrb = new MemoryGame();

                    if (round == 0)
                        mrb.Title = packet.ReadString();

                    mrb.Tournament = round > 0;
                    break;

                case E_MINI_ROOM_TYPE.MR_PersonalShop:
                    mrb = new PersonalShop();
                    mrb.Title = packet?.ReadString() ?? "My little shoppy";
                    return mrb;
                default:
                    return null;
            }

            return mrb;
        }

        public void RemoveMiniRoom()
        {
            _log.Info($"Removing miniroom SN {MiniRoomSN}");
            _miniRooms.Remove(MiniRoomSN);
        }

        public enum Opcodes : byte
        {
            // 
            MRP_Create = 0,
            MRP_CreateCancel,
            MRP_Invite,
            MRP_InviteResult,
            MRP_Enter,
            MRP_EnterResult,
            MRP_Chat,
            MRP_CreateResult,
            MRP_GameMessage,
            MRP_Avatar,
            MRP_Leave,
            MRP_Balloon,
            // ???? 12 is unknown

            // Trade
            TRP_PutItem = 13,
            TRP_PutMoney,
            TRP_Trade,
            // Entrusted Shop
            /*
            ESP_PutItem,
            ESP_BuyItem,
            ESP_PutPurchaseItem,
            ESP_SellItem,
            ESP_BuyResult,
            ESP_Refresh,
            ESP_AddSoldItem,
            ESP_MoveItemToInventory,
            ESP_GoOut,
            ESP_ArrangeItem,
            ESP_WithdrawAll,
            ESP_WithdrawAllResult,
            ESP_WithdrawMoney,
            ESP_WithdrawMoneyResult,
            ESP_AdminChangeTitle,
            ESP_DeliverVisitList,
            ESP_DeliverBlackList,
            ESP_AddBlackList,
            ESP_DeleteBlackList,
            ESP_SetTitle,
            */

            // Personal Shop
            PSP_PutItem = 18,
            PSP_BuyItem,
            PSP_BuyResult,
            PSP_Refresh,
            PSP_AddSoldItem,
            PSP_MoveItemToInventory,

            /*
            PSP_PutItem,
            PSP_BuyItem,
            PSP_PutPurchaseItem,
            PSP_BuyResult,
            PSP_SellItem,
            PSP_SellResult,
            PSP_Refresh,
            PSP_AddSoldItem,
            PSP_MoveItemToInventory,
            PSP_Ban,
            PSP_KickedTimeOver,
            PSP_SetTitle,
            PSP_TradeRestraintItem,
            */

            MGRP_TieRequest = 24,
            MGRP_TieResult,
            MGRP_GiveUpRequest,
            MGRP_GiveUpResult,
            MGRP_RetreatRequest,
            MGRP_RetreatResult,
            MGRP_LeaveEngage,
            MGRP_LeaveEngageCancel,
            MGRP_Ready,
            MGRP_CancelReady,
            MGRP_Ban,
            MGRP_Start,
            MGRP_GameResult,
            MGRP_TimeOver,

            // Omok
            ORP_PutStoneChecker,
            ORP_InvalidStonePosition,
            ORP_InvalidStonePosition_Normal,
            ORP_InvalidStonePosition_By33,

            // MatchGame
            MGP_TurnUpCard,
            MGP_MatchCard,


            PSP_SetOpened = 0xF0,
            PSP_Logout = 0xF1,
        }

        public enum ErrorMessage : byte
        {
            // Not official name, but OK
            IsAdmitted = 0,

            RoomAlreadyClosed = 1,
            FullCapacity = 2,
            OtherRequests = 3,
            CantWhileDead = 4,
            CantInMiddleOfEvent = 5,
            UnableToDoIt = 6,
            OtherItemsAtPoint = 7,
            Etc = 8, // No popup
            Trade2OnSameMap = 9,
            CantEstablishRoom = 10,
            CantStartGameHere = 11,
            BuiltAtMainTown = 12,
            UnableToEnterTournament = 13,
            OtherItemsAtPoint2 = 14,
            NotEnoughMesos = 15,
            IncorrectPassword = 16,

            // TODO: Not implemented
            IsManaging = 8,
        }

        public enum InviteResults : byte
        {
            Nothing = 0,
            UnableToFindCharacter = 1,

            // + str
            DoingSomethingElseRightNow = 2,

            // + str
            DeniedInvitation = 3,

            // + str
            CurrentlyNotAcceptingAnyInvitation = 4,
        }
    }
}