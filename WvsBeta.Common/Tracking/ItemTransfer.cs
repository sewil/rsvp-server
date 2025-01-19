using log4net;

namespace WvsBeta.Common.Tracking
{
    public class ItemTransfer
    {
        private static ILog log = LogManager.GetLogger("TransferLog");

        public int itemTransferID { get; set; }
        public short itemTransferSlot { get; set; }
        public short itemTransferAmount { get; set; }
        public int itemTransferFrom { get; set; }
        public int itemTransferTo { get; set; }
        public string itemTransferType { get; set; }
        public object itemData { get; set; }
        // Identification for multi-transfer events, such as selling or buying an item
        public string transferId { get; set; }

        public int price { get; set; }

        public bool failed { get; set; }
        public DropInfo dropInfo { get; set; }

        public static void PlayerBuysFromShop(int playerId, int npcId, int itemId, short amount, string transferId, object itemData)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = npcId,
                itemTransferTo = playerId,
                itemTransferID = itemId,
                itemTransferAmount = amount,
                itemTransferSlot = 0,
                itemTransferType = "PlayerBuysFromShop",
                transferId = transferId,
                itemData = itemData
            });
        }

        public static void PlayerSellsToShop(int playerId, int npcId, int itemId, short amount, string transferId, object itemData)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = playerId,
                itemTransferTo = npcId,
                itemTransferID = itemId,
                itemTransferAmount = amount,
                itemTransferSlot = 0,
                itemTransferType = "PlayerSellsToShop",
                transferId = transferId,
                itemData = itemData
            });
        }

        public static void ItemDropped(int playerId, int mapid, int itemId, short amount, string transferId, object itemData, DropInfo dropInfo)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = playerId,
                itemTransferTo = mapid,
                itemTransferID = itemId,
                itemTransferAmount = amount,
                itemTransferSlot = 0,
                itemTransferType = "PlayerDropItem",
                transferId = transferId,
                itemData = itemData,
                dropInfo = dropInfo
            });
        }

        public static void ItemPickedUp(int playerId, int mapid, int itemId, short amount, string transferId, object itemData, DropInfo dropInfo)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = mapid,
                itemTransferTo = playerId,
                itemTransferID = itemId,
                itemTransferAmount = amount,
                itemTransferSlot = 0,
                itemTransferType = "PlayerPickupItem",
                transferId = transferId,
                itemData = itemData,
                dropInfo = dropInfo
            });
        }
        

        public static void DropExpired(int mapid, int itemId, short amount, object itemData, DropInfo dropInfo)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = mapid,
                itemTransferTo = 0,
                itemTransferID = itemId,
                itemTransferAmount = amount,
                itemTransferSlot = 0,
                itemTransferType = "DropExpired",
                itemData = itemData,
                dropInfo = dropInfo
            });
        }

        public static void ItemUsed(int playerId, int itemId, short amount, string transferId)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = playerId,
                itemTransferTo = 0,
                itemTransferID = itemId,
                itemTransferAmount = (short)-amount,
                itemTransferSlot = 0,
                itemTransferType = "ItemUsed",
                transferId = transferId
            });
        }


        public static void PersonalShopPutUpItem(int srcPlayerId, int itemId, short amount, string transferId, object itemData)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = srcPlayerId,
                itemTransferTo = 0,
                itemTransferID = itemId,
                itemTransferAmount = (short)-amount,
                itemTransferSlot = 0,
                itemTransferType = "PersonalShopPutUpItem",
                transferId = transferId,
                itemData = itemData
            });
        }


        public static void PersonalShopGetBackItem(int srcPlayerId, int itemId, short amount, string transferId, object itemData, bool failed)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = srcPlayerId,
                itemTransferTo = 0,
                itemTransferID = itemId,
                itemTransferAmount = amount,
                itemTransferSlot = 0,
                itemTransferType = "PersonalShopGetBackItem",
                transferId = transferId,
                itemData = itemData,
                failed = failed
            });
        }

        public static void PersonalShopBoughtItem(int srcPlayerId, int destPlayerId, int itemId, short amount, string transferId, object itemData, int price)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = srcPlayerId,
                itemTransferTo = destPlayerId,
                itemTransferID = itemId,
                itemTransferAmount = amount,
                itemTransferSlot = 0,
                itemTransferType = "PersonalShopBoughtItem",
                transferId = transferId,
                itemData = itemData,
                price = price,
            });
        }


        public static void PlayerTradePutUp(int srcPlayerId, int itemId, short slot, short amount, string transferId, object itemData)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = srcPlayerId,
                itemTransferTo = 0,
                itemTransferID = itemId,
                itemTransferAmount = (short)-amount,
                itemTransferSlot = slot,
                itemTransferType = "PlayerTradePutUp",
                transferId = transferId,
                itemData = itemData
            });
        }

        public static void PlayerTradeReverted(int srcPlayerId, int itemId, short amount, string transferId, object itemData, bool failed)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = srcPlayerId,
                itemTransferTo = 0,
                itemTransferID = itemId,
                itemTransferAmount = amount,
                itemTransferSlot = 0,
                itemTransferType = "PlayerTradeReverted",
                transferId = transferId,
                itemData = itemData,
                failed = failed
            });
        }

        public static void PlayerTradeExchange(int srcPlayerId, int dstPlayerId, int itemId, short amount, string transferId, object itemData)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = srcPlayerId,
                itemTransferTo = dstPlayerId,
                itemTransferID = itemId,
                itemTransferAmount = amount,
                itemTransferSlot = 0,
                itemTransferType = "PlayerTradeExchange",
                transferId = transferId,
                itemData = itemData
            });
        }

        public static void PlayerStorageWithdraw(int playerId, int npcId, int itemId, short amount, string transferId, object itemData)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = npcId,
                itemTransferTo = playerId,
                itemTransferID = itemId,
                itemTransferAmount = amount,
                itemTransferSlot = 0,
                itemTransferType = "PlayerStorageWithdraw",
                transferId = transferId,
                itemData = itemData
            });
        }

        public static void PlayerStorageStore(int playerId, int npcId, int itemId, short amount, string transferId, object itemData)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = playerId,
                itemTransferTo = npcId,
                itemTransferID = itemId,
                itemTransferAmount = (short)-amount,
                itemTransferSlot = 0,
                itemTransferType = "PlayerStorageRetrieve",
                transferId = transferId,
                itemData = itemData
            });
        }

        public static void PlayerUsedSkill(int playerId, int skillId, int itemId, short amount)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = playerId,
                itemTransferTo = 0,
                itemTransferID = itemId,
                itemTransferAmount = (short)-amount,
                itemTransferSlot = 0,
                itemTransferType = "PlayerUsedSkill",
                transferId = "skill-" + skillId
            });
        }

        public static void PlayerGaveToNPC(int playerId, int npcId, int itemId, short amount, string transferId, object itemData)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = playerId,
                itemTransferTo = npcId,
                itemTransferID = itemId,
                itemTransferAmount = (short)-amount,
                itemTransferSlot = 0,
                itemTransferType = "PlayerGaveToNPC",
                transferId = transferId,
                itemData = itemData
            });
        }

        public static void PlayerReceivedFromNPC(int playerId, int npcId, int itemId, short amount, string transferId, object itemData)
        {
            log.Info(new ItemTransfer
            {
                itemTransferFrom = npcId,
                itemTransferTo = playerId,
                itemTransferID = itemId,
                itemTransferAmount = (short)amount,
                itemTransferSlot = 0,
                itemTransferType = "PlayerReceivedFromNPC",
                transferId = transferId,
                itemData = itemData
            });
        }
    }
}
