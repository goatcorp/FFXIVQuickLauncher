using System;
using System.Runtime.InteropServices;

namespace SteamworksSharp.Native.Libs
{
	internal interface INativeLibrary
	{
		OSPlatform Platform { get; }

		Architecture Architecture { get; }

		Lazy<byte[]> LibraryBytes { get; }

		string Extension { get; }
	}
}