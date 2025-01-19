using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using log4net;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Common.Tracking;
using WvsBeta.Game.GameObjects;

namespace WvsBeta.Game
{
    public static class InventoryPacket
    {
        private static ILog _log = LogManager.GetLogger(typeof(InventoryPacket));

        public static void HandleUseItemPacket(Character chr, Packet packet)
        {
            chr.ExclRequestSet = true;

            if (chr.PrimaryStats.HP < 1)
            {
                return;
            }

            if (chr.AssertForHack(!chr.CanAttachAdditionalProcess, "Trying to use item while !CanAttachAdditionalProcess"))
            {
                return;
            }


            var slot = packet.ReadShort();
            var itemid = packet.ReadInt();

            var item = chr.Inventory.GetItem(2, slot);
            if (item == null || item.ItemID != itemid || !DataProvider.Items.TryGetValue(itemid, out var data))
            {
                return;
            }
            const byte UseInventory = 2;

            if (!chr.Inventory.SubtractAmountFromSlot(UseInventory, slot, 1))
            {
                _log.Error("Unable to Use Item!");
                return;
            }

            var alch = chr.Skills.GetSkillLevelData(Constants.Hermit.Skills.Alchemist, out _);

            if (data.HP > 0)
            {
                var hp = data.HP;

                if (alch != null)
                {
                    hp = (short)((float)hp * alch.XValue / 100.0);
                }

                chr.ModifyHP(hp);
            }
            if (data.MP > 0)
            {
                var mp = data.MP;

                if (alch != null)
                {
                    mp = (short)((float)mp * alch.YValue / 100.0);
                }

                chr.ModifyMP(mp);
            }
            if (data.HPRate > 0)
            {
                chr.ModifyHP((short)(data.HPRate * chr.PrimaryStats.GetMaxHP(false) / 100), true);
            }
            if (data.MPRate > 0)
            {
                chr.ModifyMP((short)(data.MPRate * chr.PrimaryStats.GetMaxMP(false) / 100), true);
            }

            if (data.BuffTime > 0 || data.Cures != 0)
            {
                chr.Buffs.AddItemBuff(itemid);
            }

            

            if (chr.PrimaryStats.BuffSpeed.R == itemid)
            {
                MapPacket.SendAvatarModified(chr, MapPacket.AvatarModFlag.Speed);
            }
        }

        private static void DropItem(Character chr, byte inventory, short slot, short quantity, BaseItem itemInInventory)
        {
            chr.ExclRequestSet = true;
            
            if (chr.AssertForHack(!chr.CanAttachAdditionalProcess, "Trying to drop item while !CanAttachAdditionalProcess"))
            {
                return;
            }

            if (chr.AssertForHack(quantity < 0, $"Trying to drop {quantity} of an item!!! (must be positive)", autoban: true))
            {
                return;
            }

            if (chr.AssertForHack(inventory == 1 && quantity != 1, $"Trying to drop {quantity} equips at once!!!", autoban: true))
            {
                return;
            }

            if (Constants.isRechargeable(itemInInventory.ItemID))
            {
                if (chr.AssertForHack(quantity != 1, $"Trying to drop different amount of rechargables {quantity}"))
                {
                    return;
                }

                // Drop full amount
                quantity = itemInInventory.Amount;
            }


            // Remove items from slot
            var droppingItem = chr.Inventory.TakeItemAmountFromSlot(inventory, slot, quantity);

            if (droppingItem == null)
            {
                // Item not found or quantity not enough
                return;
            }

            var droppedFromEquips = Constants.isEquip(droppingItem.ItemID) && slot < 0;

            var drop = chr.Field.DropPool.Create(
                Reward.Create(droppingItem),
                chr.ID,
                0,
                DropOwnType.UserOwn,
                0,
                new Pos(chr.Position),
                chr.Position.X,
                0,
                chr.IsAdmin && !chr.Undercover,
                (short)(chr.Field.DropPool.DropEverlasting ? droppingItem.InventorySlot : 0),
                false
            );

            ItemTransfer.ItemDropped(
                chr.ID, 
                chr.MapID,
                droppingItem.ItemID, 
                droppingItem.Amount, 
                chr.MapID + ", " + drop.GetHashCode() + ", " + drop.OwnerID, 
                droppingItem,
                drop.GetDropInfo()
            );

            if (droppedFromEquips)
            {
                MapPacket.SendAvatarModified(chr, MapPacket.AvatarModFlag.Equips);
            }
        }

