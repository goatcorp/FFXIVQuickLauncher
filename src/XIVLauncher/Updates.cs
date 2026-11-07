using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CheapLoc;
using Newtonsoft.Json;
using Serilog;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;
using XIVLauncher.Common;
using XIVLauncher.Common.Http.HappyEyeballs;
using XIVLauncher.Common.Util;
using XIVLauncher.Windows;

#nullable enable

namespace XIVLauncher
{
    internal class Updates
    {
        public event Action<bool>? OnUpdateCheckFinished;

#if DEV_SERVER
        private const string LeaseMetaUrl = "http://localhost:5025/Launcher/GetLease";
        private const string LeaseFileUrl = "http://localhost:5025/Launcher/GetFile";
#else
        private const string LeaseMetaUrl = "https://kamori.goats.dev/Launcher/GetLease";
        private const string LeaseFileUrl = "https://kamori.goats.dev/Launcher/GetFile";
#endif

        private const string TrackRelease = "Release";
        private const string TrackPrerelease = "Prerelease";

        private const int MaxTries = 3;
        private const int CopyBufferSize = 81920;

        private static readonly TimeSpan RetryBackoff = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan StallTimeout = TimeSpan.FromMinutes(5);

        public static Lease? UpdateLease { get; private set; }

#pragma warning disable CS8618
        [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
        public class ErrorNewsData
        {
            [JsonPropertyName("until")]
            public uint ShowUntil { get; set; }

            [JsonPropertyName("message")]
            public string Message { get; set; }

            [JsonPropertyName("isError")]
            public bool IsError { get; set; }
        }
#pragma warning restore CS8618

        [Flags]
        public enum LeaseFeatureFlags
        {
            None = 0,
            GlobalDisableDalamud = 1,
        }

#pragma warning disable CS8618
        public class Lease
        {
            public bool Success { get; set; }

            public string? Message { get; set; }

            public string? CutOffBootver { get; set; }

            public string FrontierUrl { get; set; }

            public LeaseFeatureFlags Flags { get; set; }

            public string ReleasesList { get; set; }

            public string? ReleasesJson { get; set; }

            public DateTime? ValidUntil { get; set; }
        }
#pragma warning restore CS8618

        private const string FakeUrlPrefix = "https://example.com/";

        private class FakeSquirrelFileDownloader : IFileDownloader
        {
            private readonly HttpClient client;
            private readonly Lease lease;
            private readonly IReadOnlyDictionary<string, string> leaseHeaders;

            public FakeSquirrelFileDownloader(HttpClient client, Lease lease, IReadOnlyDictionary<string, string> leaseHeaders)
            {
                this.client = client;
                this.lease = lease;
                this.leaseHeaders = leaseHeaders;
            }

            public async Task DownloadFile(string url, string targetFile, Action<int> progress, IDictionary<string, string>? headers = null, double timeout = 30, CancellationToken cancelToken = new CancellationToken())
            {
                Log.Verbose("FakeSquirrel: DownloadFile from {Url} to {Target}", url, targetFile);

                if (!url.StartsWith(FakeUrlPrefix, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"DownloadFile called for unknown url: {url}");
                }

                var fileNeeded = url.Substring(FakeUrlPrefix.Length);
                var numBytes = await Retry(() => this.Download(fileNeeded, targetFile, cancelToken), cancelToken).ConfigureAwait(false);

                Log.Verbose("FakeSquirrel: OK, downloaded {NumBytes}b for {File}", numBytes, fileNeeded);
            }

            private async Task<long> Download(string fileNeeded, string targetFile, CancellationToken cancelToken)
            {
                using var request = CreateLeaseRequest($"{LeaseFileUrl}/{fileNeeded}", this.leaseHeaders);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancelToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                using var contentStream = await response.Content.ReadAsStreamAsync(cancelToken).ConfigureAwait(false);

                using Stream fileStream = File.Open(targetFile, FileMode.Create);

                using var stallCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
                var buffer = new byte[CopyBufferSize];
                long numBytes = 0;

                while (true)
                {
                    stallCts.CancelAfter(StallTimeout);
                    var read = await contentStream.ReadAsync(buffer, stallCts.Token).ConfigureAwait(false);

                    if (read == 0)
                    {
                        return numBytes;
                    }

                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancelToken).ConfigureAwait(false);
                    numBytes += read;
                }
            }

            public Task<byte[]> DownloadBytes(string url, IDictionary<string, string>? headers = null, double timeout = 30)
            {
                Log.Verbose("FakeSquirrel: DownloadUrl from {Url}", url);
                var fileNeeded = url[FakeUrlPrefix.Length..];

                if (fileNeeded.StartsWith("RELEASES", StringComparison.Ordinal))
                    return Task.FromResult(Encoding.UTF8.GetBytes(lease.ReleasesList));

                if (fileNeeded.StartsWith("releases.win.json", StringComparison.Ordinal) && !string.IsNullOrEmpty(lease.ReleasesJson))
                    return Task.FromResult(Encoding.UTF8.GetBytes(lease.ReleasesJson));

                throw new ArgumentException($"DownloadUrl called for unknown file: {url}");
            }

            public Task<string> DownloadString(string url, IDictionary<string, string>? headers = null, double timeout = 30)
            {
                Log.Verbose("FakeSquirrel: DownloadUrl from {Url}", url);
                var fileNeeded = url[FakeUrlPrefix.Length..];

                if (fileNeeded.StartsWith("RELEASES", StringComparison.Ordinal))
                    return Task.FromResult(lease.ReleasesList);

                if (fileNeeded.StartsWith("releases.win.json", StringComparison.Ordinal) && !string.IsNullOrEmpty(lease.ReleasesJson))
                    return Task.FromResult(lease.ReleasesJson);

                throw new ArgumentException($"DownloadUrl called for unknown file: {url}");
            }
        }

