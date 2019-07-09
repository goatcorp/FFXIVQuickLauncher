using System;
using System.Reflection;
using System.Runtime.InteropServices;
using SteamworksSharp.Native.Libs;

namespace SteamworksSharp.Native.Windows_x64
{
    public class NativeLibrary : INativeLibrary
    {
        public OSPlatform Platform { get; } = OSPlatform.Windows;

        public Architecture Architecture { get; } = Architecture.X64;

        public Lazy<byte[]> LibraryBytes { get; } = new Lazy<byte[]>(() => 
            LibUtils.ReadResourceBytes(typeof(NativeLibrary).GetTypeInfo().Assembly, "XIVLauncher.SteamWorksSharp.Native.Windows_x64.steam_api64.dll"));

        public string Extension { get; } = "dll";
    }
}