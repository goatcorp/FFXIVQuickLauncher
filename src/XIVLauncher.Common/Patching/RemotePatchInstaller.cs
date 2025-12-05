using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common.PatcherIpc;
using XIVLauncher.Common.Patching.Rpc;
using XIVLauncher.Common.Patching.ZiPatch;
using XIVLauncher.Common.Patching.ZiPatch.Util;

namespace XIVLauncher.Common.Patching;

public class RemotePatchInstaller
{
    private readonly IRpc rpc;
    private readonly ConcurrentQueue<PatcherIpcStartInstall> queuedInstalls = new();
    private readonly Thread patcherThread;
    private readonly CancellationTokenSource patcherCancelToken = new();

    public bool IsDone { get; private set; }

    public bool IsFailed { get; private set; }

    public bool HasQueuedInstalls => !this.queuedInstalls.IsEmpty;

    public RemotePatchInstaller(IRpc rpc)
    {
        this.rpc = rpc;
        this.rpc.MessageReceived += RemoteCallHandler;

        Log.Information("[PATCHER] IPC connected");

        rpc.SendMessage(new PatcherIpcEnvelope
        {
            OpCode = PatcherIpcOpCode.Hello,
            Data = DateTime.Now
        });

        Log.Information("[PATCHER] sent hello");

        this.patcherThread = new Thread(this.ProcessPatches);
    }

    public void Start()
    {
        this.patcherThread.Start();
    }

    private void ProcessPatches()
    {
        try
        {
            while (!this.patcherCancelToken.IsCancellationRequested)
            {
                if (!RunInstallQueue())
                {
                    IsFailed = true;
                    return;
                }

                Thread.Sleep(1000);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[PATCHER] RemotePatchInstaller loop encountered an error");
            this.rpc.SendMessage(new PatcherIpcEnvelope
            {
                OpCode = PatcherIpcOpCode.InstallFailed
            });
        }
    }

    private void RemoteCallHandler(PatcherIpcEnvelope envelope)
    {
        try
        {
            switch (envelope.OpCode)
            {
                case PatcherIpcOpCode.Bye:
                    Task.Run(() =>
                    {
                        Thread.Sleep(3000);
                        IsDone = true;
                    });
                    break;

                case PatcherIpcOpCode.StartInstall:

                    var installData = (PatcherIpcStartInstall)envelope.Data;
                    this.queuedInstalls.Enqueue(installData);
                    break;

                case PatcherIpcOpCode.Finish:
                    var path = new DirectoryInfo((string)envelope.Data);

                    try
                    {
                        VerToBck(path);
                        Log.Information("[PATCHER] VerToBck done");
                        this.rpc.SendMessage(new PatcherIpcEnvelope
                        {
                            OpCode = PatcherIpcOpCode.Finish
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[PATCHER] VerToBck failed");
                        this.rpc.SendMessage(new PatcherIpcEnvelope
                        {
                            OpCode = PatcherIpcOpCode.InstallFailed
                        });
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(envelope.OpCode), $"Unknown RPC opcode {envelope.OpCode}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[PATCHER] RemoteCallHandler encountered an error");
            this.rpc.SendMessage(new PatcherIpcEnvelope
            {
                OpCode = PatcherIpcOpCode.InstallFailed
            });
        }
    }

    private static void EnsureGameDirectories(DirectoryInfo gamePath)
    {
        if (!gamePath.Exists)
            gamePath.Create();

        // DirectoryInfo.CreateSubdirectory() has regressed in .NET Core and does not
        // work on drive roots. There seemingly are a bunch of people that do this,
        // so we have to support it here for existing installations.
        // https://github.com/dotnet/runtime/issues/116087
        var gameDir = Path.Combine(gamePath.FullName, "game");
        if (!Directory.Exists(gameDir))
            Directory.CreateDirectory(gameDir);

        var bootDir = Path.Combine(gamePath.FullName, "boot");
        if (!Directory.Exists(bootDir))
            Directory.CreateDirectory(bootDir);
    }

    private bool RunInstallQueue()
    {
        if (this.queuedInstalls.TryDequeue(out var installData))
        {
            EnsureGameDirectories(installData.GameDirectory);

            try
            {
                InstallPatch(installData.PatchFile.FullName,
                    Path.Combine(installData.GameDirectory.FullName,
                        installData.Repo == Repository.Boot ? "boot" : "game"));

                try
                {
                    installData.Repo.SetVer(installData.GameDirectory, installData.VersionId);
                    this.rpc.SendMessage(new PatcherIpcEnvelope
                    {
                        OpCode = PatcherIpcOpCode.InstallOk
                    });

                    try
                    {
                        if (!installData.KeepPatch)
                            installData.PatchFile.Delete();
                    }
                    catch (Exception exception)
                    {
                        Log.Error(exception, "Could not delete patch file");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Could not set ver file");
                    this.rpc.SendMessage(new PatcherIpcEnvelope
                    {
                        OpCode = PatcherIpcOpCode.InstallFailed
                    });

                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PATCHER] Patch install failed");
                this.rpc.SendMessage(new PatcherIpcEnvelope
                {
                    OpCode = PatcherIpcOpCode.InstallFailed
                });

                return false;
            }
        }

        return true;
    }

    public static void InstallPatch(string patchPath, string gamePath)
    {
        Log.Information("[PATCHER] Installing {0} to {1}", patchPath, gamePath);

        using var patchFile = ZiPatchFile.FromFileName(patchPath);

        using (var store = new SqexFileStreamStore())
        {
            var config = new ZiPatchConfig(gamePath) { Store = store };

            foreach (var chunk in patchFile.GetChunks())
                chunk.ApplyChunk(config);
        }

        Log.Information("[PATCHER] Patch {0} installed", patchPath);
    }

    private static void VerToBck(DirectoryInfo gamePath)
    {
        Thread.Sleep(500);

        foreach (var repository in Enum.GetValues(typeof(Repository)).Cast<Repository>())
        {
            // We haven't installed a patch for this repository yet
            if (!repository.GetVerFile(gamePath).Exists)
                continue;

            // Overwrite the old BCK with the new game version
            var ver = repository.GetVer(gamePath);

            try
            {
                repository.SetVer(gamePath, ver, true);
                Log.Information("[PATCHER] Copied {RepoName} to BCK for version {Ver}", repository, ver);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PATCHER] Could not copy to BCK");

                if (ver != Constants.BASE_GAME_VERSION)
                    throw;
            }
        }
    }
}
