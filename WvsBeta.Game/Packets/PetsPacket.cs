using System;
using System.Diagnostics;
using log4net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.Packets;

namespace WvsBeta.Game
{
    public static class PetsPacket
    {
        private static ILog _log = LogManager.GetLogger(typeof(PetsPacket));
        private static ILog _petChatlog = LogManager.GetLogger("PetChatLog");
        public static void HandleSpawnPet(Character chr, short slot)
        {
            chr.ExclRequestSet = true;

            if (!(chr.Inventory.GetItem(5, slot) is PetItem petItem))
            {
                _log.Warn($"Trying to spawn pet but slot {slot} is not a pet?");
                return;
            }

            var tmpCashID = chr.PetCashId;
            if (tmpCashID != 0)
            {
                // Already spawned a pet
                DoPetDespawn(chr, DespawnReason.OrderByUser);

                // This is an edge case for changing pets in multi-pet client. We just keep the same logic,
                // as it wouldn't break (I suppose) in our single-pet client. The only way it would,
                // would be when the cashid changes....!

                if (tmpCashID == petItem.CashId)
                {
                    // We don't want to have any pets on the field.
                    return;
                }
            }

            DoPetSpawn(chr, petItem.CashId);
        }

        public static void DoPetSpawn(Character chr, long cashId)
        {
            _log.Debug("Spawning pet");

            chr.PetCashId = cashId;

            var petItem = chr.GetSpawnedPet();
            if (petItem == null)
            {
                _log.Error($"Tried to spawn pet that doesnt exist. Cashid {cashId}");
                chr.PetCashId = 0;
                return;
            }

            petItem.LastInteraction = MasterThread.CurrentTime;

            var ml = petItem.MovableLife;
            ml.Foothold = chr.Foothold;
            ml.Position = new Pos(chr.Position);
            ml.Position.Y -= 20;
            ml.MoveAction = 0;

            chr.PetCashId = petItem.CashId;
            SendSpawnPet(chr, petItem);

            CharacterStatsPacket.SendUpdateStat(chr, StatFlags.Pet);
        }


        public enum DespawnReason
        {
            OrderByUser = 0,
            Hungry = 1,
            Died = 2,
        }

        public static void DoPetDespawn(Character chr, DespawnReason despawnReason)
        {
            if (chr.PetCashId == 0)
            {
                _log.Warn("Trying to despawn pet, but there are none equipped?");
                return;
            }

            _log.Debug($"Despawning pet with reason {despawnReason}");
            chr.PetCashId = 0;
            SendRemovePet(chr, despawnReason);

            CharacterStatsPacket.SendUpdateStat(chr, StatFlags.Pet);
        }

        public static void HandleMovePet(Character chr, Packet packet)
        {
            var petItem = chr.GetSpawnedPet();
            if (petItem == null) return;

            if (packet.ReadByte() != chr.PortalCount)
            {
                return;
            }

            var movePath = new MovePath();
            movePath.DecodeFromPacket(packet, MovePath.MovementSource.Pet);
            chr.TryTraceMovement(movePath);

            PacketHelper.ValidateMovePath(petItem.MovableLife, movePath, packet.PacketCreationTime);

            SendMovePet(chr, movePath);
        }

        public static void HandleInteraction(Character chr, Packet packet)
        {
            var petItem = chr.GetSpawnedPet();
            if (petItem == null) return;
            var currentTime = MasterThread.CurrentTime;

            var success = false;
            var multiplier = 1.0;

            var calledByName = packet.ReadBool();

            if (calledByName && Pet.IsNamedPet(petItem))
                multiplier = 1.5;

            var interactionId = packet.ReadByte();

            if (!petItem.Template.Reactions.TryGetValue(interactionId, out var petReactionData)) return;

            var timeSinceLastInteraction = currentTime - petItem.LastInteraction;

            // shouldnt be able to do this yet.
            if (petItem.Level < petReactionData.LevelMin ||
                petItem.Level > petReactionData.LevelMax ||
                timeSinceLastInteraction < 15000) goto send_response;

            // sick math

            petItem.LastInteraction = currentTime;
            var additionalSucceedProbability = (((timeSinceLastInteraction - 15000.0) / 10000.0) * 0.01 + 1.0) * multiplier;

            var random = Rand32.Next() % 100;
            if (random >= (petReactionData.Prob * additionalSucceedProbability) ||
                petItem.Fullness < 50) goto send_response;

            success = true;
            Pet.IncreaseCloseness(chr, petItem, petReactionData.Inc);

        send_response:
            SendPetInteraction(chr, interactionId, success);
        }

