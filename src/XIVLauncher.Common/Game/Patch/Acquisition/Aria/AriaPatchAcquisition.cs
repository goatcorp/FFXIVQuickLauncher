using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Aria;
using XIVLauncher.Aria.Attributes;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Game.Patch.Acquisition.Aria;

public class AriaPatchAcquisition(FileInfo logFile) : IPatchAcquisition, IDisposable
{
    private Process? ariaProcess;
    private AriaManager? manager;

    public async Task StartIfNeededAsync(long speedLimitBps)
    {
        if (ariaProcess == null || ariaProcess.HasExited)
        {
            // Kill stray aria2c-xl processes
            var stray = Process.GetProcessesByName("aria2c-xl");

            foreach (var process in stray)
            {
                try
                {
                    process.Kill();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[ARIA] Could not kill stray process.");
                }
            }

            var rng = new Random();
            var secret = BitConverter.ToString(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes($"{rng.Next()}{rng.Next()}{rng.Next()}{rng.Next()}")));

            var ariaPath = Path.Combine(Paths.ResourcesPath, "aria2c-xl.exe");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ariaPath = "aria2c";
            }

            var ariaPort = PlatformHelpers.GetAvailablePort();
            var ariaHost = $"http://localhost:{ariaPort}/jsonrpc";

            var ariaArgs =
                $"--enable-rpc --rpc-secret={secret} --rpc-listen-port={ariaPort} --log=\"{logFile.FullName}\" --log-level=notice --max-overall-download-limit={speedLimitBps} --max-connection-per-server=8 --auto-file-renaming=false --allow-overwrite=true";

            Log.Verbose($"[ARIA] Aria process not there, creating from {ariaPath} {ariaArgs}...");

            var startInfo = new ProcessStartInfo(ariaPath, ariaArgs)
            {
                UseShellExecute = false,
            };

            if (!DebugHelpers.IsDebugBuild)
            {
                startInfo.CreateNoWindow = true;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }

            ariaProcess = Process.Start(startInfo);

            Thread.Sleep(400);

            if (ariaProcess == null)
                throw new Exception("ariaProcess was null.");

            if (ariaProcess.HasExited)
                throw new Exception("ariaProcess has exited.");

            manager = new AriaManager(secret, ariaHost);

            for (var tries = 3; tries >= 0; tries--)
            {
                try
                {
                    var versionInfo = await this.manager.GetVersion();
                    Log.Debug("connected to aria2 {Version}", versionInfo.Version);
                    return;
                }
                catch (Exception)
                {
                    // ignored
                }

                await Task.Delay(1000);
            }

            throw new Exception("Could not contact aria2");
        }
    }

    public async Task SetGlobalSpeedLimitAsync(long speedLimitBps)
    {
        if (manager == null)
            throw new InvalidOperationException("AriaManager not initialized.");

        await manager.ChangeGlobalOption(new AriaOption
        {
            MaxOverallDownloadLimit = speedLimitBps.ToString()
        });
    }

    public PatchAcquisitionTask MakeTask(string url, FileInfo outFile)
    {
        if (manager == null)
            throw new InvalidOperationException("AriaManager not initialized.");

        return new AriaPatchAcquisitionTask(this.manager, url, outFile);
    }

    public void Dispose()
    {
        if (ariaProcess is null or { HasExited: true })
            return;

        var shutdownTask = Task.Run(async () =>
        {
            if (manager != null)
            {
                try
                {
                    await manager.Shutdown();
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            var tries = 20;

            while (!ariaProcess.HasExited && tries >= 0)
            {
                await Task.Delay(50);
                tries--;
            }

            if (!ariaProcess.HasExited)
                ariaProcess.Kill();
        }).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
                return;

            Log.Error(t.Exception, "Could not shut down aria2");
        });

        // Block and wait
        shutdownTask.GetAwaiter().GetResult();
    }
}
