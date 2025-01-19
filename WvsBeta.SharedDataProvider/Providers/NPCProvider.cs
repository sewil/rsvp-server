using System.Collections.Generic;
using System.Diagnostics;
using WvsBeta.Common;
using WvsBeta.SharedDataProvider.Templates;
using WzTools.FileSystem;

namespace WvsBeta.SharedDataProvider.Providers
{
    public class NPCProvider : TemplateProvider<NPCData>
    {
        public NPCProvider(WzFileSystem fileSystem) : base(fileSystem)
        {
        }
        
        public override IDictionary<int, NPCData> LoadAll()
        {
            return IterateAllToDict(FileSystem.GetPropertiesInDirectory("Npc"), property =>
            {
                var infoNode = property.GetProperty("info");

                var npc = new NPCData();
                npc.ID = (int) Utils.ConvertNameToID(property.Name);
                npc.Shop = new List<ShopItemData>();
                
                if (infoNode.HasChild("link"))
                {
                    var linkedNpcID = infoNode.GetString("link");

                    Trace.WriteLine($"NPC {npc.ID} has a link to NPC {linkedNpcID}");
                    linkedNpcID += ".img";
                    //todo: load this NPCs nodes
                }

                npc.Quest = infoNode.GetString("quest");
                npc.Trunk = infoNode.GetInt32("trunk") ?? 0;
                npc.Speed = infoNode.GetInt16("speed") ?? 0;
                npc.SpeakLineCount = (byte) (infoNode.GetProperty("speak")?.Children.Count ?? 0);

                if (infoNode.HasChild("shop"))
                {
                    foreach (var shopNode in infoNode.GetProperty("shop").PropertyChildren)
                    {
                        var item = new ShopItemData
                        {
                            ID = (int)Utils.ConvertNameToID(shopNode.Name),
                            Period = shopNode.GetUInt8("period") ?? 0,
                            Price = shopNode.GetInt32("price") ?? 0,
                            Stock = shopNode.GetInt32("stock") ?? 0,
                            UnitRechargeRate = shopNode.GetFloat("unitPrice") ?? 0.0f
                        };

                        npc.Shop.Add(item);
                    }
                }

                if (infoNode.HasChild("reg"))
                {
                    var regNode = infoNode.GetProperty("reg");
                    var reg = npc.Reg = new Dictionary<string, string>();
                    foreach (var regSubNode in regNode.Keys)
                    {
                        reg[regSubNode] = regNode.GetString(regSubNode);
                    }
                }

                return npc;
            }, x => x.ID);
        }

    }
}