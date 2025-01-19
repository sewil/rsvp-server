using System.Collections.Generic;

namespace WvsBeta.SharedDataProvider.Templates
{
    public class SkillData
    {
        public int ID { get; set; }
        public Dictionary<int, byte> RequiredSkills { get; set; }
        public byte Type { get; set; }
        public SkillLevelData[] Levels { get; set; }
        public SkillElement Element { get; set; }
        public byte Weapon { get; set; }
        public byte MaxLevel { get; set; }
    }
}

