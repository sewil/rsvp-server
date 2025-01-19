using System.Collections.Generic;

namespace WvsBeta.SharedDataProvider.Templates
{
    public class NPCData
    {
        public string Name { get; set; }
        public int ID { get; set; }
        public string Quest { get; set; }
        public int Trunk { get; set; }
        public short Speed { get; set; }
        public byte SpeakLineCount { get; set; }
        public List<ShopItemData> Shop { get; set; }

        public Dictionary<string, string> Reg { get; set; }

        public override string ToString()
        {
            return $"{ID} ({Name})";
        }
    }
}