        private static void SwapSlots(Character chr, BaseItem from, BaseItem to, short slotFrom, short slotTo)
        {
            if (to != null)
            {
                if (
                    Constants.isStackable(to.ItemID) &&
                    to.ItemID == from.ItemID &&
                    // Do not allow cashitem stacking
                    to.CashId == 0 &&
                    from.CashId == 0
                )
                {
                    StackItems(chr, from, to, slotFrom, slotTo);
                    return;
                }
            }
            var inventory = Constants.getInventory(from.ItemID);

            chr.Inventory.SetItem(inventory, slotFrom, to);
            chr.Inventory.SetItem(inventory, slotTo, from);

            SwitchSlots(chr, slotFrom, slotTo, inventory);
            MapPacket.SendAvatarModified(chr, MapPacket.AvatarModFlag.Equips);
        }

        /// <summary>
        /// TODO: Recode this crap!!!
        /// </summary>
        /// <param name="chr"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="slotFrom"></param>
        /// <param name="slotTo"></param>
        private static void StackItems(Character chr, BaseItem from, BaseItem to, short slotFrom, short slotTo)
        {
            if (chr.AssertForHack(!(from is BundleItem && to is BundleItem), "Trying to stack items that arent bundles???"))
            {
                return;
            }

            var slotMax = (short)DataProvider.Items[from.ItemID].MaxSlot;
            if (slotMax == 0)
            {
                slotMax = 100;
            }
            var inventory = Constants.getInventory(from.ItemID);

            if (to.Amount <= slotMax && to.Amount > 0) //adding to stack
            {
                var amount = to.Amount;
                var leftover = (short)(slotMax - amount);

                if (leftover < from.Amount)
                {
                    to.Amount += leftover;
                    from.Amount -= leftover;
                    UpdateItems(chr, to, from);
                }
                else if (leftover >= from.Amount)
                {
                    to.Amount += from.Amount;
                    UpdateItems(chr, to);
                    if (!chr.Inventory.SubtractAmountFromSlot(inventory, slotFrom, from.Amount))
                    {
                        _log.Error($"Unable to subtract {from.Amount} from slot {slotFrom} item {from.ItemID}??");
                    }
                }

            }
        }

        private static void EquipSpecial(Character chr, BaseItem from, BaseItem swordOrTop, short slotTo, bool unequipTwo = false)
        {
            var inventory = Constants.getInventory(from.ItemID);
            var inventorySlotOffset = (short)Math.Abs(slotTo - (slotTo % 100));
            if (unequipTwo) // If it's 2h Weapon or Overall, try to unequip both Shield + Weapon or Bottom + Top
            {
                var overallOr2h = from;
                var bottomOrShield = Constants.is2hWeapon(from.ItemID) ?
                    chr.Inventory.GetItem(inventory, (short)-(inventorySlotOffset + 10)) :
                    chr.Inventory.GetItem(inventory, (short)-(inventorySlotOffset + 6));

                if (bottomOrShield != null)
                {
                    if (swordOrTop != null)
                    {
                        if (!chr.Inventory.HasSlotsFreeForItem(bottomOrShield.ItemID, 1))
                        {
                            ShowInventoryFullError(chr);
                            return;
                        }
                    }
                    SwapSlots(chr, overallOr2h, swordOrTop, overallOr2h.InventorySlot, slotTo);
                    Unequip(chr, bottomOrShield, chr.Inventory.GetNextFreeSlotInInventory(inventory));
                    return;
                }
            }
            else // If it's Bottom or Shield, check if an Overall or 2h Weapon is equipped.
            {
                var bottomOrShield = from;
                var overallOr2h = Constants.isShield(from.ItemID) ?
                    chr.Inventory.GetItem(inventory, (short)-(inventorySlotOffset + 11)) :
                    chr.Inventory.GetItem(inventory, (short)-(inventorySlotOffset + 5));
                if (overallOr2h != null)
                {
                    if (Constants.is2hWeapon(overallOr2h.ItemID) || Constants.isOverall(overallOr2h.ItemID))
                    {
                        SwapSlots(chr, bottomOrShield, swordOrTop, from.InventorySlot, slotTo);
                        Unequip(chr, overallOr2h, chr.Inventory.GetNextFreeSlotInInventory(inventory));
                        return;
                    }
                }
            }
            Equip(chr, from, swordOrTop, from.InventorySlot, slotTo); // If there's no special action required, go through regular equip.
        }

