using System;
using System.Buffers;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.Game.Patch.PatchList;
using XIVLauncher.Common.Patching.IndexedZiPatch;
using XIVLauncher.Common.Patching.ZiPatch;
using XIVLauncher.Common.Patching.ZiPatch.Util;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.PlatformAbstractions;

namespace XIVLauncher.PatchInstaller.Commands;

public class IndexUpdateCommand
{
    public static readonly Command Command = new("index-update", "Update patch index files from internet.");

    private static readonly Option<string?> PatchRootPathOption = new(
        "-r",
        () => null,
        "Root directory of patch file hierarchy. Defaults to a directory under the temp directory of the current user.");

    private static readonly Option<string?> UserNameOption = new("-u", () => null, "User ID.");
    private static readonly Option<string?> PasswordOption = new("-p", () => null, "User password.");
    private static readonly Option<string?> OtpOption = new("-o", () => null, "User OTP.");

    private static readonly Option<bool> NoVerifyOldPatchHashOption = new("--no-verify-old-patch-hash", () => false, "Skip patch hash validation for old patch files.");
    private static readonly Option<bool> NoVerifyNewPatchHashOption = new("--no-verify-new-patch-hash", () => false, "Skip patch hash validation for newly downloaded patch files.");

    static IndexUpdateCommand()
    {
        Command.AddOption(PatchRootPathOption);
        Command.AddOption(UserNameOption);
        Command.AddOption(PasswordOption);
        Command.AddOption(OtpOption);
        Command.AddOption(NoVerifyOldPatchHashOption);
        Command.AddOption(NoVerifyNewPatchHashOption);
        Command.SetHandler(x => new IndexUpdateCommand(x.ParseResult).Handle(x.GetCancellationToken()));
    }

