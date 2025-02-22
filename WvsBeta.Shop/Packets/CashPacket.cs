using log4net;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Game;
using WvsBeta.SharedDataProvider;

namespace WvsBeta.Shop
{
    public static class CashPacket
    {
        private static readonly ILog _log = LogManager.GetLogger("CashPacket");

        public struct BuyItem
        {
            public LockerItem lockerItem { get; set; }
            public bool withMaplePoints { get; set; }
            public int cashAmount { get; set; }
            public string giftedTo { get; set; }
        }

        public struct BuyPackage
        {
            public List<LockerItem> lockerItems { get; set; }
            public bool withMaplePoints { get; set; }
            public int cashAmount { get; set; }
            public string giftedTo { get; set; }
        }


        public struct BuySlotIncrease
        {
            public byte inventory { get; set; }
            public byte newSlots { get; set; }
            public bool withMaplePoints { get; set; }
            public int cashAmount { get; set; }
        }

        public struct BuyTrunkIncrease
        {
            public byte newSlots { get; set; }
            public bool withMaplePoints { get; set; }
            public int cashAmount { get; set; }
        }

        public struct CouponUse
        {
            public CouponInfo couponInfo { get; set; }
            public string giftedTo { get; set; }
        }


        public enum CashErrors
        {
            UnknownError = 0x00, // Default statement

            UnknownErrorDC_1 = 80,
            TimeRanOutTryingToProcessRequest_TryAgain = 81,
            UnknownErrorDC_2 = 82,

            NotEnoughCash = 83,
            CantGiftUnder14Year = 84,
            ExceededAllottedLimitOfPriceForGifts = 85,
            CheckExceededNumberOfCashItems = 86,
            CheckCharacterNameOrItemRestrictions = 87,
            CheckCouponNumber = 88,

            DueGenderRestrictionsNoCouponUse = 91,
            CouponOnlyForRegularItemsThusNoGifting = 92,
            CheckFullInventory = 93,
            ItemOnlyAvailableForUsersAtPremiumInternetCafe = 94,
            CoupleItemsCanBeGivenAsAGiftToACharOfDiffGenderAtSameWorld = 95,
            ItemsAreNotAvailableForPurchaseAtThisHour = 96,
            OutOfStock = 97,
            ExceededSpendingLimitOfCash = 98,
            NotEnoughMesos = 99,
            UnavailableDuringBetaTestPhase = 100,
            InvalidDoBEntered = 101,
        }

        public enum CashPacketOpcodes
        {
            // Note: Storage == inventory
            // Client packets (C)
            C_BuyItem = 2,
            C_GiftItem = 3,
            C_UpdateWishlist = 4,
            C_IncreaseSlots = 5,
            C_IncreaseTrunk = 6,
            C_MoveLtoS = 10,
            C_MoveStoL = 11,
            C_AcceptGift = 24,
            C_BuyPackage = 25,
            C_GiftPackage = 26,
            C_BuyNormal = 27,

            // Server packets (S)
            S_LoadLocker_Done = 28,
            S_LoadLocker_Failed,
            S_LoadWish_Done,
            S_LoadWish_Failed,
            S_UpdateWish_Done,
            S_UpdateWish_Failed,
            S_Buy_Done,
            S_Buy_Failed,
            S_UseCoupon_Done,
            _S_UseCoupon_Done_NormalItem, // Not sure, not implemented
            S_GiftCoupon_Done,
            S_UseCoupon_Failed,
            _S_UseCoupon_CashItem_Failed, // Not sure, not implemented

            S_Gift_Done,
            S_Gift_Failed,

            S_IncSlotCount_Done,
            S_IncSlotCount_Failed,
            S_IncTrunkCount_Done,
            S_IncTrunkCount_Failed,


            S_MoveLtoS_Done,
            S_MoveLtoS_Failed,
            S_MoveStoL_Done,
            S_MoveStoL_Failed,
            S_Delete_Done, // + long SN
            S_Delete_Failed,
            S_Expired_Done, // + long SN
            S_Expired_Failed,

            S_Couple_Done = 72, // + Itemdata, str, int, short ??
            S_Couple_Failed,
            S_BuyPackage_Done,
            S_BuyPackage_Failed,
            S_GiftPackage_Done,
            S_GiftPackage_Failed,
            S_BuyNormal_Done,
            S_BuyNormal_Failed,
        }


        private static LockerItem CreateLockerItem(int userId, CommodityInfo ci, string buyCharacterName)
        {
            var expiration = ci.Period > 0 ? Tools.GetDateExpireFromPeriodDays(ci.Period) : BaseItem.NoItemExpiration;
            var item = new LockerItem()
            {
                ItemId = ci.ItemID,
                Amount = ci.Count,
                CashId = 0, // Will be created on insert
                Expiration = expiration,
                BuyCharacterName = buyCharacterName, // Empty, only set when gift
                CharacterId = 0, // 0, as its in the locker
                CommodityId = ci.SerialNumber,
                GiftUnread = string.IsNullOrEmpty(buyCharacterName) == false,
                UserId = userId
            };
            return item;
        }

