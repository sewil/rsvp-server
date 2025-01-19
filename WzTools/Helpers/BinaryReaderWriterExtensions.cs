using System;
using System.IO;
using System.Linq;

namespace WzTools
{
    static class BinaryReaderWriterExtensions
    {
        public static int ReadCompressedInt(this BinaryReader reader)
        {
            var x = reader.ReadSByte();
            if (x == -128) return reader.ReadInt32();
            return x;
        }

        public static void WriteCompressedInt(this BinaryWriter writer, int value)
        {
            if (value < -127 || value > 127)
            {
                writer.Write((sbyte)-128);
                writer.Write((int)value);
            }
            else
            {
                writer.Write((sbyte)value);
            }
        }

        public static long ReadCompressedLong(this BinaryReader reader)
        {
            var x = reader.ReadSByte();
            if (x == -128) return reader.ReadInt64();
            return x;
        }

        public static void WriteCompressedLong(this BinaryWriter writer, long value)
        {
            if (value < -127 || value > 127)
            {
                writer.Write((sbyte)-128);
                writer.Write(value);
            }
            else
            {
                writer.Write((sbyte)value);
            }
        }


        public static T ReadAndReturn<T>(this BinaryReader reader, Func<T> andNow)
        {
            return reader.JumpAndReturn<T>((int)reader.BaseStream.Position, andNow);
        }

        public static T JumpAndReturn<T>(this BinaryReader reader, int offset, Func<T> andNow)
        {
            var prevPos = reader.BaseStream.Position;
            reader.BaseStream.Position = offset;
            var ret = andNow();
            reader.BaseStream.Position = prevPos;
            return ret;
        }
    }
}
