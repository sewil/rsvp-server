using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualBasic;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Common.Tracking;
using Constants = WvsBeta.Common.Constants;

namespace WvsBeta.Game.GameObjects.MiniRooms
{
    public class PersonalShop : MiniRoomBase
    {
        public class ITEM
        {
            public byte TI;
            public short Pos;
            public short Number;
            public short Set;
            public int Price;
            public BaseItem Item;

            public void ReturnItem(Character chr, string _transaction)
            {
                var item = Item;
                var amount = (short)(Number * Set);
                if (amount <= 0) return;


                var failed = false;

                if (item.IsTreatSingly)
                {
                    if (chr.Inventory.DistributeItemInInventory(item) != 0)
                    {
                        _log.Error($"Unable to return item! See log {_transaction} for failed=true");
                        failed = true;
                    }
                }
                else
                {
                    if (chr.Inventory.AddNewItem(item.ItemID, amount) != amount)
                    {
                        _log.Error($"Unable to return item! See log {_transaction} for failed=true");
                        failed = true;
                    }
                }

                ItemTransfer.PersonalShopGetBackItem(
                    chr.ID,
                    item.ItemID,
                    amount,
                    _transaction,
                    item,
                    failed
                );
                Item = null;
            }
        }

        public struct SellLogEntry
        {
            public byte BuySlot;
            public short Number;
            public string Buyer;
            public int Mesos;
        }

        public int BalloonSN { get; private set; }
        private byte SlotCount { get; set; }
        public List<ITEM> Items { get; } = new List<ITEM>();


        private List<SellLogEntry> SellLog { get; } = new List<SellLogEntry>();

        public PersonalShop() : base(4)
        {
        }

        public override E_MINI_ROOM_TYPE GetTypeNumber() => E_MINI_ROOM_TYPE.MR_PersonalShop;
        public override byte GetCloseType() => 1;

        public override ErrorMessage IsAdmitted(Character chr, Packet packet, bool onCreate)
        {
            var ret = base.IsAdmitted(chr, packet, onCreate);
            if (ret != ErrorMessage.IsAdmitted) return ret;

            if (onCreate)
            {
                if (false && chr.IsGM) return ErrorMessage.UnableToDoIt;

                int itemID;

                if (packet != null)
                {
                    var itemSlot = packet.ReadShort();
                    itemID = packet.ReadInt();
                    var item = chr.Inventory.GetItem(4, itemSlot);
                    if (item == null)
                    {
                        _log.Error($"Trying to open PersonalShop with wrong slot? {itemSlot}");
                        return ErrorMessage.UnableToDoIt;
                    }

                    if (item.ItemID != itemID)
                    {
                        _log.Error($"ItemID mismatch in PersonalShop: {item.ItemID} != {itemID}");
                        return ErrorMessage.UnableToDoIt;
                    }

                    var itemType = Constants.getItemType(itemID);
                    if (itemType != Constants.Items.Types.ItemTypes.EtcStorePermit)
                    {
                        _log.Error($"Trying to open a store with a different item??? {itemType}");
                        return ErrorMessage.UnableToDoIt;
                    }

                    if (!chr.Field.AcceptPersonalShop)
                    {
                        _log.Debug("Unable to open shop, not allowed.");
                        return ErrorMessage.CantEstablishRoom;
                    }

                    if (!chr.Field.CheckBalloonAvailable(chr.Position, Map.BalloonType.MiniroomShop))
                    {
                        _log.Debug("Unable to open shop, overlapping.");
                        return ErrorMessage.CantEstablishRoom;
                    }
                }
                else
                {
                    itemID = 1; // 24 slots
                }

                MiniRoomSpec = (byte)(itemID % 10);


                BalloonSN = chr.Field.SetBalloon(chr.Position, Map.BalloonType.MiniroomShop);
            }
            else
            {
                // Check if in blacklist...
                // not sure if we have kicklist.

                if (NoMoreItems)
                {
                    return ErrorMessage.RoomAlreadyClosed;
                }
            }

            SlotCount = (byte)(MiniRoomSpec != 0 ? 24 : 16);

            return ErrorMessage.IsAdmitted;
        }

        public override void EncodeEnterResult(Character chr, Packet packet)
        {
            packet.WriteString(Title);
            packet.WriteByte(SlotCount);
            //packet.WriteBool(SlotCount == 24); // In v.12 this is a bool, not an amount.

            EncodeItemList(packet);

            var timeBeingOpenMs = MasterThread.CurrentTime - OpenTime;
            packet.WriteUInt((uint)((MaxTimeOpenMS - timeBeingOpenMs) / 1000));
        }