        private static (int characterId, int userId, int gender)? GetRecipientInformation(string recipient)
        {
            // Check recipient
            int recipientId = 0;
            int recipientUserId = 0;
            int recipientGender = 0;
            using (var mdr = (MySqlDataReader) Server.Instance.CharacterDatabase.RunQuery(
                "SELECT ID, userid, gender FROM characters WHERE `name` = @name AND deleted_at IS NULL",
                "@name", recipient
            ))
            {
                if (!mdr.Read())
                {
                    return null;
                }

                recipientId = mdr.GetInt32(0);
                recipientUserId = mdr.GetInt32(1);
                recipientGender = mdr.GetInt32(2);
            }

            return (recipientId, recipientUserId, recipientGender);
        }


        public static void HandleCashPacket(Character chr, Packet packet)
        {
            var header = packet.ReadByte();
            switch ((CashPacketOpcodes) header)
            {
                case CashPacketOpcodes.C_IncreaseSlots:
                {
                    var maplepoints = packet.ReadBool();
                    var inventory = packet.ReadByte();

                    if (!(inventory >= 1 && inventory <= 5))
                    {
                        _log.Warn("Increase slots failed: Invalid inventory");
                        SendError(chr, CashPacketOpcodes.S_IncSlotCount_Failed, CashErrors.OutOfStock);
                        return;
                    }

                    var points = chr.GetCashStatus();
                    var price = Server.Instance.SlotIncreasePrice;

                    if (price > (maplepoints ? points.maplepoints : points.nx))
                    {
                        _log.Warn("Increase slots failed: Not enough NX or maplepoints");
                        SendError(chr, CashPacketOpcodes.S_IncSlotCount_Failed, CashErrors.NotEnoughCash);
                        return;
                    }

                    var slots = chr.Inventory.MaxSlots[inventory - 1];

                    // Client sided limit
                    if (slots > 80)
                    {
                        _log.Warn($"Increase slots failed: already has {slots} slots on inventory {inventory}");
                        SendError(chr, CashPacketOpcodes.S_IncSlotCount_Failed, CashErrors.UnknownErrorDC_1);
                        return;
                    }

                    // no limiting atm
                    slots += 4;
                    chr.Inventory.SetInventorySlots(inventory, slots, false);

                    chr.AddSale($"Bought inventory expansion for inventory type {inventory} character {chr.ID}", price, maplepoints ? Character.TransactionType.MaplePoints : Character.TransactionType.NX);

                    Character.CashLog.Info(new BuySlotIncrease
                    {
                        cashAmount = price,
                        inventory = inventory,
                        newSlots = slots,
                        withMaplePoints = maplepoints
                    });

                    SendIncreasedSlots(chr, inventory, slots);
                    SendCashAmounts(chr);
                    break;
                }
                case CashPacketOpcodes.C_IncreaseTrunk:
                {
                    var maplepoints = packet.ReadBool();

                    var points = chr.GetCashStatus();
                    var price = Server.Instance.TrunkIncreasePrice;

                    if (price > (maplepoints ? points.maplepoints : points.nx))
                    {
                        _log.Warn("Increase trunk failed: Not enough NX or maplepoints");
                        SendError(chr, CashPacketOpcodes.S_IncTrunkCount_Failed, CashErrors.NotEnoughCash);
                        return;
                    }

                    var slots = chr.TrunkSlotCount;

                    // Client sided limit
                    if (slots > 80)
                    {
                        _log.Warn($"Increase trunk failed: already has {slots}");
                        SendError(chr, CashPacketOpcodes.S_IncTrunkCount_Failed, CashErrors.UnknownErrorDC_1);
                        return;
                    }

                    // no limiting atm
                    slots += 4;
                    chr.TrunkSlotCount = slots;

                    chr.AddSale($"Bought trunk increase through character {chr.ID}", price, maplepoints ? Character.TransactionType.MaplePoints : Character.TransactionType.NX);

                    Character.CashLog.Info(new BuyTrunkIncrease
                    {
                        cashAmount = price,
                        newSlots = slots,
                        withMaplePoints = maplepoints
                    });

                    SendIncreasedTrunk(chr, slots);
                    SendCashAmounts(chr);
                    break;
                }
                case CashPacketOpcodes.C_BuyItem:
                {
                    var maplepoints = packet.ReadBool();

                    var sn = packet.ReadInt();
                    if (!ShopProvider.Commodity.TryGetValue(sn, out var ci))
                    {
                        _log.Warn($"Buying failed: commodity not found for SN {sn}");
                        SendError(chr, CashPacketOpcodes.S_Buy_Failed, CashErrors.OutOfStock);
                        return;
                    }

                    if (ci.OnSale == false ||
                        ci.StockState == StockState.NotAvailable ||
                        ci.StockState == StockState.OutOfStock)
                    {
                        _log.Warn($"Buying failed: commodity {sn} not on sale {ci.OnSale} or out of stock {ci.StockState}");
                        SendError(chr, CashPacketOpcodes.S_Buy_Failed, CashErrors.OutOfStock);
                        return;
                    }

                    var points = chr.GetCashStatus();
                    if (ci.Gender != CommodityGenders.Both && (byte) ci.Gender != chr.Gender)
                    {
                        _log.Warn("Buying failed: invalid gender");
                        SendError(chr, CashPacketOpcodes.S_Buy_Failed, CashErrors.UnknownErrorDC_1);
                        return;
                    }

                    if (ci.Price > (maplepoints ? points.maplepoints : points.nx))
                    {
                        _log.Warn("Buying failed: not enough NX or maplepoints");
                        SendError(chr, CashPacketOpcodes.S_Buy_Failed, CashErrors.NotEnoughCash);
                        return;
                    }

                    var lockerItem = CreateLockerItem(chr.UserID, ci, "");
                    var baseItem = CharacterCashLocker.CreateCashItem(lockerItem);
                    chr.Locker.AddItem(lockerItem, baseItem);

                    chr.AddSale($"Bought cash item {lockerItem.ItemId} amount {lockerItem.Amount} (ref: {lockerItem.CashId:X16})", ci.Price, maplepoints ? Character.TransactionType.MaplePoints : Character.TransactionType.NX);

                    Character.CashLog.Info(new BuyItem
                    {
                        cashAmount = ci.Price,
                        lockerItem = lockerItem,
                        withMaplePoints = maplepoints
                    });

                    SendBoughtItem(chr, lockerItem);
                    SendCashAmounts(chr);

                    break;
                }
                case CashPacketOpcodes.C_GiftItem:
                {
                    var dob = packet.ReadUInt();
                    var sn = packet.ReadInt();
                    var recipient = packet.ReadString();

                    // check DoB
                    if (chr.DoB != dob)
                    {
                        _log.Warn($"Gifting failed: invalid DoB entered");
                        SendError(chr, CashPacketOpcodes.S_Gift_Failed, CashErrors.InvalidDoBEntered);
                        return;
                    }

                    // Check SN

                    if (!ShopProvider.Commodity.TryGetValue(sn, out var ci))
                    {
                        _log.Warn($"Gifting failed: commodity not found for SN {sn}");
                        SendError(chr, CashPacketOpcodes.S_Gift_Failed, CashErrors.OutOfStock);
                        return;
                    }

                    if (ci.OnSale == false ||
                        ci.StockState == StockState.NotAvailable ||
                        ci.StockState == StockState.OutOfStock)
                    {
                        _log.Warn($"Gifting failed: commodity {sn} not on sale {ci.OnSale} or out of stock {ci.StockState}");
                        SendError(chr, CashPacketOpcodes.S_Gift_Failed, CashErrors.OutOfStock);
                        return;
                    }

                    // Check price
                    var points = chr.GetCashStatus();
                    if (ci.Price > points.nx)
                    {
                        _log.Warn("Gifting failed: not enough NX");
                        SendError(chr, CashPacketOpcodes.S_Gift_Failed, CashErrors.NotEnoughCash);
                        return;
                    }

                    var recipientInfo = GetRecipientInformation(recipient);

                    if (recipientInfo == null)
                    {
                        // Not found
                        _log.Warn($"Gifting failed: character named {recipient} not found");
                        SendError(chr, CashPacketOpcodes.S_Gift_Failed, CashErrors.CheckCharacterNameOrItemRestrictions);
                        return;
                    }

                    if (ci.Gender != CommodityGenders.Both && recipientInfo.Value.gender != (int) ci.Gender)
                    {
                        _log.Warn($"Gifting failed: recipient not {ci.Gender}");
                        SendError(chr, CashPacketOpcodes.S_Gift_Failed, CashErrors.CheckCharacterNameOrItemRestrictions);
                        return;
                    }

                    var lockerItem = CreateLockerItem(recipientInfo.Value.userId, ci, chr.Name);
                    var baseItem = CharacterCashLocker.CreateCashItem(lockerItem);
                    // !!! We are saving the item to the current user, so we can save it alltogether at once!!!!
                    // !!! THIS MEANS THAT IF SOMEONE MANAGED TO CRASH THE CASHSHOP, NOTHING IS LOST !!!!
                    chr.Locker.AddItem(lockerItem, baseItem);

                    chr.AddSale($"Bought cash item {lockerItem.ItemId} amount {lockerItem.Amount} (ref: {lockerItem.CashId:X16}) as a gift for {recipient}", ci.Price, Character.TransactionType.NX);

                    Character.CashLog.Info(new BuyItem
                    {
                        cashAmount = ci.Price,
                        lockerItem = lockerItem,
                        withMaplePoints = false,
                        giftedTo = recipient
                    });

                    SendGiftDone(chr, lockerItem, recipient);
                    SendCashAmounts(chr);
                    break;
                }

                case CashPacketOpcodes.C_BuyPackage:
                {
                    var maplepoints = packet.ReadBool();
                    var sn = packet.ReadInt();
                    
                    if (!ShopProvider.Commodity.TryGetValue(sn, out var ci))
                    {
                        _log.Warn($"Buying Package failed: commodity not found for SN {sn}");
                        SendError(chr, CashPacketOpcodes.S_BuyPackage_Failed, CashErrors.OutOfStock);
                        return;
                    }

                    if (!ShopProvider.Packages.TryGetValue(ci.ItemID, out var commodities))
                    {
                        _log.Warn($"Buying Package failed: commodities not found for SN {sn}");
                        SendError(chr, CashPacketOpcodes.S_BuyPackage_Failed, CashErrors.OutOfStock);
                        return;
                    }

                    if (ci.OnSale == false ||
                        ci.StockState == StockState.NotAvailable ||
                        ci.StockState == StockState.OutOfStock)
                    {
                        _log.Warn($"Buying Package failed: commodity {sn} not on sale {ci.OnSale} or out of stock {ci.StockState}");
                        SendError(chr, CashPacketOpcodes.S_BuyPackage_Failed, CashErrors.OutOfStock);
                        return;
                    }

                    var points = chr.GetCashStatus();
                    if (ci.Price > (maplepoints ? points.maplepoints : points.nx))
                    {
                        _log.Warn("Buying Package failed: not enough NX or maplepoints");
                        SendError(chr, CashPacketOpcodes.S_BuyPackage_Failed, CashErrors.NotEnoughCash);
                        return;
                    }

                    if (ci.Gender != CommodityGenders.Both && chr.Gender != (int)ci.Gender)
                    {
                        _log.Warn($"Buying Package failed: buyer not {ci.Gender}");
                        SendError(chr, CashPacketOpcodes.S_BuyPackage_Failed, CashErrors.CheckCharacterNameOrItemRestrictions);
                        return;
                    }

                    // Everything OK

                    var lockerItems = new List<LockerItem>();
                    for (var i = 0; i < ci.Count; i++)
                    {
                        foreach (var subSN in commodities)
                        {
                            if (!ShopProvider.Commodity.TryGetValue(subSN, out var subCI))
                            {
                                _log.Warn($"Buying Package failed: commodity not found for SN {subSN} for package {sn}");
                                SendError(chr, CashPacketOpcodes.S_BuyPackage_Failed, CashErrors.OutOfStock);
                                return;
                            }

                            if (subCI.Gender != CommodityGenders.Both && chr.Gender != (int) ci.Gender)
                            {
                                _log.Warn($"Buying Package failed: recipient not {subCI.Gender}");
                                SendError(chr, CashPacketOpcodes.S_BuyPackage_Failed, CashErrors.CheckCharacterNameOrItemRestrictions);
                                return;
                            }

                            lockerItems.Add(CreateLockerItem(chr.UserID, subCI, ""));
                        }
                    }

                    // All locker items are locked and loaded, give em away

                    chr.AddSale($"Bought package SN {ci.ItemID}", ci.Price, maplepoints ? Character.TransactionType.MaplePoints : Character.TransactionType.NX);

                    foreach (var lockerItem in lockerItems)
                    {
                        var baseItem = CharacterCashLocker.CreateCashItem(lockerItem);
                        chr.Locker.AddItem(lockerItem, baseItem);
                        chr.AddSale($"Got cash item {lockerItem.ItemId} amount {lockerItem.Amount} (ref: {lockerItem.CashId:X16}) from package SN {ci.ItemID}", 0, maplepoints ? Character.TransactionType.MaplePoints : Character.TransactionType.NX);
                    }
                    
                    Character.CashLog.Info(new BuyPackage
                    {
                        cashAmount = ci.Price,
                        lockerItems = lockerItems,
                        withMaplePoints = maplepoints
                    });

                    SendBoughtPackage(chr, lockerItems);
                    SendCashAmounts(chr);
                    break;
                }

                case CashPacketOpcodes.C_GiftPackage:
                {
                    var dob = packet.ReadUInt();
                    var sn = packet.ReadInt();
                    var recipient = packet.ReadString();

                    // check DoB
                    if (chr.DoB != dob)
                    {
                        _log.Warn($"Gifting Package failed: invalid DoB entered");
                        SendError(chr, CashPacketOpcodes.S_Gift_Failed, CashErrors.InvalidDoBEntered);
                        return;
                    }

                    if (!ShopProvider.Commodity.TryGetValue(sn, out var ci))
                    {
                        _log.Warn($"Gifting Package failed: commodity not found for SN {sn}");
                        SendError(chr, CashPacketOpcodes.S_GiftPackage_Failed, CashErrors.OutOfStock);
                        return;
                    }

                    if (!ShopProvider.Packages.TryGetValue(ci.ItemID, out var commodities))
                    {
                        _log.Warn($"Gifting Package failed: commodities not found for SN {sn}");
                        SendError(chr, CashPacketOpcodes.S_GiftPackage_Failed, CashErrors.OutOfStock);
                        return;
                    }

                    if (ci.OnSale == false ||
                        ci.StockState == StockState.NotAvailable ||
                        ci.StockState == StockState.OutOfStock)
                    {
                        _log.Warn($"Gifting Package failed: commodity {sn} not on sale {ci.OnSale} or out of stock {ci.StockState}");
                        SendError(chr, CashPacketOpcodes.S_GiftPackage_Failed, CashErrors.OutOfStock);
                        return;
                    }

                    var recipientInfo = GetRecipientInformation(recipient);

                    if (recipientInfo == null)
                    {
                        // Not found
                        _log.Warn($"Gifting Package failed: character named {recipient} not found");
                        SendError(chr, CashPacketOpcodes.S_GiftPackage_Failed, CashErrors.CheckCharacterNameOrItemRestrictions);
                        return;
                    }

                    var points = chr.GetCashStatus();
                    if (ci.Price > points.nx)
                    {
                        _log.Warn("Gifting Package failed: not enough NX");
                        SendError(chr, CashPacketOpcodes.S_GiftPackage_Failed, CashErrors.NotEnoughCash);
                        return;
                    }

                    if (ci.Gender != CommodityGenders.Both && recipientInfo.Value.gender != (int)ci.Gender)
                    {
                        _log.Warn($"Gifting Package failed: recipient not {ci.Gender}");
                        SendError(chr, CashPacketOpcodes.S_GiftPackage_Failed, CashErrors.CheckCharacterNameOrItemRestrictions);
                        return;
                    }

                    // Everything OK

                    var lockerItems = new List<LockerItem>();
                    for (var i = 0; i < ci.Count; i++)
                    {
                        foreach (var subSN in commodities)
                        {
                            if (!ShopProvider.Commodity.TryGetValue(subSN, out var subCI))
                            {
                                _log.Warn($"Gifting Package failed: commodity not found for SN {subSN} for package {sn}");
                                SendError(chr, CashPacketOpcodes.S_GiftPackage_Failed, CashErrors.OutOfStock);
                                return;
                            }

                            if (subCI.Gender != CommodityGenders.Both && recipientInfo.Value.gender != (int) ci.Gender)
                            {
                                _log.Warn($"Gifting Package failed: recipient not {subCI.Gender}");
                                SendError(chr, CashPacketOpcodes.S_GiftPackage_Failed, CashErrors.CheckCharacterNameOrItemRestrictions);
                                return;
                            }

                            lockerItems.Add(CreateLockerItem(recipientInfo.Value.userId, subCI, chr.Name));
                        }
                    }

                    // All locker items are locked and loaded, give em away

                    chr.AddSale($"Bought package SN {ci.ItemID} as a gift for {recipient}", ci.Price, Character.TransactionType.NX);

                    foreach (var lockerItem in lockerItems)
                    {
                        var baseItem = CharacterCashLocker.CreateCashItem(lockerItem);
                        chr.Locker.AddItem(lockerItem, baseItem);
                        chr.AddSale($"Got cash item {lockerItem.ItemId} amount {lockerItem.Amount} (ref: {lockerItem.CashId:X16}) from package SN {ci.ItemID} as a gift for {recipient}", 0, Character.TransactionType.NX);
                    }

                    Character.CashLog.Info(new BuyPackage
                    {
                        cashAmount = ci.Price,
                        lockerItems = lockerItems,
                        withMaplePoints = false,
                        giftedTo = recipient
                    });

                    SendGiftedPackage(chr, recipient, ci.ItemID, ci.Count);
                    SendCashAmounts(chr);
                    break;
                }

                case CashPacketOpcodes.C_UpdateWishlist:
                {
                    for (byte i = 0; i < chr.Wishlist.Length; i++)
                    {
                        var val = packet.ReadInt();

                        if (val == 0 || ShopProvider.Commodity.ContainsKey(val))
                        {
                            chr.Wishlist[i] = val;
                        }
                        else
                        {
                            chr.Wishlist[i] = 0;
                            _log.Warn($"While updating wishlist, commodity not found for SN {val}");
                        }
                    }

                    SendWishlist(chr, true);
                    break;
                }
                case CashPacketOpcodes.C_MoveStoL:
                {
                    var cashid = packet.ReadLong();
                    var inv = packet.ReadByte();

                    var lockerItem = chr.Inventory.GetLockerItemByCashID(cashid);
                    if (lockerItem == null)
                    {
                        _log.Warn($"Moving Storage to Locker failed: locker item not found with cashid {cashid}");
                        SendError(chr, CashPacketOpcodes.S_MoveStoL_Failed, CashErrors.UnknownError);
                        return;
                    }

                    if (Constants.getInventory(lockerItem.ItemId) != inv)
                    {
                        _log.Warn($"Moving Storage to Locker failed: inventory didn't match.");
                        SendError(chr, CashPacketOpcodes.S_MoveStoL_Failed, CashErrors.UnknownError);
                        return;
                    }

                    var item = chr.Inventory.GetItemByCashID(cashid, inv);

                    lockerItem.CharacterId = 0; // Reset

                    chr.Inventory.RemoveLockerItem(lockerItem, item, false);
                    chr.Locker.AddItem(lockerItem, item);

                    SendPlacedItemInStorage(chr, lockerItem);

                    break;
                }
                case CashPacketOpcodes.C_MoveLtoS:
                {
                    var cashid = packet.ReadLong();
                    var inv = packet.ReadByte();
                    var slot = packet.ReadShort();

                    var lockerItem = chr.Locker.GetLockerItemFromCashID(cashid);
                    if (lockerItem == null)
                    {
                        _log.Warn($"Moving Locker to Storage failed: locker item not found with cashid {cashid}");
                        SendError(chr, CashPacketOpcodes.S_MoveLtoS_Failed, CashErrors.UnknownError);
                        return;
                    }

                    if (Constants.getInventory(lockerItem.ItemId) != inv)
                    {
                        _log.Warn($"Moving Locker to Storage failed: inventory didn't match.");
                        SendError(chr, CashPacketOpcodes.S_MoveLtoS_Failed, CashErrors.UnknownError);
                        return;
                    }

                    if (lockerItem.UserId != chr.UserID)
                    {
                        _log.Warn($"Moving Locker to Storage failed: tried to move cash item that was not from himself (packethack?)");
                        SendError(chr, CashPacketOpcodes.S_MoveLtoS_Failed, CashErrors.UnknownError);
                        return;
                    }

                    var item = chr.Locker.GetItemFromCashID(cashid, lockerItem.ItemId);
                    if (item == null)
                    {
                        _log.Warn($"Moving Locker to Storage failed: item not found with cashid {cashid} itemid {lockerItem.ItemId}");
                        SendError(chr, CashPacketOpcodes.S_MoveLtoS_Failed, CashErrors.UnknownError);
                        return;
                    }

                    if (slot < 1 || slot > chr.Inventory.MaxSlots[inv - 1])
                    {
                        _log.Warn($"Moving Locker to Storage failed: not enough slots left.");
                        SendError(chr, CashPacketOpcodes.S_MoveLtoS_Failed, CashErrors.CheckFullInventory);
                        return;
                    }

                    if (chr.Inventory.GetItem(inv, slot) != null)
                    {
                        _log.Warn($"Moving Locker to Storage failed: slot is not empty");
                        SendError(chr, CashPacketOpcodes.S_MoveLtoS_Failed, CashErrors.UnknownError);
                        return;
                    }

                    lockerItem.CharacterId = chr.ID;

                    chr.Inventory.AddLockerItem(lockerItem);
                    chr.Inventory.AddItem(inv, slot, item, false);
                    chr.Locker.RemoveItem(lockerItem, item);

                    SendPlacedItemInInventory(chr, item);
                    break;
                }
                case CashPacketOpcodes.C_AcceptGift:
                {
                    // We don't use this as we already gave the gift to the user.
                    var cashid = packet.ReadLong();
                    SendCashAmounts(chr);
                    break;
                }

                default:
                {
                    _log.Error($"Unknown packet received! {(CashPacketOpcodes) header} " + packet);
                    break;
                }
            }
        }