        private static void Equip(Character chr, BaseItem from, BaseItem to, short slotFrom, short slotTo)
        {
            var inventory = Constants.getInventory(from.ItemID);
            chr.Inventory.SetItem(inventory, slotFrom, to);
            chr.Inventory.SetItem(inventory, slotTo, from);
            SwitchSlots(chr, slotFrom, slotTo, inventory);
            MapPacket.SendAvatarModified(chr, MapPacket.AvatarModFlag.Equips);
        }

        private enum WearFailReason
        {
            None,
            StatsTooLow,
            WeaponMismatch,
            CashOnNonCashSlot,
            IncorrectBodypart,
            PetEquipNotUsable,
        }


        private static WearFailReason canWearItem(Character chr, CharacterPrimaryStats stats, EquipData data, short slot)
        {
            var absSlot = Math.Abs(slot);
            var equippingCosmeticItem = absSlot >= 100;

            var slotType = (Constants.EquipSlots.Slots)(absSlot % 100);

            // Cash item on non-cash item slot
            if (data.Cash && !equippingCosmeticItem)
                return WearFailReason.CashOnNonCashSlot;

            if (!Constants.IsCorrectBodyPart(data.ID, (short)slotType, chr.Gender))
                return WearFailReason.IncorrectBodypart;

            if (Constants.getItemType(data.ID) == Constants.Items.Types.ItemTypes.PetEquip && data.Pets != null)
            {
                // Check if pet requirement is valid
                var activePet = chr.GetSpawnedPet();
                if (activePet == null) return WearFailReason.PetEquipNotUsable;
                if (!data.Pets.Contains(activePet.ItemID))
                {
                    _log.Debug($"Unable to equip pet equip {data.ID} because pet {activePet.ItemID} isn't registered for the equip");
                    return WearFailReason.PetEquipNotUsable;
                }
            }

            if (slotType == Constants.EquipSlots.Slots.Weapon)
            {
                // Validate if we can wear this item if we already have a cosmetic one
                var coverWeaponSlot = absSlot;
                if (!equippingCosmeticItem)
                {
                    // Take the cosmetic item
                    coverWeaponSlot += 100;
                }
                else
                {
                    // Take the regular item
                    coverWeaponSlot -= 100;
                }

                Trace.WriteLine($"{coverWeaponSlot} {absSlot}");

                // Equips are negative
                coverWeaponSlot = (short)-coverWeaponSlot;

                var coverWeapon = chr.Inventory.GetItem(1, coverWeaponSlot) as EquipItem;

                if (coverWeapon != null)
                {
                    var regularWeaponID = data.ID;
                    var coverWeaponID = coverWeapon.ItemID;

                    var coverWeaponEquipData = DataProvider.Equips[coverWeapon.ItemID];
                    var regularWeaponEquipData = data;
                    if (absSlot > 100)
                    {
                        // Need to swap the cover and the regular item data
                        (coverWeaponEquipData, regularWeaponEquipData) = (regularWeaponEquipData, coverWeaponEquipData);
                        (coverWeaponID, regularWeaponID) = (regularWeaponID, coverWeaponID);
                    }
                    
                    // Validate if we are equipping the same types
                    var wt1 = Constants.getWeaponType(regularWeaponID);
                    var wt2 = Constants.getWeaponType(coverWeaponID);


                    var regularWeaponStanceData = (EquipStanceInfo)regularWeaponEquipData;
                    EquipStanceInfo coverWeaponStanceData;
                    if (coverWeaponEquipData.EquipStanceInfos != null)
                    {
                        if (!coverWeaponEquipData.EquipStanceInfos.TryGetValue((byte)wt1, out coverWeaponStanceData))
                        {
                            _log.Warn($"Unable to equip {regularWeaponID}, weapon type mismatch with {coverWeaponID}, no compatible weapon type {wt1} on cashitem");
                            return WearFailReason.WeaponMismatch;
                        }
                    }
                    else
                    {
                        coverWeaponStanceData = (EquipStanceInfo)coverWeaponEquipData;
                    }
                    
                    if (wt2 != Constants.Items.Types.WeaponTypes.Cash && wt1 != wt2)
                    {
                        _log.Warn($"Unable to equip {regularWeaponID}, weapon type mismatch with {coverWeaponID}: {wt1} != {wt2}");
                        return WearFailReason.WeaponMismatch;
                    }

                    // We hack animations so that cosmetic weapons are actually the main weapon
                    // For cash covers, we need to validate if the slots the main weapon has are in the cover
                    // Note: invisible items might have no animation frames...

                    if (wt2 == Constants.Items.Types.WeaponTypes.Cash &&
                        coverWeaponStanceData.AnimationFrames.Length > 0)
                    {
                        var missingStances = regularWeaponStanceData.AnimationFrames.Where(x => !coverWeaponStanceData.AnimationFrames.Contains(x)).ToArray();

                        if (missingStances.Length != 0)
                        {
                            _log.Warn($"Equip {regularWeaponID} has missing animation frames with {coverWeaponID}: {string.Join(", ", missingStances)}");
                            
                            // return WearFailReason.WeaponMismatch;
                        }
                    }
                }
            }

            bool validStats;

            // Ignore stat requirements for Cosmetic things
            if (equippingCosmeticItem)
            {
                validStats = true;
            }
            else
            {
                var equipStats = chr.PrimaryStats.CalculateBonusSet(slotType);
                equipStats.Str += chr.PrimaryStats.Str;
                equipStats.Dex += chr.PrimaryStats.Dex;
                equipStats.Int += chr.PrimaryStats.Int;
                equipStats.Luk += chr.PrimaryStats.Luk;


                validStats =
                    equipStats.Str >= data.RequiredStrength
                    && equipStats.Dex >= data.RequiredDexterity
                    && equipStats.Int >= data.RequiredIntellect
                    && equipStats.Luk >= data.RequiredLuck
                    && (stats.Fame >= data.RequiredFame || data.RequiredFame == 0);
            }

            var passesStatsRequirements = validStats
                   && stats.Level >= data.RequiredLevel
                   && Constants.isRequiredJob(Constants.getJobTrack(stats.Job), data.RequiredJob);

            if (!passesStatsRequirements)
            {
                return WearFailReason.StatsTooLow;
            }

            return WearFailReason.None;
        }

