using System.Collections.Generic;
using System.Security.Cryptography;
using WvsBeta.Common;

namespace System
{
    public static class Shuffler
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            var n = list.Count;
            while (n > 1)
            {
                var k = (Rand32.NextBetween() % n);
                n--;
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

    }
}