        class NormalItemData
        {
            public CouponInfo.ItemInfo itemInfo;
            public BaseItem BaseItem;
            public short Slot;
        }


        public static void HandleEnterCoupon(Character chr, Packet packet)
        {
            var recipient = packet.ReadString();
            var couponCode = packet.ReadString();
            var isGift = recipient != "";

            _log.Info($"Trying to use coupon {couponCode}");

            if (!CouponInfo.Get(couponCode, out var couponInfo))
            {
                _log.Info($"Coupon {couponCode} does not exist");
                SendError(chr, CashPacketOpcodes.S_UseCoupon_Failed, CashErrors.CheckCouponNumber);
                return;
            }

            couponCode = couponInfo.CouponCode;

            if (couponInfo.IsUsed(chr))
            {
                _log.Info($"Coupon {couponCode} is already used");
                SendError(chr, CashPacketOpcodes.S_UseCoupon_Failed, CashErrors.CheckCouponNumber);
                return;
            }

            couponInfo.LoadData?.Invoke(chr, couponInfo);

            var userId = 0;
            var gifter = "";

            var addNormalItems = new NormalItemData[0];
            var cashItems = new List<LockerItem>();

            if (isGift)
            {
                if (couponInfo.Mesos != 0)
                {
                    _log.Warn($"Gifting failed: gifting coupon has mesos. Coupon {couponCode}");
                    SendError(chr, CashPacketOpcodes.S_UseCoupon_Failed, CashErrors.CouponOnlyForRegularItemsThusNoGifting);
                    return;
                }

                if (couponInfo.NormalItems.Count != 0)
                {
                    _log.Warn($"Gifting failed: gifting coupon has normal items. Coupon {couponCode}");
                    SendError(chr, CashPacketOpcodes.S_UseCoupon_Failed, CashErrors.CouponOnlyForRegularItemsThusNoGifting);
                    return;
                }

                if (couponInfo.Flags.HasFlag(CouponInfo.CouponFlags.NoGift))
                {
                    _log.Warn($"Gifting failed: gifting coupon is flagged non-giftable. Coupon {couponCode}");
                    SendError(chr, CashPacketOpcodes.S_UseCoupon_Failed, CashErrors.CheckCharacterNameOrItemRestrictions);
                    return;
                }

                var recipientInfo = GetRecipientInformation(recipient);

                if (recipientInfo == null)
                {
                    // Not found
                    _log.Warn($"Gifting failed: character named {recipient} not found. Coupon {couponCode}");
                    SendError(chr, CashPacketOpcodes.S_UseCoupon_Failed, CashErrors.CheckCharacterNameOrItemRestrictions);
                    return;
                }

                userId = recipientInfo.Value.userId;
                gifter = chr.Name;
            }
            else
            {
                userId = chr.UserID;

                var failed = false;
                var usedSlots = new List<short>();

                addNormalItems = couponInfo.NormalItems.Concat(couponInfo.GetRandomNormalItem()).Select(x =>
                {
                    var nextSlot = chr.Inventory.GetNextFreeSlotInInventory(Constants.getInventory(x.ItemID), usedSlots.ToArray());

                    if (nextSlot == -1)
                    {
                        failed = true;
                    }

                    usedSlots.Add(nextSlot);
                    return new NormalItemData
                    {
                        BaseItem = null,
                        itemInfo = x,
                        Slot = nextSlot,
                    };
                }).ToArray();


                if (failed)
                {
                    _log.Warn($"Using coupon failed: not enough free slots. Coupon {couponCode}");
                    SendError(chr, CashPacketOpcodes.S_UseCoupon_Failed, CashErrors.CheckFullInventory);
                    return;
                }

                long totalMesos = chr.Inventory.Mesos;
                totalMesos += couponInfo.Mesos;
                if (totalMesos > int.MaxValue)
                {
                    _log.Warn($"Using coupon failed: would overflow mesos. Coupon {couponCode}");
                    SendError(chr, CashPacketOpcodes.S_UseCoupon_Failed, CashErrors.NotEnoughMesos);
                    return;
                }
            }

            // Start giving out items...
            if (!couponInfo.MarkUsed(chr))
            {
                _log.Warn($"Unable to mark coupon as used, reporting to user and DC {couponCode}");
                SendError(chr, CashPacketOpcodes.S_UseCoupon_Failed, CashErrors.UnknownErrorDC_1);
                return;
            }

            foreach (var cii in couponInfo.CashItems.Concat(couponInfo.GetRandomCashItem()))
            {
                var ci = new CommodityInfo
                {
                    Count = cii.Amount,
                    ItemID = cii.ItemID,
                    Period = cii.DaysUsable,
                };

                var lockerItem = CreateLockerItem(userId, ci, gifter);
                var baseItem = CharacterCashLocker.CreateCashItem(lockerItem);

                _log.Info($"Added cash item {ci.ItemID} {ci.Count}x for {ci.Period} days (coupon {couponCode})");

                // for gifting, its still saved on your own datastore.
                chr.Locker.AddItem(lockerItem, baseItem);
                cashItems.Add(lockerItem);
            }

            if (!isGift)
            {
                foreach (var cii in addNormalItems)
                {
                    var baseItem = BaseItem.CreateFromItemID(cii.itemInfo.ItemID, cii.itemInfo.Amount);
                    baseItem.GiveStats(ItemVariation.None);

                    var period = cii.itemInfo.DaysUsable;
                    if (period > 0)
                    {
                        baseItem.Expiration = Tools.GetDateExpireFromPeriodDays(period);
                    }

                    // Slot gets set in AddItem
                    cii.BaseItem = baseItem;

                    chr.Inventory.AddItem(
                        Constants.getInventory(baseItem.ItemID),
                        cii.Slot,
                        baseItem,
                        false
                    );

                    _log.Info($"Added normal item {cii.itemInfo.ItemID} {cii.itemInfo.Amount}x for {cii.itemInfo.DaysUsable} days (coupon {couponCode})");
                }

                chr.Inventory.Mesos += couponInfo.Mesos;
            }

            if (couponInfo.MaplePoints != 0)
            {
                var msg = $"Used coupon code {couponCode}";
                if (isGift) msg += " gifted by " + gifter;
                Server.Instance.CharacterDatabase.AddPointTransaction(userId, couponInfo.MaplePoints, "maplepoints", msg);

                _log.Info($"Added {couponInfo.MaplePoints} maplepoints (coupon {couponCode})");
            }

            Character.CashLog.Info(new CouponUse
            {
                couponInfo = couponInfo,
                giftedTo = recipient,
            });

            if (couponInfo.Flags.HasFlag(CouponInfo.CouponFlags.OncePerAccount))
            {
                Server.Instance.CharacterDatabase.AddPointTransaction(
                    chr.UserID,
                    0,
                    Character.CouponUsedTransactionType,
                    Character.CouponUsedTransactionText + couponInfo.CouponCode
                );
                _log.Info($"Marked single-use coupon (coupon {couponCode})");
            }

            // And now we finally tell the client what he got

            if (isGift)
            {
                var pw = GetPacketWriter(CashPacketOpcodes.S_GiftCoupon_Done);
                pw.WriteString(recipient);

                pw.WriteByte((byte) cashItems.Count);
                cashItems.ForEach(x => x.Encode(pw));

                pw.WriteInt(couponInfo.MaplePoints);

                chr.SendPacket(pw);
                SendCashAmounts(chr);
            }
            else
            {
                var pw = GetPacketWriter(CashPacketOpcodes.S_UseCoupon_Done);

                pw.WriteByte((byte) cashItems.Count);
                cashItems.ForEach(x => x.Encode(pw));

                pw.WriteInt(couponInfo.MaplePoints);

                pw.WriteInt(addNormalItems.Length);
                foreach (var cii in addNormalItems)
                {
                    // BMS actually uses an INT64 :mavi: Over-engineered much?
                    pw.WriteShort(cii.itemInfo.Amount);
                    pw.WriteShort(cii.Slot);
                    pw.WriteInt(cii.itemInfo.ItemID);

                    SendPlacedItemInInventory(chr, cii.BaseItem);
                }

                pw.WriteInt(couponInfo.Mesos);

                chr.SendPacket(pw);
                SendCashAmounts(chr);
            }
        }

