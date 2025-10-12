using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AriaNet;
using AriaNet.Attributes;
using Serilog;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Game.Patch.Acquisition.Aria
{
    public class AriaHttpPatchAcquisition : PatchAcquisition
    {
        private static Process? ariaProcess;
        private static AriaManager? manager;

        public static async Task InitializeAsync(long bytesPerSecond, FileInfo logFile)
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
                    $"--enable-rpc --rpc-secret={secret} --rpc-listen-port={ariaPort} --log=\"{logFile.FullName}\" --log-level=notice --max-overall-download-limit={bytesPerSecond} --max-connection-per-server=8 --auto-file-renaming=false --allow-overwrite=true";

                Log.Verbose($"[ARIA] Aria process not there, creating from {ariaPath} {ariaArgs}...");

                var startInfo = new ProcessStartInfo(ariaPath, ariaArgs)
                {
#if !DEBUG
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
#endif
                    UseShellExecute = false,
                };

                ariaProcess = Process.Start(startInfo);

                Thread.Sleep(400);

                if (ariaProcess == null)
                    throw new Exception("ariaProcess was null.");

                if (ariaProcess.HasExited)
                    throw new Exception("ariaProcess has exited.");

                manager = new AriaManager(secret, ariaHost);
            }
        }

        public static async Task UnInitializeAsync()
        {
            if (ariaProcess is { HasExited: false })
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

                await Task.Delay(1000);

                if (!ariaProcess.HasExited)
                    ariaProcess.Kill();
            }
        }

        public override async Task StartDownloadAsync(string url, FileInfo outFile)
        {
            await manager.AddUri([url], new Dictionary<string, string>
            {
                { "user-agent", Constants.PatcherUserAgent },
                { "out", outFile.Name },
                { "dir", outFile.Directory!.FullName },
                { "max-connection-per-server", "8" },
                { "max-tries", "100" },
                { "auto-file-renaming", "false" },
                { "allow-overwrite", "true" },
            }).ContinueWith(t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                {
                    Log.Error(t.Exception, $"[ARIA] Could not send download RPC for {url}");
                    OnComplete(AcquisitionResult.Error);
                    return;
                }

                var gid = t.Result;

                Log.Verbose($"[ARIA] GID# {gid} for {url}");

                var _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            var status = await manager.GetStatus(gid);

                            if (status.Status == "complete")
                            {
                                Log.Verbose($"[ARIA] GID# {gid} for {url} SUCCESS");

                                OnComplete(AcquisitionResult.Success);
                                return;
                            }

                            if (status.Status == "removed")
                            {
                                Log.Verbose($"[ARIA] GID# {gid} for {url} CANCEL");

                                OnComplete(AcquisitionResult.Cancelled);
                                return;
                            }

                            if (status.Status == "error")
                            {
                                Log.Verbose($"[ARIA] GID# {gid} for {url} FAULTED");

                                OnComplete(AcquisitionResult.Error);
                                return;
                            }

                            OnProgressChanged(new AcquisitionProgress
                            {
                                BytesPerSecondSpeed = long.Parse(status.DownloadSpeed),
                                Progress = long.Parse(status.CompletedLength),
                            });
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"[ARIA] Failed to get status for GID# {gid} ({url})");
                        }

                        Thread.Sleep(500);
                    }
                });
            });
        }

        public override async Task CancelAsync()
        {
            if (manager == null)
                throw new InvalidOperationException("AriaManager not initialized.");

            await manager.PauseAllTasks();
        }

        public static async Task SetDownloadSpeedLimit(long bytesPerSecond)
        {
            if (manager == null)
                throw new InvalidOperationException("AriaManager not initialized.");

            await manager.ChangeGlobalOption(new AriaOption
            {
                MaxOverallDownloadLimit = bytesPerSecond.ToString()
            });
        }
    }
}
