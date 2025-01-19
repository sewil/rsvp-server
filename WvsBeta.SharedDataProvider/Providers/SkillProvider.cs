using System;
using System.Collections.Generic;
using System.Linq;
using WvsBeta.Common;
using WvsBeta.SharedDataProvider.Templates;
using WzTools.FileSystem;
using WzTools.Objects;

namespace WvsBeta.SharedDataProvider.Providers
{
    public class SkillProvider : TemplateProvider<SkillData>
    {
        public SkillProvider(WzFileSystem fileSystem) : base(fileSystem)
        {
        }

        public override IDictionary<int, SkillData> LoadAll()
        {
            var allJobs = FileSystem.GetPropertiesInDirectory("Skill")
                .Where(property => property.Name != "MobSkill.img");

            return IterateAllToDict(allJobs.SelectMany(x => x.GetProperty("skill").PropertyChildren), property =>
            {
                int skillId = int.Parse(property.Name);
                var skillData = new SkillData
                {
                    ID = skillId
                };

                var elementFlags = SkillElement.Normal;

                if (property.HasChild("elemAttr"))
                {
                    string elemChar = property.GetString("elemAttr");
                    switch (elemChar.ToLowerInvariant())
                    {
                        case "i":
                            elementFlags = SkillElement.Ice;
                            break;
                        case "f":
                            elementFlags = SkillElement.Fire;
                            break;
                        case "s":
                            elementFlags = SkillElement.Poison;
                            break;
                        case "l":
                            elementFlags = SkillElement.Lightning;
                            break;
                        case "h":
                            elementFlags = SkillElement.Holy;
                            break;
                        default:
                            Console.WriteLine($"Unhandled elemAttr type {elemChar} for id {skillId}");
                            break;
                    }
                }

                skillData.Element = elementFlags;
                skillData.Type = property.GetUInt8("skillType") ?? 0;
                skillData.Weapon = property.GetUInt8("weapon") ?? 0;

                if (property.HasChild("req"))
                {
                    skillData.RequiredSkills = new Dictionary<int, byte>();
                    foreach (var reqNode in property.GetProperty("req"))
                    {
                        skillData.RequiredSkills[int.Parse(reqNode.Key)] = Convert.ToByte(reqNode.Value);
                    }
                }

                skillData.Levels = new SkillLevelData[property.GetProperty("level").Children.Count + 1];
                foreach (var skillLevelProperty in property.GetProperty("level").PropertyChildren)
                {
                    var sld = new SkillLevelData
                    {
                        SkillID = skillId,
                        XValue = skillLevelProperty.GetInt16("x") ?? 0,
                        YValue = skillLevelProperty.GetInt16("y") ?? 0,
                        ZValue = skillLevelProperty.GetInt16("z") ?? 0,
                        HitCount = skillLevelProperty.GetUInt8("attackCount") ?? 0,
                        MobCount = skillLevelProperty.GetUInt8("mobCount") ?? 0,
                        BuffTime = skillLevelProperty.GetInt32("time") ?? 0,
                        Damage = skillLevelProperty.GetInt16("damage") ?? 0,
                        AttackRange = skillLevelProperty.GetInt16("range") ?? 0,
                        Mastery = skillLevelProperty.GetUInt8("mastery") ?? 0,
                        HPProperty = skillLevelProperty.GetInt16("hp") ?? 0,
                        MPProperty = skillLevelProperty.GetInt16("mp") ?? 0,
                        Property = skillLevelProperty.GetInt16("prop") ?? 0,
                        HPUsage = skillLevelProperty.GetInt16("hpCon") ?? 0,
                        MPUsage = skillLevelProperty.GetInt16("mpCon") ?? 0,
                        ItemIDUsage = skillLevelProperty.GetInt32("itemCon") ?? 0,
                        ItemAmountUsage = skillLevelProperty.GetInt32("itemConNo") ?? 0,
                        BulletUsage = skillLevelProperty.GetInt16("bulletCount") ??
                                      skillLevelProperty.GetInt16("bulletConsume") ?? 0,
                        MesosUsage = skillLevelProperty.GetInt16("moneyCon") ?? 0,
                        Speed = skillLevelProperty.GetInt16("speed") ?? 0,
                        Jump = skillLevelProperty.GetInt16("jump") ?? 0,
                        Avoidability = skillLevelProperty.GetInt16("eva") ?? 0,
                        Accurancy = skillLevelProperty.GetInt16("acc") ?? 0,
                        MagicAttack = skillLevelProperty.GetInt16("mad") ?? 0,
                        MagicDefense = skillLevelProperty.GetInt16("mdd") ?? 0,
                        WeaponAttack = skillLevelProperty.GetInt16("pad") ?? 0,
                        WeaponDefense = skillLevelProperty.GetInt16("pdd") ?? 0,
                        LTX = (short) (skillLevelProperty.Get<WzVector2D>("lt")?.X ?? 0),
                        LTY = (short) (skillLevelProperty.Get<WzVector2D>("lt")?.Y ?? 0),
                        RBX = (short) (skillLevelProperty.Get<WzVector2D>("rb")?.X ?? 0),
                        RBY = (short) (skillLevelProperty.Get<WzVector2D>("rb")?.Y ?? 0),
                        ElementFlags = skillData.Element,
                    };


                    if (skillId == Constants.Gm.Skills.Hide)
                    {
                        // Give hide some time... like lots of hours
                        sld.BuffTime = 24 * 60 * 60;
                        sld.XValue = 1; // Eh. Otherwise there's no buff
                    }

                    skillData.Levels[byte.Parse(skillLevelProperty.Name)] = sld;
                }

                skillData.MaxLevel = (byte) (skillData.Levels.Length - 1);

                return skillData;
            }, x => x.ID);
        }
    }
}