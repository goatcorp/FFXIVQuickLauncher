using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.Remoting;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CheapLoc;
using Newtonsoft.Json;
using Serilog;
using SharedMemory;
using XIVLauncher.Game.Patch.PatchList;
using XIVLauncher.PatchInstaller;
using XIVLauncher.PatchInstaller.PatcherIpcMessages;
using XIVLauncher.Windows;

namespace XIVLauncher.Game.Patch
{
    public class PatchInstaller : IDisposable
    {
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

        public bool StartIfNeeded()
        {
            if (State != InstallerState.NotStarted)
                return true;

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
                return false;
            }

            return true;
        }

        private void RemoteCallHandler(ulong msgId, byte[] payload)
        {
            var json = PatcherMain.Base64Decode(Encoding.ASCII.GetString(payload));
            Log.Information("[PATCHERIPC] IPC({0}): {1}", msgId, json);

            var msg = JsonConvert.DeserializeObject<PatcherIpcEnvelope>(json, PatcherMain.JsonSettings);

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
                    CustomMessageBox.Show(
                        Loc.Localize("PatchInstallerInstallFailed", "The patch installer ran into an error.\nPlease report this error.\n\nPlease try again or use the official launcher."),
                        "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);

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

            MessageBox.Show(
                Loc.Localize("PatchInstallerNotOpen",
                    "Could not connect to the patch installer.\nPlease report this error."), "XIVLauncher",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(0);
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
                    KeepPatch = App.Settings.KeepPatches.GetValueOrDefault(false)
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
                var json = PatcherMain.Base64Encode(JsonConvert.SerializeObject(envelope, PatcherMain.JsonSettings));

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