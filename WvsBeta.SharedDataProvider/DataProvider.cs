using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using log4net;
using WvsBeta.Common;
using WvsBeta.SharedDataProvider.Providers;
using WvsBeta.SharedDataProvider.Templates;
using WzTools.FileSystem;

namespace WvsBeta.Game
{
    public class DataProvider
    {
        private static ILog _log = LogManager.GetLogger(typeof(DataProvider));

        public static IDictionary<int, NPCData> NPCs { get; private set; }
        public static IDictionary<int, MobData> Mobs { get; private set; }
        public static IDictionary<int, SkillData> Skills { get; private set; }
        public static IDictionary<int, Dictionary<byte, MobSkillLevelData>> MobSkills { get; private set; }
        public static IDictionary<int, DropData[]> Drops { get; private set; }
        public static IDictionary<byte, List<QuizData>> QuizQuestions { get; private set; }
        public static IDictionary<int, EquipData> Equips { get; private set; }
        public static IDictionary<int, ItemData> Items { get; private set; }
        public static IDictionary<int, PetData> Pets { get; private set; }
        public static IDictionary<string, ReactorData> Reactors { get; private set; }
        public static Dictionary<int, string> Jobs { get; private set; }
        public static DropData[] GlobalDrops { get; private set; }

        public static List<int> UntradeableDrops { get; } = new List<int>();
        public static List<int> QuestItems { get; } = new List<int>();

        [Flags]
        public enum LoadCategories
        {
            Jobs = 0x01,
            Equips = 0x02,
            Items = 0x04,
            Pets = 0x08,
            NPCs = 0x10,
            Mobs = 0x20,
            Skills = 0x40,
            MobSkills = 0x80,
            Drops = 0x100,
            Quiz = 0x200,
            Reactors = 0x400,
            Strings = 0x800,
            All = 0x7FFFFFFF
        }


