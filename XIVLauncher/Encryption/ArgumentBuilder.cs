using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace XIVLauncher.Encryption
{
    public sealed class ArgumentBuilder
    {
        private static readonly uint version = 3;

        private static readonly char[] checksumTable =
        {
            'f', 'X', '1', 'p', 'G', 't', 'd', 'S',
            '5', 'C', 'A', 'P', '4', '_', 'V', 'L'
        };

        private static char DeriveChecksum(uint key)
        {
            var index = (key & 0x000F_0000) >> 16;
            try
            {
                return checksumTable[index];
            } catch (IndexOutOfRangeException)
            {
                return '!'; // Conceivably, this shouldn't happen...
            }
        }

        private readonly List<KeyValuePair<string, string>> m_arguments;

        public ArgumentBuilder()
        {
            m_arguments = new List<KeyValuePair<string, string>>();
        }

        public ArgumentBuilder(IEnumerable<KeyValuePair<string, string>> items)
        {
            m_arguments = new List<KeyValuePair<string, string>>(items);
        }

        public ArgumentBuilder Append(string key, string value)
        {
            return Append(new KeyValuePair<string, string>(key, value));
        }

        public ArgumentBuilder Append(KeyValuePair<string, string> item)
        {
            m_arguments.Add(item);

            return this;
        }

        public ArgumentBuilder Append(IEnumerable<KeyValuePair<string, string>> items)
        {
            m_arguments.AddRange(items);

            return this;
        }

        public string Build()
        {
            return m_arguments.Aggregate(new StringBuilder(),
                // Yes, they do have a space prepended even for the first argument.
                (whole, part) => whole.Append($" /{EscapeValue(part.Key)} ={EscapeValue(part.Value)}"))
                .ToString();
        }

        public string BuildEncrypted(uint key)
        {
            var arguments = Build();

            var blowfish = new Blowfish(GetKeyBytes(key));
            var ciphertext = blowfish.Encrypt(Encoding.UTF8.GetBytes(arguments));
            var base64Str = ToSeBase64String(ciphertext);
            var checksum = DeriveChecksum(key);
            
            return $"//**sqex{version:D04}{base64Str}{checksum}**//";
        }

        public string BuildEncrypted()
        {
            uint key = DeriveKey();

            return BuildEncrypted(key);
        }

        private uint DeriveKey()
        {
            uint ticks = (uint)(Environment.TickCount & 0xFFFF_FFFFu);
            uint key = ticks & 0xFFFF_0000u;

            var keyPair = new KeyValuePair<string, string>("T", Convert.ToString(ticks));
            if (m_arguments.Count > 0 && m_arguments[0].Key == "T")
                m_arguments[0] = keyPair;
            else
                m_arguments.Insert(0, keyPair);

            return key;
        }

        private static byte[] GetKeyBytes(uint key)
        {
            var format = $"{key:x08}";

            return Encoding.UTF8.GetBytes(format);
        }

        private static string EscapeValue(string input)
        {
            return input.Replace(" ", "  ");
        }


        private static string ToSeBase64String(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}