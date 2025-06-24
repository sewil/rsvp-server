using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WvsBeta.Common.Crypto
{
    using System;
    using System.Runtime.InteropServices;

    public class RdRand_Interop
    {
        /// <summary>
        /// Name of DLL File
        /// </summary>
        public const string DLL = "RdRand.dll";

        /// <summary>
        /// Checks if the "RDRAND" Operation is supported
        /// </summary>
        /// <returns>true, if RDRAND is supported, false otherwise</returns>
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool RdRandSupported();

        /// <summary>
        /// Generates random Bytes
        /// </summary>
        /// <param name="buffer">Buffer to fill</param>
        /// <param name="Count">Number of bytes to generate</param>
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void RdRandBytes(byte[] buffer, int Count);

        /// <summary>
        /// Value that determines if RdRand is supported
        /// </summary>
        public static readonly bool IsRdRandSupported;

        /// <summary>
        /// Static Initializer
        /// </summary>
        static RdRand_Interop()
        {
            //This is done in a static manner because CPU features tend to not just magically appear.
            IsRdRandSupported = RdRandSupported();
        }

        /// <summary>
        /// Generates a user specified number of random bytes
        /// </summary>
        /// <param name="Count">Number of bytes</param>
        /// <returns>Random bytes</returns>
        public static byte[] GetRandom(int Count)
        {
            if (!IsRdRandSupported)
            {
                throw new NotSupportedException("The RdRand CPU Operation is not supported on your machine.");
            }
            if (Count < 0)
            {
                throw new ArgumentOutOfRangeException("Count", "Count must be at least 0");
            }
            //Support 0 even if it is nonsense. It makes usage easier
            if (Count == 0)
            {
                return new byte[0];
            }
            var b = new byte[Count];
            RdRandBytes(b, Count);
            return b;
        }

        /// <summary>
        /// Fills the given Array with random data
        /// </summary>
        /// <param name="Data">Array to fill</param>
        public static void GetRandom(byte[] Data)
        {
            if (Data == null)
            {
                throw new ArgumentNullException("Data");
            }
            RdRandBytes(Data, Data.Length);
        }

        /// <summary>
        /// Writes random data into array
        /// </summary>
        /// <param name="Data">Array</param>
        /// <param name="Start">Start of region</param>
        /// <param name="Count">Number of bytes to copy</param>
        public static void GetRandom(byte[] Data, int Start, int Count)
        {
            if (Data == null)
            {
                throw new ArgumentNullException("Data");
            }
            if (Start >= Data.Length || Start < 0)
            {
                throw new ArgumentOutOfRangeException("Start", "Start outside of the bounds of Data");
            }
            if (Count + Start > Data.Length)
            {
                throw new ArgumentOutOfRangeException("Count", "Number of bytes to read larger than bytes available in the array at the given Start position");
            }
            byte[] Temp = GetRandom(Count);
            Array.Copy(Temp, 0, Data, Start, Count);
        }

    }
}
