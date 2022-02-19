using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Patching.IndexedZiPatch;

namespace XIVLauncher.Game.Patch
{
    public class PatchVerifier : IDisposable
    {
        private HttpClient _client;
        private CancellationTokenSource _cancellationTokenSource = new();

        private Dictionary<Repository, string> _repoMetaPaths = new();
        private Dictionary<string, string> _patchSources = new();

        private Task _verificationTask;

        public int NumBrokenFiles { get; private set; } = 0;
        public bool IsInstalling { get; private set; } = false;
        public int TaskIndex { get; private set; }
        public long Progress { get; private set; }
        public long Total { get; private set; }
        public int TaskCount { get; private set; }
        public string CurrentFile { get; private set; }
        public Exception LastException { get; private set; }

        private const string BASE_URL = "https://raw.githubusercontent.com/goatcorp/patchinfo/main/";

        public enum VerifyState
        {
            Unknown,
            Verify,
            Done,
            Cancelled,
            Error
        }

        public VerifyState State { get; private set; } = VerifyState.Unknown;

        public PatchVerifier(Launcher.LoginResult loginResult)
        {
            _client = new HttpClient();

            SetLoginState(loginResult);
        }

        public void Start()
        {
            Debug.Assert(_repoMetaPaths.Count != 0 && _patchSources.Count != 0);

            _verificationTask = Task.Run(this.RunVerifier, _cancellationTokenSource.Token);
        }

        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }

        private void SetLoginState(Launcher.LoginResult result)
        {
            _patchSources.Clear();

            foreach (var patch in result.PendingPatches)
            {
                var repoName = patch.GetRepoName();
                if (repoName == "ffxiv")
                    repoName = "ex0";

                _patchSources.Add($"{repoName}:{Path.GetFileName(patch.GetFilePath())}", patch.Url);
            }
        }

