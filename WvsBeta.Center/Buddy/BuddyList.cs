using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using MySqlConnector;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Center
{
    public class BuddyList
    {
        public static ILog log = LogManager.GetLogger("BuddylistLog");
        private static ILog chatLog = LogManager.GetLogger("BuddyChatLog");

        private byte Capacity;
        private readonly Dictionary<int, BuddyData> Buddies;
        public readonly BuddyData Owner;
        private readonly Queue<BuddyData> BuddyRequests;

        private static readonly Dictionary<int, BuddyList> _buddyLists = new Dictionary<int, BuddyList>();

        public static BuddyList Get(int ownerId)
        {
            if (_buddyLists.TryGetValue(ownerId, out var bl)) return bl;

            return LoadBuddyList(ownerId);
        }

        public static BuddyList Get(string ownerName)
        {
            var bl = _buddyLists.Values.FirstOrDefault(x => x.Owner.CharacterName.Equals(ownerName, StringComparison.CurrentCultureIgnoreCase));

            if (bl != null) return bl;

            log.Info("Did not find characters buddylist in local cache, fetching from DB...");

            return LoadBuddyList(ownerName);
        }

        public static bool Remove(int ownerId) =>  _buddyLists.Remove(ownerId);

        private BuddyList(byte cap, int ownerId, string ownerName)
        {
            Capacity = cap;
            Owner = new BuddyData(ownerId, ownerName);
            Buddies = new Dictionary<int, BuddyData>(Capacity);
            BuddyRequests = new Queue<BuddyData>();

            _buddyLists[Owner.CharacterID] = this;
        }


        public BuddyList(Packet pr)
        {
            Capacity = pr.ReadByte();
            Owner = new BuddyData(pr);
            Buddies = new Dictionary<int, BuddyData>(Capacity);
            BuddyRequests = new Queue<BuddyData>();

            int count = pr.ReadByte();
            for (var i = 0; i < count; i++)
            {
                var buddy = new BuddyData(pr);
                Buddies.Add(buddy.CharacterID, buddy);
            }

            count = pr.ReadByte();
            for (var i = 0; i < count; i++)
            {
                var buddy = new BuddyData(pr);
                BuddyRequests.Enqueue(buddy);
            }

            _buddyLists[Owner.CharacterID] = this;
        }

        public void EncodeForTransfer(Packet pw)
        {
            pw.WriteByte(Capacity);
            Owner.EncodeForTransfer(pw);

            pw.WriteByte((byte)Buddies.Count);
            Buddies.ForEach(x => x.Value.EncodeForTransfer(pw));

            pw.WriteByte((byte)BuddyRequests.Count);
            BuddyRequests.ForEach(x => x.EncodeForTransfer(pw));
        }

        public bool IsFull() => Buddies.Values.Count >= Capacity;

        public bool HasBuddy(int id) => Buddies.ContainsKey(id);

        public bool HasBuddy(BuddyData bud) => HasBuddy(bud.CharacterID);

        public bool Add(BuddyData buddy, bool packet = true)
        {
            if (HasBuddy(buddy) || IsFull()) return false;
            Buddies[buddy.CharacterID] = buddy;

            if (packet)
            {
                SendBuddyList(FriendResReq.FriendRes_SetFriend_Done);
            }

            return true;
        }

        public void SendNextAwaitingRequest()
        {
            if (Owner.InCashShop()) return;

            if (!BuddyRequests.TryPeek(out var nextEntry)) return;

            SendInviteFrom(nextEntry);
        }

        public void RemoveBuddyOrRequest(int victimId)
        {
            DisbandFriendshipInDatabase(Owner.CharacterID, victimId);

            // Check if we already accepted it, if so, just remove the buddy
            if (Buddies.TryGetValue(victimId, out var buddy))
            {
                log.Info($"{Owner.CharacterName} removed buddy {buddy.CharacterName}");
                RemoveBuddy(victimId);
                // Try to remove us from the others buddylist.
                // Make sure to check if we can load the buddylist, as 
                // the user might've deleted his character.
                var victim = Get(victimId);
                if (victim != null) victim.RemoveBuddy(Owner.CharacterID);
                return;
            }

            // Try to check the first entry

            if (!BuddyRequests.TryPeek(out var br))
            {
                log.Warn($"[{Owner.CharacterName}] Possible exploit in buddy invite, buddy request was not accepted, but no request was open. ");
                return;
            }

            if (br.CharacterID == victimId)
            {
                log.Info($"{Owner.CharacterName} declined invite of {br.CharacterName}");
                BuddyRequests.Dequeue();
            }
            else
            {
                log.Warn($"{Owner.CharacterName} Trying to remove buddy (or blocked invite of) a buddy that didn't invite in the first place?");
            }

            SendNextAwaitingRequest();
        }

        public void RemoveBuddy(int victimId)
        {
            Buddies.Remove(victimId);
            SendBuddyList(FriendResReq.FriendRes_DeleteFriend_Done);
        }

        public void AcceptRequest()
        {
            if (!BuddyRequests.TryDequeue(out var requestor))
            {
                log.Info($"[{Owner.CharacterName}] tried accepting buddy request, but none available.");
                return;
            }

            var requestorBl = requestor.GetBuddyList();

            log.Info($"[{Owner.CharacterName}] Buddylist request accepted of {requestor.CharacterName}");

            Add(requestor);
            requestorBl.Add(Owner);

            SaveBuddiesToDb();
            requestorBl.SaveBuddiesToDb();

            SendNextAwaitingRequest();
        }

        public static void HandleBuddyInvite(Packet packet)
        {
            var stalkerCharacterID = packet.ReadInt();
            packet.ReadString(); // old inviter name
            var victimNameFromPacket = packet.ReadString();

            var stalkerBuddyList = Get(stalkerCharacterID);

            if (stalkerBuddyList == null)
            {
                log.Error($"Unable to handle invites for a character ({stalkerCharacterID}) that has no buddylist.");
                return;
            }

            var stalkerName = stalkerBuddyList.Owner.CharacterName;
            var stalkerAdminLevel = GetAdminLevel(stalkerCharacterID);
            
            var victimBuddyList = Get(victimNameFromPacket);

            if (victimBuddyList == null)
            {
                stalkerBuddyList.SendRequestError(FriendResReq.FriendRes_SetFriend_UnknownUser);
                return;
            }

            var victimCharacterID = victimBuddyList.Owner.CharacterID;

            var victimName = victimBuddyList.Owner.CharacterName;
            var victimAdminLevel = GetAdminLevel(victimCharacterID);


            log.Info($"{stalkerName} ({stalkerCharacterID}) tries to be buddies with {victimName} ({victimCharacterID})...");

            if (victimCharacterID == stalkerCharacterID)
            {
                log.Warn($"[{stalkerName}] Buddylist invite failed: inviting itself");
                // There's no error to show?
                return;
            }

            if (stalkerBuddyList.IsFull())
            {
                log.Warn($"[{stalkerName}] Buddylist invite failed: self is full");
                stalkerBuddyList.SendRequestError(FriendResReq.FriendRes_SetFriend_FullMe);
                return;
            }

            if (victimBuddyList.IsFull())
            {
                log.Warn($"[{stalkerName}] Buddylist invite failed: other is full");
                stalkerBuddyList.SendRequestError(FriendResReq.FriendRes_SetFriend_FullOther);
                return;
            }

            if (stalkerAdminLevel == 0 && victimAdminLevel != 0)
            {
                log.Warn($"[{stalkerName}] Buddylist invite failed: nonadmin (lvl {stalkerAdminLevel}) to admin (lvl {victimAdminLevel}) invite.");
                stalkerBuddyList.SendRequestError(FriendResReq.FriendRes_SetFriend_Master);
                return;
            }

            if (stalkerBuddyList.HasBuddy(victimBuddyList.Owner))
            {
                log.Warn($"[{stalkerName}] Buddylist invite failed: already as buddy");
                stalkerBuddyList.SendRequestError(FriendResReq.FriendRes_SetFriend_AlreadySet);
                return;
            }

            // Already invited the player
            if (victimBuddyList.BuddyRequests.Any(x => x.CharacterID == stalkerCharacterID))
            {
                log.Warn($"[{stalkerName}] Buddylist invite failed: already invited {victimName}");
                return;
            }

            // Already got an invite back
            if (stalkerBuddyList.BuddyRequests.Any(x => x.CharacterID == victimCharacterID))
            {
                log.Warn($"[{stalkerName}] Buddylist invite failed: already invited by {victimName}");
                return;
            }


            log.Info($"[{stalkerName}] invited {victimName}");

            victimBuddyList.BuddyRequests.Enqueue(stalkerBuddyList.Owner);
            victimBuddyList.SendNextAwaitingRequest();
            victimBuddyList.SaveBuddiesToDb();
        }

        public void IncreaseCapacity(byte amount)
        {
            var newCapacity = Capacity + amount;
            if (newCapacity > Constants.MaxBuddyListCapacity)
            {
                log.Warn($"[{Owner.CharacterName}] tried to increase buddylist capacity to {newCapacity}");

                SendCapacityChangeFailed();
                return;
            }

            Capacity = (byte)newCapacity;
            log.Info($"[{Owner.CharacterName}] Increasing buddylist capacity to {Capacity}");
            SendCapacityChange();

            SaveBuddiesToDb();
        }

        public void OnOnlineCC(bool toSave = true, bool disconnected = false)
        {
            Buddies.Values.ToList()
                .FindAll(b => Owner.CanChatTo(b))
                .ForEach(buddy => buddy.GetBuddyList().SendUpdate(Owner, disconnected));

            if (toSave == true)
            {
                SaveBuddiesToDb();
            }
        }

        public void OwnerRenamed(string newName)
        {
            Owner.CharacterName = newName;

            foreach (var buddy in Buddies.Values)
            {
                var buddyBuddylist = buddy.GetBuddyList();
                // Update BuddyData of the owner in the data of the user
                if (buddyBuddylist.Buddies.TryGetValue(Owner.CharacterID, out var x))
                {
                    x.CharacterName = newName;
                    
                    buddyBuddylist.SendUpdate(x, false);
                    buddyBuddylist.SaveBuddiesToDb();
                }
            }
        }

        #region Packets Stuff

        private void SendUpdate(BuddyData buddy, bool dc)
        {
            var flags = dc ? EncodeFlags.ForceOffline : EncodeFlags.None;

            var pw = new Packet(ServerMessages.FRIEND_RESULT);
            pw.WriteByte((byte)FriendResReq.FriendRes_Notify);
            pw.WriteInt(buddy.CharacterID);
            EncodeFriendLocation(buddy, pw, flags);

            Owner.SendPacket(pw);

            pw = new Packet(ServerMessages.FRIEND_RESULT);
            pw.WriteByte((byte)FriendResReq.FriendRes_NotifyChange_FriendInfo);
            pw.WriteInt(buddy.CharacterID);
            EncodeFriend(buddy, pw, flags);
            pw.WriteBool(buddy.InCashShop());

            Owner.SendPacket(pw);
        }

        public void BuddyChat(string message, int[] recipients)
        {
            var recipientCharacters = Buddies.Values
                .Where(e => Owner.CanChatTo(e))
                .Where(x => recipients.Contains(x.CharacterID))
                .ToList();

            Owner.GetChar().WrappedLogging(() =>
            {
                chatLog.Info(new MultiPeopleChatLog($"{Owner.CharacterName}: {message}")
                {
                    characterIDs = recipientCharacters.Select(x => x.CharacterID).ToArray(),
                    characterNames = recipientCharacters.Select(x => x.CharacterName).ToArray(),
                });
            });

            recipientCharacters
                .ForEach(b => b.GetBuddyList().SendBuddyChat(Owner.CharacterName, message, 0));
        }

        [Flags]
        enum EncodeFlags
        {
            None = 0x0,
            Hidden = 0x1,
            ForceOffline = 0x2
        }

        private void EncodeFriend(BuddyData bd, Packet pw, EncodeFlags flags)
        {
            pw.WriteInt(bd.CharacterID);
            pw.WriteString(bd.CharacterName, 13);
            EncodeFriendLocation(bd, pw, flags);
        }

        private void EncodeFriendLocation(BuddyData bd, Packet pw, EncodeFlags flags)
        {
            pw.WriteBool(flags.HasFlag(EncodeFlags.Hidden));
            pw.WriteInt(bd.GetChannel());
        }

        public void SendBuddyList(FriendResReq friendResReq)
        {
            var pw = new Packet(ServerMessages.FRIEND_RESULT);
            pw.WriteByte((byte)friendResReq);

            var buddies = new List<(BuddyData bd, bool hide)>();
            buddies.AddRange(Buddies.Values.Select(x => (x, false)));

            pw.WriteByte((byte)buddies.Count);

            buddies.ForEach(tuple =>
            {
                EncodeFriend(tuple.bd, pw, tuple.hide ? EncodeFlags.Hidden : EncodeFlags.None);
            });

            buddies.ForEach(tuple =>
            {
                pw.WriteInt(tuple.bd.InCashShop() ? 1 : 0);
            });

            Owner.SendPacket(pw);
        }

        public void SendRequestError(FriendResReq msg)
        {
            var pw = new Packet(ServerMessages.FRIEND_RESULT);
            pw.WriteByte((byte)msg);
            Owner.SendPacket(pw);
        }

        private void SendInviteFrom(BuddyData from)
        {
            var pw = new Packet(ServerMessages.FRIEND_RESULT);
            pw.WriteByte((byte)FriendResReq.FriendRes_Invite);
            pw.WriteInt(from.CharacterID);
            pw.WriteString(from.CharacterName);

            EncodeFriend(from, pw, EncodeFlags.Hidden);

            pw.WriteBool(from.InCashShop());
            Owner.SendPacket(pw);
        }

        private void SendCapacityChange()
        {
            var pw = new Packet(ServerMessages.FRIEND_RESULT);
            pw.WriteByte((byte)FriendResReq.FriendRes_IncMaxCount_Done);
            pw.WriteByte(Capacity);
            Owner.SendPacket(pw);
        }

        private void SendCapacityChangeFailed()
        {
            var pw = new Packet(ServerMessages.FRIEND_RESULT);
            pw.WriteByte((byte)FriendResReq.FriendRes_IncMaxCount_Unknown);
            Owner.SendPacket(pw);
        }

        public void SendBuddyChat(string fromName, string text, byte group)
        {
            var pw = new Packet(ServerMessages.GROUP_MESSAGE);
            pw.WriteByte(group);
            pw.WriteString(fromName);
            pw.WriteString(text);
            Owner.SendPacket(pw);
        }

        #endregion

        #region Database Stuff

        private static byte GetAdminLevel(int characterID)
        {
            using var reader = CenterServer.Instance.CharacterDatabase.RunQuery(
                "SELECT admin FROM characters c JOIN users u ON u.id = c.userid WHERE c.id = @c1",
                "@c1", characterID
            ) as MySqlDataReader;

            if (reader.Read()) return reader.GetByte("admin");

            log.Error($"Did not find character ID {characterID} for AdminLevel check.");
            return 9;
        }

        private static BuddyList LoadBuddyList(string characterName)
        {
            int characterId;
            string name;
            byte capacity;

            using (var capData = CenterServer.Instance.CharacterDatabase.RunQuery(
                "SELECT buddylist_size, ID, name FROM characters WHERE LOWER(name) = @name",
                "@name", characterName.ToLower()
            ) as MySqlDataReader)
            {
                if (!capData.Read())
                {
                    log.Error($"Did not find {characterName} in database for loading buddylist!");
                    return null;
                }
                characterId = capData.GetInt32("ID");
                name = capData.GetString("name");
                capacity = (byte)capData.GetInt32("buddylist_size");
            }

            return LoadFromDb(characterId, name, capacity);
        }

        private static BuddyList LoadBuddyList(int characterId)
        {
            string name;
            byte capacity;
            using (var capData = CenterServer.Instance.CharacterDatabase.RunQuery(
                "SELECT buddylist_size, name FROM characters WHERE id = @id",
                "@id", characterId
            ) as MySqlDataReader)
            {
                if (!capData.Read())
                {
                    return null;
                }

                name = capData.GetString("name");
                capacity = (byte)capData.GetInt32("buddylist_size");
            }

            return LoadFromDb(characterId, name, capacity);
        }

        private static BuddyList LoadFromDb(int characterId, string characterName, byte capacity)
        {
            var newlist = new BuddyList(capacity, characterId, characterName);

            using (var data = CenterServer.Instance.CharacterDatabase.RunQuery(
                "SELECT * FROM buddylist WHERE charid = @charid",
                "@charid", characterId
            ) as MySqlDataReader)
            {
                while (data.Read())
                {
                    var buddycharid = data.GetInt32("buddy_charid");
                    var buddyname = data.GetString("buddy_charname");
                    newlist.Add(new BuddyData(buddycharid, buddyname), false);
                }
            }

            using (var invitedata = CenterServer.Instance.CharacterDatabase.RunQuery(
                "SELECT * FROM buddylist_pending WHERE charid = @charid",
                "@charid", characterId
            ) as MySqlDataReader)
            {
                while (invitedata.Read())
                {
                    var inviterid = invitedata.GetInt32("inviter_charid");
                    var invitername = invitedata.GetString("inviter_charname");
                    newlist.BuddyRequests.Enqueue(new BuddyData(inviterid, invitername));
                }
            }


            return newlist;
        }

        public void SaveBuddiesToDb()
        {
            //BUDDY LIST
            CenterServer.Instance.CharacterDatabase.RunTransaction(comm =>
            {
                comm.CommandText = "DELETE FROM buddylist WHERE charid = @charid";
                comm.Parameters.AddWithValue("@charid", Owner.CharacterID);
                comm.ExecuteNonQuery();

                foreach (var buddyEntry in Buddies)
                {
                    comm.Parameters.Clear();
                    comm.CommandText = "INSERT INTO buddylist (charid, buddy_charid, buddy_charname) VALUES (@ownerCharId, @charId, @charname)";
                    comm.Parameters.AddWithValue("@ownerCharId", Owner.CharacterID);
                    comm.Parameters.AddWithValue("@charId", buddyEntry.Value.CharacterID);
                    comm.Parameters.AddWithValue("@charname", buddyEntry.Value.CharacterName);
                    comm.ExecuteNonQuery();
                }
            }, Program.MainForm.LogAppend);

            //PENDING REQUESTS
            CenterServer.Instance.CharacterDatabase.RunTransaction(comm =>
            {
                comm.CommandText = "DELETE FROM buddylist_pending WHERE charid = @charid";
                comm.Parameters.AddWithValue("@charid", Owner.CharacterID);
                comm.ExecuteNonQuery();

                foreach (var requestor in BuddyRequests.ToArray())
                {
                    comm.Parameters.Clear();
                    comm.CommandText = "INSERT INTO buddylist_pending (charid, inviter_charid, inviter_charname) VALUES (@charid, @inviterCharId, @inviterCharName)";
                    comm.Parameters.AddWithValue("@charid", Owner.CharacterID);
                    comm.Parameters.AddWithValue("@inviterCharId", requestor.CharacterID);
                    comm.Parameters.AddWithValue("@inviterCharName", requestor.CharacterName);
                    comm.ExecuteNonQuery();
                }
            }, Program.MainForm.LogAppend);

            CenterServer.Instance.CharacterDatabase.RunQuery(
                "UPDATE characters SET buddylist_size = @cap WHERE ID = @id",
                "@cap", Capacity,
                "@id", Owner.CharacterID
            );
        }

        public static void ClearDatabaseFromCharacterID(int characterID)
        {
            CenterServer.Instance.CharacterDatabase.RunTransaction(comm =>
            {
                comm.Parameters.AddWithValue("@charid", characterID);

                comm.CommandText = "DELETE FROM buddylist_pending WHERE charid = @charid OR inviter_charid = @charid";
                comm.ExecuteNonQuery();

                comm.CommandText = "DELETE FROM buddylist WHERE charid = @charid OR buddy_charid = @charid";
                comm.ExecuteNonQuery();
            }, Program.MainForm.LogAppend);
        }

        public static void DisbandFriendshipInDatabase(int char1, int char2)
        {
            CenterServer.Instance.CharacterDatabase.RunQuery(
                "DELETE FROM buddylist WHERE (charid = @c1 AND buddy_charid = @c2) OR (buddy_charid = @c1 AND charid = @c2)",
                "@c1", char1,
                "@c2", char2
            );

            CenterServer.Instance.CharacterDatabase.RunQuery(
                "DELETE FROM buddylist_pending WHERE (charid = @c1 AND inviter_charid = @c2) OR (inviter_charid = @c1 AND charid = @c2)",
                "@c1", char1,
                "@c2", char2
            );
        }

        #endregion
    }
}
