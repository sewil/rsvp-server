using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using WvsBeta.Common;
using WvsBeta.Game;
using WzTools.FileSystem;
using WzTools.Objects;

namespace WvsBeta.SharedDataProvider.Providers
{
    public class EquipProvider : TemplateProvider<EquipData>
    {
        public EquipProvider(WzFileSystem fileSystem) : base(fileSystem)
        {
        }
        
        public override IDictionary<int, EquipData> LoadAll()
        {
            var properties = FileSystem.GetPropertiesInDirectory("Character").ToImmutableArray();
            
            return IterateAllToDict(properties, property =>
            {
                if (!int.TryParse(property.Name.Replace(".img", ""), out var id) || id < 1000000) return null;
                if (!property.HasChild("info")) return null;

                var infoNode = property.GetProperty("info");

                void fillEquipStanceInfo(EquipStanceInfo esi, WzProperty prop)
                {
                    esi.AnimationFrames = prop.Keys.Where(x => x != "info" && !int.TryParse(x, out _)).ToArray();
                }

                var equipData = new EquipData
                {
                    ID = id,
                    Slots = infoNode.GetUInt8("tuc") ?? 0,
                    RequiredLevel = infoNode.GetUInt8("reqLevel") ?? 0,
                    RequiredFame = infoNode.GetUInt16("reqPOP") ?? 0,
                    RequiredDexterity = infoNode.GetUInt16("reqDEX") ?? 0,
                    RequiredIntellect = infoNode.GetUInt16("reqINT") ?? 0,
                    RequiredLuck = infoNode.GetUInt16("reqLUK") ?? 0,
                    RequiredStrength = infoNode.GetUInt16("reqSTR") ?? 0,
                    Price = infoNode.GetInt32("price") ?? 0,
                    Strength = infoNode.GetInt16("incSTR") ?? 0,
                    Dexterity = infoNode.GetInt16("incDEX") ?? 0,
                    Intellect = infoNode.GetInt16("incINT") ?? 0,
                    Luck = infoNode.GetInt16("incLUK") ?? 0,
                    MagicDefense = infoNode.GetInt16("incMDD") ?? 0,
                    WeaponDefense = infoNode.GetInt16("incPDD") ?? 0,
                    WeaponAttack = infoNode.GetInt16("incPAD") ?? 0,
                    MagicAttack = infoNode.GetInt16("incMAD") ?? 0,
                    Speed = infoNode.GetInt16("incSpeed") ?? 0,
                    Jump = infoNode.GetInt16("incJump") ?? 0,
                    Accuracy = infoNode.GetInt16("incACC") ?? 0,
                    Avoidance = infoNode.GetInt16("incEVA") ?? 0,
                    Craft = infoNode.GetInt16("incCraft") ?? 0,
                    HP = infoNode.GetInt16("incMHP") ?? 0,
                    MP = infoNode.GetInt16("incMMP") ?? 0,
                    Cash = infoNode.GetBool("cash") ?? false,
                    Attack = infoNode.GetUInt8("attack") ?? 0,
                    AttackSpeed = infoNode.GetUInt8("attackSpeed") ?? 0,
                    KnockbackRate = infoNode.GetUInt8("knockback") ?? 0,
                    TimeLimited = infoNode.GetBool("timeLimited") ?? false,
                    RecoveryRate = infoNode.GetFloat("recovery") ?? 0.0f,
                    Quest = infoNode.GetBool("quest") ?? false,
                    Only = infoNode.GetBool("only") ?? false,
                    HideRewardInfo = infoNode?.GetBool("hideRewardInfo") ?? false,
                    ItemVariation = (ItemVariation?)infoNode.GetInt32("itemVariation") ?? ItemVariation.Normal,
                };

                fillEquipStanceInfo(equipData, property);

                
                foreach (var cashCoverCategory in property.Keys.Where(x => byte.TryParse(x, out _)))
                {
                    var category = byte.Parse(cashCoverCategory);
                    var coverInfo = property.GetProperty(cashCoverCategory);
                    var dict = equipData.EquipStanceInfos ??= new Dictionary<byte, EquipStanceInfo>();
                    
                    var tmp = dict[category] = new EquipStanceInfo();
                    fillEquipStanceInfo(tmp, coverInfo);
                }

                if (infoNode.HasChild("reqJob"))
                {
                    var job = infoNode.GetInt16("reqJob") ?? 0;
                    equipData.RequiredJob = job;
                }

                if (infoNode.HasChild("questLimited"))
                {
                    equipData.Quest = true;
                    equipData.QuestLimited = new QuestLimited(infoNode.GetProperty("questLimited"));
                }

                switch (Constants.getItemType(equipData.ID))
                {
                    case Constants.Items.Types.ItemTypes.PetEquip:
                        equipData.Pets = new List<int>();

                        // The supported entries are under the main property
                        foreach (var name in property.Keys)
                        {
                            if (!int.TryParse(name, out var petId)) continue;
                            if (Constants.getItemType(petId) != Constants.Items.Types.ItemTypes.Pet) continue;
                            equipData.Pets.Add(petId);
                        }
                        break;
                }

                return equipData;
            }, x => x.ID);
        }

    }
}