    private readonly TempSettings settings;
    private readonly string? username;
    private readonly string? password;
    private readonly string? otp;
    private readonly bool noVerifyOldPatchHash;
    private readonly bool noVerifyNewPatchHash;

    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        UseCookies = false,
        MaxConnectionsPerServer = 65535,
    });

    private IndexUpdateCommand(ParseResult parseResult)
    {
        this.settings = new(
            new(parseResult.GetValueForOption(PatchRootPathOption)
                ?? Path.Combine(Path.GetTempPath(), "XIVLauncher.PatchInstaller")));
        this.username = parseResult.GetValueForOption(UserNameOption);
        this.password = parseResult.GetValueForOption(PasswordOption);
        this.otp = parseResult.GetValueForOption(OtpOption);
        this.noVerifyOldPatchHash = parseResult.GetValueForOption(NoVerifyOldPatchHashOption);
        this.noVerifyNewPatchHash = parseResult.GetValueForOption(NoVerifyNewPatchHashOption);
    }

    private async Task<int> Handle(CancellationToken cancellationToken)
    {
        var la = new Launcher((ISteam?)null, new CommonUniqueIdCache(null), this.settings, "https://launcher.finalfantasyxiv.com/v650/index.html?rc_lang={0}&time={1}");

        var bootPatchListFile = new FileInfo(Path.Combine(this.settings.GamePath.FullName, "bootlist.json"));

        if (!TryReadPatchListEntries(bootPatchListFile, out var bootPatchList) || bootPatchListFile.LastWriteTime < DateTime.Now - TimeSpan.FromHours(1))
        {
            Log.Information("Downloading boot patch information.");
            bootPatchList = await la.CheckBootVersion(this.settings.PatchPath, true);
            File.WriteAllText(bootPatchListFile.FullName, JsonConvert.SerializeObject(bootPatchList, Formatting.Indented));
        }

        await ApplyBootPatch(bootPatchList, cancellationToken);

        var gamePatchListFile = new FileInfo(Path.Combine(this.settings.GamePath.FullName, "gamelist.json"));
        PatchListEntry[] gamePatchList;

        if (this.username is not null && this.password is not null)
        {
            Log.Information("Logging in and fetching game patch information.");
            var lr = await la.Login(this.username, this.password, this.otp ?? "", false, false, this.settings.GamePath, true, false);
            gamePatchList = lr.PendingPatches;
            File.WriteAllText(gamePatchListFile.FullName, JsonConvert.SerializeObject(gamePatchList, Formatting.Indented));
        }
        else if (!TryReadPatchListEntries(gamePatchListFile, out gamePatchList))
        {
            Log.Error("No previous game patch file list found. You need to log in.");
            return -1;
        }

        var indexSources = gamePatchList.GroupBy(x => x.GetRepoName() switch
        {
            "ffxiv" => 0,
            var y => int.Parse(y.Substring(2)),
        }).ToDictionary(x => x.Key, x => x.ToArray());
        indexSources[-1] = bootPatchList;

        var fileCompletions = gamePatchList.ToDictionary(x => x, _ => new TaskCompletionSource<PatchListEntry>());

        foreach (var patch in bootPatchList)
            (fileCompletions[patch] = new()).SetResult(patch);

        await Task.WhenAll(
            Task.Run(async () => await this.DownloadAndVerifyPatchFiles(fileCompletions, gamePatchList, cancellationToken), cancellationToken),
            Task.Run(async () => await this.IndexPatchFiles(fileCompletions, indexSources, cancellationToken), cancellationToken));
        return 0;
    }

    private async Task ApplyBootPatch(PatchListEntry[] bootPatchList, CancellationToken cancellationToken)
    {
        using var zpStore = new SqexFileStreamStore();

        var zpConfig = new ZiPatchConfig(Path.Combine(this.settings.GamePath.FullName, "boot")) { Store = zpStore };
        var bootVerPath = Path.Combine(zpConfig.GamePath, "ffxivboot.ver");
        var bootBckPath = Path.Combine(zpConfig.GamePath, "ffxivboot.bck");
        var bootVerExpected = Path.GetFileNameWithoutExtension(bootPatchList.Last().Url).Substring(1);

        if (File.Exists(bootVerPath) && File.Exists(bootBckPath) && File.ReadAllText(bootVerPath) == bootVerExpected && File.ReadAllText(bootBckPath) == bootVerExpected)
            return;

        foreach (var i in Enumerable.Range(0, bootPatchList.Length))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var patch = bootPatchList[i];
            var uri = new Uri(patch.Url);
            var localPath = new FileInfo(Path.Combine(this.settings.GamePath.FullName, EnsureRelativePath(uri.LocalPath)));

            if (!localPath.Exists || localPath.Length != patch.Length)
            {
                Log.Information("[{index}/{total}] Downloading: {path}", i + 1, bootPatchList.Length, patch.Url);
                var fd = new FileDownloader(Client, patch.Url, localPath.FullName, null, cancellationToken, 8);
                var dtask = fd.Download();

                while (!dtask.IsCompleted)
                {
                    await Task.WhenAny(dtask, Task.Delay(5000, new()));
                    Log.Information(
                        "Downloaded {curr:##,###} bytes out of {total:##,###} bytes ({percentage:F2}%): {speed:##,###}b/s",
                        fd.DownloadedLength,
                        fd.TotalLength,
                        fd.TotalLength == 0 ? 0 : fd.DownloadedLength * 100 / fd.TotalLength,
                        fd.BytesPerSecond);
                }
            }

            Log.Information("[{index}/{total}] Applying: {path}", i + 1, bootPatchList.Length, Path.GetFileName(patch.Url));

            using var ziPatch = new ZiPatchFile(localPath.OpenRead());
            foreach (var chunk in ziPatch.GetChunks())
                chunk.ApplyChunk(zpConfig);
        }

        File.WriteAllText(bootVerPath, bootVerExpected);
        File.WriteAllText(Path.ChangeExtension(bootVerPath, ".bck"), bootVerExpected);
    }

    private async Task DownloadAndVerifyPatchFiles(
        IReadOnlyDictionary<PatchListEntry, TaskCompletionSource<PatchListEntry>> fileCompletions,
        IReadOnlyList<PatchListEntry> gamePatchList,
        CancellationToken cancellationToken)
    {
        foreach (var i in Enumerable.Range(0, gamePatchList.Count))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var patch = gamePatchList[i];
            var uri = new Uri(patch.Url);
            var localPath = new FileInfo(Path.Combine(this.settings.GamePath.FullName, EnsureRelativePath(uri.LocalPath)));

            if (patch.HashType != "sha1")
                throw new NotSupportedException($"HashType \"{patch.HashType}\" is not supported for: {uri}");

            var downloadRequired = !localPath.Exists || localPath.Length != patch.Length;

            if (!downloadRequired && !this.noVerifyOldPatchHash)
            {
                Log.Information("[{index}/{total}]: Verifying: {path}", i + 1, gamePatchList.Count, patch.Url);
                downloadRequired = !await CheckPatchHashAsync(localPath, patch, cancellationToken);
            }

            if (downloadRequired)
            {
                Log.Information("[{index}/{total}]: Downloading: {path}", i + 1, gamePatchList.Count, patch.Url);
                var fd = new FileDownloader(Client, patch.Url, localPath.FullName, null, cancellationToken, 8);
                var dtask = fd.Download();

                while (!dtask.IsCompleted)
                {
                    await Task.WhenAny(dtask, Task.Delay(5000, new()));
                    Log.Information(
                        "Downloaded {curr:##,###} bytes out of {total:##,###} bytes ({percentage:F2}%): {speed:##,###}b/s",
                        fd.DownloadedLength,
                        fd.TotalLength,
                        fd.TotalLength == 0 ? 0 : fd.DownloadedLength * 100 / fd.TotalLength,
                        fd.BytesPerSecond);
                }

                // propagate exception if any happened
                await dtask;

                if (!this.noVerifyNewPatchHash && !await CheckPatchHashAsync(localPath, patch, cancellationToken))
                    throw new IOException("Downloaded file did not pass hash check");
            }

            fileCompletions[patch].SetResult(patch);
        }
    }

    private async Task IndexPatchFiles(
        IReadOnlyDictionary<PatchListEntry, TaskCompletionSource<PatchListEntry>> fileCompletions,
        Dictionary<int, PatchListEntry[]> indexSources,
        CancellationToken cancellationToken)
    {
        foreach (var (expac, patches) in indexSources.Select(x => (x.Key, x.Value)))
        {
            var expacName = expac switch
            {
                -1 => "boot",
                0 => "ffxiv",
                _ => $"ex{expac}",
            };
            var patchFilePaths = patches.Select(x => Path.Combine(this.settings.GamePath.FullName, EnsureRelativePath(new Uri(x.Url).LocalPath))).ToList();
            var firstPatchFileIndex = patchFilePaths.Count - 1;
            IndexedZiPatchIndex? patchIndex = null;

            Log.Information("[{expac}]: Finding most recent reusable patch index", expacName);

            for (; firstPatchFileIndex >= 0; firstPatchFileIndex--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var indexPath = patchFilePaths[firstPatchFileIndex] + ".index";
                if (!File.Exists(indexPath))
                    continue;

                try
                {
                    patchIndex = new(new BinaryReader(new DeflateStream(new FileStream(indexPath, FileMode.Open, FileAccess.Read), CompressionMode.Decompress)));
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Failed to read; ignoring {f}", Path.GetFileName(indexPath));
                    continue;
                }

                if (patchIndex.ExpacVersion != expac)
                    continue;

                var i = 0;

                for (; i < patchIndex.Sources.Count && i < firstPatchFileIndex; i++)
                {
                    if (patchIndex.Sources[i] != Path.GetFileName(patchFilePaths[i]))
                        break;
                }

                if (i == firstPatchFileIndex)
                    break;

                firstPatchFileIndex = i;
            }

            ++firstPatchFileIndex;

            if (firstPatchFileIndex >= patchFilePaths.Count)
            {
                Log.Information("[{expac}]: Patch index generation not needed; skipping: {indexFileName}", expacName, Path.GetFileName(patchFilePaths[patchFilePaths.Count - 1]));
                continue;
            }

            var sources = new List<Stream>();
            var patchFiles = new List<ZiPatchFile>();
            patchIndex ??= new(expac);

            try
            {
                for (var i = 0; i < patchFilePaths.Count; ++i)
                {
                    await Task.WhenAny(fileCompletions[patches[i]].Task, Task.Delay(int.MaxValue, cancellationToken));
                    cancellationToken.ThrowIfCancellationRequested();

                    var patchFilePath = patchFilePaths[i];
                    sources.Add(new FileStream(patchFilePath, FileMode.Open, FileAccess.Read));
                    patchFiles.Add(new(sources[sources.Count - 1]));

                    if (i < firstPatchFileIndex)
                        continue;

                    Log.Information("[{expac}: {index}/{total}]: Indexing: {file}", expacName, i + 1, patchFilePaths.Count, patchFilePath);
                    await patchIndex.ApplyZiPatch(Path.GetFileName(patchFilePath), patchFiles[patchFiles.Count - 1], cancellationToken);

                    Log.Information("[{expac}: {index}/{total}]: Hashing: {file}", expacName, i + 1, patchFilePaths.Count, patchFilePath);
                    await patchIndex.CalculateCrc32(sources, cancellationToken);

                    using (var writer = new BinaryWriter(new DeflateStream(new FileStream(patchFilePath + ".index.tmp", FileMode.Create), CompressionLevel.Optimal)))
                        patchIndex.WriteTo(writer);

                    if (File.Exists(patchFilePath + ".index"))
                    {
                        for (var j = 0;; j++)
                        {
                            if (File.Exists($"{patchFilePath}.index.{j}.old"))
                                continue;

                            File.Move(patchFilePath + ".index", $"{patchFilePath}.index.{j}.old");
                            break;
                        }
                    }

                    File.Move($"{patchFilePath}.index.tmp", $"{patchFilePath}.index");
                }
            }
            finally
            {
                foreach (var source in sources)
                    source.Dispose();
            }
        }
    }

    private static async Task<bool> CheckPatchHashAsync(FileInfo localPath, PatchListEntry patch, CancellationToken cancellationToken)
    {
        using var sha1 = SHA1.Create();
        using var f = localPath.OpenRead();

        var buf = ArrayPool<byte>.Shared.Rent(65536);

        try
        {
            for (var j = 0; j < patch.Hashes.Length; j++)
            {
                sha1.Initialize();
                var remaining = Math.Min(patch.Length - (j * patch.HashBlockSize), patch.HashBlockSize);

                while (remaining > 0)
                {
                    var r = (int)Math.Min(buf.Length, remaining);
                    if (r != await f.ReadAsync(buf, 0, r, cancellationToken))
                        throw new IOException("Failed to read wholly");

                    sha1.TransformBlock(buf, 0, r, null, 0);
                    remaining -= r;
                }

                sha1.TransformFinalBlock([], 0, 0);

                if (string.Join("", sha1.Hash.Select(x => x.ToString("x2"))) != patch.Hashes[j])
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    private static bool TryReadPatchListEntries(FileInfo path, out PatchListEntry[] entries)
    {
        entries = null!;
        if (!path.Exists)
            return false;

        try
        {
            if (JsonConvert.DeserializeObject<PatchListEntry[]>(File.ReadAllText(path.FullName)) is { } r)
            {
                Log.Information($"Using cached file file: {path}");
                entries = r;
                return true;
            }
        }
        catch (Exception e)
        {
            Log.Information(e, $"Ignoring file: {path}");
        }

        return false;
    }

    private static string EnsureRelativePath(string path)
    {
        while (true)
        {
            if (path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("\\", StringComparison.Ordinal))
            {
                path = path.Substring(1);
                continue;
            }

            if (path.StartsWith("./", StringComparison.Ordinal) || path.StartsWith(".\\", StringComparison.Ordinal))
            {
                path = path.Substring(2);
                continue;
            }

            if (path.StartsWith("../", StringComparison.Ordinal) || path.StartsWith("..\\", StringComparison.Ordinal))
            {
                path = path.Substring(3);
                continue;
            }

            return path;
        }
    }

    private class TempSettings : ISettings
    {
        public TempSettings(DirectoryInfo patchPath)
        {
            this.PatchPath = patchPath;
        }

        public string AcceptLanguage => "en-US";
        public ClientLanguage? ClientLanguage => Common.ClientLanguage.English;
        public bool? KeepPatches => true;
        public DirectoryInfo PatchPath { get; }
        public DirectoryInfo GamePath => this.PatchPath;
        public AcquisitionMethod? PatchAcquisitionMethod => AcquisitionMethod.NetDownloader;
        public long SpeedLimitBytes { get; set; }
        public int DalamudInjectionDelayMs => 0;
    }
}
