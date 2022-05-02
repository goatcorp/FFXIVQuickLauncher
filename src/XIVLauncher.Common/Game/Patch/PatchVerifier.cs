using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common.Game.Exceptions;
using XIVLauncher.Common.Patching.IndexedZiPatch;
using XIVLauncher.Common.Patching.Util;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Game.Patch
{
    public class PatchVerifier : IDisposable
    {
        private const string RepairRecyclerDirectory = "repair_recycler";
        private static readonly Regex[] GameIgnoreUnnecessaryFilePatterns = new Regex[]
        {
            // Base game version files.
            new Regex(@"^ffxivgame\.(?:bck|ver)$", RegexOptions.IgnoreCase),

            // Expansion version files.
            new Regex(@"^sqpack/ex([1-9][0-9]*)/ex\1\.(?:bck|ver)$", RegexOptions.IgnoreCase),

            // Under WINE, since .dat files are actually WMV videos, the game will become unusable.
            // Bink videos will be used instead in those cases.
            new Regex(@"^movie/ffxiv/0000[0-3]\.bk2$", RegexOptions.IgnoreCase),
            
            // DXVK can deal with corrupted cache files by itself, so let it do the job by itself.
            new Regex(@"^ffxiv_dx11\.dxvk-cache$", RegexOptions.IgnoreCase),

            // Repair recycle bin folder.
            new Regex(@"^repair_recycler/.*$", RegexOptions.IgnoreCase),
        };

        private readonly ISettings _settings;
        private readonly int _maxExpansionToCheck;
        private HttpClient _client;
        private CancellationTokenSource _cancellationTokenSource = new();

        private Dictionary<Repository, string> _repoMetaPaths = new();
        private Dictionary<string, PatchSource> _patchSources = new();

        private Task _verificationTask;
        private List<Tuple<long, long>> _reportedProgresses = new();

        public int ProgressUpdateInterval { get; private set; }
        public int NumBrokenFiles { get; private set; } = 0;
        public string MovedFileToDir { get; private set; } = null;
        public List<string> MovedFiles { get; private set; } = new();
        public int PatchSetIndex { get; private set; }
        public int PatchSetCount { get; private set; }
        public int TaskIndex { get; private set; }
        public long Progress { get; private set; }
        public long Total { get; private set; }
        public int TaskCount { get; private set; }
        public IndexedZiPatchInstaller.InstallTaskState CurrentMetaInstallState { get; private set; } = IndexedZiPatchInstaller.InstallTaskState.NotStarted;
        public string CurrentFile { get; private set; }
        public long Speed { get; private set; }
        public Exception LastException { get; private set; }

        private const string BASE_URL = "https://raw.githubusercontent.com/goatcorp/patchinfo/main/";

        public enum VerifyState
        {
            NotStarted,
            DownloadMeta,
            VerifyAndRepair,
            Done,
            Cancelled,
            Error
        }

        private struct PatchSource
        {
            public FileInfo FileInfo;
            public Uri Uri;
        }

        private class VerifyVersions
        {
            [JsonProperty("boot")]
            public string Boot { get; set; }

            [JsonProperty("bootRevision")]
            public int BootRevision { get; set; }

            [JsonProperty("game")]
            public string Game { get; set; }

            [JsonProperty("gameRevision")]
            public int GameRevision { get; set; }

            [JsonProperty("ex1")]
            public string Ex1 { get; set; }

            [JsonProperty("ex1Revision")]
            public int Ex1Revision { get; set; }

            [JsonProperty("ex2")]
            public string Ex2 { get; set; }

            [JsonProperty("ex2Revision")]
            public int Ex2Revision { get; set; }

            [JsonProperty("ex3")]
            public string Ex3 { get; set; }

            [JsonProperty("ex3Revision")]
            public int Ex3Revision { get; set; }

            [JsonProperty("ex4")]
            public string Ex4 { get; set; }

            [JsonProperty("ex4Revision")]
            public int Ex4Revision { get; set; }
        }

        public VerifyState State { get; private set; } = VerifyState.NotStarted;

        public PatchVerifier(ISettings settings, Launcher.LoginResult loginResult, int progressUpdateInterval, int maxExpansion)
        {
            this._settings = settings;
            _client = new HttpClient();
            ProgressUpdateInterval = progressUpdateInterval;
            _maxExpansionToCheck = maxExpansion;

            SetLoginState(loginResult);
        }

        public void Start()
        {
            Debug.Assert(_patchSources.Count != 0);
            Debug.Assert(_verificationTask == null || _verificationTask.IsCompleted);

            _cancellationTokenSource = new();
            _reportedProgresses.Clear();
            NumBrokenFiles = 0;
            PatchSetIndex = 0;
            PatchSetCount = 0;
            TaskIndex = 0;
            Progress = 0;
            Total = 0;
            TaskCount = 0;
            CurrentFile = null;
            Speed = 0;
            CurrentMetaInstallState = IndexedZiPatchInstaller.InstallTaskState.NotStarted;
            LastException = null;

            _verificationTask = Task.Run(this.RunVerifier, _cancellationTokenSource.Token);
        }

        public Task Cancel()
        {
            _cancellationTokenSource.Cancel();
            return WaitForCompletion();
        }

        public Task WaitForCompletion()
        {
            return _verificationTask ?? Task.CompletedTask;
        }

        private void SetLoginState(Launcher.LoginResult result)
        {
            _patchSources.Clear();

            foreach (var patch in result.PendingPatches)
            {
                var repoName = patch.GetRepoName();
                if (repoName == "ffxiv")
                    repoName = "ex0";

                _patchSources.Add($"{repoName}:{Path.GetFileName(patch.GetFilePath())}", new PatchSource()
                {
                    FileInfo = new FileInfo(Path.Combine(_settings.PatchPath.FullName, patch.GetFilePath())),
                    Uri = new Uri(patch.Url),
                });
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

            var elapsedMs = _reportedProgresses.Last().Item1 - _reportedProgresses.First().Item1;
            if (elapsedMs == 0)
                Speed = 0;
            else
                Speed = (_reportedProgresses.Last().Item2 - _reportedProgresses.First().Item2) * 10 * 1000 * 1000 / elapsedMs;
        }

        public async Task MoveUnnecessaryFiles(IndexedZiPatchIndexRemoteInstaller remote, string gamePath, HashSet<string> targetRelativePaths)
        {
            this.MovedFileToDir = Path.Combine(gamePath, RepairRecyclerDirectory, DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            var rootPathInfo = new DirectoryInfo(gamePath);
            gamePath = rootPathInfo.FullName;

            Queue<DirectoryInfo> directoriesToVisit = new();
            HashSet<DirectoryInfo> directoriesVisited = new();
            directoriesToVisit.Enqueue(rootPathInfo);
            directoriesVisited.Add(rootPathInfo);

            while (directoriesToVisit.Any())
            {
                var dir = directoriesToVisit.Dequeue();

                // For directories, ignore if final path does not belong in the root path.
                if (!dir.FullName.ToLowerInvariant().Replace('\\', '/').StartsWith(gamePath.ToLowerInvariant().Replace('\\', '/')))
                    continue;

                var relativeDirPath = dir == rootPathInfo ? "" : dir.FullName.Substring(gamePath.Length + 1).Replace('\\', '/');
                if (GameIgnoreUnnecessaryFilePatterns.Any(x => x.IsMatch(relativeDirPath)))
                    continue;

                if (!dir.EnumerateFileSystemInfos().Any())
                {
                    await remote.RemoveDirectory(dir.FullName);
                    await remote.CreateDirectory(Path.Combine(this.MovedFileToDir, relativeDirPath));
                    continue;
                }

                foreach (var subdir in dir.EnumerateDirectories())
                {
                    if (directoriesVisited.Contains(subdir))
                        continue;

                    directoriesVisited.Add(subdir);
                    directoriesToVisit.Enqueue(subdir);
                }

                foreach (var file in dir.EnumerateFiles())
                {
                    if (!file.FullName.ToLowerInvariant().Replace('\\', '/').StartsWith(gamePath.ToLowerInvariant().Replace('\\', '/')))
                        continue;

                    var relativePath = file.FullName.Substring(gamePath.Length + 1).Replace('\\', '/');
                    if (targetRelativePaths.Any(x => x.Replace('\\', '/').ToLowerInvariant() == relativePath.ToLowerInvariant()))
                        continue;

                    if (GameIgnoreUnnecessaryFilePatterns.Any(x => x.IsMatch(relativePath)))
                        continue;

                    await remote.MoveFile(file.FullName, Path.Combine(this.MovedFileToDir, relativePath));
                    MovedFiles.Add(relativePath);
                }
            }
        }

        private async Task RunVerifier()
        {
            State = VerifyState.NotStarted;
            LastException = null;
            try
            {
                var assemblyLocation = AppContext.BaseDirectory;
                using var remote = new IndexedZiPatchIndexRemoteInstaller(Path.Combine(assemblyLocation!, "XIVLauncher.PatchInstaller.exe"),
                    AdminAccessRequired(_settings.GamePath.FullName));
                await remote.SetWorkerProcessPriority(ProcessPriorityClass.Idle).ConfigureAwait(false);

                while (!_cancellationTokenSource.IsCancellationRequested && State != VerifyState.Done)
                {
                    switch (State)
                    {

                        case VerifyState.NotStarted:
                            State = VerifyState.DownloadMeta;
                            break;

                        case VerifyState.DownloadMeta:
                            await this.GetPatchMeta().ConfigureAwait(false);
                            State = VerifyState.VerifyAndRepair;
                            break;

                        case VerifyState.VerifyAndRepair:
                            Debug.Assert(_repoMetaPaths.Count != 0);

                            const int MAX_CONCURRENT_CONNECTIONS_FOR_PATCH_SET = 8;
                            const int REATTEMPT_COUNT = 5;

                            CurrentFile = null;
                            TaskIndex = 0;
                            PatchSetIndex = 0;
                            PatchSetCount = _repoMetaPaths.Count;
                            Progress = Total = 0;

                            HashSet<string> targetRelativePaths = new();

                            var bootPath = Path.Combine(_settings.GamePath.FullName, "boot");
                            var gamePath = Path.Combine(_settings.GamePath.FullName, "game");

                            foreach (var metaPath in _repoMetaPaths)
                            {
                                var patchIndex = new IndexedZiPatchIndex(new BinaryReader(new DeflateStream(new FileStream(metaPath.Value, FileMode.Open, FileAccess.Read), CompressionMode.Decompress)));
                                var adjustedGamePath = patchIndex.ExpacVersion == IndexedZiPatchIndex.EXPAC_VERSION_BOOT ? bootPath : gamePath;

                                foreach (var target in patchIndex.Targets)
                                    targetRelativePaths.Add(target.RelativePath);

                                void UpdateVerifyProgress(int targetIndex, long progress, long max)
                                {
                                    CurrentFile = patchIndex[Math.Min(targetIndex, patchIndex.Length - 1)].RelativePath;
                                    TaskIndex = targetIndex;
                                    Progress = Math.Min(progress, max);
                                    Total = max;
                                    RecordProgressForEstimation();
                                }

                                void UpdateInstallProgress(int sourceIndex, long progress, long max, IndexedZiPatchInstaller.InstallTaskState state)
                                {
                                    CurrentFile = patchIndex.Sources[Math.Min(sourceIndex, patchIndex.Sources.Count - 1)];
                                    TaskIndex = sourceIndex;
                                    Progress = Math.Min(progress, max);
                                    Total = max;
                                    CurrentMetaInstallState = state;
                                    RecordProgressForEstimation();
                                }

                                try
                                {
                                    remote.OnVerifyProgress += UpdateVerifyProgress;
                                    remote.OnInstallProgress += UpdateInstallProgress;
                                    await remote.ConstructFromPatchFile(patchIndex, ProgressUpdateInterval).ConfigureAwait(false);

                                    var fileBroken = new bool[patchIndex.Length].ToList();
                                    var repaired = false;
                                    for (var attemptIndex = 0; attemptIndex < REATTEMPT_COUNT; attemptIndex++)
                                    {
                                        CurrentMetaInstallState = IndexedZiPatchInstaller.InstallTaskState.NotStarted;

                                        TaskCount = patchIndex.Length;
                                        Progress = Total = TaskIndex = 0;
                                        _reportedProgresses.Clear();

                                        await remote.SetTargetStreamsFromPathReadOnly(adjustedGamePath).ConfigureAwait(false);
                                        // TODO: check one at a time if random access is slow?
                                        await remote.VerifyFiles(attemptIndex > 0, Environment.ProcessorCount, _cancellationTokenSource.Token).ConfigureAwait(false);

                                        var missingPartIndicesPerTargetFile = await remote.GetMissingPartIndicesPerTargetFile().ConfigureAwait(false);
                                        if ((repaired = missingPartIndicesPerTargetFile.All(x => !x.Any())))
                                            break;
                                        else if (attemptIndex == 1)
                                            Log.Warning("One or more of local copies of patch files seem to be corrupt, if any. Ignoring local patch files for further attempts.");

                                        for (var i = 0; i < missingPartIndicesPerTargetFile.Count; i++)
                                            if (missingPartIndicesPerTargetFile[i].Any())
                                                fileBroken[i] = true;

                                        TaskCount = patchIndex.Sources.Count;
                                        Progress = Total = TaskIndex = 0;
                                        _reportedProgresses.Clear();
                                        var missing = await remote.GetMissingPartIndicesPerPatch().ConfigureAwait(false);

                                        await remote.SetTargetStreamsFromPathReadWriteForMissingFiles(adjustedGamePath).ConfigureAwait(false);
                                        var prefix = patchIndex.ExpacVersion == IndexedZiPatchIndex.EXPAC_VERSION_BOOT ? "boot:" : $"ex{patchIndex.ExpacVersion}:";
                                        for (var i = 0; i < patchIndex.Sources.Count; i++)
                                        {
                                            var patchSourceKey = prefix + patchIndex.Sources[i];

                                            if (!missing[i].Any())
                                                continue;
                                            else
                                                Log.Information("Looking for patch file {0} (key: \"{1}\")", patchIndex.Sources[i], patchSourceKey);

                                            if (!_patchSources.TryGetValue(patchSourceKey, out var source))
                                                throw new InvalidOperationException($"Key \"{patchSourceKey}\" not found in _patchSources");

                                            // We might be trying again because local copy of the patch file might be corrupt, so refer to the local copy only for the first attempt.
                                            if (attemptIndex == 0 && source.FileInfo.Exists)
                                                await remote.QueueInstall(i, source.FileInfo, MAX_CONCURRENT_CONNECTIONS_FOR_PATCH_SET).ConfigureAwait(false);
                                            else
                                                await remote.QueueInstall(i, source.Uri, null, MAX_CONCURRENT_CONNECTIONS_FOR_PATCH_SET).ConfigureAwait(false);
                                        }

                                        CurrentMetaInstallState = IndexedZiPatchInstaller.InstallTaskState.Connecting;
                                        try
                                        {
                                            await remote.Install(MAX_CONCURRENT_CONNECTIONS_FOR_PATCH_SET, _cancellationTokenSource.Token).ConfigureAwait(false);
                                            await remote.WriteVersionFiles(adjustedGamePath).ConfigureAwait(false);
                                        }
                                        catch (Exception e)
                                        {
                                            Log.Error(e, "remote.Install");
                                            if (attemptIndex == REATTEMPT_COUNT - 1)
                                                throw;
                                        }
                                    }
                                    if (!repaired)
                                        throw new IOException("Failed to repair after 5 attempts");
                                    NumBrokenFiles += fileBroken.Where(x => x).Count();
                                    PatchSetIndex++;
                                }
                                finally
                                {
                                    remote.OnVerifyProgress -= UpdateVerifyProgress;
                                    remote.OnInstallProgress -= UpdateInstallProgress;
                                }
                            }

                            await MoveUnnecessaryFiles(remote, gamePath, targetRelativePaths);

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
                    Log.Information("_patchSources had following:");
                    foreach (var kvp in _patchSources)
                    {
                        Log.Information("* \"{0}\" = {1} / {2}({3})", kvp.Key, kvp.Value.Uri.ToString(), kvp.Value.FileInfo.FullName, kvp.Value.FileInfo.Exists ? "Exists" : "Nonexistent");
                    }

                    LastException = ex;
                    State = VerifyState.Error;
                }
            }
        }

        private async Task GetPatchMeta()
        {
            PatchSetCount = 6;
            PatchSetIndex = 0;

            _repoMetaPaths.Clear();

            var metaFolder = Path.Combine(Paths.RoamingPath, "patchMeta");
            Directory.CreateDirectory(metaFolder);

            CurrentFile = "latest.json";
            Total = Progress = 0;

            var latestVersionJson = await _client.GetStringAsync(BASE_URL + "latest.json").ConfigureAwait(false);
            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            var latestVersion = JsonConvert.DeserializeObject<VerifyVersions>(latestVersionJson);

            PatchSetIndex++;
            await this.GetRepoMeta(Repository.Ffxiv, latestVersion.Game, metaFolder, latestVersion.GameRevision).ConfigureAwait(false);
            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            PatchSetIndex++;
            if (_maxExpansionToCheck >= 1)
                await this.GetRepoMeta(Repository.Ex1, latestVersion.Ex1, metaFolder, latestVersion.Ex1Revision).ConfigureAwait(false);
            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            PatchSetIndex++;
            if (_maxExpansionToCheck >= 2)
                await this.GetRepoMeta(Repository.Ex2, latestVersion.Ex2, metaFolder, latestVersion.Ex2Revision).ConfigureAwait(false);
            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            PatchSetIndex++;
            if (_maxExpansionToCheck >= 3)
                await this.GetRepoMeta(Repository.Ex3, latestVersion.Ex3, metaFolder, latestVersion.Ex3Revision).ConfigureAwait(false);
            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            PatchSetIndex++;
            if (_maxExpansionToCheck >= 4)
                await this.GetRepoMeta(Repository.Ex4, latestVersion.Ex4, metaFolder, latestVersion.Ex4Revision).ConfigureAwait(false);
            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            PatchSetIndex++;
        }

        private async Task GetRepoMeta(Repository repo, string latestVersion, string baseDir, int patchIndexFileRevision)
        {
            _reportedProgresses.Clear();
            CurrentFile = latestVersion;
            Total = 32 * 1048576;
            Progress = 0;

            var version = repo.GetVer(_settings.GamePath);
            if (version == Constants.BASE_GAME_VERSION)
                return;

            // TODO: We should not assume that this always has a "D". We should just store them by the patchlist VersionId instead.
            var repoShorthand = repo == Repository.Ffxiv ? "game" : repo.ToString().ToLower();
            var fileName = $"{latestVersion}.patch.index";

            var metaPath = Path.Combine(baseDir, repoShorthand);
            var filePath = Path.Combine(metaPath, fileName) + (patchIndexFileRevision > 0 ? $".v{patchIndexFileRevision}" : "");
            Directory.CreateDirectory(metaPath);

            if (!File.Exists(filePath))
            {
                var request = await _client.GetAsync($"{BASE_URL}{repoShorthand}/{fileName}", HttpCompletionOption.ResponseHeadersRead, _cancellationTokenSource.Token).ConfigureAwait(false);
                if (request.StatusCode == HttpStatusCode.NotFound)
                    throw new NoVersionReferenceException(repo, version);

                request.EnsureSuccessStatusCode();

                Total = request.Content.Headers.ContentLength.GetValueOrDefault(Total);

                var tempFile = new FileInfo(filePath + ".tmp");
                var complete = false;

                try
                {
                    using var sourceStream = await request.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    using var buffer = ReusableByteBufferManager.GetBuffer();

                    using (var targetStream = tempFile.OpenWrite())
                    {
                        while (true)
                        {
                            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                            int read = await sourceStream.ReadAsync(buffer.Buffer, 0, buffer.Buffer.Length, _cancellationTokenSource.Token).ConfigureAwait(false);
                            if (read == 0)
                                break;

                            Total = Math.Max(Total, Progress + read);
                            Progress += read;
                            RecordProgressForEstimation();
                            await targetStream.WriteAsync(buffer.Buffer, 0, read, _cancellationTokenSource.Token).ConfigureAwait(false);
                        }
                    }
                    complete = true;
                }
                finally
                {
                    if (complete)
                        tempFile.MoveTo(filePath);
                    else
                    {
                        try
                        {
                            if (tempFile.Exists)
                                tempFile.Delete();
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Failed to delete temp file at {0}", tempFile.FullName);
                        }
                    }
                }
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