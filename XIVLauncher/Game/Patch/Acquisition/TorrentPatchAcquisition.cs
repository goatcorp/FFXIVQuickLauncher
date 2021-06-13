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
using XIVLauncher.Game.Patch.PatchList;

namespace XIVLauncher.Game.Patch.Acquisition
{
    public class TorrentPatchAcquisition : IPatchAcquisition
    {
        private static ClientEngine torrentEngine;

        public static void Init(int maxDownloadSpeed = 0)
        {
            var builder = new EngineSettingsBuilder
            {
                MaximumDownloadSpeed = maxDownloadSpeed
            };

            torrentEngine = new ClientEngine(builder.ToSettings());
        }

        public static async Task UnInit()
        {
            if (torrentEngine != null)
            {
                await torrentEngine.StopAllAsync();
                torrentEngine = null;
            }
        }

        public static async Task SetMaxDownloadSpeed(int speed)
        {
            var builder = new EngineSettingsBuilder();
            builder.MaximumDownloadSpeed = speed;

            await torrentEngine.UpdateSettingsAsync(builder.ToSettings());
        }

        public async Task StartDownloadAsync(PatchListEntry patch, DirectoryInfo patchStore)
        {
            using var client = new WebClient();

            var torrent = Torrent.Load(client.DownloadData("http://goaaats.github.io/patchtorrent/" + patch.GetUrlPath() + ".torrent?1092381"));
            var hasSignaledComplete = false;

            var tm = await torrentEngine.AddAsync(torrent, patchStore.FullName);
            tm.TorrentStateChanged += async (sender, args) =>
            {
                if ((int) tm.Progress == 100 && !hasSignaledComplete && args.NewState == TorrentState.Seeding)
                {
                    this.Complete?.Invoke(null, null);
                    hasSignaledComplete = true;
                    await tm.StopAsync();
                }
            };

            tm.PieceHashed += (sender, args) =>
            {
                ProgressChanged?.Invoke(null, new AcquisitionProgress
                {
                    Progress = tm.Progress,
                    BytesPerSecondSpeed = tm.Monitor.DownloadSpeed
                });
            };

            await tm.StartAsync();
            await tm.DhtAnnounceAsync();
        }

        public event EventHandler<AcquisitionProgress> ProgressChanged;
        public event EventHandler Complete;
    }
}
