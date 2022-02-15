using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.PartialFile
{
    public class PartialFileVerification
    {
        public readonly PartialFileDef Definition;
        public readonly List<HashSet<Tuple<short, int>>> MissingPartIndicesPerPatch = new();
        public readonly List<HashSet<int>> MissingPartIndicesPerTargetFile = new();
        public readonly HashSet<int> TooLongTargetFiles = new();

        public int ProgressReportInterval = 250;
        private int LastProgressUpdateReport = 0;

        public delegate void OnCorruptionFoundDelegate(string relativePath, PartialFilePart part, PartialFilePart.VerifyDataResult result);
        public delegate void OnProgressDelegate(long progress, long max);

        public readonly List<OnCorruptionFoundDelegate> OnCorruptionFound = new();
        public readonly List<OnProgressDelegate> OnProgress = new();

        public PartialFileVerification(PartialFileDef def)
        {
            Definition = def;
            foreach (var _ in def.GetFiles())
                MissingPartIndicesPerTargetFile.Add(new());
            foreach (var _ in def.GetSourceFiles())
                MissingPartIndicesPerPatch.Add(new());
        }

        private void TriggerOnProgress(long progress, long max, bool forceNotify)
        {
            if (!forceNotify)
            {
                if (LastProgressUpdateReport >= 0 && Environment.TickCount < 0)
                {
                    // Overflowed; just report again
                }
                else if (LastProgressUpdateReport + ProgressReportInterval > Environment.TickCount)
                {
                    return;
                }
            }

            LastProgressUpdateReport = Environment.TickCount;
            foreach (var d in OnProgress)
                d(progress, max);
        }

        private void TriggerOnCorruptionFound(string relativePath, PartialFilePart part, PartialFilePart.VerifyDataResult result)
        {
            foreach (var d in OnCorruptionFound)
                d(relativePath, part, result);
        }

        public void VerifyFile(string relativePath, Stream local)
        {
            var targetFileIndex = Definition.GetFileIndex(relativePath);
            var file = Definition.GetFile(relativePath);

            TriggerOnProgress(0, file.FileSize, true);
            for (var i = 0; i < file.Count; ++i) {
                TriggerOnProgress(file[i].TargetOffset, file.FileSize, false);
                var verifyResult = file[i].Verify(local);
                switch (verifyResult)
                {
                    case PartialFilePart.VerifyDataResult.Pass:
                        continue;

                    case PartialFilePart.VerifyDataResult.FailUnverifiable:
                        throw new Exception($"{relativePath}:{file[i].TargetOffset}:{file[i].TargetEnd}: Should not happen; unverifiable due to insufficient source data");

                    case PartialFilePart.VerifyDataResult.FailNotEnoughData:
                    case PartialFilePart.VerifyDataResult.FailBadData:
                        if (file[i].IsFromSourceFile)
                            MissingPartIndicesPerPatch[file[i].SourceIndex].Add(Tuple.Create(targetFileIndex, i));
                        MissingPartIndicesPerTargetFile[targetFileIndex].Add(i);
                        TriggerOnCorruptionFound(relativePath, file[i], verifyResult);
                        break;
                }
            }
            if (local.Length > file.FileSize)
                TooLongTargetFiles.Add(targetFileIndex);
            TriggerOnProgress(file.FileSize, file.FileSize, true);
        }

        public void MarkFileAsMissing(string relativePath)
        {
            var targetFileIndex = Definition.GetFileIndex(relativePath);
            var file = Definition.GetFile(relativePath);
            for (var i = 0; i < file.Count; ++i)
            {
                if (file[i].IsFromSourceFile)
                    MissingPartIndicesPerPatch[file[i].SourceIndex].Add(Tuple.Create(targetFileIndex, i));
                MissingPartIndicesPerTargetFile[targetFileIndex].Add(i);
            }
        }
    }
}