        public static void SendWishlist(Character chr, bool update)
        {
            var pw = GetPacketWriter(update ? CashPacketOpcodes.S_UpdateWish_Done : CashPacketOpcodes.S_LoadWish_Done);
            foreach (var val in chr.Wishlist)
            {
                pw.WriteInt(val);
            }

            chr.SendPacket(pw);
        }

        public static void SendInfo(Character chr)
        {
            SendCashAmounts(chr);
            SendWishlist(chr, false);
            SendLocker(chr);
        }

        private static Packet GetPacketWriter(CashPacketOpcodes opcode)
        {
            var pw = new Packet(ServerMessages.CASHSHOP_ACTION);
            pw.WriteByte(opcode);
            return pw;
        }

        public static void SendLocker(Character chr)
        {
            var pw = GetPacketWriter(CashPacketOpcodes.S_LoadLocker_Done);

            var userLocker = chr.Locker.Items.Where(x => x.UserId == chr.UserID).ToList();

            pw.WriteByte((byte) userLocker.Count);

            foreach (var item in userLocker)
            {
                item.Encode(pw);
                item.GiftUnread = false;
            }

            pw.WriteShort(chr.TrunkSlotCount);
            chr.SendPacket(pw);
        }

        public static void SendBoughtItem(Character chr, LockerItem item)
        {
            var pw = GetPacketWriter(CashPacketOpcodes.S_Buy_Done);

            item.Encode(pw);
            chr.SendPacket(pw);
        }

