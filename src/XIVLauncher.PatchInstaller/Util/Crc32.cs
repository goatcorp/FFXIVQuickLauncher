using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.Util
{
    /// <summary>
    /// Performs the 32-bit reversed variant of the cyclic redundancy check algorithm.
    /// </summary>
    public class Crc32
    {
        private const uint POLY = 0xedb88320;

        private static readonly uint[] CrcArray =
            Enumerable.Range(0, 256).Select(i =>
            {
                var k = (uint)i;
                for (var j = 0; j < 8; j++)
                    k = (k & 1) != 0 ? 
                        (k >> 1) ^ POLY :
                        k >> 1;

                return k;
            }).ToArray();

        /// <summary>
        /// Gets the checksum.
        /// </summary>
        public uint Checksum => ~_crc32;

        private uint _crc32 = 0xFFFFFFFF;

        /// <summary>
        /// Initializes the CRC32's state.
        /// </summary>
        public void Init()
        {
            _crc32 = 0xFFFFFFFF;
        }

        /// <summary>
        /// Updates the CRC32 with new data.
        /// </summary>
        /// <param name="data">Data to calculate the new CRC from.</param>
        public void Update(byte[] data)
        {
            foreach (var b in data)
                Update(b);
        }

        /// <summary>
        /// Updates the CRC32 with new data.
        /// </summary>
        /// <param name="b">Data to calculate the new CRC from.</param>
        public void Update(byte b)
        {
            _crc32 = CrcArray[(_crc32 ^ b) & 0xFF] ^
                     ((_crc32 >> 8) & 0x00FFFFFF);
        }

    }
}
