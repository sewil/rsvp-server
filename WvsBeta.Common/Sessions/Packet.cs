using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace WvsBeta.Common.Sessions
{
    public class Packet : IDisposable
    {
        private static readonly Encoding StringEncoding = Encoding.GetEncoding("iso-8859-1");

        private MemoryStream _memoryStream;
        private BinaryReader _binReader;
        private BinaryWriter _binWriter;

        public MemoryStream MemoryStream => _memoryStream;


        /// <summary>
        /// Millis when this packet has been built. Recommended to use as MasterThread.CurrentTime is the processing time, not the reception time!
        /// </summary>
        public long PacketCreationTime { get; } = (long)((Stopwatch.GetTimestamp() * (1.0 / Stopwatch.Frequency)) * 1000.0);

        public byte Opcode { get; private set; }

        public Packet(byte[] pData) : this(pData, pData.Length) { }

        public Packet(byte[] pData, int length)
        {
            _memoryStream = new MemoryStream(pData, 0, length, false);
            _binReader = new BinaryReader(_memoryStream);

            Opcode = ReadByte();
            Position = 0;
        }

        /// <summary>
        /// Initialize a Packet from a compressed GZip stream
        /// </summary>
        /// <param name="gzipStream">Compressed GZip stream</param>
        public Packet(GZipStream gzipStream)
        {
            _memoryStream = new MemoryStream();
            gzipStream.CopyTo(_memoryStream);
            _memoryStream.Position = 0;
            _binReader = new BinaryReader(_memoryStream);


            Opcode = ReadByte();
            Position = 0;
        }

        public Packet(DeflateStream deflateStream)
        {
            _memoryStream = new MemoryStream();
            deflateStream.CopyTo(_memoryStream);
            _memoryStream.Position = 0;
            _binReader = new BinaryReader(_memoryStream);
        }

        public Packet()
        {
            _memoryStream = new MemoryStream();
            _binWriter = new BinaryWriter(_memoryStream);
        }

        public Packet(byte pOpcode)
        {
            _memoryStream = new MemoryStream();
            _binWriter = new BinaryWriter(_memoryStream);
            WriteByte(pOpcode);
        }

        public void Dispose()
        {
            _memoryStream?.Dispose();
            _binReader?.Dispose();
            _binWriter?.Dispose();
        }

        public Packet(ServerMessages pMessage) : this((byte)pMessage) { }
        public Packet(ISClientMessages pMessage) : this((byte)pMessage) { }
        public Packet(ISServerMessages pMessage) : this((byte)pMessage) { }

        public Packet(CfgServerMessages pMessage) : this(ServerMessages.CFG)
        {
            WriteByte(pMessage);
        }

        public byte[] ToArray()
        {
            return _memoryStream.ToArray();
        }

        public void GzipCompress(Packet packet) => GzipCompress(packet.MemoryStream);

        /// <summary>
        /// Compress the current buffer to a stream
        /// </summary>
        /// <param name="outputStream"></param>
        public void GzipCompress(Stream outputStream)
        {
            var pos = Position;
            Position = 0;
            using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress, true))
            {
                MemoryStream.CopyTo(gzipStream);
            }
            Position = pos;
        }

        public void DeflateCompress(Stream outputStream)
        {
            var pos = Position;
            Position = 0;
            using (var deflateStream = new DeflateStream(outputStream, CompressionMode.Compress, true))
            {
                MemoryStream.CopyTo(deflateStream);
            }
            Position = pos;
        }

        public int Length
        {
            get { return (int)_memoryStream.Length; }
        }

        public int Position
        {
            get { return (int)_memoryStream.Position; }
            set { _memoryStream.Position = value; }
        }

        public void Reset(int pPosition = 0)
        {
            _memoryStream.Position = pPosition;
        }

        public void Skip(int pAmount)
        {
            if (pAmount + _memoryStream.Position > Length)
                throw new Exception("!!! Cannot skip more bytes than there's inside the buffer!");
            _memoryStream.Position += pAmount;
        }

        public byte[] ReadLeftoverBytes()
        {
            return ReadBytes(Length - (int)_memoryStream.Position);
        }

        public override string ToString()
        {
            var sb = new StringBuilder(Length * 3);
            foreach (var b in ToArray())
            {
                sb.AppendFormat("{0:X2} ", b);
            }
            return sb.ToString();
        }

        public void WriteBytes(byte[] val) { _binWriter.Write(val); }

        public void WriteByte(byte val)
        {
            if (Length == 0)
                Opcode = val;
            _binWriter.Write(val);
        }

        public void WriteByte<T>(T val) where T : Enum => WriteByte(Convert.ToByte(val));
        public void WriteSByte<T>(T val) where T : Enum => WriteSByte(Convert.ToSByte(val));
        public void WriteSByte(sbyte val) { _binWriter.Write(val); }
        public bool WriteBool(bool val)
        {
            WriteByte(val ? (byte)1 : (byte)0);
            return val;
        }
        public void WriteShort(short val) { _binWriter.Write(val); }
        public void WriteInt(int val) { _binWriter.Write(val); }
        public void WriteLong(long val) { _binWriter.Write(val); }
        public void WriteUShort(ushort val) { _binWriter.Write(val); }
        public void WriteUInt(uint val) { _binWriter.Write(val); }
        public void WriteULong(ulong val) { _binWriter.Write(val); }
        public void WriteDouble(double val) { _binWriter.Write(val); }
        public void WriteFloat(float val) { _binWriter.Write(val); }
        public void WritePoint(Point val) { _binWriter.Write((short)val.X); _binWriter.Write((short)val.Y); }
        public void WriteIntPoint(Point val) { _binWriter.Write(val.X); _binWriter.Write(val.Y); }

        public void WriteString(LocalizedString ls)
        {
            var rawData = ls.RawData;

            if (rawData.Length == 0)
            {
                WriteShort(0);
                return;
            }

            var lenField = (ushort)rawData.Length;
            if (ls.CodePage == LocalizedString.CP_UTF8)
                lenField |= 0x8000;
            WriteUShort(lenField);
            WriteBytes(rawData);
        }

        public void WriteString(string val, Encoding encoding = null)
        {
            WriteUTF8String(val);
        }

        public void WriteUTF8String(string val)
        {
            var b = Encoding.UTF8.GetBytes(val);

            if (b.Length == 0)
            {
                WriteShort(0);
                return;
            }

            var len = (ushort)b.Length;
            len |= 0x8000;
            WriteUShort(len);
            _binWriter.Write(b);
        }

        public void WriteString(string val, int maxlen)
        {
            var i = 0; 
            for (; i < val.Length && i < maxlen; i++) 
                _binWriter.Write(val[i]);

            for (; i < maxlen; i++)
                WriteByte(0);
        }

        public void WriteString13(string val) { WriteString(val, 13); }

        public void WriteHexString(string pInput)
        {
            pInput = pInput.Replace(" ", "");
            if (pInput.Length % 2 != 0) throw new Exception("Hex String is incorrect (size)");
            for (int i = 0; i < pInput.Length; i += 2)
            {
                WriteByte(byte.Parse(pInput.Substring(i, 2), System.Globalization.NumberStyles.HexNumber));
            }

        }

        public byte[] ReadBytes(int pLen) { return _binReader.ReadBytes(pLen); }
        public bool ReadBool() { return _binReader.ReadByte() != 0; }
        public byte ReadByte() => _binReader.ReadByte();
        public T ReadByte<T>() where T : Enum => (T)Enum.ToObject(typeof(T), ReadByte());
        public sbyte ReadSByte() { return _binReader.ReadSByte(); }
        public short ReadShort() { return _binReader.ReadInt16(); }
        public int ReadInt() { return _binReader.ReadInt32(); }
        public T ReadInt<T>() where T : Enum => (T)Enum.ToObject(typeof(T), ReadInt());
        public long ReadLong() { return _binReader.ReadInt64(); }
        public ushort ReadUShort() { return _binReader.ReadUInt16(); }
        public uint ReadUInt() { return _binReader.ReadUInt32(); }
        public ulong ReadULong() { return _binReader.ReadUInt64(); }
        public double ReadDouble() { return _binReader.ReadDouble(); }
        public float ReadFloat() { return _binReader.ReadSingle(); }
        public Point ReadPoint() { return new Point(_binReader.ReadInt16(), _binReader.ReadInt16()); }
        public Point ReadIntPoint() { return new Point(_binReader.ReadInt32(), _binReader.ReadInt32()); }
        
        public string ReadString(short pLen)
        {
            var str = "";
            var ignore = false;
            for (var i = 0; i < pLen; i++)
            {
                var c = _binReader.ReadChar();

                if (c == 0) ignore = true;
                if (ignore) continue;

                str += c;
            }
            
            return str;
        }
        public string ReadString()
        {
            ushort len = _binReader.ReadUInt16();

            var encoding = StringEncoding;
            if ((len & 0x8000) != 0)
            {
                len ^= 0x8000;
                encoding = Encoding.UTF8;
            }

            return encoding.GetString(_binReader.ReadBytes(len));
        }

        public LocalizedString ReadLocalizedString(int codepage)
        {
            var len = _binReader.ReadUInt16();
            
            if ((len & 0x8000) != 0)
            {
                len ^= 0x8000;
                codepage = LocalizedString.CP_UTF8;
            }
            var rawString = _binReader.ReadBytes(len);
            return new LocalizedString(codepage, rawString);
        }

        public void SetBytes(int pPosition, byte[] val) { int tmp = (int)_memoryStream.Position; Reset(pPosition); _binWriter.Write(val); Reset(tmp); }
        public void SetByte(int pPosition, byte val) { int tmp = (int)_memoryStream.Position; Reset(pPosition); _binWriter.Write(val); Reset(tmp); }
        public void SetSByte(int pPosition, sbyte val) { int tmp = (int)_memoryStream.Position; Reset(pPosition); _binWriter.Write(val); Reset(tmp); }
        public void SetBool(int pPosition, bool val) { int tmp = (int)_memoryStream.Position; Reset(pPosition); WriteByte(val == true ? (byte)1 : (byte)0); Reset(tmp); }
        public void SetShort(int pPosition, short val) { int tmp = (int)_memoryStream.Position; Reset(pPosition); _binWriter.Write(val); Reset(tmp); }
        public void SetInt(int pPosition, int val) { int tmp = (int)_memoryStream.Position; Reset(pPosition); _binWriter.Write(val); Reset(tmp); }
        public void SetLong(int pPosition, long val) { int tmp = (int)_memoryStream.Position; Reset(pPosition); _binWriter.Write(val); Reset(tmp); }
        public void SetUShort(int pPosition, ushort val) { int tmp = (int)_memoryStream.Position; Reset(pPosition); _binWriter.Write(val); Reset(tmp); }
        public void SetUInt(int pPosition, uint val) { int tmp = (int)_memoryStream.Position; Reset(pPosition); _binWriter.Write(val); Reset(tmp); }
        public void SetULong(int pPosition, ulong val) { int tmp = (int)_memoryStream.Position; Reset(pPosition); _binWriter.Write(val); Reset(tmp); }

    }
}
