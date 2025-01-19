namespace WvsBeta.SharedDataProvider.Templates
{
    public class Portal
    {
        public byte ID;
        public byte Type;
        public short X;
        public short Y; //todo: subtract 40 when loading from data or when used?
        public string Name;
        public int ToMapID;
        public string ToName;
        public string Script;
        public bool Enabled;
    }
}