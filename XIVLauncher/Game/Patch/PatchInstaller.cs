using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Game.Patch.PatchList;

namespace XIVLauncher.Game.Patch
{
    public class PatchDownloadProgress
    {
        public long CurrentBytes { get; set; }
        public long Length {get;set;}
        public string Name {get;set;}
    }

    class PatchInstaller
    {
        private readonly string _patchFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XIVLauncher", "patches");

        private readonly XivGame _game;

        public PatchInstaller(XivGame game, string repository)
        {
            _game = game;
        }

        public async Task DownloadPatchesAsync(IEnumerable<PatchListEntry> patches, string uniqueId, IProgress<PatchDownloadProgress> progress)
        {
            foreach (var patchListEntry in patches)
            {
                await DownloadPatchAsync(patchListEntry, uniqueId, progress);
            }
        }

        private async Task DownloadPatchAsync(PatchListEntry patch, string uniqueId, IProgress<PatchDownloadProgress> progress)
        {
            using (var client = new WebClient())
            {
                client.Headers.Add("user-agent", "FFXIV PATCH CLIENT");
                client.Headers.Add("X-Patch-Unique-Id", uniqueId);

                var res = client.UploadString(
                    "http://patch-gamever.ffxiv.com/gen_token", patch.Url);
                
                client.DownloadProgressChanged += delegate(object sender, DownloadProgressChangedEventArgs args)
                {
                    Log.Verbose($"Downloading patch: {(int)Math.Round((double)(100 * args.BytesReceived) / patch.Length)}% ({args.BytesReceived} / {patch.Length}) - {patch.Url}");

                    progress.Report(new PatchDownloadProgress{
                        Name = patch.VersionId,
                        CurrentBytes = args.BytesReceived,
                        Length = patch.Length
                        });
                };
                
                await client.DownloadFileTaskAsync(res, Path.Combine(_patchFolder, patch.VersionId + ".patch"));

                Log.Verbose("Patch at {0} downloaded completely", patch.Url);
            }
        }
    }
}
