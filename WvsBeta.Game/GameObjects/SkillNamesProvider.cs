using System;
using System.Collections.Generic;
using System.IO;
using WvsBeta.Common;
using WvsBeta.SharedDataProvider.Providers;
using WzTools.FileSystem;

namespace WvsBeta.Game.GameObjects
{
    class SkillNamesProvider : TemplateProvider<string>
    {
        public static IDictionary<int, string> SkillNames { get; private set; }
        public static void Load()
        {
            var fileSystem = new WzFileSystem();
            fileSystem.Init(Path.Combine(Environment.CurrentDirectory, "..", "DataSvr"));

            SkillNames = new SkillNamesProvider(fileSystem).LoadAll();
            _log.Info($"SkillNames: {SkillNames.Count}");
        }

        public SkillNamesProvider(WzFileSystem fileSystem) : base(fileSystem)
        {
        }

        public override IDictionary<int, string> LoadAll()
        {
            var skillInfoProp = FileSystem.GetProperty("String/Skill.img");

            var skillNames = new Dictionary<int, string>();

            foreach (var kvp in skillInfoProp.PropertyChildren)
            {
                var name = kvp.GetString("name");
                if (name == null) continue;
                skillNames[(int)Utils.ConvertNameToID(kvp.Name)] = name;
            }

            return skillNames;
        }
    }
}
