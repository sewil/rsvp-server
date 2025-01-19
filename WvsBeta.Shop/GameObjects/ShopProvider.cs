using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using log4net;
using WvsBeta.Common;
using WzTools.FileSystem;
using WzTools.Objects;

namespace WvsBeta.Shop
{
    public class ShopProvider
    {
        private static ILog _log = LogManager.GetLogger(typeof(ShopProvider));

        public static Dictionary<int, CommodityInfo> Commodity { get; } = new Dictionary<int, CommodityInfo>();
        public static Dictionary<int, int[]> Packages { get; } = new Dictionary<int, int[]>();
        
        public static void Load()
        {
            Reload();
        }

        private static int NormalizerItemID(string name) => int.Parse(name.Replace(".img", "").TrimStart('0'));

        public static void Reload()
        {
            var fileSystem = new WzFileSystem();
            fileSystem.Init(Path.Combine(Environment.CurrentDirectory, "..", "DataSvr"));

            Commodity.Clear();


            var availableEquipsAndItems = new HashSet<int>();

            void AddItemsInProperty(WzProperty prop)
            {
                foreach (var idAndInfo in prop.PropertyChildren)
                {
                    var itemID = NormalizerItemID(idAndInfo.Name);
                    availableEquipsAndItems.Add(itemID);
                }
            }

            foreach (var category in fileSystem.GetProperty("String/Item.img").PropertyChildren)
            {
                if (category.Name == "Eqp")
                {
                    // Accessory etc
                    foreach (var x in category.PropertyChildren)
                    {
                        AddItemsInProperty(x);
                    }
                }
                else
                {
                    AddItemsInProperty(category);
                }
            }
            
            AddItemsInProperty(fileSystem.GetProperty("Item/Special/0910.img"));

            var existingCategories = new List<int>();

            foreach (var node in fileSystem.GetProperty("Etc/Category.img").PropertyChildren)
            {
                // main * 100 + sub
                int id = (int) node["Category"] * 100;
                id += (int) node["CategorySub"];
                existingCategories.Add(id);
            }

            foreach (var node in fileSystem.GetProperty("Etc/Commodity.img").PropertyChildren)
            {
                var snId = node.GetInt32("SN") ?? 0;
                var itemId = node.GetInt32("ItemId") ?? 0;

                var category = snId / 100000;
                if (!existingCategories.Contains(category))
                {
                    _log.Error($"Found invalid serial number in commodity! Node {node.Name} SN {snId} category {category}");
                    continue;
                }


                var ci = Commodity[snId] = new CommodityInfo
                {
                    Count = node.GetInt16("Count") ?? 0,
                    Gender = (CommodityGenders) (node.GetInt8("Gender") ?? 0),
                    ItemID = itemId,
                    Period = node.GetInt16("Period") ?? 0,
                    OnSale = node.GetBool("OnSale") ?? false,
                    Price = node.GetInt16("Price") ?? 25252525,
                    SerialNumber = snId
                };

                if (!availableEquipsAndItems.Contains(itemId))
                {
                    _log.Warn($"Ignoring commodity SN {snId} as it contains unknown itemid {itemId}");

                    ci.OnSale = false;
                }

                if (ci.Price == 18000 && ci.OnSale)
                {
                    _log.Warn($"Making SN {ci.SerialNumber} itemid {ci.ItemID} not OnSale because its price is 18k");
                    ci.OnSale = false;
                }

                if (!ci.OnSale)
                {
                    ci.StockState = StockState.NotAvailable;
                }
            }

            _log.Info($"Loaded {Commodity.Count} commodity items!");

            Packages.Clear();


            foreach (var node in fileSystem.GetProperty("Etc/CashPackage.img").PropertyChildren)
            {
                var sn = int.Parse(node.Name);
                var contents = node.GetProperty("SN").Children.Select(x => (int) x).ToArray();
                var error = false;
                foreach (var commoditySN in contents)
                {
                    if (Commodity.ContainsKey(commoditySN) == false)
                    {
                        error = true;
                        _log.Warn($"Ignoring Package {sn} as it contains invalid commodity id {commoditySN}");
                        break;
                    }
                }

                if (!error)
                {
                    Packages[sn] = contents;
                }
            }


            _log.Info($"Loaded {Packages.Count} cash packages!");
            GC.Collect();
        }
    }
}