        void EncodeItemList(Packet packet)
        {
            packet.WriteByte((byte)Items.Count);
            foreach (var item in Items)
            {
                packet.WriteShort(item.Number);
                packet.WriteShort(item.Set);
                packet.WriteInt(item.Price);

                packet.WriteByte(Constants.getInventory(item.Item.ItemID)); // part of GW_ItemSlotBase::Encode
                item.Item.Encode(packet);
            }
        }


        public override void SendItemList(Character chr)
        {
            // THIS IS ACTUALLY TO SEND ITEMS TO CENTER.
            // WE DONT CARE
        }

        public override void OnLeave(Character chr, LeaveReason leaveType)
        {
            if (FindUserSlot(chr) == 0)
            {
                // Is owner...

                if (chr.HuskMode)
                {
                    var sb = new StringBuilder();
                    sb.Append("Your shop was automatically closed, ");
                    if (!Items.Any(x => x.Number > 0))
                    {
                        sb.Append("because you sold everything. ");
                    }
                    else
                    {
                        sb.Append("all unsold items were returned to your inventory. ");
                    }

                    if (SellLog.Count == 0)
                    {
                        sb.Append("Sadly, no items were sold...");
                    }
                    else
                    {
                        sb.Append("\n");
                        if (SellLog.Count == 1)
                            sb.Append("The following item was sold: ");
                        else
                            sb.Append("The following items were sold: ");

                        var first = true;
                        var i = 0;
                        foreach (var sl in SellLog)
                        {
                            if (Items.Count < sl.BuySlot) continue;
                            var slot = Items[sl.BuySlot];

                            if (++i % 3 == 0)
                            {
                                first = true;
                                sb.Append("\n");
                            }

                            if (!first) sb.Append(", ");
                            first = false;

                            sb.Append($"{sl.Buyer} bought ");
                            switch (slot.Item)
                            {
                                case EquipItem ei: sb.Append(ei.Template.Name); break;
                                case BundleItem bi: sb.Append($"{sl.Number} {bi.Template.Name}"); break;
                                case PetItem pi: sb.Append(pi.Template.Name); break;
                                default: sb.Append("???"); break;
                            }

                            sb.Append($" for {sl.Mesos:N0} mesos");
                        }
                        sb.Append(".");
                    }

                    Server.Instance.CharacterDatabase.SendNoteToUser("Admin", chr.ID, sb.ToString());
                }

                Items.Where(x => x?.Item != null && x.Number > 0).ForEach(ti =>
                {
                    ti.ReturnItem(chr, TransactionID);
                });
                
                
                chr.SetMiniRoomBalloon(false);
                CloseShop(chr.Field);
            }
        }

        void OnPutItem(Character chr, Packet packet)
        {
            var storeItem = new ITEM();

            storeItem.TI = packet.ReadByte();
            storeItem.Pos = packet.ReadShort();
            storeItem.Number = packet.ReadShort();
            storeItem.Set = packet.ReadShort();
            storeItem.Price = packet.ReadInt();

            if (CurUsers == 0) return;

            if ((Opened || FindUserSlot(chr) != 0) && !IsEmployer(chr)) return;
            if (storeItem.Set < 1) return;

            if (Items.Count >= SlotCount) return;

            var item = chr.Inventory.GetItem(storeItem.TI, storeItem.Pos);

            if (item.IsTreatSingly)
            {
                if (storeItem.Number != 1)
                {
                    _log.Error($"Trying to put up {item.ItemID} that is a singly item, but tried to put up {storeItem.Number} amount");
                    return;
                }

                if (storeItem.Set != 1)
                {
                    _log.Error($"Trying to put up {item.ItemID} that is a singly item, but tried to put up {storeItem.Set} sets");
                    return;
                }

                // if (item.IsProtected) return;
            }

            var totalAmount = (short)(storeItem.Set * storeItem.Number);
            if (totalAmount < 0)
            {
                _log.Error($"PersonalShop Invalid Number:[{item.ItemID}][{storeItem.Number}][{storeItem.Set}][{item.Amount}] totalAmount");
                return;
            }

            if ((storeItem.TI == 2 || storeItem.TI == 3 || storeItem.TI == 4) && totalAmount > item.Amount)
            {
                _log.Error($"PersonalShop Invalid Number:[{item.ItemID}][{storeItem.Number}][{storeItem.Set}][{item.Amount}]");
                return;
            }

            if (Constants.isRechargeable(item.ItemID))
            {
                if (totalAmount != 1)
                {
                    _log.Error($"Trying to put up {totalAmount} of a rechargable item {item.ItemID}");
                    return;
                }

                totalAmount = item.Amount;
            }


            storeItem.Item = chr.Inventory.TakeItemAmountFromSlot(storeItem.TI, storeItem.Pos, totalAmount);
            if (storeItem.Item == null)
            {
                _log.Error($"Unable to get item from inventory! {storeItem.TI} {storeItem.Pos} amount {totalAmount} id {item.ItemID}");
                return;
            }
            Items.Add(storeItem);

            ItemTransfer.PersonalShopPutUpItem(chr.ID, item.ItemID, totalAmount, TransactionID, storeItem.Item);

            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.PSP_Refresh);
            EncodeItemList(p);