        private static void HandleEquip(Character chr, BaseItem from, BaseItem to, short slotFrom, short slotTo)
        {
            _log.Info($"Trying to equip {from.ItemID} to {slotTo}");
            var wearFailReason = canWearItem(chr, chr.PrimaryStats, DataProvider.Equips[from.ItemID], (short)-slotTo);
            if (chr.IsGM && wearFailReason != WearFailReason.None)
            {
                MessagePacket.SendNotice(chr, $"Would've blocked equipping item with error {wearFailReason}, but you are a GM.");
            }

            if (!chr.IsGM &&
                wearFailReason != WearFailReason.None)
            {
                _log.Warn($"Trying to wear an item that he cannot. from {slotFrom} to {slotTo}. Itemid: {from.ItemID}. Reason: {wearFailReason}");

                var message = wearFailReason switch
                {
                    WearFailReason.StatsTooLow => "Your stats are too low to equip this item.",
                    WearFailReason.IncorrectBodypart => "Unable to equip this item.",
                    WearFailReason.PetEquipNotUsable => "You do not have a pet that can wear this item.",
                    WearFailReason.WeaponMismatch => "The weapon type must match the equipped item.",
                    WearFailReason.CashOnNonCashSlot => "Cash items can only be equipped as cosmetics.",
                    _ => ""
                };

                if (message != "")
                {
                    MessagePacket.SendPopup(chr, message);
                }
                return;
            }

            if (Constants.isOverall(from.ItemID) || Constants.is2hWeapon(from.ItemID))
            {
                EquipSpecial(chr, from, to, slotTo, true);
            }
            else if (Constants.isBottom(from.ItemID) || Constants.isShield(from.ItemID))
            {
                EquipSpecial(chr, from, to, slotTo);
            }
            else
            {
                Equip(chr, from, to, slotFrom, slotTo);
            }
        }

        private static void Unequip(Character chr, BaseItem equip, short slotTo)
        {
            var inventory = Constants.getInventory(equip.ItemID);
            var slotFrom = equip.InventorySlot;

            var swap = chr.Inventory.GetItem(inventory, slotTo);

            if (swap == null && !chr.Inventory.HasSlotsFreeForItem(equip.ItemID, 1))  // Client checks this for us, but in case of PE
            {
                ShowInventoryFullError(chr);
                return;
            }

            if (swap != null && slotFrom < 0)
            {
                HandleEquip(chr, swap, equip, slotTo, slotFrom);
                return;
            }

            chr.Inventory.SetItem(inventory, slotFrom, swap);
            chr.Inventory.SetItem(inventory, slotTo, equip);

            SwitchSlots(chr, slotFrom, slotTo, inventory);
            MapPacket.SendAvatarModified(chr, MapPacket.AvatarModFlag.Equips);

        }

