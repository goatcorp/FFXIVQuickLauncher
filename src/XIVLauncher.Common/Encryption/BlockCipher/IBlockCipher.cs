// NOTE: This file is copy-pasted almost *as-is* from the previous work `Aither.Crypto`
//       hence currently it does not follow XL's naming convetions.
//       
//       It's totally okay to change this. But for now, this is what it is atm.
// ReSharper disable InconsistentNaming

namespace XIVLauncher.Common.Encryption.BlockCipher
{
    public interface IBlockCipher
    {
        /// <summary>
        /// A number of bytes that can be processed in a single operation.
        /// </summary>
        /// <remarks>
        /// This property is assumed to be immutable once the block cipher object is created.
        /// Breaking this assumption may cause an undefined behavior.
        /// </remarks>
        int BlockSize { get; }

        /// <summary>
        /// Encrypts a single block.
        /// </summary>
        /// <param name="input">
        /// A pointer to the data needs to be encrypted.
        /// It must be valid to read bytes from the pointer where size is indicated by BlockSize property.
        /// </param>
        /// <param name="output">
        /// A pointer to the buffer to store the result of the operation.
        /// It must be valid to write bytes to the pointer where size is indicated by BlockSize property.
        /// </param>
        /// <remarks>
        /// A pointer to input and output **can** overlap to perform in-place operation.
        /// </remarks>
        unsafe void EncryptBlockUnsafe(byte* input, byte* output);

        /// <summary>
        /// Decrypts a single block.
        /// </summary>
        /// <param name="input">
        /// A pointer to the data needs to be decrypted.
        /// It must be valid to read bytes from the pointer where size is indicated by BlockSize property.
        /// </param>
        /// <param name="output">
        /// A pointer to the buffer to store the result of the operation.
        /// It must be valid to write bytes to the pointer where size is indicated by BlockSize property.
        /// </param>
        /// <remarks>
        /// A pointer to input and output **can** overlap to perform in-place operation.
        /// </remarks>
        unsafe void DecryptBlockUnsafe(byte* input, byte* output);
    }
}

// ReSharper restore InconsistentNaming
