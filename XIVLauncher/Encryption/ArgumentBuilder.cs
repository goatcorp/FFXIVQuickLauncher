using System;
using System.Collections.Generic;
using System.Text;

namespace XIVLauncher.Encryption
{
    public sealed class ArgumentBuilder
    {
        private static char[] checksumTable =
        {
            'f', 'X', '1', 'p', 'G', 't', 'd', 'S',
            '5', 'C', 'A', 'P', '4', '_', 'V', 'L'
        };

        private static char DeriveChecksum(uint key)
        {
            var index = (key & 0x000F_0000) >> 16;

            return checksumTable[index];
        }

        private static byte[] Encrypt(string input, uint key)
        {
            var inputBytes = Encoding.UTF8.GetBytes(input);

            return Encrypt(inputBytes, key);
        }

        private static byte[] Encrypt(byte[] input, uint key)
        {
            // pad if input length is not multiple of 8
            var paddedLength = (input.Length + 8) & ~0b111;
            var paddedInput = new byte[paddedLength];
            
            Buffer.BlockCopy(input, 0, paddedInput, 0, input.Length);

            var blowfish = new Blowfish(GetKeyBytes(key));
            blowfish.Encipher(paddedInput, 0, paddedInput.Length);

            return paddedInput;
        }
        
        private static string ToSeBase64String(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static void AppendArgumentString(StringBuilder buffer, KeyValuePair<string, string> item)
        {
            var escapedName = EscapeValue(item.Key);
            var escapedValue = EscapeValue(item.Value);
            
            buffer.Append($"/{escapedName} ={escapedValue}");
        }

        private static string EscapeValue(string input)
        {
            return input.Replace(" ", "  ");
        }

        private static byte[] GetKeyBytes(uint key)
        {
            var format = $"{key:X08}";

            return Encoding.UTF8.GetBytes(format);
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
            var buffer = new StringBuilder();
            
            foreach (var argument in m_arguments)
            {
                // Yes, they do have a space prepended even for the first argument.
                buffer.Append(' ');
                AppendArgumentString(buffer, argument);
            }

            return buffer.ToString();
        }

        public string BuildEncrypted(uint key)
        {
            var arguments = Build();
            
            var data = Encrypt(arguments, key);
            var base64Str = ToSeBase64String(data);
            var checksum = DeriveChecksum(key);
            
            return $"//**sqex0003{base64Str}{checksum}**//";
        }
    }
}