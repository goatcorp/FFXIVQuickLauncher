using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.PatchInstaller.PatcherIpcMessages;
using XIVLauncher.PatchInstaller.ZiPatch;
using XIVLauncher.PatchInstaller.ZiPatch.Util;
using ZetaIpc.Runtime.Client;
using ZetaIpc.Runtime.Server;

namespace XIVLauncher.PatchInstaller
{
    public class Program
    {
        private static IpcServer _server = new IpcServer();
        private static IpcClient _client = new IpcClient();
        public const int IPC_SERVER_PORT = 0xff16;
        public const int IPC_CLIENT_PORT = 0xff30;

        public static JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
            TypeNameHandling = TypeNameHandling.All
        };

        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
//#if DEBUG
                .WriteTo.Debug()
                .MinimumLevel.Verbose()
//#endif
                .CreateLogger();

            _client.Initialize(IPC_SERVER_PORT);
            _server.Start(IPC_CLIENT_PORT);
            _server.ReceivedRequest += ServerOnReceivedRequest;

            Log.Information("[PATCHER] IPC connected");

            SendIpcMessage(new PatcherIpcEnvelope
            {
                OpCode = PatcherIpcOpCode.Hello,
                Data = DateTime.Now
            });

            Log.Information("[PATCHER] sent hello");

            while (true)
            {
                Thread.Sleep(1);
            }

            return;

