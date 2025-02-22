using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using log4net;
using MySqlConnector;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.GameObjects;
using WvsBeta.Game.GameObjects.MiniRooms;
using WvsBeta.Game.Packets;
using WvsBeta.SharedDataProvider.Templates;

namespace WvsBeta.Game
{
    public partial class Character : CharacterBase, IFieldObj
    {
        private static ILog _characterLog = LogManager.GetLogger("CharacterLog");

        // WorldServer Event (EXP rate)
        public static int ms_nIncExpRate_WSE = 100;

        // Regular EXP rate
        public static int ms_nIncEXPRate => (int)(Server.Instance.RateMobEXP * 100);

        // When married
        public static int ms_nIncExpRate_Wedding = 100;

        // User-specific exp rate
        public double m_dIncExpRate = 1.0;
        // No exp rate ticket
        public double m_dIncDropRate = 1.0;
        public double m_dIncDropRate_Ticket = 1.0;

        public static int ms_nPartyBonusEventRate = 0;


        public int UserID { get; set; }
        public short MapChair { get; set; } = -1;
        private DateTime LastSavepoint;
        private long LastPlayTimeSave;

        public Map Field { get; set; }
        public override int MapID => Field.ID;
        public byte MapPosition { get; set; }
        public byte PortalCount { get; set; } = 0;

        public bool GMAutoHideEnabled { get; set; } = true;
        public bool GMHideEnabled { get; private set; }
        public bool BetaPlayer { get; private set; }
        public bool HuskMode { get; private set; }
        
        public GameObjects.MiniRooms.MiniRoomBase RoomV2 { get; set; }
        public bool MiniRoomBalloon { get; set; }

        public CharacterInventory Inventory { get; private set; }
        public CharacterSkills Skills { get; private set; }
        public CharacterBuffs Buffs { get; private set; }
        public CharacterPrimaryStats PrimaryStats { get; private set; }
        public Rand32 CalcDamageRandomizer { get; private set; }
        public Rand32 RndActionRandomizer { get; private set; }
        public CharacterSummons Summons { get; private set; }
        public CharacterStorage Storage { get; private set; }
        public CharacterQuests Quests { get; private set; }
        public CharacterVariables Variables { get; private set; }
        public CharacterGameStats GameStats { get; private set; }
        public CharacterMonsterBook MonsterBook { get; private set; }
        
        public long PetCashId { get; set; }

        public Ring pRing { get; set; }

        public List<int> Wishlist { get; private set; }

        public object NpcSession { get; set; } = null;
        public int ShopNPCID { get; set; } = 0;
        public int TrunkNPCID { get; set; } = 0;

        public Player Player { get; set; }

        public bool Undercover { get; set; }
        public string ImitatedName { get; private set; }
        public string VisibleName => ImitatedName ?? Name;
        public DateTime MutedUntil { get; set; }
        public byte MuteReason { get; set; }

        public long LastChat { get; set; }
        
        public string ReferralCode { get; set; }
        public int ReferredBy { get; set; }

        private bool _godMode;

        public bool GMGodMode
        {
            get => IsGM && _godMode;
            set => _godMode = value;
        }

        private int _fixedDamage = -1;
        public int GMFixedDamage 
        {
            get => IsGM ? _fixedDamage : -1;
            set => _fixedDamage = value;
        }

        public bool IsInNPCChat => ShopNPCID != 0 || TrunkNPCID != 0 || NpcSession != null;
        public bool IsInMiniRoom => RoomV2 != null;
        public bool CanAttachAdditionalProcess
        {
            get
            {
                var ret = CanAttachAdditionalProcessSilent;

                if (ret == false)
                {
                    HackLog.Warn($"CanAttachAdditionalProcess: {ShopNPCID}, {TrunkNPCID}, {NpcSession}, {RoomV2}");
                }
                return ret;
            }
        }

        public bool CanAttachAdditionalProcessSilent
        {
            get => !IsInNPCChat && !IsInMiniRoom;
        }
        
        public Guild Guild => Server.Instance.GetGuildForCharacterID(ID);
        // Price to deduct when you start changing guild emblem/mark
        public int SetGuildMarkCost = 0;

        public bool ExclRequestSet { get; set; }
        public bool IsAFK => (MasterThread.CurrentTime - LastMove) > 120000 &&
                             (MasterThread.CurrentTime - LastChat) > 120000;

