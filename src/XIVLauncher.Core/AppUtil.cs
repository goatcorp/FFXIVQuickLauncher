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
}