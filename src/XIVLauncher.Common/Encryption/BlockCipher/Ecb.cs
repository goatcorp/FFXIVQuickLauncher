// NOTE: This file is copy-pasted almost *as-is* from the previous work `Aither.Crypto`
//       hence currently it does not follow XL's naming convetions.
//       
//       It's totally okay to change this. But for now, this is what it is atm.
// ReSharper disable InconsistentNaming

using System;

namespace XIVLauncher.Common.Encryption.BlockCipher
{
    public sealed class Ecb<T> : IBlockMode where T : IBlockCipher
    {
        private T m_cipher;

        public Ecb(T cipher)
        {
            m_cipher = cipher;
        }

        private void AssertSliceLength(ReadOnlySpan<byte> input, ReadOnlySpan<byte> output)
        {
            if (input.Length > output.Length)
            {
                throw new ArgumentException("The output slice must be larger than the input.", nameof(output));
            }

            var blockSize = m_cipher.BlockSize;

            if (input.Length % blockSize != 0)
            {
                throw new ArgumentException("The length of the input slice must be divisible by the block length.",
                    nameof(input));
            }
        }

        public void Encrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            AssertSliceLength(input, output);

            unsafe
            {
                fixed (byte* pInput = input)
                fixed (byte* pOutput = output)
                {
                    for (var i = 0; i < input.Length; i += m_cipher.BlockSize)
                    {
                        m_cipher.EncryptBlockUnsafe(pInput + i, pOutput + i);
                    }
                }
            }
        }

        public void Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            AssertSliceLength(input, output);

            unsafe
            {
                fixed (byte* pInput = input)
                fixed (byte* pOutput = output)
                {
                    for (var i = 0; i < input.Length; i += m_cipher.BlockSize)
                    {
                        m_cipher.DecryptBlockUnsafe(pInput + i, pOutput + i);
                    }
                }
            }
        }
    }
}

// ReSharper restore InconsistentNaming
