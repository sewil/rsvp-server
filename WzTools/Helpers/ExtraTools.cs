using System;
using System.Threading;

namespace WzTools.Helpers
{
    static class ExtraTools
    {
        public static bool DebugStringDedupe = false;
        public static bool WriteWithoutXor = false;

        private static byte[] _asciiXorBuff = { 0xAA };
        private static object _asciiBuffLock = new object();
        private static byte[] _unicodeXorBuff = { 0xAA, 0xAA };
        private static object _unicodeBuffLock = new object();

        private static byte[] TryEnlargeXorBuff(ref byte[] buff, object lockObject, int expectedLength, bool unicode)
        {
            if (buff.Length >= expectedLength) return buff;

            lock (lockObject)
            {
                var offset = buff.Length;
                Array.Resize(ref buff, expectedLength);

                if (unicode)
                {
                    ushort mask = (ushort)(buff[offset - 2] | buff[offset - 1] << 8);

                    for (var i = offset; i < expectedLength; i += 2)
                    {
                        mask++;
                        buff[i + 0] = (byte)(mask & 0xFF);
                        buff[i + 1] = (byte)((mask >> 8) & 0xFF);
                    }
                }
                else
                {
                    byte mask = buff[offset - 1];

                    for (var i = offset; i < expectedLength; i++)
                    {
                        mask++;
                        buff[i] = mask;
                    }
                }

                return buff;
            }
        }

        static ExtraTools()
        {
            TryEnlargeXorBuff(ref _unicodeXorBuff, _unicodeBuffLock, 2048, true);
            TryEnlargeXorBuff(ref _asciiXorBuff, _asciiBuffLock, 2048, false);
        }

        public static byte[] ApplyStringXor(this byte[] input, bool unicode)
        {
            var length = input.Length;
            if (unicode)
            {
                if ((length % 2) != 0) throw new Exception("Input string is not power of two");
            }

            var bytes = new byte[length];
            Buffer.BlockCopy(input, 0, bytes, 0, length);

            var xorBytes = unicode ?
                TryEnlargeXorBuff(ref _unicodeXorBuff, _unicodeBuffLock, length, true) :
                TryEnlargeXorBuff(ref _asciiXorBuff, _asciiBuffLock, length, false);


            const int bigChunkSize = 8;
            unsafe
            {
                fixed (byte* dataPtr = bytes)
                fixed (byte* xorPtr = xorBytes)
                {
                    byte* currentInputByte = dataPtr;
                    byte* currentXorByte = xorPtr;
                    var i = 0;
                    int intBlocks = length / bigChunkSize;
                    for (; i < intBlocks; ++i)
                    {
                        *(UInt64*)currentInputByte ^= *(UInt64*)currentXorByte;
                        currentInputByte += bigChunkSize;
                        currentXorByte += bigChunkSize;
                    }

                    i *= bigChunkSize;

                    for (; i < length; i++)
                    {
                        *(currentInputByte++) ^= *(currentXorByte++);
                    }
                }
            }

            return bytes;
        }
    }
}
