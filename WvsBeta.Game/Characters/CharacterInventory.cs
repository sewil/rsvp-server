using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.SharedDataProvider;

namespace WvsBeta.Game
{
    public class CharacterInventory : BaseCharacterInventory
    {
        private Character Character { get; }
        public int ChocoCount { get; private set; }
        public int ActiveItemID { get; private set; }

        public CharacterInventory(Character character) : base(character.UserID, character.ID)
        {
            Character = character;
        }

        public void ValidateInventory()
        {
            for (var i = 0; i < Items.Length; i++)
            {
                var inv = Items[i];
                for (var slot = 0; slot < inv.Length; slot++)
                {
                    var item = inv[slot];
                    if (item == null) continue;

                    var amountInSlot = item.Amount;
                    if (amountInSlot < 0)
                    {
                        Server.Instance.ServerTraceDiscordReporter.Enqueue($"Found item {item.ItemID} in inventory of characterid {CharacterID} userid {UserID} that has an invalid amount: {amountInSlot}");
                        continue;
                    }

                    var maxAmount = 0;
                    switch (item)
                    {
                        case EquipItem _:
                            maxAmount = 1;
                            break;
                        case BundleItem bi:
                            {
                                maxAmount = bi.Template.MaxSlot;

                                if (Constants.isStar(item.ItemID))
                                {
                                    // Max amount of stars you can get through Claw Mastery is +200
                                    maxAmount += 200;
                                }

                                break;
                            }
                        case PetItem _:
                            maxAmount = 1;
                            break;
                    }

                    if (amountInSlot > maxAmount)
                    {
                        Server.Instance.ServerTraceDiscordReporter.Enqueue($"Found item {item.ItemID} in inventory of characterid {CharacterID} userid {UserID} that has an invalid amount: expected max {maxAmount}, got {amountInSlot}");
                    }
                }
            }

        }

        public void SaveInventory()
        {
            ValidateInventory();
            base.SaveInventory(Program.MainForm.LogAppend);
        }

        public new void LoadInventory()
        {
            base.LoadInventory();

            UpdateChocoCount(false);
        }

        public override void AddItem(byte inventory, short slot, BaseItem item, bool isLoading)
        {
            base.AddItem(inventory, slot, item, isLoading);

            if (slot > -100 && slot < 0 && item is EquipItem equipItem)
            {
                // Add stats for non-cosmetic items
                slot = Math.Abs(slot);
                Character.PrimaryStats.AddEquipStats((sbyte)slot, equipItem, isLoading);
            }

            if (!isLoading)
                UpdateChocoCount();
        }

        public void SetItem(byte inventory, short slot, BaseItem item)
        {
            inventory -= 1;
            if (item != null) item.InventorySlot = slot;
            if (slot < 0)
            {
                slot = Math.Abs(slot);
                var equipInv = slot / 100;
                slot %= 100;

                var equipItem = item as EquipItem;

                Equips[equipInv][slot] = equipItem;
                if (equipInv == 0)
                {
                    Character.PrimaryStats.AddEquipStats((sbyte)slot, equipItem, false);
                }
            }
            else
            {
                Items[inventory][slot] = item;
            }

            UpdateChocoCount();
        }

        public bool HasEquipped(int ItemID, bool includingCosmetic = false)
        {
            var inv = Constants.getInventory(ItemID);
            if (inv != 1) return false;

            return Equips[0].Any(x => x?.ItemID == ItemID) ||
                   (includingCosmetic && Equips[1].Any(x => x?.ItemID == ItemID));
        }

        public bool HasItem(int ItemID)
        {
            var inv = Constants.getInventory(ItemID);
            if (inv == 1)
            {
                return Equips[0].Any(x => x?.ItemID == ItemID) ||
                       Equips[1].Any(x => x?.ItemID == ItemID) ||
                       Items[0].Any(x => x?.ItemID == ItemID);
            }

            return Items[inv - 1].Any(x => x?.ItemID == ItemID);
        }