        public static void ShowInventoryFullError(Character chr)
        {
            MessagePacket.SendPopup(chr, "Please check and see if your inventory is full or not.");
        }

        public static void HandleInventoryPacket(Character chr, Packet packet)
        {
            chr.ExclRequestSet = true;
            
            if (chr.AssertForHack(!chr.CanAttachAdditionalProcess, "Trying to change items while !CanAttachAdditionalProcess"))
            {
                return;
            }

            try
            {
                var inventory = packet.ReadByte();
                var slotFrom = packet.ReadShort(); // Slot from
                var slotTo = packet.ReadShort(); // Slot to

                if (slotFrom == 0 || inventory < 0 || inventory > 5) return;

                Trace.WriteLine($"Trying to swap from {slotFrom} to {slotTo}, inventory {inventory}");
                if (slotFrom < 0) Trace.WriteLine("From: " + (Constants.EquipSlots.Slots)((-slotFrom) % 100));
                if (slotTo < 0) Trace.WriteLine("To: " + (Constants.EquipSlots.Slots)((-slotTo) % 100));

                var itemFrom = chr.Inventory.GetItem(inventory, slotFrom); // Item being moved
                var itemTo = chr.Inventory.GetItem(inventory, slotTo); // Item in target position, if any

                if (itemFrom == null) return; // Packet Editing, from slot contains no item.
                if (slotTo < 0 && slotFrom < 0) return; // Packet Editing, both target and source slots are in equip.

                if ((slotFrom < 0 || slotTo < 0) && !chr.CanAttachAdditionalProcess)
                {
                    // Do not allow to change equips while in miniroom
                    return;
                }

                if (slotFrom > 0 && slotTo < 0)
                {
                    Trace.WriteLine($"HandleEquip");
                    HandleEquip(chr, itemFrom, itemTo, slotFrom, slotTo);
                }
                else if (slotFrom < 0 && slotTo > 0)
                {
                    Trace.WriteLine($"Unequip");
                    Unequip(chr, itemFrom, slotTo);
                }
                else if (slotTo == 0)
                {
                    if (chr.IsGM && !chr.IsAdmin)
                    {
                        MessagePacket.SendAdminWarning(chr, "You cannot drop items.");
                    }
                    else
                    {
                        if (chr.AssertForHack(itemFrom.CashId != 0, $"Trying to drop cash item {itemFrom.ItemID}", autoban: true))
                        {
                            return;
                        }

                        var quantity = packet.ReadShort();
                        DropItem(chr, inventory, slotFrom, quantity, itemFrom);
                    }
                }
                else
                {
                    Trace.WriteLine($"Changing slot");
                    SwapSlots(chr, itemFrom, itemTo, slotFrom, slotTo);
                }

                if (slotTo == -(int)Constants.EquipSlots.Slots.Weapon && itemTo != null)
                {
                    chr.PrimaryStats.CheckWeaponBuffs(itemTo.ItemID);
                }

                // TO-DO: Pets + Rings
            }
            catch (Exception ex)
            {
                _log.Error("Exception in item movement handler", ex);
            }

        }

        public static void HandleUseSummonSack(Character chr, Packet packet)
        {
            chr.ExclRequestSet = true;
            
            if (chr.AssertForHack(!chr.CanAttachAdditionalProcess, "Trying to use summing sack while !CanAttachAdditionalProcess"))
            {
                return;
            }

            var slot = packet.ReadShort();
            var itemid = packet.ReadInt();

            var item = chr.Inventory.GetItem(2, slot);
            if (item == null || 
                item.ItemID != itemid || 
                !DataProvider.Items.TryGetValue(itemid, out var data))
            {
                return;
            }

            if (data.Summons.Count == 0)
            {
                _log.Error($"No summons in summoning sack {itemid}");
                return;
            }

            if (chr.AssertForHack(
                    !chr.Inventory.SubtractAmountFromSlot(Constants.getInventory(itemid), slot, 1),
                    "Tried to use summon sack while not having them (???)"
                ))
                return;

            foreach (var isi in data.Summons)
            {
                if (DataProvider.Mobs.ContainsKey(isi.MobID))
                {
                    if (Rand32.Next() % 100 < isi.Chance)
                    {
                        chr.Field.CreateMobWithoutMobGen(isi.MobID, chr.Position, chr.Foothold, type: (MobAppear)data.Type);
                    }
                }
                else
                {
                    Program.MainForm.LogAppend("Summon sack {0} has mobid that doesn't exist: {1}", itemid, isi.MobID);
                }
            }

            Server.Instance.PlayerLogDiscordReporter.Enqueue($"{chr} used summoning sack {data.Name} in {chr.Field.FullName}");
        }