        private async Task RunVerifier()
        {
            State = VerifyState.Unknown;
            LastException = null;
            try
            {
                var assemblyLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                using var remote = new IndexedZiPatchIndexRemoteInstaller(Path.Combine(assemblyLocation!, "XIVLauncher.PatchInstaller.exe"),
                    true);
                await remote.SetWorkerProcessPriority(ProcessPriorityClass.Idle);

                while (!_cancellationTokenSource.IsCancellationRequested && State != VerifyState.Done)
                {
                    switch (State)
                    {

                        case VerifyState.Unknown:
                            State = VerifyState.Verify;
                            break;
                        case VerifyState.Verify:
                            const int maxConcurrentConnectionsForPatchSet = 8;

                            foreach (var metaPath in _repoMetaPaths)
                            {
                                var patchIndex = new IndexedZiPatchIndex(new BinaryReader(new DeflateStream(new FileStream(metaPath.Value, FileMode.Open, FileAccess.Read), CompressionMode.Decompress)));

                                IsInstalling = false;

                                void UpdateVerifyProgress(int targetIndex, long progress, long max)
                                {
                                    CurrentFile = patchIndex[Math.Min(targetIndex, patchIndex.Length - 1)].RelativePath;
                                    TaskIndex = targetIndex;
                                    Progress = Math.Min(progress, max);
                                    Total = max;

                                    Log.Verbose("[{0}/{1}] {2} {3}... {4:0.00}/{5:0.00}MB ({6:00.00}%)", targetIndex + 1, TaskCount, "Checking", CurrentFile, progress / 1048576.0, max / 1048576.0, 100.0 * progress / max);
                                }

                                void UpdateInstallProgress(int sourceIndex, long progress, long max)
                                {
                                    CurrentFile = patchIndex.Sources[Math.Min(sourceIndex, patchIndex.Sources.Count - 1)];
                                    TaskIndex = sourceIndex;
                                    Progress = Math.Min(progress, max);
                                    Total = max;

                                    Log.Verbose("[{0}/{1}] {2} {3}... {4:0.00}/{5:0.00}MB ({6:00.00}%)", sourceIndex + 1, TaskCount, "Installing", CurrentFile, progress / 1048576.0, max / 1048576.0, 100.0 * progress / max);
                                }

                                try
                                {
                                    remote.OnVerifyProgress += UpdateVerifyProgress;
                                    remote.OnInstallProgress += UpdateInstallProgress;
                                    TaskCount = patchIndex.Length;

                                    await remote.ConstructFromPatchFile(patchIndex);

                                    var adjustedGamePath = Path.Combine(App.Settings.GamePath.FullName, patchIndex.ExpacVersion == IndexedZiPatchIndex.EXPAC_VERSION_BOOT ? "boot" : "game");

                                    await remote.SetTargetStreamsFromPathReadOnly(adjustedGamePath);
                                    // TODO: check one at a time if random access is slow?
                                    await remote.VerifyFiles(Environment.ProcessorCount, _cancellationTokenSource.Token);

                                    TaskCount = patchIndex.Sources.Count;
                                    var missing = await remote.GetMissingPartIndicesPerPatch();
                                    NumBrokenFiles += (await remote.GetMissingPartIndicesPerTargetFile()).Count(x => x.Any());

                                    await remote.SetTargetStreamsFromPathReadWriteForMissingFiles(adjustedGamePath);
                                    var prefix = patchIndex.ExpacVersion == IndexedZiPatchIndex.EXPAC_VERSION_BOOT ? "boot:" : $"ex{patchIndex.ExpacVersion}:";
                                    for (var i = 0; i < patchIndex.Sources.Count; i++)
                                    {
                                        if (!missing[i].Any())
                                            continue;

                                        await remote.QueueInstall(i, _patchSources[prefix + patchIndex.Sources[i]], null, maxConcurrentConnectionsForPatchSet);
                                    }

                                    IsInstalling = true;

                                    await remote.Install(maxConcurrentConnectionsForPatchSet, _cancellationTokenSource.Token);
                                    await remote.WriteVersionFiles(adjustedGamePath);
                                }
                                finally
                                {
                                    remote.OnVerifyProgress -= UpdateVerifyProgress;
                                    remote.OnInstallProgress -= UpdateInstallProgress;
                                }
                            }

                            State = VerifyState.Done;
                            break;
                        case VerifyState.Done:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is Win32Exception winex && (uint)winex.HResult == 0x80004005u)  // The operation was canceled by the user (UAC dialog cancellation)
                {
                    State = VerifyState.Cancelled;
                    return;
                }
                Log.Error(ex, "Unexpected error occurred in RunVerifier");
                LastException = ex;
                State = VerifyState.Error;
            }
        }

        public async Task GetPatchMeta()
        {
            _repoMetaPaths.Clear();

            var metaFolder = Path.Combine(Paths.RoamingPath, "patchMeta");
            Directory.CreateDirectory(metaFolder);

            await this.GetRepoMeta(Repository.Ffxiv, metaFolder);
            await this.GetRepoMeta(Repository.Ex1, metaFolder);
            await this.GetRepoMeta(Repository.Ex2, metaFolder);
            await this.GetRepoMeta(Repository.Ex3, metaFolder);
            await this.GetRepoMeta(Repository.Ex4, metaFolder);
        }

        private async Task GetRepoMeta(Repository repo, string baseDir)
        {
            var version = repo.GetVer(App.Settings.GamePath);
            if (version == Constants.BASE_GAME_VERSION)
                return;

            var repoShorthand = repo == Repository.Ffxiv ? "game" : repo.ToString().ToLower();
            var fileName = $"D{version}.patch.index";

            var metaPath = Path.Combine(baseDir, repoShorthand);
            var filePath = Path.Combine(metaPath, fileName);
            Directory.CreateDirectory(metaPath);

            if (!File.Exists(filePath))
            {
                var request = await _client.GetAsync($"{BASE_URL}{repoShorthand}/{fileName}", _cancellationTokenSource.Token);
                if (request.StatusCode == HttpStatusCode.NotFound)
                    throw new NoVersionReferenceException(repo, version);

                request.EnsureSuccessStatusCode();

                File.WriteAllBytes(filePath, await request.Content.ReadAsByteArrayAsync());
            }

            _repoMetaPaths.Add(repo, filePath);
            Log.Verbose("Downloaded patch index for {Repo}({Version})", repo, version);
        }

        public void Dispose()
        {
            if (_verificationTask != null && !_verificationTask.IsCompleted)
            {
                _cancellationTokenSource.Cancel();
                _verificationTask.Wait();
            }
        }
    }
}