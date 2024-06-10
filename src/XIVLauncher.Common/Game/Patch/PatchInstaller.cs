using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Serilog;
using XIVLauncher.Common.Game.Patch.PatchList;
using XIVLauncher.Common.PatcherIpc;
using XIVLauncher.Common.Patching;
using XIVLauncher.Common.Patching.Rpc;
using XIVLauncher.Common.Patching.Rpc.Implementations;

namespace XIVLauncher.Common.Game.Patch
{
    public class PatchInstaller : IDisposable
    {
        private readonly bool keepPatches;
        private IRpc rpc;

        private RemotePatchInstaller? internalPatchInstaller;

        public enum InstallerState
        {
            NotStarted,
            NotReady,
            Ready,
            Busy,
            Failed
        }

        public InstallerState State { get; private set; } = InstallerState.NotStarted;

        public event Action OnFail;

        public PatchInstaller(bool keepPatches)
        {
            this.keepPatches = keepPatches;
        }

        public void StartIfNeeded(bool external = true)
        {
            var rpcName = "XLPatcher" + Guid.NewGuid().ToString();

            Log.Information("[PATCHERIPC] Starting patcher with '{0}'", rpcName);

            if (external)
            {
                this.rpc = new SharedMemoryRpc(rpcName);
                this.rpc.MessageReceived += RemoteCallHandler;

                var path = Path.Combine(AppContext.BaseDirectory,
                    "XIVLauncher.PatchInstaller.exe");

                var startInfo = new ProcessStartInfo(path);
                startInfo.UseShellExecute = true;

                //Start as admin if needed
                if (!EnvironmentSettings.IsNoRunas && Environment.OSVersion.Version.Major >= 6)
                    startInfo.Verb = "runas";

                if (!Debugger.IsAttached)
                {
                    startInfo.CreateNoWindow = true;
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                }

                startInfo.Arguments = $"rpc {rpcName}";

                State = InstallerState.NotReady;

                try
                {
                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Could not launch Patch Installer");
                    throw new PatchInstallerException("Start failed.", ex);
                }
            }
            else
            {
                this.rpc = new InProcessRpc(rpcName);
                this.rpc.MessageReceived += RemoteCallHandler;

                this.internalPatchInstaller = new RemotePatchInstaller(new InProcessRpc(rpcName));
                this.internalPatchInstaller.Start();
            }
        }

        private void RemoteCallHandler(PatcherIpcEnvelope envelope)
        {
            switch (envelope.OpCode)
            {
                case PatcherIpcOpCode.Hello:
                    //_client.Initialize(_clientPort);
                    Log.Information("[PATCHERIPC] GOT HELLO");
                    State = InstallerState.Ready;
                    break;

                case PatcherIpcOpCode.InstallOk:
                    Log.Information("[PATCHERIPC] INSTALL OK");
                    State = InstallerState.Ready;
                    break;

                case PatcherIpcOpCode.InstallFailed:
                    State = InstallerState.Failed;
                    OnFail?.Invoke();

                    Stop();
                    Environment.Exit(0);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void WaitOnHello()
        {
            for (var i = 0; i < 40; i++)
            {
                if (State == InstallerState.Ready)
                    return;

                Thread.Sleep(500);
            }

            throw new PatchInstallerException("Installer RPC timed out.");
        }

        public void Stop()
        {
            if (State == InstallerState.NotReady || State == InstallerState.NotStarted || State == InstallerState.Busy)
                return;

            this.rpc.SendMessage(new PatcherIpcEnvelope
            {
                OpCode = PatcherIpcOpCode.Bye
            });
        }

        public void StartInstall(DirectoryInfo gameDirectory, FileInfo file, PatchListEntry patch)
        {
            State = InstallerState.Busy;
            this.rpc.SendMessage(new PatcherIpcEnvelope
            {
                OpCode = PatcherIpcOpCode.StartInstall,
                Data = new PatcherIpcStartInstall
                {
                    GameDirectory = gameDirectory,
                    PatchFile = file,
                    Repo = patch.GetRepo(),
                    VersionId = patch.VersionId,
                    KeepPatch = this.keepPatches,
                }
            });
        }

        public void FinishInstall(DirectoryInfo gameDirectory)
        {
            this.rpc.SendMessage(new PatcherIpcEnvelope
            {
                OpCode = PatcherIpcOpCode.Finish,
                Data = gameDirectory
            });
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