        public int GetEquippedItemId(Constants.EquipSlots.Slots slot, bool cash) => GetEquippedItemId((short)slot, cash);

        public int GetEquippedItemId(short slot, bool cash)
        {
            if (!cash)
            {
                slot = Math.Abs(slot);
                if (Equips[0].Length > slot)
                {
                    return Equips[0][slot]?.ItemID ?? 0;
                }
            }
            else
            {
                if (slot < -100)
                {
                    slot += 100;
                }

                slot = Math.Abs(slot);
                if (Equips[1].Length > slot)
                {
                    if (Equips[1][slot] != null)
                    {
                        return Equips[1][slot].ItemID;
                    }
                }
            }

            return 0;
        }

        public void UpdateChocoCount(bool sendPacket = true)
        {
            int prevChocoCount = ChocoCount;
            ChocoCount = Items[Constants.getInventory(Constants.Items.Choco) - 1].Count(x => x?.ItemID == Constants.Items.Choco);
            ActiveItemID = ChocoCount > 0 ? Constants.Items.Choco : 0;

            if (sendPacket && prevChocoCount != ChocoCount)
            {
                MapPacket.SendAvatarModified(Character, MapPacket.AvatarModFlag.ItemEffects);
            }
        }

        public int ItemCount(int itemid, bool includingEquips = false)
        {
            return (includingEquips ? Equips[0].Count(x => x?.ItemID == itemid) : 0) +
                   (includingEquips ? Equips[1].Count(x => x?.ItemID == itemid) : 0) +
                    Items.Sum(items => items.Where(x => x?.ItemID == itemid).Sum(x => x.Amount));
        }

        public Exchange Exchange(IScriptV2 script) => new Exchange(Character, script);

        // Exchange(money, ( ItemID, Count ) * n)
        public bool Exchange(IScriptV2 script, int mesos, params int[] args)
        {
            if (args.Length % 2 == 1) throw new ArgumentException("Exchange shorthand parameters must be an even count");

            var exchange = Exchange(script);

            exchange.GiveMoney(mesos);

            for (var i = 0; i < args.Length; i += 2)
            {
                exchange.GiveItem(args[i], args[i + 1]);
            }

            return exchange.Perform();
        }