            chr.SendPacket(p);
        }

        private void CloseShop(Map field)
        {
            field.RemoveBalloon(BalloonSN);

            // Send packet to center
        }
        
        public const long DefaultMaxTimeOpenMS = 3 * 24 * 60 * 60 * 1000;

        public long MaxTimeOpenMS { get; private set; } = DefaultMaxTimeOpenMS;

        public override void Update(long cur)
        {
            base.Update(cur);

            if (cur - OpenTime >= MaxTimeOpenMS)
            {
                // Kick owner, that'll close the shop
                DoLeave(0, LeaveReason.PSLeave_KickedTimeOver, true);
            }
        }

        public override void EncodeLeave(LeaveReason leaveType, Packet packet)
        {
            if (leaveType == LeaveReason.PSLeave_KickedTimeOver)
            {
                // Added feature: encode the hours the shop has been open for.
                packet.WriteByte((byte)TimeSpan.FromMilliseconds(MaxTimeOpenMS).TotalHours);
            }
        }

        void OnBuyItem(Character chr, Packet packet)
        {
            var buySlot = packet.ReadByte();
            var number = packet.ReadShort();
            var userSlot = FindUserSlot(chr);

            if (CurUsers == 0 ||
                !Opened ||
                userSlot < 1 ||
                (false && chr.IsGM) ||
                buySlot < 0 ||
                buySlot >= SlotCount ||
                buySlot >= Items.Count)
            {
                _log.Error($"Error while buying item {buySlot} amount {number}");

                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.PSP_BuyResult);
                p.WriteByte(BuyResults.InventoryFull);
                chr.SendPacket(p);
                return;
            }

            var shopItem = Items[buySlot];

