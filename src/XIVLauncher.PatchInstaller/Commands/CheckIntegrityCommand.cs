using System;
using System.Buffers;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common.Game;

namespace XIVLauncher.PatchInstaller.Commands;

public class CheckIntegrityCommand
{
    public static readonly Command Command = new("check-integrity");

    private static readonly Argument<string> GameRootPathArgument = new(
        "game-path",
        "Root folder of a game installation, such as \"C:\\Program Files (x86)\\SquareEnix\\FINAL FANTASY XIV - A Realm Reborn\"");

    private static readonly Option<string> IntegrityFilePathOption = new(
        ["-f", "--integrity-file"],
        $"Path to integrity check file. Leave it empty to download from: {new Uri(IntegrityCheck.INTEGRITY_CHECK_BASE_URL).Host}");

    private static readonly Option<bool> IndexOnlyOption = new(
        ["-i", "--index-only"],
        () => false,
        $"Path to integrity check file. Leave it empty to download from: {new Uri(IntegrityCheck.INTEGRITY_CHECK_BASE_URL).Host}");

    private static readonly Option<int> ThreadCountOption = new(
        ["-t", "--threads"],
        () => Math.Min(Environment.ProcessorCount, 8),
        "Number of threads. Specifying 0 will use all available cores.");

    static CheckIntegrityCommand()
    {
        Command.AddAlias("check-integrity");
        Command.AddArgument(GameRootPathArgument);
        Command.AddOption(IntegrityFilePathOption);
        Command.AddOption(IndexOnlyOption);
        ThreadCountOption.AddValidator(x => x.ErrorMessage = x.GetValueOrDefault<int>() >= 0 ? null : "Must be 0 or more");
        Command.AddOption(ThreadCountOption);
        Command.SetHandler(x => new CheckIntegrityCommand(x.ParseResult).Handle(x.GetCancellationToken()));
    }

    private readonly string gameRootPath;
    private readonly string? integrityFilePath;
    private readonly bool indexOnly;
    private readonly int threadCount;

    private CheckIntegrityCommand(ParseResult parseResult)
    {
        this.gameRootPath = parseResult.GetValueForArgument(GameRootPathArgument);
        this.integrityFilePath = parseResult.GetValueForOption(IntegrityFilePathOption);
        this.indexOnly = parseResult.GetValueForOption(IndexOnlyOption);
        this.threadCount = parseResult.GetValueForOption(ThreadCountOption);
        if (this.threadCount == 0)
            this.threadCount = Environment.ProcessorCount;
        Debug.Assert(this.threadCount > 0);
    }

    private async Task<int> Handle(CancellationToken cancellationToken)
    {
        IntegrityCheck.IntegrityCheckResult icr;

        if (string.IsNullOrWhiteSpace(this.integrityFilePath))
        {
            var gameVersion = File.ReadAllText($@"{this.gameRootPath}\game\ffxivgame.ver");
            Log.Information("Downloading integrity check file for version: {verison}", gameVersion);
            icr = IntegrityCheck.DownloadIntegrityCheckForVersion(gameVersion);
        }
        else
        {
            icr = JsonConvert.DeserializeObject<IntegrityCheck.IntegrityCheckResult>(this.integrityFilePath);
        }

        var fileCounter = 0;
        var matchCounter = 0;

        var hashList = this.indexOnly
                           ? icr.Hashes.Where(x => Path.GetExtension(x.Key).ToLowerInvariant() == ".index").ToDictionary(x => x.Key, x => x.Value)
                           : icr.Hashes;

        await foreach (var (path, calculatedHash, exception) in RunThreadLimited(
                           hashList.Keys.Select(this.CreateValidateFileTask),
                           this.threadCount,
                           cancellationToken))
        {
            var expectedHash = hashList[path];

            fileCounter++;
            matchCounter += expectedHash == calculatedHash ? 1 : 0;

            if (exception is not null)
                Log.Warning("[{counter}/{max}] {path}: {excType}: {excMsg}", fileCounter, hashList.Count, path, exception.GetType(), exception.Message);
            else if (expectedHash != calculatedHash)
                Log.Warning("[{counter}/{max}] {path}: Hashes do not match.", fileCounter, hashList.Count, path);
            else
                Log.Information("[{counter}/{max}] {path}: Verified.", fileCounter, hashList.Count, path);
        }

        Log.Information("{ok} file(s) out of {total} file(s) are verified to be correct.", matchCounter, fileCounter);

        return fileCounter - matchCounter;
    }

    private Func<CancellationToken, Task<(string Path, string? Hash, Exception? Exception)>> CreateValidateFileTask(string path)
    {
        return ct => Task.Run(async () =>
        {
            var buf = ArrayPool<byte>.Shared.Rent(65536);
            string? hash = null;
            Exception? exception = null;

            try
            {
                ct.ThrowIfCancellationRequested();
                using var stream = File.OpenRead($@"{this.gameRootPath}\{path}");
                using var sha1 = SHA1.Create();

                sha1.Initialize();
                var remaining = stream.Length;

                while (remaining > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    var r = (int)Math.Min(buf.Length, remaining);
                    if (r != await stream.ReadAsync(buf, 0, r, ct))
                        throw new IOException("Failed to read wholly");

                    sha1.TransformBlock(buf, 0, r, null, 0);
                    remaining -= r;
                }

                sha1.TransformFinalBlock([], 0, 0);
                hash = string.Join(" ", sha1.Hash.Select(x => x.ToString("X2")));
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                exception = e;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buf);
            }

            return (path, hash, exception);
        }, ct);
    }

    /// <summary>
    /// Runs given tasks, up to <paramref name="numThreads"/> concurrent tasks active.
    /// </summary>
    /// <param name="tasks">Functions that return tasks to be run.</param>
    /// <param name="numThreads">Number of active threads.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <typeparam name="T">Return type.</typeparam>
    /// <returns>Enumerator for return values. Note that the order from <paramref name="tasks"/> is NOT preserved.</returns>
    /// <exception cref="AggregateException">If any of the tasks fail.</exception>
    private static async IAsyncEnumerable<T> RunThreadLimited<T>(
        IEnumerable<Func<CancellationToken, Task<T>>> tasks,
        int numThreads,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var runningTasks = new List<Task<T>>(numThreads);

        foreach (var task in tasks)
        {
            while (runningTasks.Count >= numThreads)
                yield return await WaitOne(cts);

            runningTasks.Add(task(cts.Token));
        }

        while (runningTasks.Any())
            yield return await WaitOne(cts);

        yield break;

        async Task<T> WaitOne(CancellationTokenSource cts2)
        {
            var completedTask = await Task.WhenAny(runningTasks);

            if (completedTask.Status != TaskStatus.RanToCompletion)
            {
                cts2.Cancel();
                await Task.WhenAll(runningTasks);
                throw new AggregateException(
                    runningTasks
                        .Select(x => x.Exception)
                        .SelectMany(x => (IEnumerable<Exception>?)x?.InnerExceptions ?? Array.Empty<Exception>()));
            }

            var result = await completedTask;
            runningTasks.Remove(completedTask);
            return result;
        }
    }
}
