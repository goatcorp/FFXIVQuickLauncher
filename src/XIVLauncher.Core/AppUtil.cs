using System.Diagnostics;
using System.Reflection;

namespace XIVLauncher.Core;

public static class AppUtil
{
    public static byte[] GetEmbeddedResourceBytes(string resourceName)
    {
        var assembly = typeof(ImGuiBindings).Assembly;

        using var s = assembly.GetManifestResourceStream(resourceName);
        if (s == null)
            throw new ArgumentException($"Resource {resourceName} not found", nameof(resourceName));

        var ret = new byte[s.Length];
        s.Read(ret, 0, (int)s.Length);
        return ret;
    }

    /// <summary>
    ///     Gets the git hash value from the assembly
    ///     or null if it cannot be found.
    /// </summary>
    public static string GetGitHash()
    {
        var asm = typeof(AppUtil).Assembly;
        var attrs = asm.GetCustomAttributes<AssemblyMetadataAttribute>();
        return attrs.FirstOrDefault(a => a.Key == "GitHash")?.Value;
    }

    /// <summary>
    ///     Gets the build origin from the assembly
    ///     or null if it cannot be found.
    /// </summary>
    public static string GetBuildOrigin()
    {
        var asm = typeof(AppUtil).Assembly;
        var attrs = asm.GetCustomAttributes<AssemblyMetadataAttribute>();
        return attrs.FirstOrDefault(a => a.Key == "BuildOrigin")?.Value;
    }

    public static string GetAssemblyVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
        return fvi.FileVersion;
    }
}