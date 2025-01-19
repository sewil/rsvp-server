using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WvsBeta.Common.Sessions
{
    public class LocalizedString
    {
        private static ILog _log = LogManager.GetLogger(typeof(LocalizedString));
        private static Dictionary<int, Encoding> _encodingMap = new Dictionary<int, Encoding>();
        public const int CP_UTF8 = 65001;
        public int CodePage { get; set; }
        public byte[] RawData { get; set; }
        public string Value { get; set; }
        public LocalizedString(int codepage, byte[] data)
        {
            if (!_encodingMap.TryGetValue(codepage, out var encoding))
            {
                encoding = _encodingMap[codepage] = Encoding.GetEncoding(codepage);
                if (encoding == null)
                {
                    _log.Error($"Unable to find Encoding object for codepage {codepage}, using fallback.");
                }
            }

            if (encoding == null) encoding = Encoding.ASCII;
            CodePage = codepage;
            RawData = data;
            Value = encoding.GetString(RawData);
        }


        public static implicit operator string(LocalizedString ls) => ls.Value;
        public static implicit operator LocalizedString(string ls) => new LocalizedString(Encoding.UTF8.CodePage, ls.ToCharArray().Select(x => (byte)x).ToArray());

        public override string ToString() => Value;
    }
}
