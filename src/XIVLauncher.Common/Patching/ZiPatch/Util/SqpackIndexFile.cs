using System.IO;

namespace XIVLauncher.Common.Patching.ZiPatch.Util
{
    /// <summary>
    /// An SQPack index file.
    /// </summary>
    class SqpackIndexFile : SqpackFile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqpackIndexFile"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        public SqpackIndexFile(BinaryReader reader) : base(reader) {}

        /// <summary>
        /// Gets the filename.
        /// </summary>
        /// <param name="platform">Platform kind.</param>
        /// <returns>Filename.</returns>
        protected override string GetFileName(ZiPatchConfig.PlatformId platform) =>
            $"{base.GetFileName(platform)}.index{(FileId == 0 ? string.Empty : FileId.ToString())}";
    }
}