        public static void SendGiftDone(Character chr, LockerItem item, string receipient)
        {
            var pw = GetPacketWriter(CashPacketOpcodes.S_Gift_Done);

            pw.WriteString(receipient);
            pw.WriteInt(item.ItemId);
            pw.WriteShort(item.Amount);
            chr.SendPacket(pw);
        }

        public static void SendBoughtPackage(Character chr, List<LockerItem> items)
        {
            var pw = GetPacketWriter(CashPacketOpcodes.S_BuyPackage_Done);
            pw.WriteByte((byte)items.Count);
            items.ForEach(x => x.Encode(pw));
            chr.SendPacket(pw);
        }

        public static void SendGiftedPackage(Character chr, string receipient, int sn, short amount)
        {
            var pw = GetPacketWriter(CashPacketOpcodes.S_GiftPackage_Done);
            pw.WriteString(receipient);
            pw.WriteInt(sn);
            pw.WriteShort(amount);
            chr.SendPacket(pw);
        }


        public static void SendIncreasedTrunk(Character chr, short slots)
        {
            var pw = GetPacketWriter(CashPacketOpcodes.S_IncTrunkCount_Done);
            pw.WriteShort(slots);
            chr.SendPacket(pw);
        }


        public static void SendIncreasedSlots(Character chr, byte inventory, short slots)
        {
            var pw = GetPacketWriter(CashPacketOpcodes.S_IncSlotCount_Done);
            pw.WriteByte(inventory);
            pw.WriteShort(slots);
            chr.SendPacket(pw);
        }

