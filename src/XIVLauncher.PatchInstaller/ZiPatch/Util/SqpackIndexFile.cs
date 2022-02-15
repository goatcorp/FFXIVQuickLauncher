using System.IO;

namespace XIVLauncher.PatchInstaller.ZiPatch.Util
{
    /// <summary>
    /// An SQPack index file.
    /// </summary>
    class SqpackIndexFile : SqpackFile
    {
        public SqpackIndexFile(BinaryReader reader) : base(reader) {}


        protected override string GetFileName(ZiPatchConfig.PlatformId platform) =>
            $"{base.GetFileName(platform)}.index{(FileId == 0 ? string.Empty : FileId.ToString())}";
    }
}
