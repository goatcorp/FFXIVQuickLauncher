using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher
{
    internal static class EnvironmentSettings
    {
        public static bool IsWine => IsLinux || IsMac || FoundWineExports;
        public static bool IsLinux => CheckEnvBool("XL_WINEONLINUX");
        public static bool IsMac => CheckEnvBool("XL_WINEONMAC");
        public static bool FoundWineExports => GetProcAddress(GetModuleHandle("ntdll.dll"), "wine_get_version") != IntPtr.Zero;
        public static bool IsDisableUpdates => CheckEnvBool("XL_NOAUTOUPDATE");
        public static bool IsPreRelease => CheckEnvBool("XL_PRERELEASE");
        public static bool IsNoRunas => CheckEnvBool("XL_NO_RUNAS");
        private static bool CheckEnvBool(string var) => bool.Parse(System.Environment.GetEnvironmentVariable(var) ?? "false");

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
}
