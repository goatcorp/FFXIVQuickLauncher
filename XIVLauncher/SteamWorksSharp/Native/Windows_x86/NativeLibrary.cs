using System;
using System.Reflection;
using System.Runtime.InteropServices;
using SteamworksSharp.Native.Libs;

namespace SteamworksSharp.Native.Windows_x86
{
	public class NativeLibrary : INativeLibrary
	{
		public OSPlatform Platform { get; } = OSPlatform.Windows;

		public Architecture Architecture { get; } = Architecture.X86;

		public Lazy<byte[]> LibraryBytes { get; } = new Lazy<byte[]>(() =>
			LibUtils.ReadResourceBytes(typeof(NativeLibrary).GetTypeInfo().Assembly, "XIVLauncher.SteamworksSharp.Native.Windows_x86.steam_api.dll"));

		public string Extension { get; } = "dll";
	}
}