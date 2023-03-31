using System;
using System.Text;

namespace XIVLauncher.Common.Util;

public static class DebugHelpers
{
    /// <summary>
    /// Create a hexdump of the provided bytes.
    /// </summary>
    /// <param name="bytes">The bytes to hexdump.</param>
    /// <param name="offset">The offset in the byte array to start at.</param>
    /// <param name="bytesPerLine">The amount of bytes to display per line.</param>
    /// <returns>The generated hexdump in string form.</returns>
    public static string ByteArrayToHex(byte[] bytes, int offset = 0, int bytesPerLine = 16)
    {
        if (bytes == null) return string.Empty;

        var hexChars = "0123456789ABCDEF".ToCharArray();

        const int OFFSET_BLOCK = 8 + 3;
        var byteBlock = OFFSET_BLOCK + (bytesPerLine * 3) + ((bytesPerLine - 1) / 8) + 2;
        var lineLength = byteBlock + bytesPerLine + Environment.NewLine.Length;

        var line = (new string(' ', lineLength - Environment.NewLine.Length) + Environment.NewLine).ToCharArray();
        var numLines = (bytes.Length + bytesPerLine - 1) / bytesPerLine;

        var sb = new StringBuilder(numLines * lineLength);

        for (var i = 0; i < bytes.Length; i += bytesPerLine)
        {
            var h = i + offset;

            line[0] = hexChars[(h >> 28) & 0xF];
            line[1] = hexChars[(h >> 24) & 0xF];
            line[2] = hexChars[(h >> 20) & 0xF];
            line[3] = hexChars[(h >> 16) & 0xF];
            line[4] = hexChars[(h >> 12) & 0xF];
            line[5] = hexChars[(h >> 8) & 0xF];
            line[6] = hexChars[(h >> 4) & 0xF];
            line[7] = hexChars[(h >> 0) & 0xF];

            var hexColumn = OFFSET_BLOCK;
            var charColumn = byteBlock;

            for (var j = 0; j < bytesPerLine; j++)
            {
                if (j > 0 && (j & 7) == 0) hexColumn++;

                if (i + j >= bytes.Length)
                {
                    line[hexColumn] = ' ';
                    line[hexColumn + 1] = ' ';
                    line[charColumn] = ' ';
                }
                else
                {
                    var by = bytes[i + j];
                    line[hexColumn] = hexChars[(by >> 4) & 0xF];
                    line[hexColumn + 1] = hexChars[by & 0xF];
                    line[charColumn] = by < 32 ? '.' : (char)by;
                }

                hexColumn += 3;
                charColumn++;
            }

            sb.Append(line);
        }

        return sb.ToString().TrimEnd(Environment.NewLine.ToCharArray());
    }

#if DEBUG
    public static bool IsDebugBuild => true;
#else
    public static bool IsDebugBuild => false;
#endif
}