        /// <summary>
        /// This is the map ID of where the door was casted (not town).
        /// </summary>
        public int DoorMapId = Constants.InvalidMap;
        public long tLastDoor = 0;

        public RateCredits RateCredits { get; private set; }

        public class GuildInviteData
        {
            public int GuildID { get; set; }
            public int InviterID { get; set; }
        }
        public GuildInviteData GuildInvite { get; set; }

        public long LastPingPacket { get; set; }
        public bool MemosSent { get; set; }

        public string ClientUILanguage { get; set; }
        public int ClientActiveCodePage { get; set; }
        public bool ForcedLocation { get; set; }

        public PartyData Party
        {
            get
            {
                PartyData.Parties.TryGetValue(PartyID, out var pd);
                return pd;
            }
        }

        public bool InvokeForcedReturnOnShopExit { get; set; }

        public Character(int CharacterID)
        {
            ID = CharacterID;
        }

        public void SetMiniRoom(GameObjects.MiniRooms.MiniRoomBase room)
        {
            _characterLog.Info($"SetMiniRoom {room}");
            RoomV2 = room;
        }

        public void SetMiniRoomBalloon(bool open)
        {
            var p = new Packet(ServerMessages.MINI_ROOM_BALLOON);
            p.WriteInt(ID);
            
            MiniRoomBalloon = RoomV2 != null && open;
            EncodeMiniRoomBalloon(p);

            Field.SendPacket(p);
        }

        public void EncodeMiniRoomBalloon(Packet p)
        {
            if (MiniRoomBalloon && RoomV2 == null) MiniRoomBalloon = false;

            if (MiniRoomBalloon)
            {
                var r = RoomV2;

                p.WriteByte(r.GetTypeNumber());
                p.WriteInt(r.MiniRoomSN);
                p.WriteString(r.Title);
                p.WriteBool(r.Private);
                p.WriteByte(r.MiniRoomSpec);
                p.WriteByte(r.CurUsers);
                p.WriteByte(r.MaxUsers);
                p.WriteBool(r.GameOn);
            }
            else
            {
                p.WriteByte(0);
            }
        }

        public void SendPacket(byte[] pw)
        {
            Player?.Socket?.SendData(pw);
        }

        public void SendPacket(Packet pw)
        {
            switch ((ServerMessages) pw.Opcode)
            {
                case ServerMessages.MINI_ROOM_BASE:
                    var barr = pw.ToArray();
                    _characterLog.Debug($"MiniRoomLog OUT {(GameObjects.MiniRooms.MiniRoomBase.Opcodes)barr[1]}: {pw}");
                    break;
            }

            Player?.Socket?.SendPacket(pw);
        }

        public PetItem GetSpawnedPet()
        {
            if (PetCashId == 0) return null;
            return Inventory.GetItemByCashID(PetCashId, 5) as PetItem;
        }

        public void HandleDeath(bool tryReviveInCurrentMap)
        {
            if (tryReviveInCurrentMap)
            {
                // Check if we can actually do this
                if (Field is Map_Elimination elim)
                {
                    if (elim.RevivesLeft <= 0)
                    {
                        _characterLog.Info("Tried to revive in elimination map, but no revives left.");
                        tryReviveInCurrentMap = false;
                    }
                    else if (Inventory.TakeItem(Map_Elimination.ReviveCoupon, 1) != 0)
                    {
                        _characterLog.Info("Tried to revive in elimination map, but no coupons left.");
                        tryReviveInCurrentMap = false;
                    }
                    else
                    {
                        elim.RevivesLeft -= 1;
                        elim.UpdateRevives();
                    }
                }
                else
                {
                    _characterLog.Error($"Tried to revive in {Field}, which doesn't allow for revival in map???");
                    tryReviveInCurrentMap = false;
                }
            }

            HackLog.Info("Player will be moved back to town/return map");
            ModifyHP(50, false); // Note: will not update HP just yet! The ChangeMap packet contains the HP

            // Remove all buffs
            PrimaryStats.Reset(true);

            var returnMap = Field.ReturnMap == Constants.InvalidMap ? Field.ID : Field.ReturnMap;
            if (tryReviveInCurrentMap)
            {
                returnMap = Field.ID;
            }

            // There's only 1 map that has this. Its the pharmacy map in kerning
            ChangeMap(returnMap);
        }


