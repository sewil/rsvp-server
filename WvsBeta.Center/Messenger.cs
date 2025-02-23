using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using log4net;
using WvsBeta.Center.CharacterPackets;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Center
{
    public enum MessengerFunction : byte
    {
        SelfEnterResult = 0x00,
        Enter = 0x01,
        Leave = 0x02,
        Invite = 0x03,
        InviteResult = 0x04,
        Blocked = 0x05,
        Chat = 0x06,
        Avatar = 0x07,
        Migrated = 0x08,
    }

    public class Messenger
    {
        public static ILog log = LogManager.GetLogger("MessengerLog");
        private static ILog chatLog = LogManager.GetLogger("MessengerChatLog");

        public static List<Messenger> Messengers = new List<Messenger>();

        private static int MessengerIDCounter = 1;

        public int ID { get; }
        public Character Owner { get; }
        public Character[] Users { get; }
        public const int MAX_USERS = 3;
        public IEnumerable<Character> AvailableUsers => Users.Where(x => x != null);

        public Messenger(Character pOwner)
        {
            Users = new Character[MAX_USERS];
            Owner = pOwner;
            ID = MessengerIDCounter++;

            AddCharacter(pOwner);
            Messengers.Add(this);
        }

        public static void EncodeForMigration(Packet pw)
        {
            pw.WriteInt(Messengers.Count);
            foreach (var messenger in Messengers)
            {
                pw.WriteInt(messenger.ID);
                for (var i = 0; i < MAX_USERS; i++)
                {
                    pw.WriteInt(messenger.Users[i]?.ID ?? -1);
                }
            }
        }

        public static void DecodeForMigration(Packet pr)
        {
            var amount = pr.ReadInt();

            var charids = new int[MAX_USERS];
            for (var i = 0; i < amount; i++)
            {
                var ownerId = pr.ReadInt();
                for (var j = 0; j < MAX_USERS; j++)
                {
                    charids[j] = pr.ReadInt();
                }

                var owner = CenterServer.Instance.FindCharacter(ownerId);
                if (owner == null) continue;

                var messenger = new Messenger(owner);
                for (byte j = 0; j < MAX_USERS; j++)
                {
                    var character = CenterServer.Instance.FindCharacter(charids[j]);
                    // Re-assign user
                    if (character == null) continue;
                    messenger.AddCharacter(character);
                }
            }
        }

        public static void JoinMessenger(Packet packet)
        {
            var messengerID = packet.ReadInt();
            var chr = ParseMessengerCharacter(packet);
            
            if (messengerID > 0)
            {
                var messenger = Messengers.FirstOrDefault(m => m.ID == messengerID);
                if (messenger == null)
                {
                    chr.WrappedLogging(() =>
                    {
                        log.Error($"{chr.Name} tried to enter a messenger that doesn't exist. [ID: {messengerID}]");
                    });

                    // Open up a clean one
                    CreateMessenger(chr);

                    return;
                }
                
                JoinExistingMessenger(messenger, chr);
            }
            else
            {
                CreateMessenger(chr);
            }
        }


        private static void CreateMessenger(Character pOwner)
        {
            // Looks unused, but the Messenger is registered
            new Messenger(pOwner);

            pOwner.SendPacket(MessengerPacket.Enter(pOwner.MessengerSlot));
        }

        private static void JoinExistingMessenger(Messenger messenger, Character chr)
        {
            if (!messenger.AddCharacter(chr)) return;

            chr.WrappedLogging(() =>
            {
                log.Info($"{chr.Name} joined messenger chat from {messenger.Owner.Name} [ID: {messenger.ID}]");
            });
            
            foreach (var mChr in messenger.AvailableUsers)
            {
                if (mChr.ID == chr.ID)
                {
                    chr.SendPacket(MessengerPacket.Enter(chr.MessengerSlot));
                }
                else
                {
                    chr.SendPacket(MessengerPacket.SelfEnter(mChr)); // Announce existing players to joinee
                    mChr.SendPacket(MessengerPacket.SelfEnter(chr)); // Announce joinee to existing players
                }
            }
        }

        public static void LeaveMessenger(int cid)
        {
            var chr = CenterServer.Instance.FindCharacter(cid);
            var messenger = chr.Messenger;

            if (messenger == null) return;

            var slot = chr.MessengerSlot;
            var empty = true;

            foreach (var mChr in messenger.AvailableUsers)
            {
                if (mChr.ID != chr.ID)
                {
                    // If someone else is still left, this chat is not empty.
                    empty = false;
                }

                mChr.SendPacket(MessengerPacket.Leave(slot));
            }

            chr.WrappedLogging(() =>
            {
                log.Info($"{chr.Name} left messenger chat of {messenger.Owner.Name} [ID: {messenger.ID}]");
            });


            messenger.RemoveCharacter(chr);


            if (empty)
            {
                log.Info($"Stopping messenger [ID: {messenger.ID}]");
                Messengers.Remove(messenger);
            }
        }
        
        private int UserCount => AvailableUsers.Count();

        public static void SendInvite(int senderID, string recipientName)
        {
            var recipient = CenterServer.Instance.FindCharacter(recipientName);
            var sender = CenterServer.Instance.FindCharacter(senderID);

            if (sender == null) return;
            
            if (recipient == null)
            {
                sender.WrappedLogging(() =>
                {
                    log.Info($"{sender.Name} unable to invite {recipientName}: recipient not found.");
                });
                
                sender.SendPacket(MessengerPacket.InviteResult(recipientName, false));
                return;
            }

            if (recipient == sender)
            {
                sender.WrappedLogging(() =>
                {
                    log.Info($"{sender.Name} unable to invite {recipientName}: inviting him/herself...");
                });
                
                sender.SendPacket(MessengerPacket.InviteResult(recipientName, false));
                return;
            }
            
            var messenger = sender.Messenger;
            if (messenger == null)
            {
                sender.WrappedLogging(() =>
                {
                    log.Info($"{sender.Name} unable to invite {recipient.Name}: no messenger opened");
                });
                
                sender.SendPacket(MessengerPacket.InviteResult(recipientName, false));
                return;
            }
            
            if (messenger.UserCount >= MAX_USERS)
            {
                sender.WrappedLogging(() =>
                {
                    log.Info($"{sender.Name} unable to invite {recipient.Name}: no slots left. [ID: {messenger.ID}]");
                });
                
                sender.SendPacket(MessengerPacket.InviteResult(recipientName, false));
                return;
            }
            
            sender.WrappedLogging(() =>
            {
                log.Info($"{sender.Name} invited {recipient.Name} to messenger [ID: {messenger.ID}]");
            });

            recipient.SendPacket(MessengerPacket.Invite(sender.Name, sender.ChannelID, messenger.ID, sender.IsGM));
            sender.SendPacket(MessengerPacket.InviteResult(recipientName, true));
        }

        public static void Chat(int cid, string message)
        {
            var chr = CenterServer.Instance.FindCharacter(cid);
            if (chr == null) return;

            var messenger = chr.Messenger;
            if (messenger == null) return;

            var recipients = messenger.AvailableUsers.Where(x => x.ID != cid).ToList();

            chr.WrappedLogging(() =>
            {
                chatLog.Info(new MultiPeopleChatLog($"{chr.Name}: {message}")
                {
                    characterNames = recipients.Select(x => x.Name).ToArray(),
                    characterIDs = recipients.Select(x => x.ID).ToArray(),
                    chatIdentifier = $"messenger: {messenger.ID}"
                });
            });

            recipients.ForEach(x => x.SendPacket(MessengerPacket.Chat(message)));
        }

        private static Character ParseMessengerCharacter(Packet packet)
        {
            var character = CenterServer.Instance.FindCharacter(packet.ReadInt());
            character.UpdateFromAvatarLook(packet);
            return character;
        }

        public bool AddCharacter(Character character)
        {
            var slotInt = Array.IndexOf(Users, null);
            if (slotInt == -1)
            {
                character.WrappedLogging(() =>
                {
                    log.Info($"Unable to add {character.Name} to messenger, no slots left. [ID: {ID}]");
                });
                return false;
            }

            var slot = (byte)slotInt;
            Users[slot] = character;
            character.MessengerSlot = slot;
            character.Messenger = this;
            
            character.WrappedLogging(() =>
            {
                log.Info($"Added {character.Name} to messenger in slot {slot}. [ID: {ID}]");
            });

            return true;
        }

        public void RemoveCharacter(Character character)
        {
            character.WrappedLogging(() =>
            {
                log.Info($"Removing {character.Name} from messenger on slot {character.MessengerSlot}. [ID: {ID}]");
            });

            Users[character.MessengerSlot] = null;
            character.Messenger = null;
            character.MessengerSlot = 0;
        }

        public static void Block(Packet packet)
        {
            //TODO
        }

        public static void OnAvatar(Packet packet)
        {
            var chr = ParseMessengerCharacter(packet);
            var messenger = chr.Messenger;

            foreach (var c in messenger.AvailableUsers)
            {
                c.SendPacket(MessengerPacket.Avatar(chr));
            }
        }
    }
}
