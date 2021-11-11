using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AriaNet;
using Serilog;
using XIVLauncher.Game.Patch.PatchList;

namespace XIVLauncher.Game.Patch.Acquisition.Aria
{
    public class AriaHttpPatchAcquisition : PatchAcquisition
    {
        private static Process ariaProcess;
        private static AriaManager manager;
        private static long maxDownloadSpeed;

        public const int DEFAULT_ARIA_SERVER_PORT = 0xff45;

        public static async Task InitializeAsync(long maxDownloadSpeed)
        {
            AriaHttpPatchAcquisition.maxDownloadSpeed = maxDownloadSpeed;

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

                // I don't really see the point of this, but aria complains if we don't provide a secret
                var rng = new Random();
                var secret = BitConverter.ToString(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes($"{rng.Next()}{rng.Next()}{rng.Next()}{rng.Next()}")));

                var ariaPath = Path.Combine(Paths.ResourcesPath, "aria2c-xl.exe");

                var ariaPort = Util.GetAvailablePort(DEFAULT_ARIA_SERVER_PORT);
                var ariaHost = $"http://localhost:{ariaPort}/jsonrpc";

                var ariaArgs =
                    $"--enable-rpc --rpc-secret={secret} --rpc-listen-port={ariaPort} --log=\"{Path.Combine(Paths.RoamingPath, "aria.log")}\" --log-level=notice --max-connection-per-server=8 --auto-file-renaming=false --allow-overwrite=true";

                Log.Verbose($"[ARIA] Aria process not there, creating from {ariaPath} {ariaArgs}...");

                var startInfo = new ProcessStartInfo(ariaPath, ariaArgs)
                {
#if !DEBUG
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
#endif
                    UseShellExecute = true
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
            if (ariaProcess is {HasExited: false})
            {
                try
                {
                    await manager.Shutdown();
                }
                catch (Exception)
                {
                    // ignored
                }

                Thread.Sleep(1000);

                if (!ariaProcess.HasExited)
                    ariaProcess.Kill();
            }
        }

        public override async Task StartDownloadAsync(PatchListEntry patch, FileInfo outFile)
        {
            await manager.AddUri(new List<string>()
            {
                patch.Url
            }, new Dictionary<string, string>()
            {
                {"user-agent", "FFXIV PATCH CLIENT"},
                {"out", outFile.Name},
                {"dir", outFile.Directory.FullName},
                {"max-connection-per-server", "8"},
                {"max-tries", "100"},
                {"max-download-limit", maxDownloadSpeed.ToString()},
                {"auto-file-renaming", "false"},
                {"allow-overwrite", "true"},
            }).ContinueWith(t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                {
                    Log.Error(t.Exception, $"[ARIA] Could not send download RPC for {patch}");
                    OnComplete(AcquisitionResult.Error);
                    return;
                }

                var gid = t.Result;

                Log.Verbose($"[ARIA] GID# {gid} for {patch}");

                var _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            var status = await manager.GetStatus(gid);

                            if (status.Status == "complete")
                            {
                                Log.Verbose($"[ARIA] GID# {gid} for {patch} SUCCESS");

                                OnComplete(AcquisitionResult.Success);
                                return;
                            }

                            if (status.Status == "removed")
                            {
                                Log.Verbose($"[ARIA] GID# {gid} for {patch} CANCEL");

                                OnComplete(AcquisitionResult.Cancelled);
                                return;
                            }

                            if (status.Status == "error")
                            {
                                Log.Verbose($"[ARIA] GID# {gid} for {patch} FAULTED");

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
                            Log.Error(ex, $"[ARIA] Failed to get status for GID# {gid} ({patch})");
                        }

                        Thread.Sleep(500);
                    }
                });
            });
        }

        public override async Task CancelAsync()
        {
            await manager.PauseAllTasks();
        }
    }
}