        public void SetIncExpRate()
        {
            var currentDateTime = MasterThread.CurrentDate;
            SetIncExpRate(currentDateTime.Day, currentDateTime.Hour);
        }

        public void SetIncExpRate(int day, int hour)
        {
            const int Exp_Normal = 100;
            const int Exp_Premium = 100;
            const int Drop_Normal = 100;
            const int Drop_Premium = 100;

            bool isPremium = false;
            double expRate = 1.0;
            double dropRate = 1.0;

            // TODO: check inventories

            if (ms_nIncEXPRate != 100)
            {
                // Check player range, we don't care lol
                expRate = ms_nIncEXPRate * expRate * 0.01;
            }

            if (isPremium)
            {
                expRate *= 1.2;
            }
            
            // Check inventories for droprate tickets

            if (isPremium)
            {
                expRate *= Exp_Premium * 0.01;
                dropRate *= Drop_Premium * 0.01;
            }
            else
            {
                expRate *= Exp_Normal * 0.01;
                dropRate *= Drop_Normal * 0.01;
            }

            m_dIncDropRate_Ticket = 1.0;

            m_dIncDropRate = dropRate;
            m_dIncExpRate = expRate;

            Trace.WriteLine($"Rates: EXP {m_dIncExpRate}, Drop {m_dIncDropRate}, Drop ticket {m_dIncDropRate_Ticket}");
        }

        public bool IsShownTo(IFieldObj Object)
        {
            if (GMHideEnabled)
            {
                return Object is Character player && player.IsGM;
            }

            return true;
        }

        public void EnterHuskMode()
        {
            _characterLog.Info("Entering Husk Mode");
            HuskMode = true;

            if (PartyID != 0)
            {
                Server.Instance.CenterConnection.LeaveParty(ID);
            }

            Field.RemoveController(this);

            var p = new Packet(CfgServerMessages.CFG_RETURN_TO_LOGIN);
            SendPacket(p);

            Save();

            var player = Player;
            Player = null;
            player.Character = null;
            player.Socket.Loaded = false;

            RedisBackend.Instance.RemovePlayerOnline(UserID);

            // Player should disconnect itself...
            // This is kinda risky, but Disconnecting here makes the packet sometimes not send!
        }


        public void LeaveHuskMode()
        {
            _characterLog.Info("Leaving Husk Mode");
            HuskMode = false;
        }

        public void Destroy(bool cc)
        {
            var chr = this;
            var migrating = Server.Instance.InMigration && !Server.Instance.IsNewServerInMigration;

            Program.MainForm.LogAppend($"{chr.Name} disconnected. CC? {cc} Migrating? {migrating}");

            try
            {
                DoorManager.TryRemoveDoor(this);

                if (chr.MapChair != -1 && !migrating)
                {
                    chr.Field.UsedSeats.Remove(chr.MapChair);
                    chr.MapChair = -1;
                    MapPacket.SendCharacterSit(chr, -1);
                }
            }
            catch (Exception ex)
            {
                _characterLog.Error("Unable to destruct door/map chair", ex);
            }

            try
            {
                chr.DestroyAdditionalProcess();
            }
            catch (Exception ex)
            {
                _characterLog.Error("Unable to destruct AdditionalProcess", ex);
            }

            try
            {
                chr.Field.LeavePlayer(chr);
                chr.Summons.RemoveAllSummons();
            }
            catch (Exception ex)
            {
                _characterLog.Error("Unable to leave map or remove summons", ex);
            }

            try
            {
                chr.FlushDamageLog(true);
            }
            catch (Exception ex)
            {
                _characterLog.Error("Unable to flush damage log", ex);
            }

            if (cc == false)
            {
                chr.Guild?.UpdatePlayer(chr, true);
            }

            Server.Instance.CharacterList.Remove(chr.ID);
            Server.Instance.StaffCharacters.Remove(chr);

            chr.Save();

            Server.Instance.CenterConnection?.UnregisterCharacter(chr.ID, cc);

            Program.MainForm.ChangeLoad(false);

            if (!cc)
            {
                RedisBackend.Instance.RemovePlayerOnline(chr.UserID);
            }

            RedisBackend.Instance.RemovePlayerCCIsBeingProcessed(chr.ID);

            if (Player != null)
            {
                Player.Character = null;
            }
        }

