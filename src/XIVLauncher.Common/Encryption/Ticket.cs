using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Serilog;
using XIVLauncher.Common.Encryption.BlockCipher;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Encryption;

public class Ticket
{
    public string Text { get; }
    public int Length { get; }

    private const string FUCKED_GARBAGE_ALPHABET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";

    private Ticket(string text, int length)
    {
        this.Text = text;
        this.Length = length;
    }

    public static Ticket? Get(ISteam steam)
    {
        var ticketBytes = steam.GetAuthSessionTicket();

        if (ticketBytes == null)
            return null;

        var time = 60 * ((steam.GetServerRealTime() - 5) / 60); // MAY BE WRONG?

        var ticketString = BitConverter.ToString(ticketBytes).Replace("-", "").ToLower();
        var rawTicketBytes = Encoding.ASCII.GetBytes(ticketString);

        var rawTicket = new byte[rawTicketBytes.Length + 1];
        Array.Copy(rawTicketBytes, rawTicket, rawTicketBytes.Length);
        rawTicket[rawTicket.Length - 1] = 0;

        Log.Information(ByteArrayToHex(rawTicket));

        var blowfishKey = $"{time:x08}#un@e=x>";

        using var memorySteam = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memorySteam);

        /* REGULAR SUM + TICKET */
        ushort ticketSum = 0;

        foreach (byte b in rawTicket)
        {
            ticketSum += b;
        }

        binaryWriter.Write(ticketSum);
        binaryWriter.Write(rawTicket);

        /* GARBAGE */
        int castTicketSum = unchecked((short)ticketSum);
        Log.Information($"{castTicketSum:X}");
        var seed = time ^ castTicketSum;
        var rand = new CrtRand((uint)seed);

        var numRandomBytes = ((ulong)(rawTicket.Length + 9) & 0xFFFFFFFFFFFFFFF8) - 2 - (ulong)rawTicket.Length;
        var garbage = new byte[numRandomBytes];

        var poop = BitConverter.ToUInt32(memorySteam.ToArray(), 0);

        uint fuckedSum = poop;

        for (var i = 0u; i < numRandomBytes; i++)
        {
            var randChar = FUCKED_GARBAGE_ALPHABET[(int)(fuckedSum + rand.Next()) & 0x3F];
            garbage[i] = (byte)randChar;
            fuckedSum += randChar;
        }

        binaryWriter.Write(garbage);

        memorySteam.Seek(0, SeekOrigin.Begin);
        binaryWriter.Write(fuckedSum);

        Log.Information("[STEAM] time: {Time}, bfKey: {FishKey}, rawTicket.Length: {TicketLen}, ticketSum: {TicketSum}, fuckedSum: {FuckedSum}, seed: {Seed}, numRandomBytes: {NumRandomBytes}", time,
            blowfishKey, rawTicket.Length, ticketSum, fuckedSum, seed, numRandomBytes);

        /* ENC + SPLIT */
        var finalBytes = memorySteam.ToArray();

        var t = finalBytes[0];
        finalBytes[0] = finalBytes[1];
        finalBytes[1] = t;

        var keyBytes = Encoding.ASCII.GetBytes(blowfishKey);
        Log.Information(ByteArrayToHex(keyBytes));

        var blowfish = new Blowfish(keyBytes);
        var ecb = new Ecb<Blowfish>(blowfish);

        Log.Information(ByteArrayToHex(finalBytes));

        var encBytes = new byte[finalBytes.Length];
        Debug.Assert(encBytes.Length % 8 == 0);

        ecb.Encrypt(finalBytes, encBytes);
        var encString = Util.ToMangledSeBase64(encBytes);

        Log.Information(ByteArrayToHex(encBytes));

        const int SPLIT_SIZE = 300;
        var parts = ChunksUpto(encString, SPLIT_SIZE).ToArray();

        var finalString = string.Join(",", parts);

        return new Ticket(finalString, finalString.Length - (parts.Length - 1));
    }

    private static IEnumerable<string> ChunksUpto(string str, int maxChunkSize)
    {
        for (var i = 0; i < str.Length; i += maxChunkSize)
            yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
    }

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

        var offsetBlock = 8 + 3;
        var byteBlock = offsetBlock + (bytesPerLine * 3) + ((bytesPerLine - 1) / 8) + 2;
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

            var hexColumn = offsetBlock;
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
}