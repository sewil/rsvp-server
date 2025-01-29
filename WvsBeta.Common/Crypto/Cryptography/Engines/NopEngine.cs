using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto;

namespace WvsBeta.Common.Crypto.Cryptography.Engines
{
    // This is not what MapleGlobal used.
    class NopEngine : IBlockCipher
    {
		private bool initialised;
		private const int BlockSize = 1;

		public virtual void Init(
			bool forEncryption,
			ICipherParameters parameters)
		{
			// we don't mind any parameters that may come in
			initialised = true;
		}

        public int ProcessBlock(ReadOnlySpan<byte> input, Span<byte> output)
        {
            if (!initialised)
                throw new InvalidOperationException("Null engine not initialised");
			input[0..BlockSize].CopyTo(output);
			return BlockSize;
        }

        public virtual string AlgorithmName
		{
			get { return "Null"; }
		}

		public virtual bool IsPartialBlockOkay
		{
			get { return true; }
		}

		public virtual int GetBlockSize()
		{
			return BlockSize;
		}

		public virtual int ProcessBlock(
			byte[] input,
			int inOff,
			byte[] output,
			int outOff)
		{
			if (!initialised)
				throw new InvalidOperationException("Null engine not initialised");

			//Check.DataLength(input, inOff, BlockSize, "input buffer too short");
			//Check.OutputLength(output, outOff, BlockSize, "output buffer too short");

			for (int i = 0; i < BlockSize; ++i)
			{
				output[outOff + i] = input[inOff + i];
			}

			return BlockSize;
		}

		public virtual void Reset()
		{
			// nothing needs to be done
		}
	}
}
