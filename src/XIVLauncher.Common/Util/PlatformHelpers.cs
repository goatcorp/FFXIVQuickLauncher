using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace XIVLauncher.Common.Util;

public static class PlatformHelpers
{
    public static Platform GetPlatform()
    {
        if (EnvironmentSettings.IsWine)
            return Platform.Win32OnLinux;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return Platform.Linux;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Platform.Mac;
        else
            return Platform.Win32;

        // TODO(goat): Add mac here, once it's merged
    }

    /// <summary>
    ///     Generates a temporary file name.
    /// </summary>
    /// <returns>A temporary file name that is almost guaranteed to be unique.</returns>
    public static string GetTempFileName()
    {
        // https://stackoverflow.com/a/50413126
        return Path.Combine(Path.GetTempPath(), "xivlauncher_" + Guid.NewGuid());
    }

    public static void DeleteAndRecreateDirectory(DirectoryInfo dir)
    {
        if (!dir.Exists)
        {
            dir.Create();
        }
        else
        {
            dir.Delete(true);
            dir.Create();
        }
    }

    public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
    {
        foreach (var dir in source.GetDirectories())
            CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));

        foreach (var file in source.GetFiles())
            file.CopyTo(Path.Combine(target.FullName, file.Name));
    }

    public static void OpenBrowser(string url)
    {
        // https://github.com/dotnet/corefx/issues/10361
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            url = url.Replace("&", "^&");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    [DllImport("libc")]
    private static extern uint geteuid();

    public static bool IsElevated()
    {
        switch (Environment.OSVersion.Platform)
        {
            case PlatformID.Win32NT:
                return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

            case PlatformID.Unix:
                return geteuid() == 0;

            default:
                return false;
        }
    }

    /// <summary>
    /// Recursively create all directories in the specified path.
    /// </summary>
    /// <param name="dir"></param>
    public static void CreateDirectoryHierarchy(DirectoryInfo dir)
    {
        if (dir.Parent != null)
            CreateDirectoryHierarchy(dir.Parent);

        if (!dir.Exists)
            dir.Create();
    }

    /// <summary>
    /// Check if elevation is required to write to the specified directory.
    /// This is done by attempting to write a temporary file to the directory.
    ///
    /// Creates the directory if it does not exist.
    /// </summary>
    /// <param name="dir">The directory to check.</param>
    /// <returns>Whether elevation is required.</returns>
    public static bool IsElevationRequiredForWrite(DirectoryInfo dir)
    {
        string tempFn;

        do
        {
            tempFn = Path.Combine(dir.FullName, Guid.NewGuid().ToString());
        }
        while (File.Exists(tempFn));

        try
        {
            CreateDirectoryHierarchy(dir);
            File.WriteAllText(tempFn, "");
            File.Delete(tempFn);
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }

        return false;
    }

    public static void Untar(string path, string output)
    {
        var psi = new ProcessStartInfo("tar")
        {
            Arguments = $"-xf \"{path}\" -C \"{output}\""
        };

        var tarProcess = Process.Start(psi);

        if (tarProcess == null)
            throw new Exception("Could not start tar.");

        tarProcess.WaitForExit();

        if (tarProcess.ExitCode != 0)
            throw new Exception("Could not untar.");
    }

    private static readonly IPEndPoint DefaultLoopbackEndpoint = new(IPAddress.Loopback, port: 0);

    public static int GetAvailablePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        socket.Bind(DefaultLoopbackEndpoint);
        return ((IPEndPoint)socket.LocalEndPoint).Port;
    }

#if WIN32
    /*
     * WINE: The APIs DriveInfo uses are buggy on Wine. Let's just use the kernel32 API instead.
     */

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    public static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
                                                 out ulong lpFreeBytesAvailable,
                                                 out ulong lpTotalNumberOfBytes,
                                                 out ulong lpTotalNumberOfFreeBytes);

    public static long GetDiskFreeSpace(DirectoryInfo info)
    {
        if (info == null)
        {
            throw new ArgumentNullException(nameof(info));
        }

        ulong dummy = 0;

        if (!GetDiskFreeSpaceEx(info.FullName, out ulong freeSpace, out dummy, out dummy))
        {
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        }

        return (long)freeSpace;
    }
#else
        public static long GetDiskFreeSpace(DirectoryInfo info)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            DriveInfo drive = new DriveInfo(info.FullName);

            return drive.AvailableFreeSpace;
        }
#endif
}