        public void Disconnect()
        {
            WrappedLogging(() =>
            {
                if (Player != null)
                {
                    // We've got a player attached to us
                    Player.Socket?.Disconnect();
                }
                else
                {
                    Destroy(false);
                }
            });
        }

        public void DestroyStorage()
        {
            if (TrunkNPCID != 0)
            {
                Storage.Save();
            }
            TrunkNPCID = 0;
        }

        public void DestroyAdditionalProcess()
        {
            RoomV2?.OnUserLeave(this);
            RoomV2 = null;

            ShopNPCID = 0;
            DestroyStorage();

            switch (NpcSession)
            {
                case NpcChatSession ncs:
                    ncs.Stop();
                    break;
                case IScriptV2 v2:
                    if (v2.WaitingForResponse)
                    {
                        v2.TerminateScript();
                        v2.Dispose();
                    }

                    break;
            }
            NpcSession = null;
        }

        public void TryHideOnMapEnter()
        {
            if (!IsGM || GMHideEnabled) return;
            if (!GMAutoHideEnabled) return;
            if (Undercover) return;
            
            SetHide(true, true);
        }

        public void Save()
        {
            if (ImitatedName != null) return;

            _characterLog.Debug("Saving character...");
            Server.Instance.CharacterDatabase.RunTransaction(comm =>
            {
                var saveQuery = new StringBuilder();

                saveQuery.Append("UPDATE characters SET ");
                saveQuery.Append("skin = '" + Skin + "', ");
                saveQuery.Append("hair = '" + Hair + "', ");
                saveQuery.Append("gender = '" + Gender + "', ");
                saveQuery.Append("eyes = '" + Face + "', ");
                saveQuery.Append("map = '" + MapID + "', ");
                saveQuery.Append("pos = '" + MapPosition + "', ");
                saveQuery.Append("level = '" + PrimaryStats.Level + "', ");
                saveQuery.Append("job = '" + PrimaryStats.Job + "', ");
                saveQuery.Append("chp = '" + PrimaryStats.HP + "', ");
                saveQuery.Append("cmp = '" + PrimaryStats.MP + "', ");
                saveQuery.Append("mhp = '" + PrimaryStats.MaxHP + "', ");
                saveQuery.Append("mmp = '" + PrimaryStats.MaxMP + "', ");
                saveQuery.Append("`int` = '" + PrimaryStats.Int + "', ");
                saveQuery.Append("dex = '" + PrimaryStats.Dex + "', ");
                saveQuery.Append("str = '" + PrimaryStats.Str + "', ");
                saveQuery.Append("luk = '" + PrimaryStats.Luk + "', ");
                saveQuery.Append("ap = '" + PrimaryStats.AP + "', ");
                saveQuery.Append("sp = '" + PrimaryStats.SP + "', ");
                saveQuery.Append("fame = '" + PrimaryStats.Fame + "', ");
                saveQuery.Append("exp = '" + PrimaryStats.EXP + "', ");
                saveQuery.Append($"pet_cash_id = 0x{PetCashId:X16},");
                saveQuery.Append("last_savepoint = '" + LastSavepoint.ToString("yyyy-MM-dd HH:mm:ss") + "' ");
                saveQuery.Append("WHERE ID = " + ID);

                comm.CommandText = saveQuery.ToString();
                comm.ExecuteNonQuery();
            }, Program.MainForm.LogAppend);

            LastPlayTimeSave = MasterThread.CurrentTime;

            Server.Instance.CharacterDatabase.RunTransaction(comm =>
            {
                comm.CommandText = "DELETE FROM character_wishlist WHERE charid = " + ID;
                comm.ExecuteNonQuery();

                var eligibleWishlistItems = Wishlist.Where(x => x != 0).ToArray();

                if (eligibleWishlistItems.Length > 0)
                {
                    var wishlistQuery = new StringBuilder();

                    wishlistQuery.Append("INSERT INTO character_wishlist VALUES ");
                    wishlistQuery.Append(string.Join(", ", eligibleWishlistItems.Select(serial => "(" + ID + ", " + serial + ")")));

                    comm.CommandText = wishlistQuery.ToString();
                    comm.ExecuteNonQuery();
                }
            }, Program.MainForm.LogAppend);

            Inventory.SaveInventory();
            Inventory.SaveCashItems(null);
            Skills.SaveSkills();
            Quests.SaveQuests();
            Variables.Save();
            GameStats.Save();
            MonsterBook.Save();
            RateCredits.Save();

            _characterLog.Debug("Saving finished!");
        }

