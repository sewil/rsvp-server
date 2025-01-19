using System.Collections.Generic;
using System.Linq;
using WvsBeta.Common;
using WzTools.FileSystem;
using WzTools.Objects;

namespace WvsBeta.SharedDataProvider.Providers
{
    public class ItemProvider : TemplateProvider<ItemData>
    {
        public ItemProvider(WzFileSystem fileSystem) : base(fileSystem)
        {
        }
        
        public override IDictionary<int, ItemData> LoadAll()
        {
            var properties = FileSystem.GetPropertiesInDirectory("Item")
                .Where(property => int.TryParse(property.Name.Replace(".img", ""), out var id) && id < 500)
                .SelectMany(property => property.PropertyChildren);
            
            return IterateAllToDict(properties, property =>
            {
                var infoNode = property.GetProperty("info");
                var specNode = property.GetProperty("spec");

                var itemData = new ItemData
                {
                    // Info
                    ID = (int) Utils.ConvertNameToID(property.Name),
                    Type = infoNode?.GetInt8("type") ?? 0,
                    MaxSlot = infoNode?.GetUInt16("slotMax") ?? 100,
                    Mesos = infoNode?.GetInt32("meso") ?? 0,
                    Price = infoNode?.GetInt32("price") ?? 0,
                    IncStr = infoNode?.GetUInt8("incSTR") ?? 0,
                    IncDex = infoNode?.GetUInt8("incDEX") ?? 0,
                    IncInt = infoNode?.GetUInt8("incINT") ?? 0,
                    IncLuk = infoNode?.GetUInt8("incLUK") ?? 0,
                    IncWDef = infoNode?.GetUInt8("incPDD") ?? 0,
                    IncMDef = infoNode?.GetUInt8("incMDD") ?? 0,
                    IncWAtk = infoNode?.GetUInt8("incPAD") ?? infoNode?.GetUInt8("pad") ?? 0,
                    IncMAtk = infoNode?.GetUInt8("incMAD") ?? 0,
                    IncSpeed = infoNode?.GetUInt8("incSpeed") ?? 0,
                    IncJump = infoNode?.GetUInt8("incJump") ?? 0,
                    IncAcc = infoNode?.GetUInt8("incACC") ?? 0,
                    IncAvo = infoNode?.GetUInt8("incEVA") ?? 0,
                    IncMHP = infoNode?.GetUInt8("incMHP") ?? 0,
                    IncMMP = infoNode?.GetUInt8("incMMP") ?? 0,
                    Rate = infoNode?.GetUInt8("rate") ?? 0,
                    Cash = infoNode?.GetBool("cash") ?? false,
                    TimeLimited = infoNode?.GetBool("timeLimited") ?? false,
                    IsQuest = infoNode?.GetBool("quest") ?? false,
                    Only = infoNode?.GetBool("only") ?? false,
                    PickupBlocked = infoNode?.GetBool("pickUpBlock") ?? false,
                    ScrollSuccessRate = infoNode?.GetUInt8("success") ?? 0,
                    ScrollCurseRate = infoNode?.GetUInt8("cursed") ?? 0,
                    PetLife = infoNode?.GetInt32("life") ?? 0,
                    HideRewardInfo = infoNode?.GetBool("hideRewardInfo") ?? false,

                    // Spec
                    MoveTo = specNode?.GetInt32("moveTo") ?? 0,
                    HP = specNode?.GetInt16("hp") ?? 0,
                    MP = specNode?.GetInt16("mp") ?? 0,
                    HPRate = specNode?.GetInt16("hpR") ?? 0,
                    MPRate = specNode?.GetInt16("mpR") ?? 0,
                    Speed = specNode?.GetInt16("speed") ?? 0,
                    Jump = specNode?.GetInt16("jump") ?? 0,
                    Avoidance = specNode?.GetInt16("eva") ?? 0,
                    Accuracy = specNode?.GetInt16("acc") ?? 0,
                    MagicAttack = specNode?.GetInt16("mad") ?? 0,
                    WeaponAttack = specNode?.GetInt16("pad") ?? 0,
                    WeaponDefense = specNode?.GetInt16("pdd") ?? 0,
                    Thaw = specNode?.GetInt16("thaw") ?? 0,
                    BuffTime = specNode?.GetInt32("time") ?? 0,

                    // Debuffs
                    Curse = specNode?.GetInt16("curse_") ?? 0,
                    Darkness = specNode?.GetInt16("darkness_") ?? 0,
                    Poison = specNode?.GetInt16("poison_") ?? 0,
                    Seal = specNode?.GetInt16("seal_") ?? 0,
                    Weakness = specNode?.GetInt16("weakness_") ?? 0,
                    Stun = specNode?.GetInt16("stun_") ?? 0,
                };
                
                ItemData.CureFlags flag = 0;
                flag |= specNode?.HasChild("curse") ?? false ? ItemData.CureFlags.Curse : 0;
                flag |= specNode?.HasChild("darkness") ?? false ? ItemData.CureFlags.Darkness : 0;
                flag |= specNode?.HasChild("poison") ?? false ? ItemData.CureFlags.Poison : 0;
                flag |= specNode?.HasChild("seal") ?? false ? ItemData.CureFlags.Seal : 0;
                flag |= specNode?.HasChild("weakness") ?? false ? ItemData.CureFlags.Weakness : 0;
                flag |= specNode?.HasChild("stun") ?? false ? ItemData.CureFlags.Stun : 0;

                itemData.Cures = flag;

                if (infoNode.HasChild("time"))
                {
                    itemData.RateTimes = new Dictionary<byte, List<KeyValuePair<byte, byte>>>();
                    foreach (var timeNode in infoNode.GetProperty("time").Children.OfType<string>())
                    {
                        var val = timeNode;
                        var day = val.Substring(0, 3);
                        var hourStart = byte.Parse(val.Substring(4, 2));
                        var hourEnd = byte.Parse(val.Substring(7, 2));
                        byte dayid = 0;

                        switch (day)
                        {
                            case "MON": dayid = 0; break;
                            case "TUE": dayid = 1; break;
                            case "WED": dayid = 2; break;
                            case "THU": dayid = 3; break;
                            case "FRI": dayid = 4; break;
                            case "SAT": dayid = 5; break;
                            case "SUN": dayid = 6; break;
                            case "HOL": dayid = ItemData.HOLIDAY_DAY; break;
                        }
                        if (!itemData.RateTimes.ContainsKey(dayid))
                            itemData.RateTimes.Add(dayid, new List<KeyValuePair<byte, byte>>());

                        itemData.RateTimes[dayid].Add(new KeyValuePair<byte, byte>(hourStart, hourEnd));
                    }
                }

                if (infoNode.HasChild("questLimited"))
                {
                    itemData.IsQuest = true;
                    itemData.QuestLimited = new QuestLimited(infoNode.GetProperty("questLimited"));
                }

                if (infoNode.HasChild("mob"))
                {
                    var mobs = new List<int>();
                    switch (infoNode["mob"])
                    {
                        case int mobId: mobs.Add(mobId); break;
                        case WzProperty mobIds:
                            mobs.AddRange(mobIds.Select(x => (int)x.Value));
                            break;
                    }

                    itemData.Mobs = mobs.ToArray();
                }

                switch (Constants.getItemType(itemData.ID))
                {
                    case Constants.Items.Types.ItemTypes.ItemSummonBag:

                        itemData.Summons = property.GetProperty("mob").PropertyChildren.Select(wzProperty =>
                            new ItemSummonInfo
                            {
                                MobID = wzProperty.GetInt32("id") ?? (int.TryParse(wzProperty.GetString("id"), out var summonId) ? summonId : 0),
                                Chance = wzProperty.GetUInt8("prob") ?? 0
                            }).ToList();
                        break;

                    case Constants.Items.Types.ItemTypes.ItemPetFood:
                        itemData.Pets = new List<int>();
                        for (var i = 0;; i++)
                        {
                            var petId = specNode.GetInt32(""+i);
                            if (petId == null) break;
                            itemData.Pets.Add(petId.Value);
                        }

                        itemData.PetFullness = specNode.GetInt32("inc") ?? 0;
                        break;
                }

                return itemData;
            }, x => x.ID);
        }

    }
}