using System;
using System.Collections.Generic;
using System.IO;
using WvsBeta.Common;
using WvsBeta.SharedDataProvider.Providers;
using WzTools.FileSystem;

namespace WvsBeta.Game.GameObjects
{
    class QuestNamesProvider : TemplateProvider<string>
    {
        public static IDictionary<int, string> QuestNames { get; private set; }

        public static void Load()
        {
            var fileSystem = new WzFileSystem();
            fileSystem.Init(Path.Combine(Environment.CurrentDirectory, "..", "DataSvr"));

            QuestNames = new QuestNamesProvider(fileSystem).LoadAll();
            _log.Info($"QuestNames: {QuestNames.Count}");
        }

        public QuestNamesProvider(WzFileSystem fileSystem) : base(fileSystem)
        {
        }

        public override IDictionary<int, string> LoadAll()
        {
            var questInfoProp = FileSystem.GetProperty("Etc/QuestInfo.img");
            var questNames = new Dictionary<int, string>();

            foreach (var kvp in questInfoProp.PropertyChildren)
            {
                var info = kvp.GetProperty("info");
                questNames[(int)Utils.ConvertNameToID(kvp.Name)] = info?.GetString("subject") ?? "???";
            }

            return questNames;
        }
    }
}