        // ExchangeEx(money, ( ItemOpt, Count ) * n)
        // ItemOpt is comma separated.
        public bool ExchangeEx(IScriptV2 script, int mesos, params object[] args)
        {
            if (args.Length % 2 == 1) throw new ArgumentException("Exchange shorthand parameters must be an even count");

            var exchange = Exchange(script);

            exchange.GiveMoney(mesos);

            for (var i = 0; i < args.Length; i += 2)
            {
                string itemOpt;
                if (args[i] is string optStr) itemOpt = optStr;
                else if (args[i] is int optInt) itemOpt = optInt.ToString();
                else
                {
                    throw new ArgumentException($"Unable to parse {args[i]} as ItemOpt string/int");
                }

                var amountOpt = args[i + 1] as int?;

                if (amountOpt == null)
                {
                    throw new ArgumentException($"Unable to parse {args[i + 1]} as Count/int");
                }

                var amount = amountOpt.Value;


                // GetExchangeItemFromOptions...


                var opts = itemOpt.Split(',');
                if (!int.TryParse(opts[0], out var itemId))
                    throw new ArgumentException($"Unable to parse {opts[0]} as ItemID/int");

                if (amount < 0)
                {
                    exchange.TakeItem(itemId, -amount);
                    continue;
                }

                var item = BaseItem.CreateFromItemID(itemId, (short)amount);
                if (item == null)
                {
                    throw new ArgumentException($"Unable to create item with item id {itemId} and amount {amount}");
                }

                var itemVariation = ItemVariation.None;

                int period = 0;
                int dateExpire = 0;


                for (var optIndex = 1; optIndex < opts.Length; optIndex++)
                {
                    var opt = opts[optIndex];
                    var elems = opt.Split(':');
                    if (elems.Length != 2)
                    {
                        throw new ArgumentException($"Invalid option given {opt}");
                    }

                    var optName = elems[0];
                    var optValue = elems[1];
                    var isIntParsable = int.TryParse(optValue, out var optValueInt);

                    switch (optName)
                    {
                        case "Count":
                            if (!isIntParsable) throw new ArgumentException($"Expected int for {optName}, but it is not. Value: {optValue}");
                            amount = optValueInt;
                            break;
                        case "DateExpire":
                            if (!isIntParsable) throw new ArgumentException($"Expected int for {optName}, but it is not. Value: {optValue}");
                            dateExpire = optValueInt;
                            break;
                        case "Period":
                            if (!isIntParsable) throw new ArgumentException($"Expected int for {optName}, but it is not. Value: {optValue}");
                            period = optValueInt;
                            break;
                        case "Variation":
                            if (!isIntParsable) throw new ArgumentException($"Expected int for {optName}, but it is not. Value: {optValue}");
                            itemVariation = (ItemVariation)optValueInt;
                            break;
                        default:
                            throw new ArgumentException($"Unknown option: {optName}");
                    }
                }


                if (dateExpire != 0 && period != 0)
                {
                    throw new ArgumentException($"Cannot set both DateExpire and Period!");
                }

                if (dateExpire != 0)
                    item.Expiration = dateExpire.AsYYYYMMDDHHDateTime().ToFileTimeUtc();
                else if (period != 0)
                    item.Expiration = Tools.GetDateExpireFromPeriodMinutes(period);


                item.Amount = (short)amount;

                // Finish up giving the item

                if (item is EquipItem equip)
                {
                    equip.Amount = 1;
                    equip.GiveStats(itemVariation);
                    exchange.GiveItem(item);
                }
                else if (item is BundleItem bundle)
                {
                    // BMS tells us that it just adds slots.
                    var amountLeft = amount;
                    var maxAmount = Character.Skills.GetBundleItemMaxPerSlot(itemId);
                    if (maxAmount <= 0)
                    {
                        throw new Exception($"No bundle info for item {itemId} ??");
                    }

                    while (amountLeft > 0)
                    {
                        var bi = bundle.Duplicate() as BundleItem;
                        if (amountLeft > maxAmount)
                            bi.Amount = (short)maxAmount;
                        else
                            bi.Amount = (short)amountLeft;

                        exchange.GiveItem(bi);

                        amountLeft -= bi.Amount;
                    }
                }
                else if (item is PetItem pet)
                {
                    // Ehhh??
                    throw new InvalidOperationException("Trying to make pet? We dont support that");
                }
            }

            return exchange.Perform();
        }