        public class LeaseAcquisitionException : Exception
        {
            public LeaseAcquisitionException(string message)
                : base($"Couldn't acquire lease: {message}")
            {
            }
        }

        private class UpdateResult(UpdateManager manager, Lease lease)
        {
            public UpdateManager Manager { get; private set; } = manager;
            public Lease Lease { get; private set; } = lease;
        }

        private static async Task<UpdateResult> LeaseUpdateManager(HttpClient client, bool prerelease)
        {
            var leaseHeaders = new Dictionary<string, string>
            {
                ["X-XL-Track"] = prerelease ? TrackPrerelease : TrackRelease,
                ["X-XL-LV"] = "0",
                ["X-XL-HaveVersion"] = AppUtil.GetAssemblyVersion(),
                ["X-XL-HaveAddon"] = App.Settings.InGameAddonEnabled ? "yes" : "no",
                ["X-XL-FirstStart"] = App.Settings.VersionUpgradeLevel == 0 ? "yes" : "no",
                ["X-XL-HaveWine"] = EnvironmentSettings.IsWine ? "yes" : "no",
            };

            var leaseJson = await Retry(() => GetLease(client, leaseHeaders), CancellationToken.None).ConfigureAwait(false);
            var leaseData = JsonConvert.DeserializeObject<Lease>(leaseJson);

            if (leaseData == null || !leaseData.Success)
                throw new LeaseAcquisitionException(leaseData?.Message ?? "No lease data");

            var fakeDownloader = new FakeSquirrelFileDownloader(client, leaseData, leaseHeaders);
            var source = new SimpleWebSource(FakeUrlPrefix, fakeDownloader);

            // Velopack bug: Delta updates are not reliable at the moment
            // https://github.com/velopack/velopack/issues/751
            var updateOptions = new UpdateOptions
            {
                MaximumDeltasBeforeFallback = -1,
            };

            var manager = new UpdateManager(source, updateOptions);

            return new UpdateResult(manager, leaseData);
        }

        private static HttpRequestMessage CreateLeaseRequest(string url, IReadOnlyDictionary<string, string> leaseHeaders)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("XIVLauncher", AppUtil.GetGitHash()));

            foreach (var (key, value) in leaseHeaders)
            {
                request.Headers.AddWithoutValidation(key, value);
            }