            if (args.Length == 3)
            {
                var patchlist = new[]
                {
                    "4e9a232b/H2017.06.06.0000.0001a.patch",
                    "4e9a232b/H2017.06.06.0000.0001b.patch",
                    "4e9a232b/H2017.06.06.0000.0001c.patch",
                    "4e9a232b/H2017.06.06.0000.0001d.patch",
                    "4e9a232b/H2017.06.06.0000.0001e.patch",
                    "4e9a232b/H2017.06.06.0000.0001f.patch",
                    "4e9a232b/H2017.06.06.0000.0001g.patch",
                    "4e9a232b/H2017.06.06.0000.0001h.patch",
                    "4e9a232b/H2017.06.06.0000.0001i.patch",
                    "4e9a232b/H2017.06.06.0000.0001j.patch",
                    "4e9a232b/H2017.06.06.0000.0001k.patch",
                    "4e9a232b/H2017.06.06.0000.0001l.patch",
                    "4e9a232b/H2017.06.06.0000.0001m.patch",
                    "4e9a232b/H2017.06.06.0000.0001n.patch",
                    "4e9a232b/D2017.07.11.0000.0001.patch",
                    "4e9a232b/D2017.09.24.0000.0001.patch",
                    "4e9a232b/D2017.10.11.0000.0001.patch",
                    "4e9a232b/D2017.10.31.0000.0001.patch",
                    "4e9a232b/D2017.11.24.0000.0001.patch",
                    "4e9a232b/D2018.01.12.0000.0001.patch",
                    "4e9a232b/D2018.02.09.0000.0001.patch",
                    "4e9a232b/D2018.04.27.0000.0001.patch",
                    "4e9a232b/D2018.05.26.0000.0001.patch",
                    "4e9a232b/D2018.06.19.0000.0001.patch",
                    "4e9a232b/D2018.07.18.0000.0001.patch",
                    "4e9a232b/D2018.09.05.0000.0001.patch",
                    "4e9a232b/D2018.10.19.0000.0001.patch",
                    "4e9a232b/D2018.12.15.0000.0001.patch",
                    "4e9a232b/D2019.01.26.0000.0001.patch",
                    "4e9a232b/D2019.03.12.0000.0001.patch",
                    "4e9a232b/D2019.03.15.0000.0001.patch",
                    "4e9a232b/D2019.04.12.0000.0001.patch",
                    "4e9a232b/D2019.04.16.0000.0000.patch",
                    "4e9a232b/D2019.05.08.0000.0001.patch",
                    "4e9a232b/D2019.05.09.0000.0000.patch",
                    "4e9a232b/D2019.05.29.0000.0000.patch",
                    "4e9a232b/D2019.05.29.0001.0000.patch",
                    "4e9a232b/D2019.05.31.0000.0001.patch",
                    "4e9a232b/D2019.06.07.0000.0001.patch",
                    "4e9a232b/D2019.06.18.0000.0001.patch",
                    "4e9a232b/D2019.06.22.0000.0000.patch",
                    "4e9a232b/D2019.07.02.0000.0000.patch",
                    "4e9a232b/D2019.07.09.0000.0000.patch",
                    "4e9a232b/D2019.07.10.0000.0001.patch",
                    "4e9a232b/D2019.07.10.0001.0000.patch",
                    "4e9a232b/D2019.07.24.0000.0001.patch",
                    "4e9a232b/D2019.07.24.0001.0000.patch",
                    "4e9a232b/D2019.08.20.0000.0000.patch",
                    "4e9a232b/D2019.08.21.0000.0000.patch",
                    "4e9a232b/D2019.10.11.0000.0000.patch",
                    "4e9a232b/D2019.10.16.0000.0001.patch",
                    "4e9a232b/D2019.10.19.0000.0001.patch",
                    "4e9a232b/D2019.10.24.0000.0000.patch",
                    "4e9a232b/D2019.11.02.0000.0000.patch",
                    "4e9a232b/D2019.11.05.0000.0001.patch",
                    "4e9a232b/D2019.11.06.0000.0000.patch",
                    "4e9a232b/D2019.11.19.0000.0000.patch",
                    "4e9a232b/D2019.12.04.0000.0001.patch",
                    "4e9a232b/D2019.12.05.0000.0000.patch",
                    "4e9a232b/D2019.12.17.0000.0000.patch",
                    "4e9a232b/D2019.12.19.0000.0000.patch",
                    "4e9a232b/D2020.01.31.0000.0000.patch",
                    "4e9a232b/D2020.01.31.0001.0000.patch",
                    "4e9a232b/D2020.02.10.0000.0001.patch",
                    "4e9a232b/D2020.02.11.0000.0000.patch",
                    "4e9a232b/D2020.02.27.0000.0000.patch",
                    "4e9a232b/D2020.02.29.0000.0001.patch",
                    "4e9a232b/D2020.03.04.0000.0000.patch",
                    "4e9a232b/D2020.03.12.0000.0000.patch",
                    "4e9a232b/D2020.03.24.0000.0000.patch",
                    "4e9a232b/D2020.03.25.0000.0000.patch",
                    "4e9a232b/D2020.03.26.0000.0000.patch",
                    "4e9a232b/D2020.03.27.0000.0000.patch",
                    /*"ex1/6b936f08/H2017.06.01.0000.0001a.patch",
                    "ex1/6b936f08/H2017.06.01.0000.0001b.patch",
                    "ex1/6b936f08/H2017.06.01.0000.0001c.patch",
                    "ex1/6b936f08/H2017.06.01.0000.0001d.patch",
                    "ex1/6b936f08/D2017.09.24.0000.0000.patch",
                    "ex1/6b936f08/D2018.09.05.0000.0001.patch",
                    "ex1/6b936f08/D2019.11.06.0000.0001.patch",
                    "ex1/6b936f08/D2020.03.04.0000.0001.patch",
                    "ex1/6b936f08/D2020.03.24.0000.0000.patch",
                    "ex1/6b936f08/D2020.03.26.0000.0000.patch",
                    "ex2/f29a3eb2/D2017.03.18.0000.0000.patch",
                    "ex2/f29a3eb2/D2017.05.26.0000.0000.patch",
                    "ex2/f29a3eb2/D2017.05.26.0001.0000.patch",
                    "ex2/f29a3eb2/D2017.05.26.0002.0000.patch",
                    "ex2/f29a3eb2/D2017.06.27.0000.0001.patch",
                    "ex2/f29a3eb2/D2017.09.24.0000.0001.patch",
                    "ex2/f29a3eb2/D2018.01.12.0000.0001.patch",
                    "ex2/f29a3eb2/D2018.02.23.0000.0001.patch",
                    "ex2/f29a3eb2/D2018.04.27.0000.0001.patch",
                    "ex2/f29a3eb2/D2018.07.18.0000.0001.patch",
                    "ex2/f29a3eb2/D2018.09.05.0000.0001.patch",
                    "ex2/f29a3eb2/D2018.12.14.0000.0001.patch",
                    "ex2/f29a3eb2/D2019.01.26.0000.0001.patch",
                    "ex2/f29a3eb2/D2019.03.12.0000.0001.patch",
                    "ex2/f29a3eb2/D2019.05.29.0000.0001.patch",
                    "ex2/f29a3eb2/D2019.12.04.0000.0001.patch",
                    "ex2/f29a3eb2/D2020.02.29.0000.0001.patch",
                    "ex2/f29a3eb2/D2020.03.24.0000.0000.patch",
                    "ex2/f29a3eb2/D2020.03.26.0000.0000.patch",
                    "ex3/859d0e24/D2019.04.02.0000.0000.patch",
                    "ex3/859d0e24/D2019.05.29.0000.0000.patch",
                    "ex3/859d0e24/D2019.05.29.0001.0000.patch",
                    "ex3/859d0e24/D2019.05.29.0002.0000.patch",
                    "ex3/859d0e24/D2019.07.09.0000.0001.patch",
                    "ex3/859d0e24/D2019.10.11.0000.0001.patch",
                    "ex3/859d0e24/D2020.01.31.0000.0001.patch",
                    "ex3/859d0e24/D2020.02.29.0000.0001.patch",
                    "ex3/859d0e24/D2020.03.24.0000.0000.patch",
                    "ex3/859d0e24/D2020.03.26.0000.0000.patch"*/
                };
                foreach (var s in patchlist)
                    DebugPatch(args[0] + s, args[1]);
                return;
            }

