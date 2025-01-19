using System.Collections.Generic;

namespace WvsBeta.Game.GameObjects
{
    public class SummonPool
    {
        private readonly Map Field;
        private readonly HashSet<Summon> Summons = new HashSet<Summon>();

        public SummonPool(Map field)
        {
            Field = field;
        }

        public void RegisterSummon(Summon summon)
        {
            Summons.Add(summon);
            Field.SendPacket(MapPacket.ShowSummon(summon, 1));
        }

        public void DeregisterSummon(Summon summon, byte removetype)
        {
            if (Summons.Remove(summon))
            {
                Field.SendPacket(MapPacket.RemoveSummon(summon, removetype));
            }
        }

        public void ShowAllSummonsTo(Character chr)
        {
            foreach (var summon in Summons)
                chr.SendPacket(MapPacket.ShowSummon(summon, 0));
        }
    }
}