        public void PartyHPUpdate()
        {
            if (PartyID == 0) return;

            Field
                .GetInParty(PartyID)
                .Where(p => p.ID != ID)
                .ForEach(p => p.SendPacket(PartyPacket.GetHPUpdatePacket(this)));

        }

        public void FullPartyHPUpdate()
        {
            if (PartyID == 0) return;

            var partyMembers = Field.GetInParty(PartyID).ToList();
            
            foreach (var partyMember in partyMembers)
            {
                var updatePacket = PartyPacket.GetHPUpdatePacket(partyMember);
                partyMembers.Where(x => x != partyMember).ForEach(x => x.SendPacket(updatePacket));
            }
        }

        // !loadhusk 30437
        public static Character LoadAsHusk(int characterId, out LoadFailReasons lfr)
        {
            lfr = LoadFailReasons.None;

            if (Server.Instance.GetCharacter(characterId) != null)
            {
                lfr = LoadFailReasons.UserAlreadyOnline;
                return null;
            }
            
            var character = new Character(characterId);
            lfr = character.Load("1.2.3.4");
            if (lfr != LoadFailReasons.None) return null;

            
            Server.Instance.CharacterList[characterId] = character;
            if (character.IsGM)
                Server.Instance.StaffCharacters.Add(character);
            
            character.Field.AddPlayer(character);
            Server.Instance.CenterConnection.RegisterCharacter(character);

            character.IsOnline = true;
            character.HuskMode = true;
            
            MiniRoomBase.Create(character, MiniRoomBase.E_MINI_ROOM_TYPE.MR_PersonalShop, null, false, 0);

            if (character.RoomV2 is PersonalShop ps)
            {
                ps.DoNotRemoveMe = true;
            }


            return character;
        }

        public enum LoadFailReasons
        {
            None,
            UnknownCharacter,
            NotFromPreviousIP,
            UserAlreadyOnline,
            TransitionTimeout
        }

