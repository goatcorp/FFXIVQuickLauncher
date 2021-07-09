using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

        public static async Task InitializeAsync(long maxDownloadSpeed)
        {
            AriaHttpPatchAcquisition.maxDownloadSpeed = maxDownloadSpeed;

            if (ariaProcess == null || ariaProcess.HasExited)
            {
                // I don't really see the point of this, but aria complains if we don't provide a secret
                var rng = new Random();
                var secret = BitConverter.ToString(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes($"{rng.Next()}{rng.Next()}{rng.Next()}{rng.Next()}")));

                var ariaPath = Path.Combine(Paths.ResourcesPath, "aria2c.exe");

                Log.Verbose($"[ARIA] Aria process not there, creating from {ariaPath}...");

                var startInfo = new ProcessStartInfo(ariaPath, $"--enable-rpc --rpc-secret={secret} --log=\"{Path.Combine(Paths.RoamingPath, "aria.log")}\" --log-level=notice --max-connection-per-server=8")
                {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true
                };

                ariaProcess = Process.Start(startInfo);

                Thread.Sleep(400);

                if (ariaProcess == null)
                    throw new Exception("ariaProcess was null.");

                if (ariaProcess.HasExited)
                    throw new Exception("ariaProcess has exited.");

                manager = new AriaManager(secret);
            }
        }

        public static async Task UnInitializeAsync()
        {
            if (ariaProcess is {HasExited: false})
            {
                await manager.Shutdown();
            }
        }

        public override async Task StartDownloadAsync(PatchListEntry patch, FileInfo outFile)
        {
            var gid = await manager.AddUri(new List<string>()
            {
                patch.Url
            }, new Dictionary<string, string>()
            {
                {"user-agent", "FFXIV PATCH CLIENT"},
                {"out", outFile.Name},
                {"dir", outFile.Directory.FullName},
                {"max-connection-per-server", "8"},
                {"max-tries", "100"},
                {"max-download-limit", maxDownloadSpeed.ToString()}
            });

            Log.Verbose($"[ARIA] GID# {gid} for {patch}");

            var _ = Task.Run(async () =>
            {
                try
                {
                    while (true)
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

                        Thread.Sleep(500);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"[ARIA] Failed to get status for GID# {gid} ({patch})");
                }
                
            });
        }

        public override async Task CancelAsync()
        {
            await manager.PauseAllTasks();
        }
    }
}
