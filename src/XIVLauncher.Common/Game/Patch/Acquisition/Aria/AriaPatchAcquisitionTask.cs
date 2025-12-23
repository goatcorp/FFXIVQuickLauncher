using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Aria;

namespace XIVLauncher.Common.Game.Patch.Acquisition.Aria;

public class AriaPatchAcquisitionTask(AriaManager manager, string url, FileInfo target) : PatchAcquisitionTask
{
    private readonly CancellationTokenSource cts = new();
    private string? gid;

    public override async Task StartAsync()
    {
        await manager.AddUri([url], new Dictionary<string, string>
        {
            { "user-agent", Constants.PatcherUserAgent },
            { "out", target.Name },
            { "dir", target.Directory!.FullName },
            { "max-connection-per-server", "8" },
            { "max-tries", "100" },
            { "auto-file-renaming", "false" },
            { "allow-overwrite", "true" },
        }).ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully)
            {
                Log.Error(t.Exception, "[ARIA] Could not send download RPC for {Url}", url);
                OnComplete(AcquisitionResult.Error);
                return;
            }

            this.gid = t.Result;

            Log.Verbose("[ARIA] GID# {Gid} for {Url}", this.gid, url);

            _ = Task.Run(async () =>
            {
                while (!this.cts.IsCancellationRequested)
                {
                    try
                    {
                        var status = await manager.GetStatus(this.gid);

                        if (status.Status == "complete")
                        {
                            Log.Verbose($"[ARIA] GID# {this.gid} for {url} SUCCESS");

                            this.OnComplete(AcquisitionResult.Success);
                            return;
                        }

                        if (status.Status == "removed")
                        {
                            Log.Verbose("[ARIA] GID# {Gid} for {Url} CANCEL", this.gid, url);

                            this.OnComplete(AcquisitionResult.Cancelled);
                            return;
                        }

                        if (status.Status == "error")
                        {
                            Log.Verbose("[ARIA] GID# {Gid} for {Url} FAULTED", this.gid, url);

                            this.OnComplete(AcquisitionResult.Error);
                            return;
                        }

                        this.OnProgressChanged(new AcquisitionProgress
                        {
                            BytesPerSecondSpeed = long.Parse(status.DownloadSpeed),
                            Progress = long.Parse(status.CompletedLength),
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[ARIA] Failed to get status for GID# {Gid} ({Url})", this.gid, url);
                        this.OnComplete(AcquisitionResult.Error);
                        return;
                    }

                    Thread.Sleep(500);
                }
            }, this.cts.Token);
        });
    }

    public override async Task CancelAsync()
    {
        this.cts.Cancel();

        try
        {
            await manager.PauseTask(this.gid);
        }
        catch
        {
            // ignored
        }
    }
}
