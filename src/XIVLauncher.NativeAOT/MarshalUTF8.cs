using System.Runtime.InteropServices;

namespace MarshalUTF8Extensions
{
    internal static class MarshalUtf8
    {
        public static nint StringToHGlobal(string s, out int length)
        {
            if (s == null)
            {
                length = 0;
                return nint.Zero;
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            var ptr = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            Marshal.WriteByte(ptr, bytes.Length, 0);
            length = bytes.Length;

            return ptr;
        }

        public static nint StringToHGlobal(string s)
        {
            return StringToHGlobal(s, out int _);
        }
    }
}

