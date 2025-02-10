using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace WzTools.Helpers
{
    public class ArchiveReader : BinaryReader
    {

        private int contentsStart;

        public ArchiveReader(Stream output, int offset = 0) : base(output)
        {
            contentsStart = offset;
        }

        public string ReadStringWithID(byte id, byte existingID, byte newID)
        {
            if (id == newID)
            {
                return DecodeString();
            }

            if (id == existingID)
            {
                return ReadDeDuplicatedString();
            }

            throw new Exception($"Unknown ID. Expected {existingID} or {newID}, but got {id}.");
        }

        public string ReadString(byte existingID, byte newID)
        {
            var p = ReadByte();
            return ReadStringWithID(p, existingID, newID);
        }

        private string ReadDeDuplicatedString()
        {
            var off = ReadInt32();

            off += contentsStart;

            var ret = this.JumpAndReturn(off, DecodeString);

            Debug.WriteLineIf(ExtraTools.DebugStringDedupe, $"Reading deduped '{ret}' with offset '{off - contentsStart}'");
            return ret;
        }


        private string DecodeString()
        {
            // unicode/ascii switch
            var len = ReadSByte();
            if (len == 0) return "";

            var unicode = len > 0;

            if (unicode) return DecodeStringUnicode(len);
            else return DecodeStringASCII(len);
        }

        public static bool IsLegalUnicode(string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                var uc = char.GetUnicodeCategory(str, i);

                if (uc == UnicodeCategory.Surrogate)
                {
                    // Unpaired surrogate, like  "😵"[0] + "A" or  "😵"[1] + "A"
                    return false;
                }

                if (uc == UnicodeCategory.OtherNotAssigned)
                {
                    // \uF000 or \U00030000
                    return false;
                }

                // Correct high-low surrogate, we must skip the low surrogate
                // (it is correct because otherwise it would have been a 
                // UnicodeCategory.Surrogate)
                if (char.IsHighSurrogate(str, i))
                {
                    i++;
                }
            }

            return true;
        }

        private string DecodeStringASCII(sbyte len)
        {
            int actualLen;
            if (len == -128) actualLen = ReadInt32();
            else actualLen = -len;

            var bytes = ReadBytes(actualLen).ApplyStringXor(false);
            return Encoding.ASCII.GetString(bytes);
        }

        private string DecodeStringUnicode(sbyte len)
        {
            int actualLen = len;
            if (len == 127) actualLen = ReadInt32();
            actualLen *= 2;

            var bytes = ReadBytes(actualLen).ApplyStringXor(true);

            // _encryption.TryDecryptString(bytes, null, false);

            return Encoding.Unicode.GetString(bytes);
        }
    }
}
