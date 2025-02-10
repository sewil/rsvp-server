using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace WzTools.Helpers
{
    public class ArchiveWriter : BinaryWriter
    {
        private Dictionary<string, long> _stringPool = new();
        private int contentsStart = 0;

        public ArchiveWriter(Stream output) : base(output)
        {
        }

        public void Write(string value, byte existingId, byte newId)
        {
            if (_stringPool.TryGetValue(value, out var location))
            {
                Write((byte)existingId);
                var offset = (int)(location - contentsStart);
                Debug.WriteLineIf(ExtraTools.DebugStringDedupe, $"Writing deduped '{value}' with offset '{offset}'");
                this.Write(offset);
            }
            else
            {
                Write((byte)newId);

                if (value.Length > 4)
                    _stringPool[value] = BaseStream.Position;

                var bytes = EncodeString(value, out var unicode);
                var actualLength = bytes.Length;
                if (unicode)
                {
                    actualLength /= 2;
                    if (actualLength >= 127)
                    {
                        Write((sbyte)127);
                        Write((int)actualLength);
                    }
                    else
                    {
                        Write((sbyte)actualLength);
                    }
                }
                else
                {
                    if (actualLength >= 127)
                    {
                        Write((sbyte)-128);
                        Write((int)actualLength);
                    }
                    else
                    {
                        Write((sbyte)-actualLength);
                    }
                }

                Write(bytes);
            }
        }

        public byte[] EncodeString(string value, out bool unicode)
        {
            unicode = value.Any(x => x >= 0x80);

            byte[] bytes;

            var encoding = unicode ? Encoding.Unicode : Encoding.ASCII;

            bytes = encoding.GetBytes(value);

            // Encryption.Encrypt(bytes);

            if (!ExtraTools.WriteWithoutXor)
            {
                bytes = bytes.ApplyStringXor(unicode);
            }
            return bytes;
        }
    }
}