            if (args.Length == 1)
            {
                return;
            }

            Console.WriteLine("XIVLauncher.PatchInstaller\n\nUsage:\nXIVLauncher.PatchInstaller.exe <patch path> <game path> <repository>\nOR\nXIVLauncher.PatchInstaller.exe <pipe name>");
        }

        private static void ServerOnReceivedRequest(object sender, ReceivedRequestEventArgs e)
        {
            Log.Information("[PATCHER] IPC: " + e.Request);

            var msg = JsonConvert.DeserializeObject<PatcherIpcEnvelope>(e.Request, JsonSettings);

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
                    Task.Run(() =>
                        DebugPatch(installData.PatchFile.FullName,
                            Path.Combine(installData.GameDirectory.FullName, installData.IsBootPatch ? "boot" : "game")))
                        .ContinueWith(t =>
                    {
                        if (!t.Result)
                        {
                            Log.Error(t.Exception, "PATCH INSTALL FAILED");
                            SendIpcMessage(new PatcherIpcEnvelope
                            {
                                OpCode = PatcherIpcOpCode.InstallFailed
                            });
                        }
                        else
                            SendIpcMessage(new PatcherIpcEnvelope
                            {
                                OpCode = PatcherIpcOpCode.InstallOk
                            });
                    });
                    break;
            }
        }

        private static void SendIpcMessage(PatcherIpcMessages.PatcherIpcEnvelope envelope)
        {
            _client.Send(JsonConvert.SerializeObject(envelope, Formatting.None, JsonSettings));
        }

        private static bool DebugPatch(string patchPath, string gamePath)
        {
            try
            {
                Log.Debug("Installing {0} to {1}", patchPath, gamePath);

                var patchFile = ZiPatchFile.FromFileName(patchPath);

                using (var store = new SqexFileStreamStore())
                {
                    var config = new ZiPatchConfig(gamePath) { Store = store };

                    foreach (var chunk in patchFile.GetChunks())
                        chunk.ApplyChunk(config);
                }

                Log.Debug("Patch {0} installed", patchPath);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Patch install failed.");
                return false;
            }
        }


        private static void InstallPatch(string path, string gamePath, string repository)
        {
            var execute = new ZiPatchExecute(gamePath, repository);

            try
            {
                execute.Execute(path);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to execute ZiPatch.");
                throw;
            }
        }
    }
}
