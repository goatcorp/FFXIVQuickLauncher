using System.IO;
using System.Reflection;

namespace SteamworksSharp.Native.Libs
{
	internal static class LibUtils
	{
		public static byte[] ReadResourceBytes(Assembly assembly, string resourceName)
		{
			using (var stream = assembly.GetManifestResourceStream(resourceName))
			using (var reader = new BinaryReader(stream))
			{
				return reader.ReadBytes((int)stream.Length);
			}
		}
	}
}