        public static void HandlePetLoot(Character chr, Packet packet)
        {
            var pet = chr.GetSpawnedPet();
            if (pet == null) return;
            
            packet.Skip(4); // X, Y

            var dropid = packet.ReadInt();

            if (!chr.Field.DropPool.Drops.TryGetValue(dropid, out var drop) ||
                !drop.CanTakeDrop(chr))
            {
                return;
            }

            var dropLootRange = drop.Pt2 - pet.MovableLife.Position;

            chr.AssertForHack(dropLootRange > 700, "Possible pet drop VAC! Distance: " + dropLootRange, dropLootRange > 250);

            chr.Field.DropPool.TakeDrop(drop, chr, true);
        }

        public static void HandlePetAction(Character chr, Packet packet)
        {
            var type = packet.ReadByte();
            var action = packet.ReadByte();
            var message = packet.ReadString();

            Trace.WriteLine($"Pet Action {type} {action} {message}");


            var pet = chr.GetSpawnedPet();
            if (pet == null) return;

            if (action > 0)
            {
                var namedAction = action - 9;
                if (namedAction >= 0)
                {
                    if (pet.Template.Actions.Count < namedAction)
                    {
                        _log.Error($"Unknown action for pet. Action {action}, namedAction {namedAction}");
                        return;
                    }
                }
            }

            _petChatlog.Info($"{pet.Name}: {message}");
            SendPetAction(chr, type, action, message);
        }

        public static void HandlePetFood(Character chr, Packet packet)
        {
            chr.ExclRequestSet = true;
            
            if (chr.AssertForHack(!chr.CanAttachAdditionalProcess, "Trying to use pet food while !CanAttachAdditionalProcess"))
            {
                return;
            }

            var slot = packet.ReadShort();
            var itemid = packet.ReadInt();

            var spawnedPet = chr.GetSpawnedPet();
            var item = chr.Inventory.GetItem(2, slot);
            if (item == null ||
                item.ItemID != itemid ||
                !DataProvider.Items.TryGetValue(itemid, out var data) ||
                Constants.getItemType(item.ItemID) != Constants.Items.Types.ItemTypes.ItemPetFood ||
                spawnedPet == null ||
                !data.Pets.Contains(spawnedPet.ItemID))
            {
                InventoryPacket.NoChange(chr);
                return;
            }


            var itemIncreaseFullness = data.PetFullness;

            var increaseFullness = itemIncreaseFullness;
            if (spawnedPet.Fullness + increaseFullness > 100)
                increaseFullness = 100 - spawnedPet.Fullness;


            var overeat = spawnedPet.OvereatTimes;
            spawnedPet.Fullness += (byte)increaseFullness;
            spawnedPet.RemainHungriness = TimeSpan.FromSeconds(Rand32.Next() % 10 + overeat * overeat + 10);

            var incCloseness = (short)0;

            if (itemid != 2120000 && increaseFullness != 0)
                incCloseness = 10;

            if ((10 * increaseFullness / itemIncreaseFullness) <= Rand32.Next() % 12 ||
                (spawnedPet.Fullness / 10 <= Rand32.Next() % 12))
            {
                if (increaseFullness == 0)
                {
                    _log.Warn("Overfeeding pet... rude!");

                    // The more you overfeed, the more feral your pet becomes. :eyes:
                    if (Rand32.Next() % Math.Max(1, overeat - 10) > 0)
                    {
                        spawnedPet.OvereatTimes++;
                    }
                    else
                    {
                        _log.Warn("Pet turning feral...");
                        incCloseness = -1;
                        spawnedPet.OvereatTimes = 0;
                    }
                }
            }
            else
            {
                _log.Warn("Increasing pet closeness!");
                if (itemid == 2120000)
                    incCloseness = 1;
            }

            Pet.IncreaseCloseness(chr, spawnedPet, incCloseness);

            chr.Inventory.TakeItem(itemid, 1);
            SendPetFeed(chr, increaseFullness > 0);
        }