        public LoadFailReasons Load(string IP)
        {
            var loadStartTime = MasterThread.CurrentTime;

            var imitateId = RedisBackend.Instance.GetImitateID(ID);
            var imitating = imitateId.HasValue;
            var originalId = ID;
            if (imitating)
            {
                ID = imitateId.Value;
                _characterLog.Debug($"Loading character {ID} from IP {IP}... (IMITATION from ID {originalId})");
            }
            else
            {
                _characterLog.Debug($"Loading character {ID} from IP {IP}...");
            }

            // Initial load of the original client

            using (var data = (MySqlDataReader)Server.Instance.CharacterDatabase.RunQuery(
                "SELECT " +
                "c.userid, c.name, u.admin, u.referral_code, u.referred_by " +
                "FROM characters c " +
                "LEFT JOIN users u ON u.id = c.userid " +
                "WHERE c.id = @id",
                "@id", originalId))
            {
                if (!data.Read())
                {
                    _characterLog.Debug("Loading failed: unknown character (1st load part).");
                    return LoadFailReasons.UnknownCharacter;
                }
                
                UserID = data.GetInt32("userid");
                Name = data.GetString("name");
                ImitatedName = null;
                GMLevel = data.GetByte("admin");

                ReferralCode = data["referral_code"] as string;
                ReferredBy = data["referred_by"] as int? ?? -1;
            }

            // Loading of the imitated data
            var tmpUserId = UserID;

            using (var data = (MySqlDataReader)Server.Instance.CharacterDatabase.RunQuery(
                "SELECT " +
                "c.*, u.beta, u.quiet_ban_expire, u.quiet_ban_reason " +
                "FROM characters c " +
                "LEFT JOIN users u ON u.id = c.userid " +
                "WHERE c.id = @id",
                "@id", ID))
            {
                if (!data.Read())
                {
                    _characterLog.Debug("Loading failed: unknown character (2nd load part).");
                    if (imitating)
                    {
                        // Reset!
                        RedisBackend.Instance.SetImitateID(originalId, 0);
                    }

                    return LoadFailReasons.UnknownCharacter;
                }

                BetaPlayer = data.GetBoolean("beta");
                UserID = data.GetInt32("userid"); // For cashitem loading
                if (imitating)
                {
                    ImitatedName = data.GetString("name");
                }

                Gender = data.GetByte("gender");
                Skin = data.GetByte("skin");
                Hair = data.GetInt32("hair");
                Face = data.GetInt32("eyes");
                PetCashId = data.GetInt64("pet_cash_id");
                MutedUntil = data.GetDateTime("quiet_ban_expire");
                MuteReason = data.GetByte("quiet_ban_reason");
                LastSavepoint = data.GetDateTime("last_savepoint");
                LastPlayTimeSave = MasterThread.CurrentTime;

                var _mapId = data.GetInt32("map");

                Map field;
                if (!MapProvider.Maps.TryGetValue(_mapId, out field))
                {
                    Program.MainForm.LogAppend(
                        "The map of {0} is not valid (nonexistent)! Map was {1}. Returning to 0", ID, _mapId);
                    field = MapProvider.Maps[0];
                    MapPosition = 0;
                }
                Field = field;

                // Push back player when there's a forced return value
                if (field.ForcedReturn != Constants.InvalidMap)
                {
                    _mapId = field.ForcedReturn;
                    if (!MapProvider.Maps.TryGetValue(_mapId, out field))
                    {
                        Program.MainForm.LogAppend(
                            "The map of {0} is not valid (nonexistent)! Map was {1}. Returning to 0", ID, _mapId);
                        // Note: using Field here
                        Field = MapProvider.Maps[0];
                    }
                    else
                    {
                        Field = MapProvider.Maps[_mapId];
                    }
                    MapPosition = 0;
                }
                else
                {
                    MapPosition = (byte)data.GetInt16("pos");
                }

                // Select portal to spawn on.
                {
                    Portal portal = Field.SpawnPoints.Find(x => x.ID == MapPosition);
                    if (portal == null) portal = Field.GetRandomStartPoint();
                    Position = new Pos(portal.X, portal.Y);
                }
                MoveAction = 0;
                Foothold = 0;

                CalcDamageRandomizer = new Rand32();
                RndActionRandomizer = new Rand32();


                PrimaryStats = new CharacterPrimaryStats(this)
                {
                    Level = data.GetByte("level"),
                    Job = data.GetInt16("job"),
                    Str = data.GetInt16("str"),
                    Dex = data.GetInt16("dex"),
                    Int = data.GetInt16("int"),
                    Luk = data.GetInt16("luk"),
                    HP = data.GetInt16("chp"),
                    MaxHP = data.GetInt16("mhp"),
                    MP = data.GetInt16("cmp"),
                    MaxMP = data.GetInt16("mmp"),
                    AP = data.GetInt16("ap"),
                    SP = data.GetInt16("sp"),
                    EXP = data.GetInt32("exp"),
                    Fame = data.GetInt16("fame"),
                    BuddyListCapacity = data.GetInt32("buddylist_size")
                };

                // Make sure we don't update too many times
                lastSaveStep = CalculateSaveStep();
            }

            Inventory = new CharacterInventory(this);
            Inventory.LoadInventory();

            UserID = tmpUserId;

            Ring.LoadRings(this);

            Skills = new CharacterSkills(this);
            Skills.LoadSkills();

            Storage = new CharacterStorage(this);

            Buffs = new CharacterBuffs(this);

            Summons = new CharacterSummons(this);

            Quests = new CharacterQuests(this);
            Quests.LoadQuests();

            Variables = new CharacterVariables(this);
            Variables.Load();

            GameStats = new CharacterGameStats(this);
            GameStats.Load();
            
            MonsterBook = new CharacterMonsterBook(this);
            MonsterBook.Load();

            Wishlist = new List<int>();
            using (var data = (MySqlDataReader)Server.Instance.CharacterDatabase.RunQuery("SELECT serial FROM character_wishlist WHERE charid = @charid AND serial <> 0", "@charid", ID))
            {
                while (data.Read())
                {
                    Wishlist.Add(data.GetInt32(0));
                }
            }

            InitDamageLog();

            SetIncExpRate();
            
            RateCredits = new RateCredits(this);
            RateCredits.Load();

            // Loading done, switch back ID
            ID = originalId;

            var muteTimeSpan = RedisBackend.Instance.GetCharacterMuteTime(ID);
            HacklogMuted = muteTimeSpan.HasValue ? MasterThread.CurrentDate.Add(muteTimeSpan.Value) : DateTime.MinValue;

            Undercover = RedisBackend.Instance.IsUndercover(ID);

            RedisBackend.Instance.SetPlayerOnline(
                UserID,
                Server.Instance.GetOnlineId()
            );

            _characterLog.Debug($"Loaded! Time (MS) {MasterThread.CurrentTime - loadStartTime}");
            return LoadFailReasons.None;
        }


