using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleStarter
{
    public class Packet : IDisposable
    {
        private static readonly Encoding StringEncoding = Encoding.GetEncoding("iso-8859-1");

        private MemoryStream ms { get; }
        private BinaryReader br;

        public Packet(byte[] data)
        {
            ms = new MemoryStream(data);
            br = new BinaryReader(ms);
        }

        public ushort ReadUShort() => br.ReadUInt16();
        public byte ReadByte() => br.ReadByte();
        public long ReadLong() => br.ReadInt64();
        public bool ReadBool() => ReadByte() != 0;
        public string ReadString()
        {
            ushort len = br.ReadUInt16();

            var encoding = StringEncoding;
            if ((len & 0x8000) != 0)
            {
                len ^= 0x8000;
                encoding = Encoding.UTF8;
            }

            return encoding.GetString(br.ReadBytes(len));
        }
        
        public void Dispose()
        {
            br.Dispose();
            ms.Dispose();
        }
    }
}