        /// <summary>
        /// This function tries to put the given <paramref name="itemToAdd"/> into the inventory.
        /// If the item is a Singly one (eg cashitem, expiration), it'll try to find an empty slot.
        /// If not, it'll distribute the item to one or multiple slots.
        /// The input itemToAdd will ONLY be modified if its a Singly item, because the InventorySlot will be assigned.
        /// </summary>
        /// <param name="itemToAdd">The item that will be added in the inventory. Something like 3000 crystal ilbis, or 1 dagger.</param>
        /// <param name="sendpacket">Send packet with updates to the client</param>
        /// <param name="amountOfRechargableStacks">If the item is a rechargable, give this amount of stacks</param>
        /// <returns>Amount of items that weren't added to the inventory</returns>
        public short DistributeItemInInventory(BaseItem itemToAdd, bool sendpacket = true, short amountOfRechargableStacks = 1)
        {
            var inventory = Constants.getInventory(itemToAdd.ItemID);
            var amountToDistribute = itemToAdd.Amount;
            var itemIsRechargable = Constants.isRechargeable(itemToAdd.ItemID);
            var inventoryOps = new List<InventoryPacket.IInventoryOperation>();

            var slotsInInventory = MaxSlots[inventory - 1];

            if (itemIsRechargable)
            {
                amountToDistribute = amountOfRechargableStacks;
            }

            // Figure out how many items we can even carry in a single slot
            short maxSlots = 1;
            if (DataProvider.Items.TryGetValue(itemToAdd.ItemID, out var itemData))
            {
                maxSlots = (short)itemData.MaxSlot;
                if (maxSlots == 0)
                {
                    // 1, 100 or specified
                    maxSlots = 100;
                }
            }

            void tryAddToStack(BaseItem itemInSlot, short inventorySlot)
            {
                // Occupied slot, lets try to stack

                if (itemIsRechargable) return;

                if (itemToAdd.ItemID != itemInSlot.ItemID) return;
                if (itemInSlot.Amount >= maxSlots) return;

                // Looks a valid slot to stack onto

                var amountLeftInSlot = (short)(maxSlots - itemInSlot.Amount);

                // Max out the slot if we can, otherwise add the leftover we need to distribute
                var amountToAdd = Math.Min(amountLeftInSlot, amountToDistribute);

                amountToDistribute -= amountToAdd;
                itemInSlot.Amount += amountToAdd;

                AddItem(inventory, inventorySlot, itemInSlot, false);

                // Tell client we updated the slot
                inventoryOps.Add(new InventoryPacket.ItemUpdateAmount(itemInSlot));
            }

            if (!itemToAdd.IsTreatSingly)
            {
                // Lets stack first
                for (short i = 1; i <= slotsInInventory; i++)
                {
                    if (amountToDistribute == 0) break;

                    var itemInSlot = GetItem(inventory, i);
                    if (itemInSlot != null)
                    {
                        tryAddToStack(itemInSlot, i);
                    }
                }
            }


            for (short i = 1; i <= slotsInInventory; i++)
            {
                if (amountToDistribute == 0) break;

                // Slot 1 - 24, not 0 - 23
                var itemInSlot = GetItem(inventory, i);

                if (itemToAdd.IsTreatSingly)
                {
                    // If its occupied, find another slot
                    if (itemInSlot != null) continue;

                    // Found the slot.

                    if (itemIsRechargable)
                        amountToDistribute -= 1;
                    else
                        amountToDistribute -= itemToAdd.Amount;

                    SetItem(inventory, i, itemToAdd);

                    inventoryOps.Add(new InventoryPacket.ItemAdd(itemToAdd));
                }
                else
                {
                    // Handle stackable items.

                    if (itemInSlot != null)
                    {
                        tryAddToStack(itemInSlot, i);
                    }
                    else
                    {
                        // This is a new slot, lets add the item
                        var newItem = BaseItem.CreateFromItemID(itemToAdd.ItemID, Math.Min(maxSlots, amountToDistribute));
                        amountToDistribute -= newItem.Amount;
                        AddItem(inventory, i, newItem, false);

                        // Tell the client we added an item
                        inventoryOps.Add(new InventoryPacket.ItemAdd(newItem));
                    }
                }
            }

            if (sendpacket && inventoryOps.Count > 0)
            {
                // Send all updates to the client
                InventoryPacket.InventoryOps(Character, inventoryOps.ToArray());
            }

            return amountToDistribute;
        }

