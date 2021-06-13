using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MonoTorrent;
using MonoTorrent.BEncoding;
using MonoTorrent.Client;
using Serilog;
using XIVLauncher.Game.Patch.PatchList;

namespace XIVLauncher.Game.Patch.Acquisition
{
    public class TorrentPatchAcquisition : IPatchAcquisition
    {
        private static ClientEngine torrentEngine;

        private TorrentManager _torrentManager;
        private byte[] _torrentBytes;

        public static async Task InitAsync(int maxDownloadSpeed = 0)
        {
            torrentEngine = new ClientEngine();

            var builder = new EngineSettingsBuilder(torrentEngine.Settings);
            builder.MaximumDownloadSpeed = maxDownloadSpeed;

            await torrentEngine.UpdateSettingsAsync(builder.ToSettings());
        }

        public static async Task UnInit()
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

                this._torrentBytes =
                    client.DownloadData("http://goaaats.github.io/patchtorrent/" + patch.GetUrlPath() + ".torrent");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[TORRENT] Could not get torrent for patch: {patch.GetUrlPath()}");
                return false;
            }

            return true;
        }

        public async Task StartDownloadAsync(PatchListEntry patch, FileInfo outFile)
        {
            if (this._torrentBytes == null)
            {
                if (!IsApplicable(patch))
                    throw new Exception("This patch is not applicable to be downloaded with this acquisition method.");
            }

            var torrent = await Torrent.LoadAsync(this._torrentBytes);
            var hasSignaledComplete = false;

            _torrentManager = await torrentEngine.AddAsync(torrent, outFile.Directory.FullName);
            _torrentManager.TorrentStateChanged += async (sender, args) =>
            {
                if ((int) _torrentManager.Progress == 100 && !hasSignaledComplete && args.NewState == TorrentState.Seeding)
                {
                    this.Complete?.Invoke(null, AcquisitionResult.Success);
                    hasSignaledComplete = true;
                    await _torrentManager.StopAsync();
                }
            };

            _torrentManager.PieceHashed += (sender, args) =>
            {
                Log.Information("Progress");
                ProgressChanged?.Invoke(null, new AcquisitionProgress
                {
                    Progress = _torrentManager.Monitor.DataBytesDownloaded,
                    BytesPerSecondSpeed = _torrentManager.Monitor.DownloadSpeed
                });
            };

            await _torrentManager.StartAsync();
            await _torrentManager.DhtAnnounceAsync();
        }

        public async Task CancelAsync()
        {
            await _torrentManager.StopAsync();
            await torrentEngine.RemoveAsync(_torrentManager);
        }

        public event EventHandler<AcquisitionProgress> ProgressChanged;
        public event EventHandler<AcquisitionResult> Complete;
    }
}
