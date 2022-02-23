using System.IO;
using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Util
{
    public abstract class SqpackFile : SqexFile
    {
        protected ushort MainId { get; }
        protected ushort SubId { get; }
        protected uint FileId { get; }

        protected byte ExpansionId => (byte)(SubId >> 8);

        protected SqpackFile(BinaryReader reader)
        {
            MainId = reader.ReadUInt16BE();
            SubId = reader.ReadUInt16BE();
            FileId = reader.ReadUInt32BE();

            RelativePath = GetExpansionPath();
        }

        protected string GetExpansionPath() =>
            $@"/sqpack/{GetExpansionFolder(ExpansionId)}/";

        protected virtual string GetFileName(ZiPatchConfig.PlatformId platform) =>
            $"{GetExpansionPath()}{MainId:x2}{SubId:x4}.{platform.ToString().ToLower()}";

        public void ResolvePath(ZiPatchConfig.PlatformId platform) =>
            RelativePath = GetFileName(platform);

        public override string ToString()
        {
            // Default to Win32 for prints; we're unlikely to run in PS3/PS4
            return GetFileName(ZiPatchConfig.PlatformId.Win32);
        }
    }
}