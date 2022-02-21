using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
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
        private Dictionary<string, object> _patchSources = new();

        private Task _verificationTask;
        private List<Tuple<long, long>> _reportedProgresses = new();

        public int ProgressUpdateInterval { get; private set; }
        public int NumBrokenFiles { get; private set; } = 0;
        public bool IsInstalling { get; private set; } = false;
        public int PatchSetIndex { get; private set; }
        public int PatchSetCount { get; private set; }
        public int TaskIndex { get; private set; }
        public long Progress { get; private set; }
        public long Total { get; private set; }
        public int TaskCount { get; private set; }
        public string CurrentFile { get; private set; }
        public long Speed { get; private set; }
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

        private class VerifyVersions
        {
            [JsonPropertyName("boot")]
            public string Boot { get; set; }

            [JsonPropertyName("game")]
            public string Game { get; set; }

            [JsonPropertyName("ex1")]
            public string Ex1 { get; set; }

            [JsonPropertyName("ex2")]
            public string Ex2 { get; set; }

            [JsonPropertyName("ex3")]
            public string Ex3 { get; set; }

            [JsonPropertyName("ex4")]
            public string Ex4 { get; set; }
        }

        public VerifyState State { get; private set; } = VerifyState.Unknown;

        public PatchVerifier(Launcher.LoginResult loginResult, int progressUpdateInterval)
        {
            _client = new HttpClient();
            ProgressUpdateInterval = progressUpdateInterval;

            SetLoginState(loginResult);
        }

        public void Start()
        {
            Debug.Assert(_repoMetaPaths.Count != 0 && _patchSources.Count != 0);

            _verificationTask = Task.Run(this.RunVerifier, _cancellationTokenSource.Token);
        }

        public async Task Cancel()
        {
            _cancellationTokenSource.Cancel();
            await _verificationTask;
        }

        private void SetLoginState(Launcher.LoginResult result)
        {
            _patchSources.Clear();

            foreach (var patch in result.PendingPatches)
            {
                var repoName = patch.GetRepoName();
                if (repoName == "ffxiv")
                    repoName = "ex0";

                var file = new FileInfo(Path.Combine(App.Settings.PatchPath.FullName, patch.GetFilePath()));
                if (file.Exists && file.Length == patch.Length)
                    _patchSources.Add($"{repoName}:{Path.GetFileName(patch.GetFilePath())}", file);
                else
                    _patchSources.Add($"{repoName}:{Path.GetFileName(patch.GetFilePath())}", new Uri(patch.Url));
            }
        }

        private bool AdminAccessRequired(string gameRootPath)
        {
            string tempFn;
            do
            {
                tempFn = Path.Combine(gameRootPath, Guid.NewGuid().ToString());
            } while (File.Exists(tempFn));
            try
            {
                File.WriteAllText(tempFn, "");
                File.Delete(tempFn);
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
            return false;
        }

        private void RecordProgressForEstimation()
        {
            var now = DateTime.Now.Ticks;
            _reportedProgresses.Add(Tuple.Create(now, Progress));
            while ((now - _reportedProgresses.First().Item1) > 10 * 1000 * 8000)
                _reportedProgresses.RemoveAt(0);

            try
            {
                Speed = (_reportedProgresses.Last().Item2 - _reportedProgresses.First().Item2) * 10 * 1000 * 1000 / (_reportedProgresses.Last().Item1 - _reportedProgresses.First().Item1);
            }
            catch (DivideByZeroException)
            {
                Speed = 0;
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
                    AdminAccessRequired(App.Settings.GamePath.FullName));
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

                            PatchSetIndex = 0;
                            PatchSetCount = _repoMetaPaths.Count;
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
                                    RecordProgressForEstimation();
                                }

                                void UpdateInstallProgress(int sourceIndex, long progress, long max)
                                {
                                    CurrentFile = patchIndex.Sources[Math.Min(sourceIndex, patchIndex.Sources.Count - 1)];
                                    TaskIndex = sourceIndex;
                                    Progress = Math.Min(progress, max);
                                    Total = max;
                                    RecordProgressForEstimation();
                                }

                                try
                                {
                                    remote.OnVerifyProgress += UpdateVerifyProgress;
                                    remote.OnInstallProgress += UpdateInstallProgress;
                                    TaskCount = patchIndex.Length;
                                    _reportedProgresses.Clear();

                                    await remote.ConstructFromPatchFile(patchIndex, ProgressUpdateInterval);

                                    var adjustedGamePath = Path.Combine(App.Settings.GamePath.FullName, patchIndex.ExpacVersion == IndexedZiPatchIndex.EXPAC_VERSION_BOOT ? "boot" : "game");

                                    await remote.SetTargetFilesFromAllInRootPath(adjustedGamePath);
                                    // TODO: check one at a time if random access is slow?
                                    await remote.VerifyFiles(Environment.ProcessorCount, _cancellationTokenSource.Token);

                                    TaskCount = patchIndex.Sources.Count;
                                    _reportedProgresses.Clear();
                                    var missing = await remote.GetMissingPartIndicesPerPatch();
                                    NumBrokenFiles += (await remote.GetMissingPartIndicesPerTargetFile()).Count(x => x.Any());

                                    await remote.SetTargetFilesFromMissingFilesInRootPath(adjustedGamePath);
                                    var prefix = patchIndex.ExpacVersion == IndexedZiPatchIndex.EXPAC_VERSION_BOOT ? "boot:" : $"ex{patchIndex.ExpacVersion}:";
                                    for (var i = 0; i < patchIndex.Sources.Count; i++)
                                    {
                                        if (!missing[i].Any())
                                            continue;

                                        var source = _patchSources[prefix + patchIndex.Sources[i]];
                                        if (source is Uri uri)
                                            await remote.QueueInstall(i, uri, null, maxConcurrentConnectionsForPatchSet);
                                        else if (source is FileInfo file)
                                            await remote.QueueInstall(i, file, maxConcurrentConnectionsForPatchSet);
                                        else
                                            throw new InvalidOperationException("_patchSources contains non-Uri/FileInfo");
                                    }

                                    IsInstalling = true;

                                    await remote.Install(maxConcurrentConnectionsForPatchSet, _cancellationTokenSource.Token);
                                    await remote.WriteVersionFiles(adjustedGamePath);

                                    PatchSetIndex++;
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
                if (ex is OperationCanceledException)
                    State = VerifyState.Cancelled;
                else if (_cancellationTokenSource.IsCancellationRequested)
                    State = VerifyState.Cancelled;
                else if (ex is Win32Exception winex && (uint)winex.HResult == 0x80004005u)  // The operation was canceled by the user (UAC dialog cancellation)
                    State = VerifyState.Cancelled;
                else
                {
                    Log.Error(ex, "Unexpected error occurred in RunVerifier");
                    LastException = ex;
                    State = VerifyState.Error;
                }
            }
        }

        public async Task GetPatchMeta()
        {
            _repoMetaPaths.Clear();

            var metaFolder = Path.Combine(Paths.RoamingPath, "patchMeta");
            Directory.CreateDirectory(metaFolder);

            var latestVersionJson = await _client.GetStringAsync(BASE_URL + "latest.json");
            var latestVersion = JsonConvert.DeserializeObject<VerifyVersions>(latestVersionJson);

            await this.GetRepoMeta(Repository.Ffxiv, latestVersion.Game, metaFolder);
            await this.GetRepoMeta(Repository.Ex1, latestVersion.Ex1, metaFolder);
            await this.GetRepoMeta(Repository.Ex2, latestVersion.Ex2, metaFolder);
            await this.GetRepoMeta(Repository.Ex3, latestVersion.Ex3, metaFolder);
            await this.GetRepoMeta(Repository.Ex4, latestVersion.Ex4, metaFolder);
        }

        private async Task GetRepoMeta(Repository repo, string latestVersion, string baseDir)
        {
            var version = repo.GetVer(App.Settings.GamePath);
            if (version == Constants.BASE_GAME_VERSION)
                return;

            // TODO: We should not assume that this always has a "D". We should just store them by the patchlist VersionId instead.
            var repoShorthand = repo == Repository.Ffxiv ? "game" : repo.ToString().ToLower();
            var fileName = $"{latestVersion}.patch.index";

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