        public static void Load(LoadCategories lc = LoadCategories.All)
        {
            var fileSystem = new WzFileSystem();
            fileSystem.Init(Path.Combine(Environment.CurrentDirectory, "..", "DataSvr"));
            _log.Info("Loading DataProvider");

            if (lc.HasFlag(LoadCategories.Jobs))
            {
                Jobs = fileSystem.GetPropertiesInDirectory("Skill")
                    .Where(property => property.Name != "MobSkill.img")
                    .Select(p => int.Parse(p.Name.Replace(".img", ""))).ToDictionary(x => x, x => "IAMERROR");

                Jobs[0] = "Beginner";
                Jobs[Constants.Swordman.ID] = "Swordman";
                Jobs[Constants.Fighter.ID] = "Fighter";
                Jobs[Constants.Crusader.ID] = "Crusader";
                Jobs[Constants.Page.ID] = "Page";
                Jobs[Constants.WhiteKnight.ID] = "Knight"; // White Knight
                Jobs[Constants.Spearman.ID] = "Spearman";
                Jobs[Constants.DragonKnight.ID] = "Dragon Knight";
                
                Jobs[Constants.Magician.ID] = "Wizard";
                Jobs[Constants.FPWizard.ID] = "F/P Wizard";
                Jobs[Constants.FPMage.ID] = "F/P Mage";
                Jobs[Constants.ILWizard.ID] = "I/L Wizard";
                Jobs[Constants.ILMage.ID] = "I/L Mage";
                Jobs[Constants.Cleric.ID] = "Cleric";
                Jobs[Constants.Priest.ID] = "Priest";
                
                Jobs[Constants.Archer.ID] = "Archer";
                Jobs[Constants.Hunter.ID] = "Hunter";
                Jobs[Constants.Ranger.ID] = "Ranger";
                Jobs[Constants.Crossbowman.ID] = "Crossbow Man";
                Jobs[Constants.Sniper.ID] = "Sniper";
                
                Jobs[Constants.Rogue.ID] = "Rogue";
                Jobs[Constants.Assassin.ID] = "Assassin";
                Jobs[Constants.Hermit.ID] = "Hermit";
                Jobs[Constants.Bandit.ID] = "Bandit";
                Jobs[Constants.ChiefBandit.ID] = "Chief Bandit";
                
                Jobs[Constants.Gm.ID] = "GM";

                _log.Info($"Jobs: {Jobs.Count}");
            }

            if (lc.HasFlag(LoadCategories.Equips))
            {
                Equips = new EquipProvider(fileSystem).LoadAll();
                _log.Info($"Equips: {Equips.Count}");

                UntradeableDrops.AddRange(Equips.Values.Where(eq => eq.Only || eq.Quest).Select(eq => eq.ID));
                QuestItems.AddRange(Equips.Values.Where(eq => eq.Quest).Select(eq => eq.ID));
            }

            if (lc.HasFlag(LoadCategories.Items))
            {
                Items = new ItemProvider(fileSystem).LoadAll();
                _log.Info($"Items: {Items.Count}");

                UntradeableDrops.AddRange(Items.Values.Where(item => item.Only || item.IsQuest).Select(item => item.ID));
                QuestItems.AddRange(Items.Values.Where(item => item.IsQuest).Select(item => item.ID));
            }

            if (lc.HasFlag(LoadCategories.Pets))
            {
                Pets = new PetProvider(fileSystem).LoadAll();
                _log.Info($"Pets: {Pets.Count}");
            }

            if (lc.HasFlag(LoadCategories.NPCs))
            {
                NPCs = new NPCProvider(fileSystem).LoadAll();
                _log.Info($"NPCs: {NPCs.Count}");
            }

            if (lc.HasFlag(LoadCategories.Skills))
            {
                Skills = new SkillProvider(fileSystem).LoadAll();
                _log.Info($"Skill: {Skills.Count}");
            }

            if (lc.HasFlag(LoadCategories.MobSkills))
            {
                MobSkills = new MobSkillProvider(fileSystem).LoadAll();
                _log.Info($"MobSkll: {MobSkills.Sum(x => x.Value.Count)}");
            }

            if (lc.HasFlag(LoadCategories.Reactors))
            {
                Reactors = new ReactorProvider(fileSystem).LoadAll();
                _log.Info($"Reactors: {Reactors.Count}");
            }

            if (lc.HasFlag(LoadCategories.Drops))
            {
                var dropProvider = new DropProvider(fileSystem);
                Drops = dropProvider.LoadAll();
                GlobalDrops = dropProvider.LoadGlobalDrops();

                _log.Info($"Drop: {Drops.Sum(x => x.Value.Length)} at {Drops.Count} mobs");
                _log.Info($"GlobalDrops: {GlobalDrops.Length}");


                if (lc.HasFlag(LoadCategories.Reactors))
                {
                    var reactorDrops = dropProvider.LoadReactorDrops();
                    var used = 0;
                    foreach (var kvp in reactorDrops)
                    {
                        if (Reactors.TryGetValue(kvp.Key, out var rd))
                        {
                            rd.RewardInfo = kvp.Value;
                            used++;
                        }
                    }

                    _log.Info($"ReactorDrops: used {used} of {reactorDrops.Count} reactors' dropdata");
                }
            }

            if (lc.HasFlag(LoadCategories.Mobs))
            {
                Mobs = new MobProvider(fileSystem).LoadAll();

                _log.Info($"Mobs: {Mobs.Count}");
            }

            if (lc.HasFlag(LoadCategories.Quiz))
            {
                QuizQuestions = new QuizProvider(fileSystem).LoadAll();
                _log.Info($"Quiz: {QuizQuestions.Sum(x => x.Value.Count)}");
            }


            if (lc.HasFlag(LoadCategories.Strings))
            {
                if (Items.Count > 0)
                {
                    var conProp = fileSystem.GetProperty("String/Item.img/Con");
                    var insProp = fileSystem.GetProperty("String/Item.img/Ins");
                    var etcProp = fileSystem.GetProperty("String/Item.img/Etc");

                    foreach (var (id, itemData) in Items)
                    {
                        var prop = Constants.getInventory(id) switch
                        {
                            2 => conProp,
                            3 => insProp,
                            4 => etcProp,
                            _ => throw new Exception($"Unexpected item in items: {id}"),
                        };

                        prop = prop.GetProperty(id.ToString());

                        itemData.Name = prop?.GetString("name");
                        if (itemData.Name == null)
                        {
                            _log.Error($"Couldn't find name for item {id}");
                        }
                    }
                }

                if (Equips.Count > 0)
                {
                    var eqpProp = fileSystem.GetProperty("String/Item.img/Eqp");

                    foreach (var categoryNode in eqpProp.PropertyChildren)
                    {
                        foreach (var itemNode in categoryNode.PropertyChildren)
                        {
                            if (!int.TryParse(itemNode.Name, out var id)) continue;
                            if (!Equips.TryGetValue(id, out var data)) continue;

                            if (data.Name != null)
                            {
                                _log.Error($"Duplicate equip name for item {id}: {data.Name} previously found.");
                            }

                            data.Name = itemNode.GetString("name");
                            if (data.Name == null)
                            {
                                _log.Error($"Couldn't find name for equip {id}");
                            }
                        }
                    }
                }

                if (Mobs.Count > 0)
                {
                    var mobProp = fileSystem.GetProperty("String/Mob.img");

                    foreach (var (id, data) in Mobs)
                    {
                        var prop = mobProp.GetProperty(id.ToString());

                        data.Name = prop?.GetString("name");
                        if (data.Name == null)
                        {
                            _log.Error($"Couldn't find name for mob {id}");
                        }
                    }
                }

                if (NPCs.Count > 0)
                {
                    var npcProp = fileSystem.GetProperty("String/Npc.img");

                    foreach (var (id, data) in NPCs)
                    {
                        var prop = npcProp.GetProperty(id.ToString());

                        data.Name = prop?.GetString("name");
                        if (data.Name == null)
                        {
                            _log.Error($"Couldn't find name for NPC {id}");
                        }
                    }
                }

            }

            // Data validation

            if (lc.HasFlag(LoadCategories.Drops))
            {
                foreach (var (mobID, drops) in Drops.ToList())
                {
                    if (lc.HasFlag(LoadCategories.Mobs) && !Mobs.ContainsKey(mobID))
                    {
                        // Trace.WriteLine($"Removing nonexistant {mobId} mob from drops");
                        Drops.Remove(mobID);
                        continue;
                    }

                    if (lc.HasFlag(LoadCategories.Items) || lc.HasFlag(LoadCategories.Equips))
                    {
                        Drops[mobID] = drops.Where(x =>
                        {
                            var itemId = x.ItemID;
                            if (itemId == 0) return true;

                            if (HasItem(itemId)) return true;
                            
                            _log.Warn($"Removing item {itemId} from droptable of {mobID} of because the item doesn't exist.");
                            return false;
                        }).ToArray();
                    }
                }

            }

            if (lc.HasFlag(LoadCategories.Mobs))
            {
                foreach (var mob in Mobs.Values)
                {
                    if (lc.HasFlag(LoadCategories.Drops) && !Drops.ContainsKey(mob.ID))
                    {
                        Trace.WriteLine($"Mob {mob} does not have drops!");
                    }

                    if (lc.HasFlag(LoadCategories.MobSkills) && mob.Skills != null)
                    {
                        foreach (var mobSkillData in mob.Skills)
                        {
                            if (MobSkills.TryGetValue(mobSkillData.SkillID, out var msld) &&
                                msld.ContainsKey(mobSkillData.Level))
                                continue;

                            _log.Error($"Mob {mob} has skill {mobSkillData.SkillID} ({(Constants.MobSkills.Skills)mobSkillData.SkillID}) with level {mobSkillData.Level}, but it does not exist!");
                        }
                    }
                }
            }

            if (lc.HasFlag(LoadCategories.NPCs))
            {
                foreach (var npc in NPCs.Values)
                {
                    if (npc.Shop != null)
                    {
                        npc.Shop = npc.Shop.Select(x =>
                        {
                            if (!HasItem(x.ID))
                            {
                                _log.Error($"Unknown item {x.ID} in shop of NPC {npc}");
                                return null;
                            }
                            return x;
                        }).Where(x => x != null).ToList();
                    }
                }
            }

            if (lc.HasFlag(LoadCategories.Items) && lc.HasFlag(LoadCategories.Strings))
            {
                foreach (var item in Items.Values)
                {
                    if (item.Name != null) continue;

                    _log.Error($"Found item with id {item.ID} that is missing a String/name property!");
                    item.Name = $"Missing String prop {item.ID}";
                }
            }
            if (lc.HasFlag(LoadCategories.Equips) && lc.HasFlag(LoadCategories.Strings))
            {
                foreach (var item in Equips.Values)
                {
                    if (item.Name != null) continue;

                    _log.Error($"Found equip with id {item.ID} that is missing a String/name property!");
                    item.Name = $"Missing String prop {item.ID}";
                }
            }

            _log.Info("Finished loading DataProvider");
        }

        public static bool HasJob(short JobID) => Jobs.ContainsKey(JobID);

        public static bool HasItem(int itemid)
        {
            var inventory = Constants.getInventory(itemid);

            if (inventory == 1) return Equips == null || Equips.ContainsKey(itemid);
            if (inventory == 5) return Pets == null || Pets.ContainsKey(itemid);

            return Items == null || Items.ContainsKey(itemid);
        }

        public static bool IsOnlyItem(int itemID)
        {
            var inventory = Constants.getInventory(itemID);

            if (inventory == 1 && Equips.TryGetValue(itemID, out var equipData))
            {
                return equipData.Only;
            }

            if (Items.TryGetValue(itemID, out var itemData))
            {
                return itemData.Only;
            }

            return false;
        }

        public static bool IsPickupBlocked(int itemID)
        {
            if (Items.TryGetValue(itemID, out var itemData))
            {
                return itemData.PickupBlocked;
            }

            return false;
        }
    }
}