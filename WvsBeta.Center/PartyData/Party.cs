using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Center
{
    public class DoorInformation
    {
        public int TownID { get; private set; }
        public int FieldID { get; private set; }
        public short FieldPosX { get; private set; }
        public short FieldPosY { get; private set; }

        public DoorInformation(int townId, int fieldId, short fieldPosX, short fieldPosY)
        {
            TownID = townId;
            FieldID = fieldId;
            FieldPosX = fieldPosX;
            FieldPosY = fieldPosY;
        }

        public static readonly DoorInformation DefaultNoDoor = new DoorInformation(Constants.InvalidMap, Constants.InvalidMap, -1, -1);

        public void Encode(Packet packet)
        {
            packet.WriteInt(TownID);
            packet.WriteInt(FieldID);
            packet.WriteShort(FieldPosX);
            packet.WriteShort(FieldPosY);
        }

        public void Decode(Packet packet)
        {
            TownID = packet.ReadInt();
            FieldID = packet.ReadInt();
            FieldPosX = packet.ReadShort();
            FieldPosY = packet.ReadShort();
        }
        
        /// <summary>
        /// Edge case for memcpy'd struct (eg PARTYDATA::Decode), where it memcpys a tagPOINT{int,int}
        /// </summary>
        /// <param name="packet"></param>
        public void EncodeWithInts(Packet packet)
        {
            packet.WriteInt(TownID);
            packet.WriteInt(FieldID);
            packet.WriteInt(FieldPosX);
            packet.WriteInt(FieldPosY);
        }

        /// <summary>
        /// Decode a particular packet as shorts, NOT ints.
        /// </summary>
        /// <param name="packet"></param>
        public DoorInformation(Packet packet)
        {
            Decode(packet);
        }
    }

    public class PartyMember
    {
        public int CharacterID { get; }
        public string CharacterName { get; set; }
        public bool IsLeader { get; set; }
        public DoorInformation Door { get; set; } = DoorInformation.DefaultNoDoor;

        public PartyMember(int i, string n, bool l)
        {
            CharacterID = i;
            CharacterName = n;
            IsLeader = l;
        }

        public void SendPacket(Packet packet)
        {
            GetCharacter(true)?.SendPacket(packet);
        }

        public bool IsOnline => CenterServer.Instance.IsOnline(CharacterID);

        public Character GetCharacter(bool onlyOnline)
        {
            return CenterServer.Instance.FindCharacter(CharacterID, onlyOnline);
        }

        public int GetChannel()
        {
            return GetCharacter(true)?.ChannelID ?? PartyPacket.CHANNEL_ID_OFFLINE;
        }

        public int GetMap()
        {
            return GetCharacter(true)?.MapID ?? 0;
        }

        public void SendHpUpdate()
        {
            var chr = GetCharacter(true);
            if (chr == null) return;
            CenterServer.Instance.SendPacketToServer(PartyPacket.RequestHpUpdate(chr.ID), chr.ChannelID);
        }

        public void EncodeForMigration(Packet pw)
        {
            pw.WriteInt(CharacterID);
            pw.WriteString(CharacterName);
            pw.WriteBool(IsLeader);
            
            Door.Encode(pw);
        }
        
        public PartyMember(Packet pr)
        {
            CharacterID = pr.ReadInt();
            CharacterName = pr.ReadString();
            IsLeader = pr.ReadBool();

            Door = new DoorInformation(pr);
        }

    }

    public class Party
    {
        private static ILog _log = LogManager.GetLogger("Party");
        private static ILog _chatLog = LogManager.GetLogger("PartyChatLog");

        public readonly int partyId;
        public readonly int world;
        public readonly PartyMember[] members = new PartyMember[Constants.MaxPartyMembers];
        public PartyMember leader { get; private set; }

        private Party(int id, PartyMember ldr)
        {
            partyId = id;
            leader = ldr;
            members[0] = ldr;
            SendUpdatePartyData();
        }

        public Party(Packet pr)
        {
            partyId = pr.ReadInt();
            var leaderId = pr.ReadInt();

            for (var i = 0; i < Constants.MaxPartyMembers; i++)
            {
                if (!pr.ReadBool()) continue;

                var member = new PartyMember(pr);
                members[i] = member;
                if (member.CharacterID == leaderId) leader = member;

                var actualPlayer = member.GetCharacter(false);

                if (actualPlayer != null)
                {
                    actualPlayer.PartyID = partyId;
                }
            }

            if (leader == null)
            {
                _log.Error($"Built a party without a leader? Expected charid {leaderId} to be the member, but not found in data.");
            }
        }

        /// <summary>
        /// Get the PartyMember element by Character ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public PartyMember GetById(int id)
        {
            return members.FirstOrDefault(x => x != null && x.CharacterID == id);
        }

        public bool IsFull() => GetFirstFreeSlot() == -1;
        public int GetFirstFreeSlot() => GetCharacterSlot(null);

        public int GetCharacterSlot(PartyMember pm)
        {
            for (var i = 0; i < Constants.MaxPartyMembers; i++)
            {
                if (members[i] == pm) return i;
            }
            return -1;
        }

        public int GetCharacterSlot(int characterId)
        {
            for (var i = 0; i < Constants.MaxPartyMembers; i++)
            {
                if (members[i]?.CharacterID == characterId) return i;
            }
            return -1;
        }

        
        public IEnumerable<PartyMember> GetAllMembers(int skip = -1, bool onlyOnline = true)
        {
            for (var i = 0; i < Constants.MaxPartyMembers; i++)
            {
                var partyMember = members[i];
                if (partyMember == null) continue;
                if (partyMember.CharacterID == skip) continue;
                if (onlyOnline && partyMember.IsOnline == false) continue;

                yield return partyMember;
            }
        }

        public void ForAllMembers(Action<PartyMember> action, int skip = -1, bool onlyOnline = true)
        {
            GetAllMembers(skip, onlyOnline).ForEach(action);
        }

        public void Invite(int invitor, int invitee) => OnlyByLeader(invitor, ldr =>
        {
            var toInvite = CenterServer.Instance.FindCharacter(invitee);
            if (toInvite == null)
            {
                ldr.SendPacket(PartyPacket.PartyError(PartyFunction.UNABLE_TO_FIND_PLAYER));
            }
            else if (Invites.ContainsKey(toInvite.ID))
            {
                ldr.SendPacket(PartyPacket.PartyErrorWithName(PartyFunction.INVITE_USER_ALREADY_HAS_INVITE, toInvite.Name));
            }
            else if (toInvite.PartyID != 0)
            {
                ldr.SendPacket(PartyPacket.PartyError(PartyFunction.JOIN_ALREADY_JOINED));
            }
            else if (IsFull())
            {
                ldr.SendPacket(PartyPacket.PartyError(PartyFunction.JOIN_ALREADY_FULL));
            }
            else
            {
                _log.Debug($"Sending invite from party {partyId} from character {invitor} to {invitee}");
                toInvite.SendPacket(PartyPacket.PartyInvite(this));
                Invites.Add(toInvite.ID, this);
                //TODO do invites expire?
            }
        });

        public void DeclineInvite(Character decliner)
        {
            if (Invites.ContainsKey(decliner.ID))
            {
                _log.Debug($"Invite to party {partyId} has been declined by {decliner.ID}");
                Invites.Remove(decliner.ID);
                leader.SendPacket(PartyPacket.PartyErrorWithName(PartyFunction.INVITE_REJECTED, decliner.Name));
            }
            else
            {
                Program.MainForm.LogAppend("Trying to decline party invite while no invite exists. CharacterID: {0}, party ID {1}", decliner.ID, partyId);
            }
        }

        public void TryJoin(Character chr)
        {
            if (!Invites.ContainsKey(chr.ID))
            {
                Program.MainForm.LogAppend("Trying to join party while no invite. CharacterID: {0}, party ID {1}",
                    chr.ID, partyId);
                chr.SendPacket(PartyPacket.PartyError(PartyFunction.UNABLE_TO_FIND_PLAYER));
                return;
            }

            Invites.Remove(chr.ID);
            if (IsFull())
            {
                _log.Warn($"Invite accepted to party {partyId} by {chr.ID}, but its already full.");
                chr.SendPacket(PartyPacket.PartyError(PartyFunction.JOIN_ALREADY_FULL));
                return;
            }

            if (chr.PartyID != 0)
            {
                _log.Warn($"Invite accepted to party {partyId} by {chr.ID}, the person is already in a party");
                chr.SendPacket(PartyPacket.PartyError(PartyFunction.JOIN_ALREADY_JOINED));
                return;
            }

            if (leader.GetMap() != chr.MapID)
            {
                _log.Warn($"Invite accepted to party {partyId} by {chr.ID}, but is not in the same map.");
                chr.SendPacket(PartyPacket.PartyError(PartyFunction.UNABLE_TO_FIND_PLAYER));
                return;
            }

            Join(chr);
        }

        private void Join(Character chr)
        {
            var slot = GetFirstFreeSlot();
            if (slot == -1)
            {
                _log.Error($"Trying to join the party, but the free slot is -1??? Party {partyId} Character {chr.ID}");
                return;
            }
            
            _log.Debug($"{chr.ID} joins the party {partyId} under slot {slot}");

            chr.PartyID = partyId;
            var member = new PartyMember(chr.ID, chr.Name, false);

            members[slot] = member;

            ForAllMembers(m => m.SendPacket(PartyPacket.JoinParty(member, this)));
            member.SendHpUpdate();
            SendUpdatePartyData();
        }

        public void Leave(Character fucker)
        {
            var slot = GetCharacterSlot(fucker.ID);

            if (slot == -1 || fucker.PartyID == 0)
            {
                _log.Error($"{fucker.ID} tried to get out of party {partyId}, but is not in it?");
                fucker.SendPacket(PartyPacket.PartyError(PartyFunction.WITHDRAW_NOT_JOINED));
            }
            else if (fucker.ID == leader.CharacterID)
            {
                _log.Debug($"Disbanding because {fucker.ID} left the party {partyId} (leader)");
                Disband(fucker);
            }
            else
            {
                _log.Debug($"{fucker.ID} left the party {partyId} from slot {slot}");
                var leaving = members[slot];
                members[slot] = null;
                ForAllMembers(m => m.SendPacket(PartyPacket.MemberLeft(m, leaving, this, false, false)));
                leaving.SendPacket(PartyPacket.MemberLeft(leaving, leaving, this, false, false));
                fucker.PartyID = 0;
                SendUpdatePartyData();
            }
        }

        public void SilentUpdate(int charId, int disconnecting = -1)
        {
            var member = GetById(charId);
            ForAllMembers(m => m.SendPacket(PartyPacket.SilentUpdate(m, this, disconnecting)));
            member.SendHpUpdate();
            SendUpdatePartyData();
        }
        
        private void Disband(Character disbander) => OnlyByLeader(disbander.ID, ldr =>
        {
            _log.Debug($"Disbanding party {partyId} by character {disbander.ID}");

            ForAllMembers(m =>
            {
                m.SendPacket(PartyPacket.MemberLeft(m, ldr, this, true, false));
                var c = m.GetCharacter(false);

                if (c != null)
                {
                    c.PartyID = 0;
                }
                else
                {
                    _log.Debug($"Unable to set PartyID to 0 of {m.CharacterID}");
                }
            }, -1, false);

            for (var i = 0; i < Constants.MaxPartyMembers; i++)
            {
                members[i] = null;
            }

            leader = null;

            Parties.Remove(partyId);
            var discardedInvites = Invites.Where(x => x.Value.partyId == partyId).Select(x => x.Key).ToArray();
            discardedInvites.ForEach(x => Invites.Remove(x));

            SendPartyDisband();
        });

        public void Expel(int expellerId, int toExpel) => OnlyByLeader(expellerId, ldr =>
        {   
            var slot = GetCharacterSlot(toExpel);

            if (slot == -1)
            {
                _log.Error($"Expelling {toExpel} from party {partyId} by {expellerId}, but was not in party???");
                return;
            }

            _log.Info($"Expelling {toExpel} from party {partyId} by {expellerId}?");

            var expelled = members[slot];
            members[slot] = null;
            ForAllMembers(m => m.SendPacket(PartyPacket.MemberLeft(m, expelled, this, false, true)));
            expelled.SendPacket(PartyPacket.MemberLeft(expelled, expelled, this, false, true));

            var expellchr = expelled.GetCharacter(false);
            if (expellchr != null)
            {
                expellchr.PartyID = 0;
            }
            else
            {
                _log.Debug($"Unable to set PartyID to 0 of {toExpel}");
            }
            

            SendUpdatePartyData();
        });

        public void Chat(int chatter, string text)
        {
            var chr = GetById(chatter);
            if (members.Count(e => e?.IsOnline ?? false) <= 1)
            {
                chr.SendPacket(PartyPacket.NoneOnline());
            }
            else
            {
                var allMembers = GetAllMembers(chatter).ToArray();

                chr.GetCharacter(false).WrappedLogging(() =>
                {
                    _chatLog.Info(new MultiPeopleChatLog($"{chr.CharacterName}: {text}")
                    {
                        characterIDs = allMembers.Select(x => x.CharacterID).ToArray(),
                        characterNames = allMembers.Select(x => x.CharacterName).ToArray(),
                    });
                });

                // Send to all other members
                allMembers.ForEach(m => m.SendPacket(PartyPacket.PartyChat(chr.CharacterName, text, 1)));
            }
        }

        public void OnlyByLeader(int possibleImpostor, Action<PartyMember> action)
        {
            if (possibleImpostor == leader.CharacterID)
            {
                action(leader);
            }
            else
            {
                _log.Warn($"Trying to run func for only the leader, but {possibleImpostor} is not the leader of party {partyId}!");
            }
        }

        public void UpdateDoor(DoorInformation newDoor, int charId)
        {
            Program.MainForm.LogDebug("UPDATING DOOR: " + charId);
            var member = GetById(charId);
            if (member == null) return;
            member.Door = newDoor;
            var idx = GetCharacterSlot(member);
            var packet = PartyPacket.UpdateDoor(newDoor, (byte)idx);
            ForAllMembers(m =>
            {
                m.SendPacket(packet);
            });
        }
        

        private void SendUpdatePartyData()
        {
            var pw = new Packet(ISServerMessages.PartyInformationUpdate);
            pw.WriteInt(partyId);
            pw.WriteInt(leader.CharacterID);

            for (var i = 0; i < Constants.MaxPartyMembers; i++)
            {
                var member = members[i];
                pw.WriteInt(member?.CharacterID ?? 0);
            }

            CenterServer.Instance.World.SendPacketToEveryGameserver(pw);
        }

        private void SendPartyDisband()
        {
            var pw = new Packet(ISServerMessages.PartyDisbanded);
            pw.WriteInt(partyId);
            CenterServer.Instance.World.SendPacketToEveryGameserver(pw);
        }

        /****************************************************************/

        private static readonly LoopingID IdGenerator = new LoopingID(1, int.MaxValue);
        public static readonly Dictionary<int, Party> Parties = new Dictionary<int, Party>(); //partyId -> party
        public static readonly Dictionary<int, Party> Invites = new Dictionary<int, Party>(); //invitee -> party

        public static void CreateParty(Character leader, DoorInformation doorInfo)
        {
            if (leader == null)
            {
                return;
            }

            // Allow beginners to create parties from lvl 10 and up
            if (leader.Job == 0 && leader.Level < 10)
            {
                _log.Warn($"Cannot create party because the leader would be beginner under lvl 10. Leader {leader.ID}");
                leader.SendPacket(PartyPacket.PartyError(PartyFunction.CREATE_NEW_BEGINNER_DISALLOWED));
                return;
            }

            if (leader.PartyID != 0)
            {
                _log.Warn($"Cannot create party because the leader is already in a party. Leader {leader.ID}");
                leader.SendPacket(PartyPacket.PartyError(PartyFunction.CREATE_NEW_ALREADY_JOINED));
                return;
            }

            var ldr = new PartyMember(leader.ID, leader.Name, true);
            ldr.Door = doorInfo;
            var id = IdGenerator.NextValue();
            var pty = new Party(id, ldr);
            _log.Info($"Created party {id} with leader {leader.ID}");

            Parties.Add(pty.partyId, pty);
            leader.PartyID = pty.partyId;
            leader.SendPacket(PartyPacket.PartyCreated(pty));
        }

        public static void EncodeForMigration(Packet pw)
        {
            pw.WriteInt(IdGenerator.Current);
            pw.WriteInt(Parties.Count);
            foreach (var kvp in Parties)
            {
                pw.WriteInt(kvp.Key);

                var party = kvp.Value;

                pw.WriteInt(party.leader.CharacterID);

                for (var i = 0; i < Constants.MaxPartyMembers; i++)
                {
                    var member = party.members[i];
                    if (member != null)
                    {
                        pw.WriteBool(true);
                        member.EncodeForMigration(pw);
                    }
                    else
                    {
                        pw.WriteBool(false);
                    }
                }
            }
        }

        public static void DecodeForMigration(Packet pr)
        {
            IdGenerator.Reset(pr.ReadInt());
            var parties = pr.ReadInt();
            for (var i = 0; i < parties; i++)
            {
                var party = new Party(pr);
                Parties.Add(party.partyId, party);
            }
        }
    }
}
