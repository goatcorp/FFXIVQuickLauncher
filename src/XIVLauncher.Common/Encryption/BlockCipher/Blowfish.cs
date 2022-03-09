// NOTE: This file is copy-pasted almost *as-is* from the previous work `Aither.Crypto`
//       hence currently it does not follow XL's naming convetions.
//       
//       It's totally okay to change this. But for now, this is what it is atm.
// ReSharper disable InconsistentNaming

using System;
using System.Buffers.Binary;

namespace XIVLauncher.Common.Encryption.BlockCipher
{
    public sealed class Blowfish : IBlockCipher
    {
        /// <inheritdoc />
        public int BlockSize => 8;

        // NOTE: this field should never be marked as readonly as it actually creates a defensive copy on every access. (it's a trap)
        // https://devblogs.microsoft.com/premier-developer/the-in-modifier-and-the-readonly-structs-in-c/

        private BlowfishState m_state;

        /// <summary>
        /// Initializes a new instance of the Blowfish class.
        /// </summary>
        /// <remarks>
        /// This function also calculates P-array and S-boxes from the given key. This is most expensive operation in blowfish algorithm.
        /// </remarks>
        /// <param name="key">A secret key used for blowfish. Key length must be between 32 and 448 bits.</param>
        /// <exception cref="ArgumentException">Length of the key is either too short or too long.</exception>
        public Blowfish(ReadOnlySpan<byte> key)
        {
            m_state = new BlowfishState(key);
        }

        public unsafe void EncryptBlockUnsafe(byte* input, byte* output)
        {
            var inputBlock = (uint*)input;
            var outputBlock = (uint*)output;

            var xl = inputBlock[0];
            var xr = inputBlock[1];

            // will be elided by JIT
            if (BitConverter.IsLittleEndian)
            {
                xl = BinaryPrimitives.ReverseEndianness(xl);
                xr = BinaryPrimitives.ReverseEndianness(xr);
            }

            (xl, xr) = m_state.EncryptBlock(xl, xr);

            // will be elided by JIT
            if (BitConverter.IsLittleEndian)
            {
                xl = BinaryPrimitives.ReverseEndianness(xl);
                xr = BinaryPrimitives.ReverseEndianness(xr);
            }

            outputBlock[0] = xl;
            outputBlock[1] = xr;
        }

        public unsafe void DecryptBlockUnsafe(byte* input, byte* output)
        {
            var inputBlock = (uint*)input;
            var outputBlock = (uint*)output;

            var xl = inputBlock[0];
            var xr = inputBlock[1];

            // will be elided by JIT
            if (BitConverter.IsLittleEndian)
            {
                xl = BinaryPrimitives.ReverseEndianness(xl);
                xr = BinaryPrimitives.ReverseEndianness(xr);
            }

            (xl, xr) = m_state.DecryptBlock(xl, xr);

            // will be elided by JIT
            if (BitConverter.IsLittleEndian)
            {
                xl = BinaryPrimitives.ReverseEndianness(xl);
                xr = BinaryPrimitives.ReverseEndianness(xr);
            }

            outputBlock[0] = xl;
            outputBlock[1] = xr;
        }
    }
}

// ReSharper restore InconsistentNaming