        /// <summary>
        /// AddNewItem will generate <paramref name="amountToGenerate"/> items with item id <paramref name="itemID"/>, and equip stat variation <paramref name="equipItemVariation"/>.
        /// When the item is a Rechargable, we'll generate multiple stacks of this item, with Max Stack Amount.
        /// When the item is an Equip or Pet (god forbid), treat it as a Singly item (so 1 per time).
        /// 
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="amountToGenerate"></param>
        /// <param name="equipItemVariation"></param>
        /// <returns>Amount of items that are given to the user</returns>
        public short AddNewItem(int itemID, short amountToGenerate, ItemVariation equipItemVariation = ItemVariation.None)
        {
            if (!DataProvider.HasItem(itemID))
            {
                _log.Error($"Trying to add item {itemID}, but it doesn't exist!");
                return 0;
            }

            short maxSlot = 1;
            if (!Constants.isEquip(itemID) && !Constants.isPet(itemID))
            {
                maxSlot = (short)DataProvider.Items[itemID].MaxSlot;
                if (maxSlot == 0)
                {
                    maxSlot = 100;
                }
            }

            short thisAmount = 0, givenAmount = 0;
            do
            {
                if (Constants.isRechargeable(itemID))
                {
                    thisAmount = (short)Character.Skills.GetBundleItemMaxPerSlot(itemID);
                    amountToGenerate -= 1;
                }
                else if (Constants.isEquip(itemID) || Constants.isPet(itemID))
                {
                    // Equips and Pets only hold 1 at a time
                    thisAmount = 1;
                    amountToGenerate -= 1;
                }
                else if (amountToGenerate > maxSlot)
                {
                    // In case of stackables like pots, just fill them to the brim

                    thisAmount = maxSlot;
                    amountToGenerate -= maxSlot;
                }
                else
                {
                    // In all other cases, fill a single stack
                    thisAmount = amountToGenerate;
                    amountToGenerate = 0;
                }

                var item = BaseItem.CreateFromItemID(itemID);
                item.Amount = thisAmount;

                if (Constants.isEquip(itemID))
                {
                    item.GiveStats(equipItemVariation);
                }

                givenAmount += thisAmount;

                var notGivenAmount = DistributeItemInInventory(item);
                givenAmount -= notGivenAmount;
                if (notGivenAmount > 0)
                {
                    // Inventory full
                    break;
                }
            } while (amountToGenerate > 0);

            return givenAmount;
        }

        public bool HasSlotsFreeForItem(int itemid, short amount)
        {
            if (Character.AssertForHack(amount < 0, $"Unable to check if we have slots free to item {itemid} as requested amount is {amount}"))
            {
                return false;
            }

            var stackable = Constants.isStackable(itemid);

            short slotsRequired = 0;
            byte inventory = Constants.getInventory(itemid);
            if (!Constants.isStackable(itemid) && !Constants.isStar(itemid))
            {
                slotsRequired = amount;
            }
            else if (Constants.isStar(itemid))
            {
                slotsRequired = 1;
            }
            else
            {
                short maxPerSlot = (short)DataProvider.Items[itemid].MaxSlot;
                if (maxPerSlot == 0) maxPerSlot = 100; // default 100 O.o >_>
                short amountAlready = (short)(ItemAmounts.ContainsKey(itemid) ? ItemAmounts[itemid] : 0);
                if (stackable && amountAlready > 0)
                {
                    // We should try to see which slots we can fill, and determine how much new slots are left

                    short amountLeft = amount;
                    byte inv = Constants.getInventory(itemid);
                    inv -= 1;
                    foreach (var item in Items[inv].ToList().FindAll(x => x != null && x.ItemID == itemid && x.Amount < maxPerSlot))
                    {
                        amountLeft -= (short)(maxPerSlot - item.Amount); // Substract the amount of 'slots' left for this slot
                        if (amountLeft <= 0)
                        {
                            amountLeft = 0;
                            break;
                        }
                    }

                    // Okay, so we need to figure out where to keep these stackable items.

                    // Apparently we've got space left on slots
                    if (amountLeft == 0) return true;

                    // Hmm, still need to get more slots
                    amount = amountLeft;
                }

                slotsRequired = (short)(amount / maxPerSlot);
                // Leftover slots to handle
                if ((amount % maxPerSlot) > 0)
                    slotsRequired++;
            }

            return SlotCount(inventory) >= slotsRequired;
        }

