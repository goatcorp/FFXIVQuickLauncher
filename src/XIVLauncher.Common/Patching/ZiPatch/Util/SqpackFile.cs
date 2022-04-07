using System.IO;

using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Util
{
    /// <summary>
    /// An SQPack file abstraction.
    /// </summary>
    public abstract class SqpackFile : SqexFile
    {
        /// <summary>
        /// Gets the main ID.
        /// </summary>
        protected ushort MainId { get; }

        /// <summary>
        /// Gets the sub ID.
        /// </summary>
        protected ushort SubId { get; }

        /// <summary>
        /// Gets the file ID.
        /// </summary>
        protected uint FileId { get; }

        /// <summary>
        /// Gets the expansion ID.
        /// </summary>
        protected byte ExpansionId => (byte)(SubId >> 8);

        /// <summary>
        /// Initializes a new instance of the <see cref="SqpackFile"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        protected SqpackFile(BinaryReader reader)
        {
            MainId = reader.ReadUInt16BE();
            SubId = reader.ReadUInt16BE();
            FileId = reader.ReadUInt32BE();

            RelativePath = GetExpansionPath();
        }

        /// <summary>
        /// Get the expansion path.
        /// </summary>
        /// <returns>Expansion filepath.</returns>
        protected string GetExpansionPath() =>
            $@"/sqpack/{GetExpansionFolder(ExpansionId)}/";

        /// <summary>
        /// Get a platform dependent filename.
        /// </summary>
        /// <param name="platform">Platform kind.</param>
        /// <returns>The filename.</returns>
        protected virtual string GetFileName(ZiPatchConfig.PlatformId platform) =>
            $"{GetExpansionPath()}{MainId:x2}{SubId:x4}.{platform.ToString().ToLower()}";

        /// <summary>
        /// Resolve a platform dependent filepath and store it.
        /// </summary>
        /// <param name="platform">Platform kind.</param>
        public void ResolvePath(ZiPatchConfig.PlatformId platform) =>
            RelativePath = GetFileName(platform);

        /// <inheritdoc/>
        public override string ToString()
        {
            // Default to Win32 for prints; we're unlikely to run in PS3/PS4
            return GetFileName(ZiPatchConfig.PlatformId.Win32);
        }
    }
}
