using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
using XIVLauncher.PatchInstaller.PatcherIpcMessages;
using XIVLauncher.PatchInstaller.ZiPatch;
using XIVLauncher.PatchInstaller.ZiPatch.Util;

namespace XIVLauncher.PatchInstaller
{
    public class PatcherMain
    {
        public const string BASE_GAME_VERSION = "2012.01.01.0000.0000";

        private static RpcBuffer _rpc;

        public static JsonSerializerSettings JsonSettings = new()
        {
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
            TypeNameHandling = TypeNameHandling.All
        };

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
                        InstallPatch(args[1], args[2]);
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

                if (args.Length == 0 || args[0] != "rpc")
                {
                    Log.Information("usage: XIVLauncher.PatchInstaller.exe install <patch> <game dir>\n" +
                                    "OR\n" +
                                    "usage: XIVLauncher.PatchInstaller.exe <server port> <client port>");

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
            var json = Base64Decode(Encoding.ASCII.GetString(payload));
            Log.Information("[PATCHER] IPC({0}): {1}", msgId, json);

            var msg = JsonConvert.DeserializeObject<PatcherIpcEnvelope>(json, JsonSettings);

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

        private static void SendIpcMessage(PatcherIpcMessages.PatcherIpcEnvelope envelope)
        {
            try
            {
                var json = PatcherMain.Base64Encode(JsonConvert.SerializeObject(envelope, PatcherMain.JsonSettings));

                Log.Information("[PATCHERIPC] SEND: " + json);
                _rpc.RemoteRequest(Encoding.ASCII.GetBytes(json));
            }
            catch (Exception e)
            {
                Log.Error(e, "[PATCHERIPC] Failed to send message.");
            }
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
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

                    if (ver != BASE_GAME_VERSION)
                        throw;
                }
            }
        }
    }
}