        public static void SendPlacedItemInInventory(Character chr, BaseItem item)
        {
            var pw = GetPacketWriter(CashPacketOpcodes.S_MoveLtoS_Done);
            pw.WriteShort(item.InventorySlot);
            pw.WriteByte(Constants.getInventory(item.ItemID));
            item.Encode(pw);
            chr.SendPacket(pw);
        }

        public static void SendPlacedItemInStorage(Character chr, LockerItem item)
        {
            var pw = GetPacketWriter(CashPacketOpcodes.S_MoveStoL_Done);
            item.Encode(pw);
            chr.SendPacket(pw);
        }


        public static void SendItemExpired(Character chr, long cashId)
        {
            var pw = GetPacketWriter(CashPacketOpcodes.S_Expired_Done);
            pw.WriteLong(cashId);
            chr.SendPacket(pw);
        }


        public static void SendError(Character chr, CashPacketOpcodes opcode, CashErrors error, int v = 0)
        {
            var pw = new Packet(ServerMessages.CASHSHOP_ACTION);
            pw.WriteByte(opcode);
            pw.WriteByte(error);
            pw.WriteInt(v);

            chr.SendPacket(pw);
        }

        public static void SendCashAmounts(Character chr)
        {
            var points = chr.GetCashStatus();

            var pw = new Packet(ServerMessages.CASHSHOP_UPDATE_AMOUNTS);
            pw.WriteInt(points.nx);
            pw.WriteInt(points.maplepoints);
            chr.SendPacket(pw);
        }
    }
}