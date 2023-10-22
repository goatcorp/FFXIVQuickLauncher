using System.IO.Compression;
// ReSharper disable InconsistentNaming

namespace XIVLauncher.Common.Patching.Util
{
    // works around https://docs.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/partial-byte-reads-in-streams
    static class FullDeflateStreamReader
    {
        public static void FullRead(this DeflateStream stream, byte[] array, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int bytesRead = stream.Read(array, offset + totalRead, count - totalRead);
                if (bytesRead == 0) break;
                totalRead += bytesRead;
            }
        }
    }
}