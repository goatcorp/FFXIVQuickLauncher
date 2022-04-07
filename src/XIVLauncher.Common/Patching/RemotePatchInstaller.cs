using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using SharedMemory;
using XIVLauncher.Common.PatcherIpc;
using XIVLauncher.Common.Patching.ZiPatch;
using XIVLauncher.Common.Patching.ZiPatch.Util;

namespace XIVLauncher.Common.Patching;

public class RemotePatchInstaller
{
    private readonly RpcBuffer rpc;
    private readonly ConcurrentQueue<PatcherIpcStartInstall> queuedInstalls = new();
    private readonly Thread patcherThread;
    private readonly CancellationTokenSource patcherCancelToken = new();

    public bool IsDone { get; private set; }

    public RemotePatchInstaller(string rpcName)
    {
        this.rpc = new RpcBuffer(rpcName, RemoteCallHandler);

        Log.Information("[PATCHER] IPC connected");

        SendIpcMessage(new PatcherIpcEnvelope
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
                if (Process.GetProcesses().All(x => x.ProcessName != "XIVLauncher") && this.queuedInstalls.IsEmpty || !RunInstallQueue())
                {
                    IsDone = true;
                    return;
                }

                Thread.Sleep(1000);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[PATCHER] RemotePatchInstaller loop encountered an error");
            SendIpcMessage(new PatcherIpcEnvelope
            {
                OpCode = PatcherIpcOpCode.InstallFailed
            });
        }
    }

    private void RemoteCallHandler(ulong msgId, byte[] payload)
    {
        var json = IpcHelpers.Base64Decode(Encoding.ASCII.GetString(payload));
        Log.Information("[PATCHER] IPC({0}): {1}", msgId, json);

        var msg = JsonConvert.DeserializeObject<PatcherIpcEnvelope>(json, IpcHelpers.JsonSettings);

        switch (msg.OpCode)
        {
            case PatcherIpcOpCode.Bye:
                Task.Run(() =>
                {
                    Thread.Sleep(3000);
                    IsDone = true;
                });
                break;

            case PatcherIpcOpCode.StartInstall:

                var installData = (PatcherIpcStartInstall)msg.Data;
                this.queuedInstalls.Enqueue(installData);
                break;

            case PatcherIpcOpCode.Finish:
                var path = (DirectoryInfo)msg.Data;

                try
                {
                    VerToBck(path);
                    Log.Information("VerToBck done");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "VerToBck failed");
                    SendIpcMessage(new PatcherIpcEnvelope
                    {
                        OpCode = PatcherIpcOpCode.InstallFailed
                    });
                }

                break;
        }
    }

    private void SendIpcMessage(PatcherIpcEnvelope envelope)
    {
        try
        {
            var json = IpcHelpers.Base64Encode(JsonConvert.SerializeObject(envelope, IpcHelpers.JsonSettings));
            rpc.RemoteRequest(Encoding.ASCII.GetBytes(json));
        }
        catch (Exception e)
        {
            Log.Error(e, "[PATCHERIPC] Failed to send message");
        }
    }

    private bool RunInstallQueue()
    {
        if (this.queuedInstalls.TryDequeue(out var installData))
        {
            // Ensure that subdirs exist
            if (!installData.GameDirectory.Exists)
                installData.GameDirectory.Create();

            installData.GameDirectory.CreateSubdirectory("game");
            installData.GameDirectory.CreateSubdirectory("boot");

            try
            {
                InstallPatch(installData.PatchFile.FullName,
                    Path.Combine(installData.GameDirectory.FullName,
                        installData.Repo == Repository.Boot ? "boot" : "game"));

                try
                {
                    installData.Repo.SetVer(installData.GameDirectory, installData.VersionId);
                    SendIpcMessage(new PatcherIpcEnvelope
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
                    SendIpcMessage(new PatcherIpcEnvelope
                    {
                        OpCode = PatcherIpcOpCode.InstallFailed
                    });

                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PATCHER] Patch install failed");
                SendIpcMessage(new PatcherIpcEnvelope
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
            // Overwrite the old BCK with the new game version
            var ver = repository.GetVer(gamePath);

            try
            {
                repository.SetVer(gamePath, ver, true);
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