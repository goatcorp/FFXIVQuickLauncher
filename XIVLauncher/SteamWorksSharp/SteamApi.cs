using System.IO;
using System.Reflection;
using System.Text;
using Valve.Interop;

namespace SteamworksSharp
{
	public static class SteamApi
	{
		/// <summary>
		///     This method must be called before doing anything else.
		/// </summary>
		/// <returns>True if initialized successfully.</returns>
		public static bool Initialize(int appId = -1)
		{
			// Write app id to file if set.
			if (appId > 0)
			{
				var currentLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

				File.WriteAllText(Path.Combine(currentLocation, "steam_appid.txt"), $"{appId}", Encoding.ASCII);
			}

			// Initialize native library.
			return SteamAPIInterop.SteamAPI_Init();
		}

		public static void Uninitialize()
		{
			SteamAPIInterop.SteamAPI_Shutdown();
		}

		public static bool IsSteamRunning()
		{
			return NativeEntrypoints.SteamAPI_IsSteamRunning();
		}
	}
}