using System.Linq;
using WvsBeta.Common.Sessions;
using WvsBeta.SharedDataProvider;

namespace WvsBeta.Shop
{
    public static class MapPacket
    {
        public static string GetCensoredNxLoginID(string name)
        {
            // Not like GMS but w/e
            var x = "";
            var nameLen = name.Length;
            for (int i = 0; i < nameLen; i++)
            {
                if (i >= (nameLen < 6 ? 0 : 2) && i <= nameLen - (nameLen > 6 ? 3 : 2))
                {
                    x += '*';
                }
                else
                {
                    x += name[i];
                }
            }

            return x;
        }

        public static void SendJoinCashServer(Character chr)
        {
            Packet pack = new Packet(ServerMessages.SET_CASH_SHOP);
            var flags = (
                CharacterDataFlag.Stats |
                CharacterDataFlag.Money |
                CharacterDataFlag.Equips |
                CharacterDataFlag.Consume |
                CharacterDataFlag.Install |
                CharacterDataFlag.Etc |
                CharacterDataFlag.Pet |
                CharacterDataFlag.Skills);
            pack.WriteShort((short)flags);

            if (flags.HasFlag(CharacterDataFlag.Stats))
            {
                chr.CharacterStat.Encode(pack);

                pack.WriteByte(20); // Buddylist slots
            }
            // Note: Money is in InventoryPacket

            chr.Inventory.GenerateInventoryPacket(pack, flags);

            if (flags.HasFlag(CharacterDataFlag.Skills))
            {
                pack.WriteShort((short)chr.Skills.Count);

                foreach (var skillId in chr.Skills)
                {
                    pack.WriteInt(skillId);
                    pack.WriteInt(1);
                }
            }


            // No quests, etc
            const bool showUsername = false;
            // If you put false here, you cannot buy anything. Everything will be showing a 'beta' message.
            pack.WriteBool(true);
            if (showUsername)
            {
                pack.WriteString(GetCensoredNxLoginID(chr.UserName));
            }
            else
            {
                pack.WriteString("");
            }

            // If you want to show all items, write 1 not sold SN.
            // The rest will pop up

            var itemsNotOnSale = ShopProvider.Commodity.Where(x => x.Value.OnSale == false).Select(x => x.Key).ToList();

            pack.WriteShort((short)itemsNotOnSale.Count);
            itemsNotOnSale.ForEach(pack.WriteInt);
            
            // Client does not have modified commodity support...

            // Newer versions will have discount-per-category stuff here
            // byte amount, foreach { byte category, byte categorySub, byte discountRate  }

            
            // BEST

            // Categories
            for (byte i = 1; i <= 8; i++)
            {
                // Gender (0 = male, 1 = female)
                for (byte j = 0; j <= 1; j++)
                {
                    // Top 5 items
                    for (byte k = 0; k < 5; k++)
                    {
                        Server.Instance.BestItems.TryGetValue((i, j, k), out var sn);
                        pack.WriteInt(i);
                        pack.WriteInt(j);
                        pack.WriteInt(sn);
                    }
                }
            }

            // -1 == available, 2 is not available, 1 = default?

            var customStockState = ShopProvider.Commodity.Values.Where(x => x.StockState != StockState.DefaultState).ToList();

            pack.WriteUShort((ushort)customStockState.Count);
            customStockState.ForEach(x =>
            {
                pack.WriteInt(x.SerialNumber);
                pack.WriteInt((int)x.StockState);
            });


            chr.SendPacket(pack);
        }
    }
}