        public int ItemAmountAvailable(int itemid)
        {
            byte inv = Constants.getInventory(itemid);
            int available = 0;
            short maxPerSlot = (short)(DataProvider.Items.ContainsKey(itemid) ? DataProvider.Items[itemid].MaxSlot : 1); // equip
            if (maxPerSlot == 0) maxPerSlot = 100; // default 100 O.o >_>

            short openSlots = SlotCount(inv);
            available += (openSlots * maxPerSlot);

            for (short i = 1; i <= MaxSlots[inv - 1]; i++)
            {
                var temp = GetItem(inv, i);
                if (temp?.ItemID == itemid)
                    available += (maxPerSlot - temp.Amount);
            }

            return available;
        }

        /// <summary>
        /// Returns the amount of free slots in given inventory
        /// </summary>
        /// <param name="inventory"></param>
        /// <returns></returns>
        public short SlotCount(byte inventory)
        {
            short amount = 0;
            for (short i = 1; i <= MaxSlots[inventory - 1]; i++)
            {
                if (GetItem(inventory, i) == null)
                    amount++;
            }

            return amount;
        }


        public void GenerateInventoryPacket(Packet packet)
        {
            packet.WriteInt(Mesos);

            foreach (var item in Equips[0])
            {
                if (item == null) continue;
                PacketHelper.AddItemData(packet, item, item.InventorySlot, false);
            }

            packet.WriteByte(0);

            foreach (var item in Equips[1])
            {
                if (item == null) continue;
                PacketHelper.AddItemData(packet, item, item.InventorySlot, false);
            }

            packet.WriteByte(0);

            for (int i = 0; i < 5; i++)
            {
                packet.WriteByte(MaxSlots[i]);
                foreach (BaseItem item in Items[i])
                {
                    if (item != null && item.InventorySlot > 0)
                    {
                        PacketHelper.AddItemData(packet, item, item.InventorySlot, false);
                    }
                }

                packet.WriteByte(0);
            }
        }

