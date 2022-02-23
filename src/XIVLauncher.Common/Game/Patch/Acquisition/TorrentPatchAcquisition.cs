using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using MonoTorrent.Client;
using Serilog;
using XIVLauncher.Common.Game.Patch.PatchList;

namespace XIVLauncher.Common.Game.Patch.Acquisition
{
    public class TorrentPatchAcquisition : PatchAcquisition
    {
        private static ClientEngine torrentEngine;

        private TorrentManager _torrentManager;
        private byte[] _torrentBytes;

        public static async Task InitializeAsync(long maxDownloadSpeed)
        {
            if (torrentEngine == null)
            {
                torrentEngine = new ClientEngine();

                var builder = new EngineSettingsBuilder(torrentEngine.Settings) {MaximumDownloadSpeed = (int)maxDownloadSpeed};

                await torrentEngine.UpdateSettingsAsync(builder.ToSettings());
            }
        }

        public static async Task UnInitializeAsync()
        {
            if (torrentEngine != null)
            {
                await torrentEngine.StopAllAsync();
                torrentEngine = null;
            }
        }

        public bool IsApplicable(PatchListEntry patch)
        {
            try
            {
                using var client = new WebClient();

                _torrentBytes = client.DownloadData("http://goaaats.github.io/patchtorrent/" + patch.GetUrlPath() + ".torrent");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[TORRENT] Could not get torrent for patch: {patch.GetUrlPath()}");
                return false;
            }

            return true;
        }

        public override async Task StartDownloadAsync(string url, FileInfo outFile)
        {
            throw new NotImplementedException("WIP");

            /*
            if (_torrentBytes == null)
            {
                if (!IsApplicable(patch))
                    throw new Exception("This patch is not applicable to be downloaded with this acquisition method.");
            }

            var torrent = await Torrent.LoadAsync(_torrentBytes);
            var hasSignaledComplete = false;

            _torrentManager = await torrentEngine.AddAsync(torrent, outFile.Directory.FullName);
            _torrentManager.TorrentStateChanged += async (sender, args) =>
            {
                if ((int) _torrentManager.Progress == 100 && !hasSignaledComplete && args.NewState == TorrentState.Seeding)
                {
                    OnComplete(AcquisitionResult.Success);
                    hasSignaledComplete = true;
                    await _torrentManager.StopAsync();
                }
            };

            _torrentManager.PieceHashed += (sender, args) =>
            {
                OnProgressChanged(new AcquisitionProgress
                {
                    Progress = _torrentManager.Monitor.DataBytesDownloaded,
                    BytesPerSecondSpeed = _torrentManager.Monitor.DownloadSpeed
                });
            };

            await _torrentManager.StartAsync();
            await _torrentManager.DhtAnnounceAsync();
            */
        }

        public override async Task CancelAsync()
        {
            if (_torrentManager == null)
                return;

            await _torrentManager.StopAsync();
            await torrentEngine.RemoveAsync(_torrentManager);
        }
    }
}