using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using log4net;
using WvsBeta.Common;
using WvsBeta.SharedDataProvider.Providers;
using WvsBeta.SharedDataProvider.Templates;
using WzTools.FileSystem;
using WzTools.Objects;

namespace WvsBeta.Game.GameObjects
{
    public class QuestDemand
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public int[] Mobs { get; set; }
    }

    public class QuestsProvider : TemplateProvider<QuestDemand>
    {
        private new static ILog _log = LogManager.GetLogger(typeof(MapProvider));

        public static IDictionary<int, QuestDemand> QuestDemands { get; private set; }
        public static IDictionary<int, List<QuestDemand>> MobsToQuestDemands { get; private set; }

        public static void Load()
        {
            var fileSystem = new WzFileSystem();
            fileSystem.Init(Path.Combine(Environment.CurrentDirectory, "..", "DataSvr"));

            QuestDemands = new QuestsProvider(fileSystem).LoadAll();
            _log.Info($"QuestDemands: {QuestDemands.Count}");
        }

        public QuestsProvider(WzFileSystem fileSystem) : base(fileSystem)
        {
        }

        public override IDictionary<int, QuestDemand> LoadAll()
        {
            var questInfoProp = FileSystem.GetProperty("Etc/QuestInfo.img");
            var questDemandProp = FileSystem.GetProperty("Server/QuestDemand.img");

            var killQuests = new Dictionary<int, string>();

            foreach (var kvp in questInfoProp.PropertyChildren)
            {
                if (kvp.HasChild("000") ||
                    kvp.HasChild("000000") ||
                    kvp.HasChild("000000000"))
                {
                    var info = kvp.GetProperty("info");
                    killQuests[(int) Utils.ConvertNameToID(kvp.Name)] = info?.GetString("subject") ?? "???";
                }
            }


            var ret = IterateAllToDict(questDemandProp.PropertyChildren, property =>
            {
                var quest = new QuestDemand();
                quest.ID = (int) Utils.ConvertNameToID(property.Name);


                quest.Name = killQuests.GetValueOrDefault(quest.ID) ?? "???";

                // Current implementation only covers mobs...
                quest.Mobs = property.Children.Select(x => (int) Utils.ConvertNameToID(x.ToString())).ToArray();

                return new Tuple<int, QuestDemand>(quest.ID, quest);
            }, x => x.Item1, x => x.Item2);

            foreach (var questId in ret.Keys)
            {
                if (killQuests.TryGetValue(questId, out var questName))
                {
                    _log.Info($"Initialized killquest {questId} ({questName})");
                }

                killQuests.Remove(questId);
            }

            foreach (var kvp in killQuests)
            {
                _log.Warn($"Did not initialize killquest {kvp.Key} ({kvp.Value})");
            }

            return ret;
        }


        public static void FinishLoading()
        {
            MobsToQuestDemands = new Dictionary<int, List<QuestDemand>>();

            foreach (var quest in QuestDemands.Values)
            {
                foreach (var mobId in quest.Mobs)
                {
                    if (!DataProvider.Mobs.ContainsKey(mobId))
                    {
                        _log.Error($"QuestDemand {quest.ID} has mob {mobId} that does not exist in data!");
                        continue;
                    }

                    if (!MobsToQuestDemands.TryGetValue(mobId, out var list))
                    {
                        list = new List<QuestDemand>();
                        MobsToQuestDemands[mobId] = list;
                    }

                    list.Add(quest);
                }
            }
        }
    }
}