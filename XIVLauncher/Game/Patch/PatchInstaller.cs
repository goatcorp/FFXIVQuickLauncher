using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Game.Patch.PatchList;
using XIVLauncher.PatchInstaller;
using XIVLauncher.PatchInstaller.PatcherIpcMessages;
using ZetaIpc.Runtime.Client;
using ZetaIpc.Runtime.Server;

namespace XIVLauncher.Game.Patch
{
    public class PatchInstaller : IDisposable
    {
        private IpcServer _server = new IpcServer();
        private IpcClient _client = new IpcClient();

        public class OnPatchCommandCompleteEventArgs : EventArgs
        {
            public bool IsSuccess { get; set; }
            public int CreatedFile { get; set; }
            public int CreatedFolder { get; set; }
        }

        public enum InstallerState
        {   
            NotStarted,
            NotReady,
            Ready,
            Busy,
            Failed
        }

        public InstallerState State { get; private set; } = InstallerState.NotStarted;

        public event EventHandler<OnPatchCommandCompleteEventArgs> OnPatchCommandComplete;

        public void StartIfNeeded()
        {
            if (State != InstallerState.NotStarted)
                return;

            _server.ReceivedRequest += ServerOnReceivedRequest;
            _server.Start(XIVLauncher.PatchInstaller.Program.IPC_SERVER_PORT);

            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "XIVLauncher.PatchInstaller.exe");

            var startInfo = new ProcessStartInfo(path);
            startInfo.UseShellExecute = true;

            //Start as admin
            if (System.Environment.OSVersion.Version.Major >= 6)
            {
                startInfo.Verb = "runas";
            }

            State = InstallerState.NotReady;

            Process.Start(startInfo);
        }

        public void WaitOnHello()
        {
            while (State != InstallerState.Ready)
            {
                Thread.Sleep(1);
            }
        }

        private void ServerOnReceivedRequest(object sender, ReceivedRequestEventArgs e)
        {
            Log.Information("[PATCHERIPC] IPC: " + e.Request);

            var msg = JsonConvert.DeserializeObject<PatcherIpcEnvelope>(e.Request, XIVLauncher.PatchInstaller.Program.JsonSettings);

            switch (msg.OpCode)
            {
                case PatcherIpcOpCode.Hello:
                    _client.Initialize(XIVLauncher.PatchInstaller.Program.IPC_CLIENT_PORT);
                    Log.Information("[PATCHERIPC] GOT HELLO");
                    State = InstallerState.Ready;
                    break;
                case PatcherIpcOpCode.InstallOk:
                    Log.Information("[PATCHERIPC] INSTALL OK");
                    State = InstallerState.Ready;
                    break;
                case PatcherIpcOpCode.InstallFailed:
                    State = InstallerState.Failed;
                    MessageBox.Show(
                        "The patch installer ran into an error.\nPlease report this error.\nPlease use the official launcher.");
                    Stop();
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Stop()
        {
            if (State == InstallerState.NotReady || State == InstallerState.NotStarted || State == InstallerState.Busy)
                return;

            SendIpcMessage(new PatcherIpcEnvelope
            {
                OpCode = PatcherIpcOpCode.Bye
            });
        }

        public void StartInstall(DirectoryInfo gameDirectory, FileInfo file, PatchListEntry patch, Repository repo)
        {
            State = InstallerState.Busy;
            SendIpcMessage(new PatcherIpcEnvelope
            {
                OpCode = PatcherIpcOpCode.StartInstall,
                Data = new PatcherIpcStartInstall
                {
                    GameDirectory = gameDirectory,
                    PatchFile = file,
                    Repo = repo,
                    VersionId = patch.VersionId
                }
            });
        }

        public void FinishInstall(DirectoryInfo gameDirectory)
        {
            SendIpcMessage(new PatcherIpcEnvelope
            {
                OpCode = PatcherIpcOpCode.Finish,
                Data = gameDirectory
            });
        }

        private void SendIpcMessage(PatcherIpcEnvelope envelope)
        {
            try
            {
                _client.Send(JsonConvert.SerializeObject(envelope, Formatting.Indented, XIVLauncher.PatchInstaller.Program.JsonSettings));
            }
            catch (Exception e)
            {
                Log.Error(e, "[PATCHERIPC] Failed to send message.");
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
