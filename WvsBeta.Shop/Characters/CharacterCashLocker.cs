
using WvsBeta.Common;
using WvsBeta.Game;
using WvsBeta.SharedDataProvider;

namespace WvsBeta.Shop
{
    public class CharacterCashLocker : CharacterCashItems
    {
        public Character Character { get; private set; }
        
        // No CharacterID as this is the cross-account locker
        public CharacterCashLocker(Character chr) : base(chr.UserID, 0)
        {
            Character = chr;
        }

        public void SortItems()
        {
            short slot = 0;
            foreach (var lockerItem in Items)
            {
                var item = GetItemFromCashID(lockerItem.CashId, lockerItem.ItemId);
                item.InventorySlot = slot++;
            }
        }

        public static BaseItem CreateCashItem(LockerItem li)
        {
            li.CashId = (long)((long)(Rand32.Next()) << 32 | Rand32.Next());
            li.CashId &= 0x00FFFFFFFFFFFFFF; // Get rid of the first byte

            var item = BaseItem.CreateFromItemID(li.ItemId, li.Amount);
            item.CashId = li.CashId;
            item.Expiration = li.Expiration;
            item.GiveStats(ItemVariation.None);

            return item;
        }

        public void CheckExpired()
        {
            var currentTime = MasterThread.CurrentDate.ToFileTimeUtc();
            GetExpiredItems(currentTime, expiredItems =>
            {
                expiredItems.ForEach(x =>
                {
                    var baseItem = GetItemFromCashID(x.CashId);

                    if (baseItem == null)
                    {
                        // ???
                        return;
                    }

                    CashPacket.SendItemExpired(Character, x.CashId);
                    RemoveLockerItem(x, baseItem, true);
                });
            });
        }
    }
}