        public short DeleteFirstItemInInventory(int inv)
        {
            for (short i = 1; i <= MaxSlots[inv]; i++)
            {
                var item = Items[inv][i];
                if (item != null && item.CashId == 0)
                {
                    Items[inv][i] = null;
                    UpdateChocoCount();
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        /// Set the MaxSlots for <param name="inventory"/> to <param name="slots" />.
        /// If the Items array is already initialized, it will either expand the array,
        /// or, when <param name="slots" /> is less, will remove items and shrink it.
        /// </summary>
        /// <param name="inventory">Inventory ID, 1-5</param>
        /// <param name="slots">Amount of slots</param>
        public override void SetInventorySlots(byte inventory, byte slots, bool sendPacket = true)
        {
            base.SetInventorySlots(inventory, slots, sendPacket);

            if (sendPacket)
                InventoryPacket.IncreaseSlots(Character, inventory, slots);
        }

        /// <summary>
        /// Try to remove <paramref name="amount"/> amount of itemid <paramref name="itemid"/>.
        /// Does not 'remove' rechargeable stacks, keeps them as-is (with 0 items).
        /// </summary>
        /// <param name="itemid">The Item ID</param>
        /// <param name="amount">Amount</param>
        /// <returns>Amount of items that were _not_ taken away</returns>
        public short TakeItem(int itemid, short amount)
        {
            if (amount == 0) return 0;
            if (Character.AssertForHack(amount < 0, $"Trying to TakeItem {itemid} with negative value {amount}"))
            {
                return -1;
            }

            var isRechargeable = Constants.isRechargeable(itemid);
            byte inventory = Constants.getInventory(itemid);

            var events = new List<InventoryPacket.IInventoryOperation>();

            for (short i = 1; i <= MaxSlots[inventory - 1]; i++)
            {
                BaseItem item = GetItem(inventory, i);
                if (item == null || item.ItemID != itemid) continue;

                var maxRemove = Math.Min(item.Amount, amount);
                item.Amount -= maxRemove;
                if (item.Amount == 0 && !isRechargeable)
                {
                    // Your item. Gone.
                    // NOTE: queue packet data before unsetting the data
                    events.Add(new InventoryPacket.ItemDelete(item));
                    SetItem(inventory, i, null);
                    TryRemoveCashItem(item);
                }
                else
                {
                    // Update item with new amount
                    events.Add(new InventoryPacket.ItemUpdateAmount(item));
                }

                amount -= maxRemove;

                // If we removed enough, stop the loop
                if (amount == 0) break;
            }

            if (events.Count > 0)
            {
                InventoryPacket.InventoryOps(Character, events.ToArray());
            }

            return amount;
        }

        /// <summary>
        /// Removes or splits the given slot, based on the fact if the amount you Take is equal or lower to the amount that is on the slot.
        /// However, if the item is a Singly item, the whole item is returned from the slot.
        /// </summary>
        /// <param name="inventory">The inventory the slot is in</param>
        /// <param name="slot">The slot the item is at</param>
        /// <param name="amount">amount that needs to be taken</param>
        /// <returns></returns>
        public BaseItem TakeItemAmountFromSlot(byte inventory, short slot, short amount)
        {
            if (Character.AssertForHack(amount < 0, $"Trying to take a negative amount from inventory {inventory} slot {slot} amount {amount}"))
            {
                return null;
            }

            var item = GetItem(inventory, slot);

            if (item == null)
            {
                _log.Warn($"Item not found while trying to TakeItemAmountFromSlot: inv {inventory} slot {slot} amount {amount}");
                return null;
            }
            
            if (item.Amount - amount < 0)
            {
                return null;
            }

            if (Constants.isEquip(item.ItemID) && amount != 1)
            {
                _log.Error($"Trying to take {amount} of equips (???) from inventory");
                return null;
            }

            bool removeItem;
            BaseItem newItem;
            if (item.IsTreatSingly)
            {
                if (item.Amount != amount)
                {
                    _log.Error($"Trying to take {amount} of {item.ItemID} from inventory, but theres {item.Amount} in the slot");
                    return null;
                }
                // Take the whole item
                newItem = item;
                removeItem = true;
            }
            else
            {
                newItem = item.SplitInTwo(amount);
                if (newItem == null)
                {
                    _log.Error($"Unable to split item in two! ItemID {item.ItemID}, amount {amount}");
                    return null;
                }
                removeItem = item.Amount == 0 && Constants.isRechargeable(item.ItemID) == false;
            }

            if (removeItem)
            {
                TryRemoveCashItem(item);
                InventoryPacket.DeleteItems(Character, inventory, slot);
                SetItem(inventory, slot, null);
            }
            else
            {
                // Update item
                InventoryPacket.UpdateItems(Character, item);
            }

            return newItem;
        }
        
        /// <summary>
        /// Deduce/subtract <paramref name="amount"/> from the given slot, and do not make it a new item.
        /// Only bundles can be deduced.
        /// The item will be deleted from the inventory if its used up (except for rechargable items)
        /// </summary>
        /// <param name="inventory">Inventory ID, eg 1-5</param>
        /// <param name="slot">Inventory slot</param>
        /// <param name="amount">Amount that needs to be deduced, must be positive</param>
        /// <returns>True if amount is deduced</returns>
        public bool SubtractAmountFromSlot(byte inventory, short slot, short amount)
        {
            if (Character.AssertForHack(amount < 0, $"Trying to take a negative amount from inventory {inventory} slot {slot} amount {amount}"))
            {
                return false;
            }

            var item = GetItem(inventory, slot);

            if (item == null)
            {
                _log.Warn($"Item not found while trying to SubtractAmountFromSlot: inv {inventory} slot {slot} amount {amount}");
                return false;
            }

            if (!(item is BundleItem))
            {
                _log.Warn($"Unable to deduce from slot, as item is not a bundle: inv {inventory} slot {slot} amount {amount} item {item.ItemID}");
                return false;
            }

            if (item.Amount < amount)
            {
                // Not enough to subtract
                return false;
            }

            item.Amount -= amount;
            
            var removeItem = item.Amount == 0 && Constants.isRechargeable(item.ItemID) == false;
            
            if (removeItem)
            {
                TryRemoveCashItem(item);
                InventoryPacket.DeleteItems(Character, inventory, slot);
                SetItem(inventory, slot, null);
            }
            else
            {
                // Update item
                InventoryPacket.UpdateItems(Character, item);
            }

            return true;
        }

        public double GetExtraExpRate()
        {
            // Holiday stuff here.
            double rate = 1;

            foreach (BaseItem item in this.Items[3])
            {
                if (item == null || item.ItemID < 4100000 || item.ItemID >= 4200000) continue;
                ItemData id = DataProvider.Items[item.ItemID];
                if (ItemData.RateCardEnabled(id, false))
                {
                    if (rate < id.Rate) rate = id.Rate;
                }
            }

            return rate;
        }


        private long lastCheck = 0;

        public void GetExpiredItems(long fileTime, Action<List<BaseItem>> callback)
        {
            if (fileTime - lastCheck < 45000) return;
            lastCheck = fileTime;

            var allItems = Equips[0]
                .Concat(Equips[1])
                .Concat(Items[0])
                .Concat(Items[1])
                .Concat(Items[2])
                .Concat(Items[3])
                .Concat(Items[4])
                .Where(x =>
                    x != null &&
                    x.Expiration <= fileTime
                )
                .ToList();

            if (allItems.Count == 0) return;

            callback(allItems);
        }


        public void CheckExpired()
        {
            // Note: having to use current server time, not the 'running time' of the server (CurrentTime)
            var currentFileTime = MasterThread.CurrentDate.ToFileTimeUtc();
            _cashItems.GetExpiredItems(currentFileTime, expiredItems =>
            {
                var dict = new Dictionary<byte, List<short>>();
                expiredItems.ForEach(x =>
                {
                    InventoryPacket.SendCashItemExpired(Character, x.ItemId);
                    var inventory = Constants.getInventory(x.ItemId);
                    var baseItem = GetItemByCashID(x.CashId, inventory);

                    if (baseItem != null)
                    {
                        if (!dict.ContainsKey(inventory)) dict[inventory] = new List<short>();
                        dict[inventory].Add(baseItem.InventorySlot);
                    }
                    else
                    {
                        _log.Warn($"Unable to find BaseItem for cash item {x.CashId:X16}? Unable to remove from users inventory client-side");
                    }

                    RemoveLockerItem(x, baseItem, true);
                });

                dict.ForEach(x => InventoryPacket.DeleteItems(Character, x.Key, x.Value.ToArray()));
            });

            GetExpiredItems(currentFileTime, expiredItems =>
            {
                var dict = new Dictionary<byte, List<short>>();
                var itemIds = new List<int>();
                expiredItems.ForEach(baseItem =>
                {
                    var inventory = Constants.getInventory(baseItem.ItemID);

                    if (!dict.ContainsKey(inventory)) dict[inventory] = new List<short>();
                    dict[inventory].Add(baseItem.InventorySlot);

                    if (baseItem.CashId != 0)
                    {
                        TryRemoveCashItem(baseItem);
                    }

                    SetItem(inventory, baseItem.InventorySlot, null);
                    itemIds.Add(baseItem.ItemID);
                });

                InventoryPacket.SendItemsExpired(Character, itemIds);
                dict.ForEach(x => InventoryPacket.DeleteItems(Character, x.Key, x.Value.ToArray()));
            });
        }

        public bool HasSlotsFreeForItems(IEnumerable<BaseItem> items)
        {
            //todo: calculate per slot to merge them, see Exchange
            return items
                .GroupBy(item => Constants.getInventory(item.ItemID))
                .All(grouping => SlotCount(grouping.Key) >= grouping.Count());
        }
    }
}