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
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Game.Patch
{
    public class PatchInstaller(DirectoryInfo gamePath, bool keepPatches) : IDisposable
    {
        private IRpc rpc;

        private RemotePatchInstaller? internalPatchInstaller;

        private Process? process;

        private readonly object stateLock = new();

        private bool isStopping;

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

        public void StartIfNeeded(bool external = true)
        {
            var rpcName = "XLPatcher" + Guid.NewGuid();

            Log.Information("[PATCHERIPC] Starting patcher with '{0}'", rpcName);

            if (external)
            {
                this.rpc = new SharedMemoryRpc(rpcName);
                this.rpc.MessageReceived += RemoteCallHandler;

                var path = Path.Combine(AppContext.BaseDirectory, "patcher", "XIVLauncher.PatchInstaller.exe");

                var startInfo = new ProcessStartInfo(path);
                startInfo.UseShellExecute = true;

                //Start as admin if needed
                if (!EnvironmentSettings.IsNoRunas &&
                    Environment.OSVersion.Version.Major >= 6 &&
                    PlatformHelpers.IsElevationRequiredForWrite(gamePath))
                {
                    startInfo.Verb = "runas";
                }

                if (!Debugger.IsAttached)
                {
                    startInfo.CreateNoWindow = true;
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                }

                startInfo.Arguments = $"rpc {rpcName}";

                State = InstallerState.NotReady;

                try
                {
                    this.process = Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Could not launch Patch Installer");
                    throw new PatchInstallerException("Start failed.", ex);
                }

                WatchProcess();
            }
            else
            {
                this.rpc = new InProcessRpc(rpcName);
                this.rpc.MessageReceived += RemoteCallHandler;

                this.internalPatchInstaller = new RemotePatchInstaller(new InProcessRpc(rpcName));
                this.internalPatchInstaller.Start();
            }
        }

        private void WatchProcess()
        {
            if (this.process == null)
            {
                return;
            }

            try
            {
                this.process.Exited += OnProcessExited;
                this.process.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PATCHERIPC] Could not watch patcher process");
            }
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            // Exiting is fine if we asked for it
            if (this.isStopping || State is InstallerState.NotStarted or InstallerState.NotReady)
            {
                return;
            }

            Log.Error("[PATCHERIPC] Patcher exited unexpectedly");
            Fail();
        }

        private void Fail()
        {
            lock (this.stateLock)
            {
                if (State == InstallerState.Failed)
                {
                    return;
                }

                State = InstallerState.Failed;
            }

            OnFail?.Invoke();
        }

        // This should only ever be called last-resort style, since it can leave patches half-installed
        internal void Abort()
        {
            Log.Error("[PATCHERIPC] Aborting patcher");

            try
            {
                this.process?.Kill();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PATCHERIPC] Could not kill patcher");
            }

            Fail();
        }

        private bool HasProcessExited()
        {
            try
            {
                return this.process is { HasExited: true };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PATCHERIPC] Could not query patcher process");
                return false;
            }
        }

        private bool TrySetBusy()
        {
            lock (this.stateLock)
            {
                if (State == InstallerState.Failed)
                {
                    return false;
                }

                State = InstallerState.Busy;
                return true;
            }
        }

        private void TrySetReady()
        {
            lock (this.stateLock)
            {
                if (State == InstallerState.Failed)
                {
                    return;
                }

                State = InstallerState.Ready;
            }
        }

        private void RemoteCallHandler(PatcherIpcEnvelope envelope)
        {
            switch (envelope.OpCode)
            {
                case PatcherIpcOpCode.Hello:
                    Log.Information("[PATCHERIPC] GOT HELLO");
                    TrySetReady();
                    break;

                case PatcherIpcOpCode.InstallOk:
                    Log.Information("[PATCHERIPC] INSTALL OK");
                    TrySetReady();
                    break;

                case PatcherIpcOpCode.Finish:
                    Log.Information("[PATCHERIPC] INSTALL FINISH");
                    TrySetReady();
                    break;

                case PatcherIpcOpCode.InstallFailed:
                    Log.Error("[PATCHERIPC] INSTALL FAILED");
                    Fail();
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

            if (HasProcessExited())
            {
                return;
            }

            this.isStopping = true;
            this.rpc.SendMessage(new PatcherIpcEnvelope
            {
                OpCode = PatcherIpcOpCode.Bye
            });
        }

        public void StartInstall(DirectoryInfo gameDirectory, FileInfo file, PatchListEntry patch)
        {
            if (!TrySetBusy())
            {
                return;
            }

            var sent = this.rpc.SendMessage(new PatcherIpcEnvelope
            {
                OpCode = PatcherIpcOpCode.StartInstall,
                Data = new PatcherIpcStartInstall
                {
                    GameDirectory = gameDirectory,
                    PatchFile = file,
                    Repo = patch.GetRepo(),
                    VersionId = patch.VersionId,
                    KeepPatch = keepPatches,
                }
            });

            if (!sent)
            {
                Fail();
            }
        }

        public void FinalizeAndWait(DirectoryInfo gameDirectory)
        {
            if (!TrySetBusy())
            {
                return;
            }

            var sent = this.rpc.SendMessage(new PatcherIpcEnvelope
            {
                OpCode = PatcherIpcOpCode.Finish,
                Data = gameDirectory.ToString()
            });

            if (!sent)
            {
                Fail();
                return;
            }

            var cts = new CancellationTokenSource(5000);

            while (this.State == InstallerState.Busy)
            {
                Thread.Sleep(100);

                if (cts.Token.IsCancellationRequested)
                {
                    // Fail() instead of only OnFail: leaving Busy made Stop() skip Bye, so the patcher lingered
                    Log.Error("Timeout waiting for installer to finish after finishing install.");
                    Fail();
                    break;
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