        public static void HandleUseReturnScroll(Character chr, Packet packet)
        {
            chr.ExclRequestSet = true;
            
            if (chr.AssertForHack(!chr.CanAttachAdditionalProcess, "Trying to use return scroll while !CanAttachAdditionalProcess"))
            {
                return;
            }

            var slot = packet.ReadShort();
            var itemid = packet.ReadInt();

            const byte UseInventory = 2;

            var item = chr.Inventory.GetItem(UseInventory, slot);
            if (item == null || item.ItemID != itemid || !DataProvider.Items.TryGetValue(itemid, out var data))
            {
                return;
            }

            if (data == null || data.MoveTo == 0)
            {
                return;
            }
            int map;
            if (data.MoveTo == Constants.InvalidMap || !MapProvider.Maps.ContainsKey(data.MoveTo))
            {
                map = chr.Field.ReturnMap;
            }
            else
            {
                map = data.MoveTo;
            }

            if (!chr.Inventory.SubtractAmountFromSlot(UseInventory, slot, 1))
            {
                _log.Error("Unable to use Return scroll, cant remove it from slot.");
                return;
            }
            
            chr.ChangeMap(map);
        }

        public static ILog _scrollingLog = LogManager.GetLogger("ScrollingLog");
        public struct ScrollResult
        {
            public int itemId { get; set; }
            public int scrollId { get; set; }
            public object scrollData { get; set; }
            public object itemData { get; set; }
            public bool succeeded { get; set; }
            public bool cursed { get; set; }
            public uint successRoll { get; set; }
            public uint curseRoll { get; set; }
        }

        public static void HandleScrollItem(Character chr, Packet packet)
        {
            chr.ExclRequestSet = true;
            
            if (chr.AssertForHack(!chr.CanAttachAdditionalProcess, "Trying to scroll item while !CanAttachAdditionalProcess"))
            {
                return;
            }

            var scrollslot = packet.ReadShort();
            var itemslot = packet.ReadShort();

            const byte UseInventory = 2;
            const byte EquipInventory = 1;

            var scroll = chr.Inventory.GetItem(UseInventory, scrollslot);
            var equip = (EquipItem)chr.Inventory.GetItem(EquipInventory, itemslot);
            if (scroll == null ||
                equip == null ||
                Constants.itemTypeToScrollType(equip.ItemID) != Constants.getScrollType(scroll.ItemID) ||
                !DataProvider.Items.TryGetValue(scroll.ItemID, out var scrollData)
                )
            {
                _scrollingLog.Warn($"Tried to use a scroll that didn't exist {scroll == null}, equip that didnt exist {equip == null}, scroll types that didnt match or no scroll data available.");
                return;
            }

            var scrollBundleItem = scroll as BundleItem;
            if (scrollBundleItem == null)
            {
                _scrollingLog.Error("Scroll is not a bundle? what in the actual fuck");
                return;
            }

            if (scrollData.ScrollSuccessRate == 0 || equip.Slots == 0)
            {
                _scrollingLog.Warn($"Tried to scroll, but the equip has no scrolls left {equip.Slots} or zero success rate {scrollData.ScrollSuccessRate}");
                return;
            }

            if (!chr.Inventory.SubtractAmountFromSlot(UseInventory, scrollslot, 1))
            {
                _scrollingLog.Error("Unable to take away scroll!");
                return;
            }

            var successRoll = Rand32.Next() % 101;
            var curseRoll = Rand32.Next() % 101;

            var succeeded = false;
            var cursed = false;


            if (successRoll <= scrollData.ScrollSuccessRate)
            {
                var oldQuality = equip.Quality;

                equip.Str += scrollData.IncStr;
                equip.Dex += scrollData.IncDex;
                equip.Int += scrollData.IncInt;
                equip.Luk += scrollData.IncLuk;
                equip.HP += scrollData.IncMHP;
                equip.MP += scrollData.IncMMP;
                equip.Watk += scrollData.IncWAtk;
                equip.Wdef += scrollData.IncWDef;
                equip.Matk += scrollData.IncMAtk;
                equip.Mdef += scrollData.IncMDef;
                equip.Acc += scrollData.IncAcc;
                equip.Avo += scrollData.IncAvo;
                equip.Jump += scrollData.IncJump;
                equip.Speed += scrollData.IncSpeed;
                equip.Hands += scrollData.IncHands;
                equip.Scrolls++;
                equip.Slots--;

                succeeded = true;

                AddItems(chr, equip);
                MapPacket.SendAvatarModified(chr, MapPacket.AvatarModFlag.Equips);
                MapPacket.SendScrollResult(chr, true);
                SendItemScrolled(chr, true);
                if (itemslot < 0 && itemslot > -100)
                {
                    chr.PrimaryStats.AddEquipStats((sbyte)itemslot, equip, false);
                }

                if (equip.Quality >= 800)
                {
                    Server.Instance.PlayerLogDiscordReporter.Enqueue($"{chr} has scrolled a pretty good {equip.Template.Name} (old quality: {oldQuality}) by passing {scrollBundleItem.Template.Name} {scrollData.ScrollSuccessRate}% (rolling {successRoll}): ```{equip.GetStatDescription()}```");
                }
            }
            else
            {
                if (scrollData.ScrollCurseRate > 0 && curseRoll <= scrollData.ScrollCurseRate)
                {
                    cursed = true;
                    chr.Inventory.TryRemoveCashItem(equip);

                    DeleteItems(chr, EquipInventory, itemslot);
                    chr.Inventory.SetItem(EquipInventory, itemslot, null);
                }
                else
                {
                    equip.Slots--;
                    // Stats have changed, so we tell the client its a new item...
                    AddItems(chr, equip);
                }
                SendItemScrolled(chr, false);
                MapPacket.SendScrollResult(chr, false);
                chr.PrimaryStats.CheckWeaponBuffs(equip.ItemID);
            }

            _scrollingLog.Info(new ScrollResult
            {
                itemData = equip,
                itemId = equip.ItemID,
                scrollData = scrollData,
                scrollId = scroll.ItemID,
                succeeded = succeeded,
                cursed = cursed,
                curseRoll = curseRoll,
                successRoll = successRoll,
            });
        }


