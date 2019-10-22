using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
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

        public void StartPatcher()
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = Path.Combine(Directory.GetCurrentDirectory(), "XIVLauncher.PatchInstaller.exe"),
                    WorkingDirectory = Directory.GetCurrentDirectory()
                }
            };

            process.StartInfo.Verb = "runas";

            using (var pipeServer =
                new AnonymousPipeServerStream(PipeDirection.InOut,
                    HandleInheritability.Inheritable))
            {
                Console.WriteLine("[SERVER] Current TransmissionMode: {0}.",
                    pipeServer.TransmissionMode);

                // Pass the client process a handle to the server.
                process.StartInfo.Arguments =
                    pipeServer.GetClientHandleAsString();
                process.StartInfo.UseShellExecute = false;
                process.Start();

                pipeServer.DisposeLocalCopyOfClientHandle();

                try
                {
                    // Read user input and send that to the client process.
                    using (StreamWriter sw = new StreamWriter(pipeServer))
                    {
                        sw.AutoFlush = true;
                        // Send a 'sync message' and wait for client to receive it.
                        sw.WriteLine("SYNC");
                        pipeServer.WaitForPipeDrain();
                        // Send the console input to the client process.
                        Console.Write("[SERVER] Enter text: ");
                        sw.WriteLine(Console.ReadLine());
                    }
                }
                // Catch the IOException that is raised if the pipe is broken
                // or disconnected.
                catch (IOException e)
                {
                    Console.WriteLine("[SERVER] Error: {0}", e.Message);
                }
            }
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
