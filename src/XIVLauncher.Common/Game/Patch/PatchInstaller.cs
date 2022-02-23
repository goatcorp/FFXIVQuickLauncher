using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Serilog;
using SharedMemory;
using XIVLauncher.Common.Game.Patch.PatchList;
using XIVLauncher.Common.PatcherIpc;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Game.Patch
{
    public class PatchInstaller : IDisposable
    {
        private readonly ISettings _settings;
        private RpcBuffer _rpc;

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

        public PatchInstaller(ISettings _settings)
        {
            this._settings = _settings;
        }

        public void StartIfNeeded()
        {
            var rpcName = "XLPatcher" + Guid.NewGuid().ToString();

            Log.Information("[PATCHERIPC] Starting patcher with '{0}'", rpcName);

            _rpc = new RpcBuffer(rpcName, RemoteCallHandler);

            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "XIVLauncher.PatchInstaller.exe");

            var startInfo = new ProcessStartInfo(path);
            startInfo.UseShellExecute = true;

            //Start as admin if needed
            if (!EnvironmentSettings.IsNoRunas && Environment.OSVersion.Version.Major >= 6)
                startInfo.Verb = "runas";

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

        private void RemoteCallHandler(ulong msgId, byte[] payload)
        {
            var json = IpcHelpers.Base64Decode(Encoding.ASCII.GetString(payload));
            Log.Information("[PATCHERIPC] IPC({0}): {1}", msgId, json);

            var msg = JsonConvert.DeserializeObject<PatcherIpcEnvelope>(json, IpcHelpers.JsonSettings);

            switch (msg.OpCode)
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
                    VersionId = patch.VersionId,
                    KeepPatch = _settings.KeepPatches.GetValueOrDefault(false)
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
                var json = IpcHelpers.Base64Encode(JsonConvert.SerializeObject(envelope, IpcHelpers.JsonSettings));

                Log.Information("[PATCHERIPC] SEND: " + json);
                _rpc.RemoteRequest(Encoding.ASCII.GetBytes(json));
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