            if (shopItem.Number < number || number <= 0)
            {
                _log.Error($"Not enough stock to buy from. {number} < {shopItem.Number}");

                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.PSP_BuyResult);
                p.WriteByte(BuyResults.NotEnoughInStock);
                chr.SendPacket(p);
                return;
            }

            var err = DoTransaction(chr, buySlot, shopItem, number, out var mesosGained);

            if (err != BuyResults.NoError)
            {
                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.PSP_BuyResult);
                p.WriteByte(err);
                chr.SendPacket(p);
                return;
            }

            {
                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.PSP_Refresh);
                EncodeItemList(p);
                Broadcast(p, null);
            }

            var sl = new SellLogEntry
            {
                Number = number,
                BuySlot = buySlot,
                Buyer = chr.VisibleName,
                Mesos = mesosGained,
            };

            SellLog.Add(sl);
            SendSellLogEntry(sl);

            if (!IsEntrusted())
            {
                CheckNoMoreItems();
            }
        }


        void SendSellLogEntry(SellLogEntry sl)
        {
            // Update the sellers dialog
            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.PSP_AddSoldItem);
            p.WriteByte(sl.BuySlot);
            p.WriteShort(sl.Number);
            p.WriteString(sl.Buyer);
            FindUserSlot(0).SendPacket(p);
        }

        private bool NoMoreItems => !Items.Any(x => x.Number > 0);

        void CheckNoMoreItems()
        {
            if (NoMoreItems)
            {
                if (false)
                {
                    // If we want to kick players...
                    for (var i = 1; i < MaxUsers; i++)
                    {
                        DoLeave(i, LeaveReason.PSLeave_NoMoreItem, true);
                    }
                }

                var owner = FindUserSlot(0);
                owner.SetMiniRoomBalloon(false);
                CloseShop(owner.Field);
            }
        }

        BuyResults DoTransaction(Character chr, byte slot, ITEM item, short number, out int mesosGained)
        {
            // number is the amount of sets
            var price = item.Price * number;
            var count = number * item.Set;
            mesosGained = 0;

            if (price <= 0)
            {
                _log.Error($"Trying to buy {number} of item {item.Item.ItemID}, but price dropped to {price}");
                return BuyResults.NotEnoughInStock;
            }

            var toBuyer = new Exchange(chr, null, false, TransactionID);
            var toSeller = new Exchange(FindUserSlot(0), null, false, TransactionID);

            toBuyer.GiveMoney(-price);
            toSeller.GiveMoney(price);

            if (!toBuyer.Check()) return BuyResults.NotEnoughMesos;
            if (!toSeller.Check()) return BuyResults.NotEnoughMesos;

            if (item.Item.IsTreatSingly)
            {
                // We duplicate the item here, because otherwise if someone scrolls an item
                // after buying it in the shop, it'll display the scrolled effects in the shop too.
                toBuyer.GiveItem(item.Item.Duplicate());
            }
            else
            {
                toBuyer.GiveItem(item.Item.ItemID, count);
            }

            if (!toBuyer.Check())
            {
                _log.Error("Buyers inventory is full");
                return BuyResults.InventoryFull;
            }

            if (!toSeller.Check())
            {
                _log.Error("Huh? this error should not happen...");
                return BuyResults.NotEnoughInStock;
            }

            if (!toSeller.Perform() || !toBuyer.Perform())
            {
                _log.Error("Huh? this error should not happen...");
                return BuyResults.PriceTooHigh;
            }

            MesosTransfer.PlayerBuysFromPersonalShop(
                toBuyer.Character.ID,
                toSeller.Character.ID,
                price,
                TransactionID
            );

            ItemTransfer.PersonalShopBoughtItem(
                toSeller.Character.ID,
                toBuyer.Character.ID,
                item.Item.ItemID,
                (short)count,
                TransactionID,
                item.Item,
                price
            );

            if (item.Item.IsTreatSingly)
            {
                item.Number = 0;
            }
            else
            {
                item.Number -= number;
            }

            mesosGained = price;

            return BuyResults.NoError;
        }

        void OnMoveItemToInventory(Character chr, Packet packet)
        {
            var slot = packet.ReadShort();
            var userSlot = FindUserSlot(chr);

            if (Opened || CurUsers == 0 || userSlot != 0)
            {
                return;
            }


            if (slot < 0 ||
                slot >= SlotCount ||
                slot >= Items.Count)
            {
                return;
            }

            var shopItem = Items[slot];
            shopItem.ReturnItem(chr, TransactionID); // Also writes the change in the log
            Items.Remove(shopItem);


            var p = new Packet(ServerMessages.MINI_ROOM_BASE);
            p.WriteByte(Opcodes.PSP_MoveItemToInventory);
            p.WriteByte((byte)Items.Count);
            p.WriteShort(slot);
            chr.SendPacket(p);
        }

        public override bool ProcessChat(Character chr, string message)
        {
            return base.ProcessChat(chr, message);
        }

        public override void ResumeFromHuskMode(Character chr)
        {
            base.ResumeFromHuskMode(chr);

            SellLog.ForEach(SendSellLogEntry);
            chr.SetMiniRoomBalloon(Opened);

            if (Opened)
            {
                var p = new Packet(ServerMessages.MINI_ROOM_BASE);
                p.WriteByte(Opcodes.PSP_SetOpened);
                chr.SendPacket(p);
            }
        }

        public override bool CanEnterHuskMode(Character chr)
        {
            if (NoMoreItems)
            {
                ShowMessage(0, "Unable to logout, all your items are sold out.", chr);
                return false;
            }

            return true;
        }

        public override void OnPacket(Opcodes type, Character chr, Packet packet)
        {
            switch (type)
            {
                case Opcodes.PSP_PutItem:
                    OnPutItem(chr, packet);
                    return;

                case Opcodes.PSP_BuyItem:
                    OnBuyItem(chr, packet);
                    return;

                case Opcodes.PSP_MoveItemToInventory:
                    OnMoveItemToInventory(chr, packet);
                    return;
                case Opcodes.PSP_Logout:
                    EnterHuskMode(chr);
                    return;

                default:
                    base.OnPacket(type, chr, packet);
                    return;
            }
        }

        enum BuyResults
        {
            NoError = 0,
            NotEnoughInStock,
            NotEnoughMesos,
            PriceTooHigh,
            BuyerOutOfMesos,
            InventoryFull,
        }
    }
}