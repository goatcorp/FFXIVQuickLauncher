// NOTE: This file is copy-pasted almost *as-is* from the previous work `Aither.Crypto`
//       hence currently it does not follow XL's naming convetions.
//       
//       It's totally okay to change this. But for now, this is what it is atm. 
// ReSharper disable InconsistentNaming

using System;

namespace XIVLauncher.Common.Encryption.BlockCipher
{
    public interface IBlockMode
    {
        void Encrypt(ReadOnlySpan<byte> input, Span<byte> output);
        void Decrypt(ReadOnlySpan<byte> input, Span<byte> output);
    }
}

// ReSharper restore InconsistentNaming
