using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using SteamworksSharp.Native.Libs;

namespace SteamworksSharp.Native
{
    public static class SteamNative
    {
        private static readonly List<INativeLibrary> NativeLibraries = new List<INativeLibrary>
		{
            (INativeLibrary)Activator.CreateInstance(typeof(Windows_x86.NativeLibrary)),
			(INativeLibrary)Activator.CreateInstance(typeof(Windows_x64.NativeLibrary))
		};

        private static string CurrentLocation { get; } = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

        public static void Initialize()
        {
            // Write needed native libraries to the disk.
            WriteNativeLibrary();
        }

		public static void Uninitialize()
		{
			var appIDPath = Path.Combine(CurrentLocation, "steam_appid.txt");

			if (File.Exists(appIDPath))
				File.Delete(appIDPath);
		}

        private static void WriteNativeLibrary()
        {
            var currentArchitecture = RuntimeInformation.ProcessArchitecture;

#if NET452 || NET46 || NET461 || NET462 || NET47 || NET471
            if (Environment.Is64BitProcess)
            {
                currentArchitecture = Architecture.X64;
            }
#endif

            var currentPlatform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? OSPlatform.Windows
                : (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? OSPlatform.Linux
                    : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                        ? OSPlatform.OSX
                        : throw new Exception("Unknown OSPlatform."));
            
            var nativeLibrary = NativeLibraries.FirstOrDefault(x =>
                x.Architecture == currentArchitecture && 
                x.Platform == currentPlatform);

            if (nativeLibrary == null)
            {
                throw new Exception("SteamNative is unable to find native library " +
                                    $"for platform {currentPlatform} and architecture {currentArchitecture}, " +
                                    "have you installed the correct NuGet packages?");
            }

            var destination = Path.Combine(CurrentLocation, $"steam_api.{nativeLibrary.Extension}");

            File.WriteAllBytes(destination, nativeLibrary.LibraryBytes.Value);
        }
    }
}