        public interface IInventoryOperation
        {
            void Encode(Packet packet);
            bool IsMovementInfoTrigger();
        }

        public class ItemMove : IInventoryOperation
        {
            public byte Inventory;
            public short FromSlot, ToSlot;
            public ItemMove(byte inventory, short fromSlot, short toSlot)
            {
                Inventory = inventory;
                FromSlot = fromSlot;
                ToSlot = toSlot;
            }

            public void Encode(Packet packet)
            {
                packet.WriteByte(2);
                packet.WriteByte(Inventory);
                packet.WriteShort(FromSlot);

                packet.WriteShort(ToSlot);
            }

            public bool IsMovementInfoTrigger() => Inventory == 1 && (FromSlot < 0 || ToSlot < 0);
        }

        public class ItemDelete : ItemMove
        {
            public ItemDelete(byte inventory, short slot) : base(inventory, slot, 0) { }
            public ItemDelete(BaseItem item) : this(Constants.getInventory(item.ItemID), item.InventorySlot) { }
        }

        public class ItemUpdateAmount : IInventoryOperation
        {
            public byte Inventory;
            public short Slot, Amount;

            public ItemUpdateAmount(byte inventory, short slot, short amount)
            {
                Inventory = inventory;
                Slot = slot;
                Amount = amount;
            }

            public ItemUpdateAmount(BaseItem item) : this(Constants.getInventory(item.ItemID), item.InventorySlot, item.Amount)
            {

            }

            public void Encode(Packet packet)
            {
                packet.WriteByte(1);
                packet.WriteByte(Inventory);
                packet.WriteShort(Slot);

                packet.WriteShort(Amount);
            }

            public bool IsMovementInfoTrigger() => false;
        }


        public class ItemAdd : IInventoryOperation
        {
            public BaseItem Item;
            public ItemAdd(BaseItem item)
            {
                Item = item;
            }

            public void Encode(Packet packet)
            {
                packet.WriteByte(0);
                packet.WriteByte(Constants.getInventory(Item.ItemID));
                PacketHelper.AddItemData(packet, Item, Item.InventorySlot, true);
            }


            public bool IsMovementInfoTrigger() => Constants.getInventory(Item.ItemID) == 1 && Item.InventorySlot < 0;
        }