        public override void SetupLogging()
        {
            base.SetupLogging();
            ThreadContext.Properties["UserID"] = UserID;
            switch (NpcSession)
            {
                case IScriptV2 v2:
                    ThreadContext.Properties["NpcScript"] = v2.ScriptName;
                    break;
                case NpcChatSession ncs:
                    ThreadContext.Properties["NpcScript"] = $"{ncs.mID}-old";
                    break;
            }
        }

        public new static void RemoveLogging()
        {
            CharacterBase.RemoveLogging();
            ThreadContext.Properties.Remove("UserID");
            ThreadContext.Properties.Remove("NpcScript");
        }

        public override void WrappedLogging(Action cb)
        {
            var userID = ThreadContext.Properties["UserID"];
            var npcScript = ThreadContext.Properties["NpcScript"];
            try
            {
                base.WrappedLogging(cb);
            }
            finally
            {
                ThreadContext.Properties["UserID"] = userID;
                ThreadContext.Properties["NpcScript"] = npcScript;
            }
        }

        private void GiveReferralCash()
        {
            if (ReferredBy == -1) return;

            //todo: tweak these numbers
            if (Level == 30)
            {
                int amount = 2000;

                // New player event in July 2022
                if (DateTime.UtcNow >= DateTime.Parse("2022-07-01") && DateTime.UtcNow < DateTime.Parse("2022-08-01"))
                {
                    amount = 4000;
                }

                Server.Instance.CharacterDatabase.AddPointTransaction(
                    ReferredBy,
                    2000,
                    "nx",
                    $"Referral cash gained from user {UserID}"
                );
                
                Server.Instance.CharacterDatabase.AddPointTransaction(
                    UserID,
                    amount,
                    "nx",
                    $"Referral cash gained from leveling to 30 while having referral user {UserID}"
                );
                
                // Set referred id to null so it doesn't give cash again
                Server.Instance.CharacterDatabase.RunQuery(
                    "UPDATE users SET referred_by = NULL WHERE `ID` = @userid",
                    "userid", UserID);
                
                MessagePacket.SendText(MessagePacket.MessageTypes.RedText, "You and your referrer have gained Cash for reaching level 30. Congratulations!", this, MessagePacket.MessageMode.ToPlayer);

                ReferredBy = -1;
            }
        }

        public void CheckPetDead()
        {
            var activePet = GetSpawnedPet();
            if (activePet == null) return;
            if (activePet.DeadDate > MasterThread.CurrentDate.ToFileTimeUtc()) return;

            _characterLog.Info("Pet died.");
            activePet.DeadDate = BaseItem.NoItemExpiration;
            InventoryPacket.UpdateItems(this, activePet);

            PetsPacket.DoPetDespawn(this, PetsPacket.DespawnReason.Died);
        }

        public void UpdateActivePet(long currentTime)
        {
            var activePet = GetSpawnedPet();
            if (activePet == null) return;

            var updated = activePet.Update(currentTime, out var remove);

            if (updated)
            {
                InventoryPacket.UpdateItems(this, activePet);
            }

            if (remove)
            {
                PetsPacket.DoPetDespawn(this, PetsPacket.DespawnReason.Hungry);
            }
        }

        public override string ToString()
        {
            var nameText = Name;
            if (VisibleName != Name) nameText = $"{Name} ({VisibleName})";
            return $"{nameText} lvl {Level} job {Job} ip {Player?.Socket?.IP ?? "not connected"} UI lang {ClientUILanguage} codepage {ClientActiveCodePage}";
        }
    }
}