            return request;
        }

        private static async Task<string> GetLease(HttpClient client, IReadOnlyDictionary<string, string> leaseHeaders)
        {
            using var request = CreateLeaseRequest(LeaseMetaUrl, leaseHeaders);
            using var response = await client.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.Headers.TryGetValues("X-XL-Canary", out var values) &&
                values.FirstOrDefault() == "yes")
            {
                Log.Information("Updates: Received canary track lease!");
            }

            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private static async Task<T> Retry<T>(Func<Task<T>> action, CancellationToken cancelToken)
        {
            for (var tries = 1;; tries++)
            {
                try
                {
                    return await action().ConfigureAwait(false);
                }
                catch (Exception ex) when (tries < MaxTries && !cancelToken.IsCancellationRequested && IsTransient(ex))
                {
                    Log.Warning(ex, "Updates: Request failed, try {TryCnt}/{MaxTries}", tries, MaxTries);
                }

                await Task.Delay(RetryBackoff, cancelToken).ConfigureAwait(false);
            }
        }

        private static bool IsTransient(Exception ex) => ex switch
        {
            HttpRequestException { StatusCode: { } status } => status == HttpStatusCode.TooManyRequests || status >= HttpStatusCode.InternalServerError,
            HttpRequestException or IOException or OperationCanceledException => true,
            _ => false,
        };

        public static async Task<ErrorNewsData?> GetErrorNews()
        {
            ErrorNewsData? newsData = null;

            try
            {
                const string NEWS_URL = "https://gist.githubusercontent.com/goaaats/5968072474f79b066a60854d38b95280/raw/xl-news.txt";

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                var text = await client.GetStringAsync(NEWS_URL).ConfigureAwait(false);
                newsData = JsonConvert.DeserializeObject<ErrorNewsData>(text);
            }
            catch (Exception newsEx)
            {
                Log.Error(newsEx, "Could not get error news");
            }

            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() > newsData?.ShowUntil ? null : newsData;
        }

        public static bool HaveFeatureFlag(LeaseFeatureFlags flag)
        {
            return UpdateLease != null && UpdateLease.Flags.HasFlag(flag);
        }

        public async Task Run(bool downloadPrerelease, ChangelogWindow? changelogWindow)
        {
            try
            {
                using var client = new HttpClient(new SocketsHttpHandler()
                {
                    ConnectCallback = new HappyEyeballsCallback().ConnectCallback
                })
                {
                    Timeout = RequestTimeout,
                };

                var updateResult = await LeaseUpdateManager(client, downloadPrerelease).ConfigureAwait(false);
                UpdateLease = updateResult.Lease;

                // Log feature flags
                try
                {
                    var flags = string.Join(", ", Enum.GetValues(typeof(LeaseFeatureFlags))
                        .Cast<LeaseFeatureFlags>()
                        .Where(f => UpdateLease.Flags.HasFlag(f))
                        .Select(f => f.ToString()));

                    Log.Information("Feature flags: {Flags}", flags);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Could not log feature flags");
                }

                var updateManager = updateResult.Manager;
                var updateInfo = await updateManager.CheckForUpdatesAsync().ConfigureAwait(false);

                if (updateInfo != null)
                {
                    Log.Information("Downloading update {Version}", updateInfo.TargetFullRelease.Version);
                    await updateManager.DownloadUpdatesAsync(updateInfo).ConfigureAwait(false);

                    try
                    {
                        // Reset UID cache after updating
                        App.UniqueIdCache.Reset();
                    }
                    catch
                    {
                        // ignored
                    }

                    Log.Information("Update ready to install");

                    if (changelogWindow == null)
                    {
                        Log.Error("changelogWindow was null");
                        Apply(updateManager, updateInfo);
                        return;
                    }

                    try
                    {
                        changelogWindow.Dispatcher.Invoke(() =>
                        {
                            changelogWindow.UpdateVersion(updateInfo.TargetFullRelease.Version.ToString());
                            changelogWindow.Show();
                            changelogWindow.Closed += (_, _) =>
                            {
                                Apply(updateManager, updateInfo);
                            };
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Could not show changelog window");
                        Apply(updateManager, updateInfo);
                        return;
                    }

                    OnUpdateCheckFinished?.Invoke(false);
                }
                else
                {
                    OnUpdateCheckFinished?.Invoke(true);
                }
            }
            catch (AcquireLockFailedException ex)
            {
                Log.Error(ex, "Update failed, another instance is updating");
                CustomMessageBox.Show(Loc.Localize("updatelockerror", "Another XIVLauncher window is currently installing an update or your antivirus is blocking the installation.\n\nIf this issue persists, please report it."),
                    "XIVLauncher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Asterisk);

                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Update failed");
                var newsData = await GetErrorNews().ConfigureAwait(false);

                if (newsData != null && !string.IsNullOrEmpty(newsData.Message))
                {
                    CustomMessageBox.Show(newsData.Message,
                        "XIVLauncher",
                        MessageBoxButton.OK,
                        newsData.IsError ? MessageBoxImage.Error : MessageBoxImage.Asterisk, showOfficialLauncher: true);
                }
                else
                {
                    CustomMessageBox.Show(Loc.Localize("updatefailureerror", "XIVLauncher failed to check for updates. This may be caused by internet connectivity issues. Wait a few minutes and try again.\nTry using a VPN if you aren't already or disabling your VPN if you currently are on one. You may also have to exclude XIVLauncher from your antivirus.\nIf this continues to fail after several minutes, please check out the FAQ."),
                        "XIVLauncher",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error, showOfficialLauncher: true);
                }

                Environment.Exit(1);
            }
        }

        private static void Apply(UpdateManager manager, UpdateInfo info)
        {
            try
            {
                var restartArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
                manager.ApplyUpdatesAndRestart(info, restartArgs);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not apply update");
                CustomMessageBox.Show(Loc.Localize("updateapplyerror", "XIVLauncher could not install a pending update. Please restart XIVLauncher to try again.\nYou may have to exclude XIVLauncher from your antivirus.\n\nIf this error persists, please report it."),
                    "XIVLauncher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error, showOfficialLauncher: true);

                Environment.Exit(1);
            }
        }
    }
}