        public static void InventoryOps(Character chr, params IInventoryOperation[] operations)
        {
            if (operations.Length == 0)
            {
                NoChange(chr);
                return;
            }

            var byExclRequest = chr.ExclRequestSet;
            chr.ExclRequestSet = false;
            // _log.Debug($"Send InventoryOps with {byExclRequest}");

            const byte MaxSizePerPacket = 100;
            var opsCount = operations.Length;
            for (var i = 0; i < opsCount;)
            {
                var chunk = Math.Min(MaxSizePerPacket, opsCount - i);

                var pw = new Packet(ServerMessages.INVENTORY_OPERATION);
                // Set ExclRequest aka block shit
                // However, if you don't set ExclRequest value and it was requested
                // by a function that sets ExclRequest, your inventory or w/e is blocked.
                pw.WriteBool(i == 0 ? byExclRequest : false);
                pw.WriteByte((byte)chunk);

                var sendMovementInfoByte = false;

                for (var j = 0; j < chunk; j++)
                {
                    var op = operations[i + j];
                    if (op.IsMovementInfoTrigger()) sendMovementInfoByte = true;
                    op.Encode(pw);
                }

                if (sendMovementInfoByte)
                {
                    // This will be used to track server-client movement speed updates.
                    pw.WriteByte(0);
                }

                chr.SendPacket(pw);

                i += chunk;
            }
        }

        public static void SwitchSlots(Character chr, short slot1, short slot2, byte inventory)
        {
            InventoryOps(chr, new ItemMove(inventory, slot1, slot2));
        }

        public static void DeleteItems(Character chr, byte inventory, params short[] slots)
        {
            InventoryOps(
                chr,
                slots.Select(slot => new ItemDelete(inventory, slot)).Cast<IInventoryOperation>().ToArray()
            );
        }

        public static void UpdateItems(Character chr, params BaseItem[] items)
        {
            InventoryOps(
                chr,
                items.Select(item =>
                {
                    // It doesn't make sense to update item amounts of an equip or pets
                    // Also ignore hint on redundant cast. The moment you remove em, Select starts complaining.
                    if (item is BundleItem)
                        return (IInventoryOperation)new ItemUpdateAmount(item);
                    else
                        return (IInventoryOperation)new ItemAdd(item);
                }).ToArray()
            );
        }

        public static void AddItems(Character chr, params BaseItem[] items)
        {
            InventoryOps(
                chr,
                items.Select(item => new ItemAdd(item)).Cast<IInventoryOperation>().ToArray()
            );
        }

        public static void NoChange(Character chr)
        {
            if (!chr.ExclRequestSet) return;
            var byExclRequest = chr.ExclRequestSet;

            chr.ExclRequestSet = false;

            var pw = new Packet(ServerMessages.INVENTORY_OPERATION);
            pw.WriteBool(byExclRequest);
            pw.WriteByte(0);
            chr.SendPacket(pw);
        }

        public static void IncreaseSlots(Character chr, byte inventory, byte amount)
        {
            var pw = new Packet(ServerMessages.INVENTORY_GROW);
            pw.WriteByte(inventory);
            pw.WriteByte(amount);
            chr.SendPacket(pw);
        }

        public static void SendItemScrolled(Character chr, bool pSuccessfull)
        {
            var pw = new Packet(ServerMessages.SHOW_STATUS_INFO);
            pw.WriteByte(4);
            pw.WriteBool(pSuccessfull);
            chr.SendPacket(pw);
        }

        public static void SendItemsExpired(Character chr, List<int> pExpiredItems) // "The item [name] has been expired, and therefore, deleted from your inventory." * items
        {
            if (pExpiredItems.Count == 0) return;
            const byte MaxSizePerPacket = 100;
            for (var i = 0; i < pExpiredItems.Count; i += MaxSizePerPacket)
            {
                var amount = Math.Min(MaxSizePerPacket, pExpiredItems.Count - i);

                var pw = new Packet(ServerMessages.SHOW_STATUS_INFO);
                pw.WriteByte(5);
                pw.WriteByte((byte)amount);

                foreach (var item in pExpiredItems.Skip(i).Take(amount))
                    pw.WriteInt(item);

                chr.SendPacket(pw);
            }
        }

        public static void SendCashItemExpired(Character chr, int pExpiredItem) // "The available time for the cash item [name] has passedand the item is deleted."
        {
            var pw = new Packet(ServerMessages.SHOW_STATUS_INFO);
            pw.WriteByte(2);
            pw.WriteInt(pExpiredItem);
            chr.SendPacket(pw);
        }
    }
}