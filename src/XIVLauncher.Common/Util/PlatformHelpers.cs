using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using Serilog;

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

    /// <summary>
    ///     Deletes a temporary file without throwing, for use in finally blocks.
    /// </summary>
    /// <param name="path">The file to delete. May not exist.</param>
    public static void DeleteTempFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not delete temp file {Path}", path);
        }
    }

    public static void DeleteAndRecreateDirectory(DirectoryInfo dir)
    {
        if (!dir.Exists)
        {
            dir.Create();
        }
        else
        {
            ForceDeleteDirectory(dir);
            dir.Create();
        }
    }

    private const int DeleteTries = 3;
    private static readonly TimeSpan DeleteRetryDelay = TimeSpan.FromSeconds(1);

    internal static void ForceDeleteDirectory(DirectoryInfo dir)
    {
        for (var tries = 1;; tries++)
        {
            dir.Refresh();

            if (!dir.Exists)
            {
                return;
            }

            try
            {
                ClearReadOnly(dir);
                dir.Delete(true);
                return;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException && tries < DeleteTries)
            {
                Log.Warning(ex, "Could not delete {Path}, try {Try}/{MaxTries}", dir.FullName, tries, DeleteTries);
                Thread.Sleep(DeleteRetryDelay);
            }
        }
    }

    private static void ClearReadOnly(DirectoryInfo dir)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var entries = dir.EnumerateFileSystemInfos("*", SearchOption.AllDirectories).Append(dir);

        foreach (var entry in entries)
        {
            if (!entry.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                continue;
            }

            entry.Attributes &= ~FileAttributes.ReadOnly;
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
        if (OperatingSystem.IsWindows())
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return geteuid() == 0;
        }

        return false;
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

        if (!OperatingSystem.IsWindows())
        {
            return new DriveInfo(info.FullName).AvailableFreeSpace;
        }

        ulong dummy = 0;

        if (!GetDiskFreeSpaceEx(info.FullName, out ulong freeSpace, out dummy, out dummy))
        {
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        }

        return (long)freeSpace;
    }
}
