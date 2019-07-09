using System.Runtime.InteropServices;

namespace Valve.Interop
{
	internal static class Native
	{
#if NET452 || NET46 || NET461 || NET462 || NET47 || NET471
        internal const string SteamApiLibraryName = "steam_api.dll";
#else
		internal const string SteamApiLibraryName = "steam_api";
#endif
	}

	public class NativeEntrypoints
	{
		[DllImport(Native.SteamApiLibraryName, EntryPoint = "SteamAPI_IsSteamRunning", CallingConvention = CallingConvention.Cdecl)]
		public static extern bool SteamAPI_IsSteamRunning();
	}

	public class SteamAPIInterop
	{
		[DllImport(Native.SteamApiLibraryName, EntryPoint = "SteamAPI_Init", CallingConvention = CallingConvention.Cdecl)]
		internal static extern bool SteamAPI_Init();

		[DllImport(Native.SteamApiLibraryName, EntryPoint = "SteamAPI_Shutdown", CallingConvention = CallingConvention.Cdecl)]
		internal static extern bool SteamAPI_Shutdown();
	}
}