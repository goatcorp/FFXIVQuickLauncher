using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace XIVLauncher.PatchInstaller.PartialFile.PartialPatchRpc
{
    class PartialPatchRpcOperationUnimplementedClient : PartialPatchRpcOperationClient
    {
        public PartialPatchRpcOperationUnimplementedClient(string rpcChannelName)
            : base(rpcChannelName)
        {
        }

        public override void Run()
        {
            // TODO: load patch index files and provide those to server
            throw new NotImplementedException();

            ProvideIndexFile(@"C:\Game Path\boot", "ffxivboot", new FileStream("D2021.11.16.0000.0001.patch.index", FileMode.Open, FileAccess.Read));
            ProvideIndexFile(@"C:\Game Path\game", "ffxivgame", new FileStream("D2022.01.25.0000.0000.patch.index", FileMode.Open, FileAccess.Read));
            ProvideIndexFileFinish();

            base.Run();
        }

        protected override void OnRequestPartialFile(int patchSetIndex, int patchFileIndex, string patchFileName, List<RequestedPartInfo> parts)
        {
            // TODO: download `patchFileName` for the parts specified in `parts`
            throw new NotImplementedException();

            ProvidePartialFile(patchSetIndex, patchFileIndex, $@"C:\Temp\{patchFileName}");
        }

        protected override void OnStatusUpdate(float progress, long applyProgress, long applyProgressMax)
        {
            // TODO: show progress
            throw new NotImplementedException();
        }

        protected override void OnFinishPartialFile(int patchSetIndex, int patchFileIndex, string patchFileName)
        {
            // TODO: maybe delete temporary patch file
            throw new NotImplementedException();
        }
    }
}