        public static void SendPetAction(Character chr, byte type, byte action, string text)
        {
            var pw = new Packet(ServerMessages.PET_ACTION);
            pw.WriteInt(chr.ID);
            pw.WriteByte(type);
            pw.WriteByte(action);
            pw.WriteString(text);
            chr.Field.SendPacket(chr, pw);
        }

        public static void SendPetNamechange(Character chr)
        {
            var petItem = chr.GetSpawnedPet();
            if (petItem == null) return;

            var pw = new Packet(ServerMessages.PET_NAME_CHANGED);
            pw.WriteInt(chr.ID);
            pw.WriteString(petItem.Name);
            chr.Field.SendPacket(chr, pw);
        }

        public static void SendPetLevelup(Character chr) => SendPetEffect(chr, PetEffects.LevelUp);

        public enum PetEffects
        {
            LevelUp = 0,
            TeleportToBase = 1,
            TeleportToBack = 2,
        }

        public static void SendPetEffect(Character chr, PetEffects effect)
        {
            MapPacket.SendPlayerEffect(chr, MapPacket.PlayerEffectTargets.ToPlayerAndMap, UserEffect.Pet, pw => pw.WriteByte(effect));
        }

        public static void SendPetInteraction(Character chr, byte action, bool inc)
        {
            var pw = new Packet(ServerMessages.PET_INTERACTION);
            pw.WriteInt(chr.ID);
            pw.WriteByte(0);
            pw.WriteByte(action);
            pw.WriteBool(inc);
            chr.SendPacket(pw);
        }

        public static void SendPetFeed(Character chr, bool inc)
        {
            var pw = new Packet(ServerMessages.PET_INTERACTION);
            pw.WriteInt(chr.ID);
            pw.WriteByte(1);
            pw.WriteBool(inc);
            chr.SendPacket(pw);
        }

        public static void SendMovePet(Character chr, MovePath movePath)
        {
            var pw = new Packet(ServerMessages.PET_MOVE);
            pw.WriteInt(chr.ID);
            movePath.EncodeToPacket(pw);

            chr.Field.SendPacket(chr, pw, chr);
        }

        public static void SendSpawnPet(Character chr, PetItem pet, Character tochar = null)
        {
            // 43 10000000 01 404B4C00 0300312031 3A00000000000000 0000 00 0000  000000000000000000000000000000000000000000000000000000 
            var pw = new Packet(ServerMessages.SPAWN_PET);
            pw.WriteInt(chr.ID);
            pw.WriteBool(true); // Spawns
            pw.WriteInt(pet.ItemID);
            pw.WriteString(pet.Name);
            pw.WriteLong(pet.CashId);
            pw.WriteShort(pet.MovableLife.Position.X);
            pw.WriteShort(pet.MovableLife.Position.Y);
            pw.WriteByte(pet.MovableLife.MoveAction);
            pw.WriteShort(pet.MovableLife.Foothold);
            pw.WriteLong(0);
            pw.WriteLong(0);
            if (tochar == null)
                chr.Field.SendPacket(chr, pw);
            else
                tochar.SendPacket(pw);
        }

        public static void SendRemovePet(Character chr, DespawnReason reason, bool gmhide = false)
        {
            var pw = new Packet(ServerMessages.SPAWN_PET);
            pw.WriteInt(chr.ID);
            pw.WriteBool(false);
            pw.WriteByte(reason);
            chr.Field.SendPacket(chr, pw, (gmhide ? chr : null));
        }
    }
}