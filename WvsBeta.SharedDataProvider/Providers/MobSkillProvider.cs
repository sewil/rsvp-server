using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WvsBeta.SharedDataProvider.Templates;
using WzTools.FileSystem;
using WzTools.Objects;

namespace WvsBeta.SharedDataProvider.Providers
{
    public class MobSkillProvider : TemplateProvider<Dictionary<byte, MobSkillLevelData>>
    {
        public MobSkillProvider(WzFileSystem fileSystem) : base(fileSystem)
        {
        }

        public override IDictionary<int, Dictionary<byte, MobSkillLevelData>> LoadAll()
        {
            return IterateAllToDict(FileSystem.GetProperty("Skill", "MobSkill.img").PropertyChildren, property =>
                {
                    var dict = new Dictionary<byte, MobSkillLevelData>();
                    var skillId = (byte) int.Parse(property.Name);

                    foreach (var mobSkillLevelProperty in property.GetProperty("level").PropertyChildren)
                    {
                        var level = (byte) int.Parse(mobSkillLevelProperty.Name);

                        var levelData = new MobSkillLevelData
                        {
                            SkillID = skillId,
                            Level = level,
                            Time = mobSkillLevelProperty.GetInt16("time") ?? 0,
                            MPConsume = mobSkillLevelProperty.GetInt16("mpCon") ?? 0,
                            X = mobSkillLevelProperty.GetInt32("x") ?? 0,
                            Y = mobSkillLevelProperty.GetInt32("y") ?? 0,
                            Prop = mobSkillLevelProperty.GetUInt8("prop") ?? 0,
                            Cooldown = mobSkillLevelProperty.GetInt16("interval") ?? 0,
                            HPLimit = mobSkillLevelProperty.GetUInt8("hp") ?? 0,
                            SummonLimit = mobSkillLevelProperty.GetUInt16("limit") ?? 0,
                            SummonEffect = mobSkillLevelProperty.GetUInt8("summonEffect") ?? 0,
                            LTX = (short) (mobSkillLevelProperty.Get<WzVector2D>("lt")?.X ?? 0),
                            LTY = (short) (mobSkillLevelProperty.Get<WzVector2D>("lt")?.Y ?? 0),
                            RBX = (short) (mobSkillLevelProperty.Get<WzVector2D>("rb")?.X ?? 0),
                            RBY = (short) (mobSkillLevelProperty.Get<WzVector2D>("rb")?.Y ?? 0),
                            Summons = mobSkillLevelProperty.Where(pair => pair.Key.All(char.IsDigit))
                                .Select(pair => pair.Value)
                                .OfType<int>()
                                .ToList(),
                        };


                        dict.Add(level, levelData);
                    }
                    
                    return new Tuple<int, Dictionary<byte, MobSkillLevelData>>(skillId, dict);
                }, x => x.Item1, x => x.Item2);
        }
    }
}