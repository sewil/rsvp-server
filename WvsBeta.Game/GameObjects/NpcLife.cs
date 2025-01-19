using System.Collections.Generic;
using WvsBeta.SharedDataProvider.Templates;

namespace WvsBeta.Game
{
    public class NpcLife : LifeWrapper, IFieldObj
    {
        public Map Field { get; }
        public uint SpawnID { get; set; }
        public bool IsSpawned { get; set; }

        public NpcLife(Life life, Map field) : base(life)
        {
            Field = field;
        }

        public bool IsShownTo(IFieldObj Object) => IsSpawned;

        public Dictionary<string, string> Vars { get; set; }

        public void Spawn()
        {
            IsSpawned = true;
            NpcPacket.SendMakeEnterFieldPacket(this, null);
        }

        public void Despawn()
        {
            NpcPacket.SendMakeLeaveFieldPacket(this);
            IsSpawned = false;
        }
    }
}