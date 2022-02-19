using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Serilog;
using SharedMemory;
using XIVLauncher.Common;
using XIVLauncher.Common.PatcherIpc;
using XIVLauncher.Common.Patching.IndexedZiPatch;
using XIVLauncher.Common.Patching.ZiPatch;
using XIVLauncher.Common.Patching.ZiPatch.Util;

namespace XIVLauncher.PatchInstaller
{
    public class Program
    {
        private static RpcBuffer _rpc;

        private static readonly ConcurrentQueue<PatcherIpcStartInstall> _queuedInstalls = new();

        static void Main(string[] args)
        {
            try
            {
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.File(Path.Combine(Paths.RoamingPath, "patcher.log"))
                    .WriteTo.Debug()
                    .MinimumLevel.Verbose()
                    .CreateLogger();


                if (args.Length > 1 && args[0] == "install")
                {
                    try
                    {
                        foreach (var file in args.Skip(1).Take(args.Length - 2).ToList())
                            InstallPatch(file, args[args.Length - 1]);
                        Log.Information("OK");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Patch installation failed.");
                        Environment.Exit(-1);
                    }

                    Environment.Exit(0);
                    return;
                }

                if (args.Length > 1 && args[0] == "index-create")
                {
                    try
                    {
                        IndexedZiPatchOperations.CreateZiPatchIndices(int.Parse(args[1]), args.Skip(2).ToList()).Wait();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to create patch index files.");
                        Environment.Exit(-1);
                    }

                    Environment.Exit(0);
                    return;
                }

                if (args.Length > 2 && args[0] == "index-verify")
                {
                    try
                    {
                        IndexedZiPatchOperations.VerifyFromZiPatchIndex(args[1], args[2]).Wait();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to verify from patch index file.");
                        Environment.Exit(-1);
                    }

                    Environment.Exit(0);
                    return;
                }

                if (args.Length > 2 && args[0] == "index-repair")
                {
                    try
                    {
                        IndexedZiPatchOperations.RepairFromPatchFileIndexFromFile(args[1], args[2], args[3], 8).Wait();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to repair from patch index file.");
                        Environment.Exit(-1);
                    }

                    Environment.Exit(0);
                    return;
                }

                if (args.Length > 2 && args[0] == "index-rpc")
                {
                    new IndexedZiPatchIndexRemoteInstaller.WorkerSubprocessBody(int.Parse(args[1]), args[2]).RunToDisposeSelf();
                    return;
                }

                if (args.Length > 0 && args[0] == "index-rpc-test")
                {
                    IndexedZiPatchIndexRemoteInstaller.Test();
                    return;
                }

                if (args.Length == 0 || args[0] != "rpc")
                {
                    Log.Information("usage:\n" +
                                    "* XIVLauncher.PatchInstaller.exe install <oldest>.patch <oldest2>.patch ... <newest>.patch <game dir>\n" +
                                    "  * Install patch files in the given order.\n" +
                                    "* XIVLauncher.PatchInstaller.exe index-create <expac version; -1 for boot> <oldest>.patch <oldest2>.patch ... <newest>.patch\n" +
                                    "  * Index game patch files in the given order.\n" +
                                    "* XIVLauncher.PatchInstaller.exe index-verify <patch index file> <game dir>\n" +
                                    "  * Verify game installation from patch file index.\n" +
                                    "* XIVLauncher.PatchInstaller.exe index-repair <patch index file> <game dir> <patch file directory>\n" +
                                    "  * Verify and repair game installation from patch file index, looking for patch files in given patch file directory.\n" +
                                    "* XIVLauncher.PatchInstaller.exe <server port> <client port>");

                    Environment.Exit(-1);
                    return;
                }

                _rpc = new RpcBuffer(args[1], RemoteCallHandler);

                Log.Information("[PATCHER] IPC connected");

                SendIpcMessage(new PatcherIpcEnvelope
                {
                    OpCode = PatcherIpcOpCode.Hello,
                    Data = DateTime.Now
                });

                Log.Information("[PATCHER] sent hello");

                try
                {
                    while (true)
                    {
                        if ((Process.GetProcesses().All(x => x.ProcessName != "XIVLauncher") && _queuedInstalls.IsEmpty) || !RunInstallQueue())
                        {
                            Environment.Exit(0);
                            return;
                        }

                        Thread.Sleep(1000);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "PatcherMain loop encountered an error.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Patcher init failed.\n\n" + ex, "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void RemoteCallHandler(ulong msgId, byte[] payload)
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
                        Environment.Exit(0);
                    });
                    break;

                case PatcherIpcOpCode.StartInstall:

                    var installData = (PatcherIpcStartInstall) msg.Data;
                    _queuedInstalls.Enqueue(installData);
                    break;

                case PatcherIpcOpCode.Finish:
                    var path = (DirectoryInfo) msg.Data;
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

        private static void SendIpcMessage(PatcherIpcEnvelope envelope)
        {
            try
            {
                var json = IpcHelpers.Base64Encode(JsonConvert.SerializeObject(envelope, IpcHelpers.JsonSettings));

                Log.Information("[PATCHERIPC] SEND: " + json);
                _rpc.RemoteRequest(Encoding.ASCII.GetBytes(json));
            }
            catch (Exception e)
            {
                Log.Error(e, "[PATCHERIPC] Failed to send message.");
            }
        }

        private static bool RunInstallQueue()
        {
            if (_queuedInstalls.TryDequeue(out var installData))
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
                            Log.Error(exception, "Could not delete patch file.");
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
                    Log.Error(ex, "PATCH INSTALL FAILED");
                    SendIpcMessage(new PatcherIpcEnvelope
                    {
                        OpCode = PatcherIpcOpCode.InstallFailed
                    });

                    return false;
                }
            }

            return true;
        }

        private static void InstallPatch(string patchPath, string gamePath)
        {
            Log.Information("Installing {0} to {1}", patchPath, gamePath);

            using var patchFile = ZiPatchFile.FromFileName(patchPath);

            using (var store = new SqexFileStreamStore())
            {
                var config = new ZiPatchConfig(gamePath) { Store = store };

                foreach (var chunk in patchFile.GetChunks())
                    chunk.ApplyChunk(config);
            }

            Log.Information("Patch {0} installed", patchPath);
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
                    Log.Error(ex, "Could not copy to BCK");

                    if (ver != Constants.BASE_GAME_VERSION)
                        throw;
                }
            }
        }
    }
}