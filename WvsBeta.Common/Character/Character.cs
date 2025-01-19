using System;
using System.Threading;
using log4net;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Common
{
    public class CharacterBase : MovableLife
    {
        public string Name { get; set; }
        public int ID { get; set; }
        public short Job { get; set; }
        public byte Level { get; set; }

        public byte Gender { get; set; }
        public byte Skin { get; set; }
        public int Face { get; set; }
        public int Hair { get; set; }

        public virtual int MapID { get; set; }

        public virtual int PartyID { get; set; }

        public bool IsOnline { get; set; }

        public byte GMLevel { get; set; }
        public bool IsGM => GMLevel > 0;
        public bool IsAdmin => GMLevel >= 3;

        public void EncodeForTransfer(Packet pw)
        {
            pw.WriteString(Name);
            pw.WriteInt(ID);
            pw.WriteShort(Job);
            pw.WriteByte(Level);

            pw.WriteByte(Gender);
            pw.WriteByte(Skin);
            pw.WriteInt(Face);
            pw.WriteInt(Hair);

            pw.WriteInt(MapID);
            pw.WriteInt(PartyID);
            pw.WriteBool(IsOnline);
            pw.WriteByte(GMLevel);
        }


        public void DecodeForTransfer(Packet pr)
        {
            Name = pr.ReadString();
            ID = pr.ReadInt();
            Job = pr.ReadShort();
            Level = pr.ReadByte();

            Gender = pr.ReadByte();
            Skin = pr.ReadByte();
            Face = pr.ReadInt();
            Hair = pr.ReadInt();

            MapID = pr.ReadInt();
            PartyID = pr.ReadInt();
            IsOnline = pr.ReadBool();
            GMLevel = pr.ReadByte();
        }


        public virtual void SetupLogging()
        {
            ThreadContext.Properties["CharacterID"] = ID;
            ThreadContext.Properties["CharacterName"] = Name;
            ThreadContext.Properties["MapID"] = MapID;
            ThreadContext.Properties["GMLevel"] = GMLevel;
        }

        public static void RemoveLogging()
        {
            ThreadContext.Properties.Remove("CharacterID");
            ThreadContext.Properties.Remove("CharacterName");
            ThreadContext.Properties.Remove("MapID");
            ThreadContext.Properties.Remove("GMLevel");
        }


        public T WrappedLogging<T>(Func<T> cb)
        {
            T res = default(T);
            WrappedLogging(() =>
            {
                res = cb();
            });
            return res;
        }

        public virtual void WrappedLogging(Action cb)
        {
            var c = ThreadContext.Properties["CharacterID"];
            var cn = ThreadContext.Properties["CharacterName"];
            var mi = ThreadContext.Properties["MapID"];
            var gmLevel = ThreadContext.Properties["GMLevel"];
            SetupLogging();
            try
            {
                cb();
            }
            finally
            {
                RemoveLogging();
                if (c != null)
                {
                    ThreadContext.Properties["CharacterID"] = c;
                    ThreadContext.Properties["CharacterName"] = cn;
                    ThreadContext.Properties["MapID"] = mi;
                    ThreadContext.Properties["GMLevel"] = gmLevel;
                }
            }

        }
    }
}
