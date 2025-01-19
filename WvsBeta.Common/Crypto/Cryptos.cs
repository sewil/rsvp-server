using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace WvsBeta.Common
{
    public class Cryptos
    {

        static Random rnd = new Random();
        public static string GetNewSessionHash()
        {
            var bytes = new byte[16];
            rnd.NextBytes(bytes);

            return string.Join("", bytes.Select(x => x.ToString("X2")));
        }

    }
}
