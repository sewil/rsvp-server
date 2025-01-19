using System.Collections.Generic;
using System.Linq;
using WvsBeta.Common;
using WvsBeta.SharedDataProvider.Templates;
using WzTools.FileSystem;
using WzTools.Objects;

namespace WvsBeta.SharedDataProvider.Providers
{
    public class MobProvider : TemplateProvider<MobData>
    {
        public MobProvider(WzFileSystem fileSystem) : base(fileSystem)
        {
        }
        
        public override IDictionary<int, MobData> LoadAll()
        {
            byte GetGroupIdx(string str) => (byte)(str[^1] - '1');
            
            return IterateAllToDict(FileSystem.GetPropertiesInDirectory("Mob"), property =>
            {
                var infoNode = property.GetProperty("info");
                
                var data = new MobData
                {
                    ID = (int) Utils.ConvertNameToID(property.Name),
                    Level = infoNode.GetUInt8("level") ?? 0,
                    Undead = infoNode.GetBool("undead") ?? false,
                    BodyAttack = infoNode.GetBool("bodyAttack") ?? false,
                    SummonType = infoNode.GetUInt8("summonType") ?? 0,
                    EXP = infoNode.GetInt32("exp") ?? 0,
                    MaxHP = infoNode.GetInt32("maxHP") ?? 0,
                    MaxMP = infoNode.GetInt32("maxMP") ?? 0,
                    PAD = infoNode.GetInt32("PADamage") ?? 0,
                    PDD = infoNode.GetInt32("PDDamage") ?? 0,
                    MAD = infoNode.GetInt32("MADamage") ?? 0,
                    MDD = infoNode.GetInt32("MDDamage") ?? 0,
                    Eva = infoNode.GetInt32("maxHP") ?? 0,
                    Pushed = infoNode.GetBool("pushed") ?? false,
                    NoRegen = infoNode.GetBool("noregen") ?? false,
                    Invincible = infoNode.GetBool("invincible") ?? false,
                    SelfDestructionHP = -1,
                    FirstAttack = infoNode.GetBool("firstAttack") ?? false,
                    Acc = infoNode.GetInt32("acc") ?? 0,
                    PublicReward = infoNode.GetBool("publicReward") ?? false,
                    ExplosiveReward = infoNode.GetBool("explosiveReward") ?? false,
                    NoGlobalReward = infoNode.GetBool("noGlobalReward") ?? false,
                    FS = infoNode.GetFloat("fs") ?? 0.0f,
                    Flies = infoNode.HasChild("flySpeed"),
                    Speed = infoNode.GetInt16("speed") ?? (infoNode.GetInt16("flySpeed") ?? 0),
                    Revive = infoNode.GetProperty("revive")?.Children.OfType<int>().ToList() ?? null,
                    HPRecoverAmount = infoNode.GetInt32("hpRecovery") ?? 0,
                    MPRecoverAmount = infoNode.GetInt32("mpRecovery") ?? 0,
                    HPTagColor = (uint)(infoNode.GetInt32("hpTagColor") ?? 0),
                    HPTagBgColor = (uint)(infoNode.GetInt32("hpTagBgcolor") ?? 0),
                    Boss = infoNode.GetBool("boss") ?? false,
                    Attacks = new Dictionary<byte, MobAttackData>(),
                    EliminationPoints = infoNode.GetInt32("points") ?? 0,
                };

                if (infoNode.HasChild("selfDestruction"))
                {
                    var selfDestruction = infoNode["selfDestruction"];
                    if (selfDestruction is WzProperty)
                    {
                        _log.Error($"Found new-style selfDestruction prop in mob {data.ID}!");
                    }
                    else
                    {
                        data.SelfDestructionHP = infoNode.GetInt32("selfDestruction").Value;
                    }
                }

                if (infoNode.HasChild("elemAttr"))
                {
                    data.elemAttr = infoNode.GetString("elemAttr");
                }

                if (infoNode.HasChild("skill"))
                {
                    data.Skills = infoNode.GetProperty("skill")
                            .PropertyChildren
                            .Select(skillProperty => new MobSkillData
                            {
                                SkillID = skillProperty.GetUInt8("skill") ?? 0,
                                Level = skillProperty.GetUInt8("level") ?? 0,
                                EffectAfter = skillProperty.GetInt16("effectAfter") ?? 0
                            }).ToList();
                }

                var nonInfoNodes = property;
                
                if (infoNode.HasChild("link"))
                {
                    var linkedMobId = int.Parse(infoNode.GetString("link"));
                    
                    nonInfoNodes = FileSystem.GetProperty($"Mob/{linkedMobId:D7}.img");
                }

                
                if (nonInfoNodes.HasChild("fly")) data.MoveAbility = MoveAbility.Fly;
                else if (nonInfoNodes.HasChild("jump")) data.MoveAbility = MoveAbility.Jump;
                else if (nonInfoNodes.HasChild("move")) data.MoveAbility = MoveAbility.Walk;
                else data.MoveAbility = MoveAbility.Stop;
                
                var attackNodes =
                    nonInfoNodes.PropertyChildren.Where(wzProperty => wzProperty.Name.StartsWith("attack"));
                
                foreach (var attackNode in attackNodes)
                {
                    var id = GetGroupIdx(attackNode.Name);
                    var attackInfoNode = attackNode.GetProperty("info");

                    var mad = new MobAttackData
                    {
                        ID = id,
                        Disease = attackInfoNode.GetUInt8("disease") ?? 0,
                        ElemAttr = attackInfoNode.HasChild("elemAttr") ? attackInfoNode.GetString("elemAttr")[0] : default,
                        MPConsume = attackInfoNode.GetInt16("conMP") ?? 0,
                        Magic = attackInfoNode.GetBool("magic") ?? false,
                        Type = attackInfoNode.GetUInt8("type") ?? 0,
                        PADamage = attackInfoNode.GetInt16("PADamage") ?? 0,
                        MPBurn = attackInfoNode.GetInt16("mpBurn") ?? 0,
                        SkillLevel = attackInfoNode.GetUInt8("level") ?? 0,
                        DeadlyAttack = attackInfoNode.GetBool("deadlyAttack") ?? false
                    };

                    if (attackInfoNode.HasChild("range"))
                    {
                        var rangeNode = attackInfoNode.GetProperty("range");
                        
                        if (rangeNode.HasChild("lt"))
                        {
                            var lt = rangeNode.Get<WzVector2D>("lt");
                            mad.RangeLTX = (short) lt.X;
                            mad.RangeLTY = (short) lt.Y;
                            var rb = rangeNode.Get<WzVector2D>("rb");
                            mad.RangeRBX = (short) rb.X;
                            mad.RangeRBY = (short) rb.Y;
                        }
                        else
                        {
                            mad.RangeR = rangeNode.GetInt16("r") ?? 0;
                            var sp = rangeNode.Get<WzVector2D>("sp");
                            mad.RangeSPX = (short) sp.X;
                            mad.RangeSPY = (short) sp.Y;
                        }
                    }

                    data.Attacks.Add(id, mad);
                }
                
                return data;
            }, x => x.ID);
        }

    }
}