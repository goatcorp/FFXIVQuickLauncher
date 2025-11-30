using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Serilog;

namespace XIVLauncher.Common.Util;

/// <summary>
/// A set of utilities to help manage Unix sockets.
/// </summary>
internal static class SocketHelpers
{
    // Default probe timeout in milliseconds.
    private const int DefaultProbeMs = 200;

    /// <summary>
    /// Test whether a Unix socket is alive/listening.
    /// </summary>
    /// <param name="path">The path to test.</param>
    /// <param name="timeoutMs">How long to wait for a connection success.</param>
    /// <returns>A task result representing if a socket is alive or not.</returns>
    public static async Task<bool> IsSocketAlive(string path, int timeoutMs = DefaultProbeMs)
    {
        if (string.IsNullOrEmpty(path)) return false;

        var endpoint = new UnixDomainSocketEndPoint(path);
        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        var connectTask = client.ConnectAsync(endpoint);
        var completed = await Task.WhenAny(connectTask, Task.Delay(timeoutMs)).ConfigureAwait(false);

        if (completed == connectTask)
        {
            // Connected or failed very quickly. If the task is successful, the socket is alive.
            if (connectTask.IsCompletedSuccessfully)
            {
                try
                {
                    client.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                    // ignored
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Find and remove stale Dalamud RPC sockets.
    /// </summary>
    /// <param name="directory">The directory to scan for stale sockets.</param>
    /// <param name="searchPattern">The search pattern to find socket files.</param>
    /// <param name="probeTimeoutMs">The timeout to wait for a connection attempt to succeed.</param>
    /// <returns>A task that executes when sockets are purged.</returns>
    public static async Task CleanStaleSockets(string directory, string searchPattern = "DalamudRPC.*.sock", int probeTimeoutMs = DefaultProbeMs)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;

        foreach (var file in Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly))
        {
            // we don't need to check ourselves.
            if (file.Contains(Environment.ProcessId.ToString())) continue;

            bool shouldDelete;

            try
            {
                shouldDelete = !await IsSocketAlive(file, probeTimeoutMs);
            }
            catch
            {
                shouldDelete = true;
            }

            if (shouldDelete)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Could not delete stale socket file: {File}", file);
                }
            }
        }
    }
}
