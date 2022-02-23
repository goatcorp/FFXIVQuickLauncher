using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XIVLauncher.Common.Patching.ZiPatch.Util
{
    /// <summary>
    /// An SQEX file.
    /// </summary>
    public class SqexFile
    {
        /// <summary>
        /// Gets or sets the relative path.
        /// </summary>
        public string RelativePath { get; set; }

        protected SqexFile() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqexFile"/> class.
        /// </summary>
        /// <param name="relativePath">File path.</param>
        public SqexFile(string relativePath)
        {
            RelativePath = relativePath;
        }

        /// <summary>
        /// Open a file stream.
        /// </summary>
        /// <param name="basePath">Filepath.</param>
        /// <param name="mode">Read/write mode.</param>
        /// <param name="tries">Attempts.</param>
        /// <param name="sleeptime">Interval between attempts.</param>
        /// <returns>The filestream.</returns>
        public SqexFileStream OpenStream(string basePath, FileMode mode, int tries = 5, int sleeptime = 1) =>
            SqexFileStream.WaitForStream($@"{basePath}/{RelativePath}", mode, tries, sleeptime);

        /// <summary>
        /// Open a file stream.
        /// </summary>
        /// <param name="store">Filestream store.</param>
        /// <param name="basePath">Filepath.</param>
        /// <param name="mode">Read/write mode.</param>
        /// <param name="tries">Attempts.</param>
        /// <param name="sleeptime">Interval between attempts.</param>
        /// <returns>The filestream.</returns>
        public SqexFileStream OpenStream(SqexFileStreamStore store, string basePath, FileMode mode,
                                         int tries = 5, int sleeptime = 1) =>
            store.GetStream($@"{basePath}/{RelativePath}", mode, tries, sleeptime);

        /// <summary>
        /// Create directories as needed.
        /// </summary>
        /// <param name="basePath">Directory path.</param>
        public void CreateDirectoryTree(string basePath)
        {
            var dirName = Path.GetDirectoryName($@"{basePath}/{RelativePath}");
            if (dirName != null)
                Directory.CreateDirectory(dirName);
        }

        /// <inheritdoc/>
        public override string ToString() => RelativePath;

        /// <summary>
        /// Get an expansion folder.
        /// </summary>
        /// <param name="expansionId">Expansion ID.</param>
        /// <returns>Expansion folder path.</returns>
        public static string GetExpansionFolder(byte expansionId) =>
            expansionId == 0 ? "ffxiv" : $"ex{expansionId}";

        /// <summary>
        /// Get all expansion files.
        /// </summary>
        /// <param name="fullPath">Folder path.</param>
        /// <param name="expansionId">Expansion ID.</param>
        /// <returns>All files for the specified expansion.</returns>
        public static IEnumerable<string> GetAllExpansionFiles(string fullPath, ushort expansionId)
        {
            var xpacPath = GetExpansionFolder((byte)expansionId);

            var sqpack = $@"{fullPath}\sqpack\{xpacPath}";
            var movie = $@"{fullPath}\movie\{xpacPath}";

            var files = Enumerable.Empty<string>();

            if (Directory.Exists(sqpack))
                files = files.Concat(Directory.GetFiles(sqpack));

            if (Directory.Exists(movie))
                files = files.Concat(Directory.GetFiles(movie));

            return files;
        }
    }
}
