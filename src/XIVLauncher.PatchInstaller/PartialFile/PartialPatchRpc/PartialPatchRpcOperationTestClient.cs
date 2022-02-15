using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace XIVLauncher.PatchInstaller.PartialFile.PartialPatchRpc
{
    class PartialPatchRpcOperationTestClient : PartialPatchRpcOperationClient
    {
        private readonly PartialPatchRpcOperationServer Server;
        private readonly List<string> RootPaths = new();
        private readonly List<List<string>> PatchFiles = new();
        private readonly List<string> VersionFileNames = new();

        public PartialPatchRpcOperationTestClient()
            : base("PartialPatchRpcOperationTest" + Guid.NewGuid().ToString())
        {
            Server = new(RpcName);
            new Thread(() => Server.Run()).Start();
        }

        public void AddFiles(List<string> patchFiles, string gameRoot, string versionFileName)
        {
            RootPaths.Add(gameRoot);
            PatchFiles.Add(patchFiles);
            VersionFileNames.Add(versionFileName);
        }

        public override void Run()
        {
            for (var i = 0; i < RootPaths.Count; i++)
                ProvideIndexFile(RootPaths[i], VersionFileNames[i], new FileStream(PatchFiles[i].Last() + ".index", FileMode.Open, FileAccess.Read));
            ProvideIndexFileFinish();

            base.Run();
        }

        protected override void OnRequestPartialFile(int patchSetIndex, int patchFileIndex, string patchFileName, List<RequestedPartInfo> parts)
        {
            var patchFilePath = PatchFiles[patchSetIndex].First(x => x.EndsWith(patchFileName));
            ProvidePartialFile(patchSetIndex, patchFileIndex, patchFilePath);
        }

        protected override void OnStatusUpdate(float progress, long applyProgress, long applyProgressMax)
        {
            base.OnStatusUpdate(progress, applyProgress, applyProgressMax);
        }

        protected override void OnFinishPartialFile(int patchSetIndex, int patchFileIndex, string patchFileName)
        {
            base.OnFinishPartialFile(patchSetIndex, patchFileIndex, patchFileName);
        }

        public override void Dispose()
        {
            base.Dispose();
            Server.Dispose();
        }
    }
}
