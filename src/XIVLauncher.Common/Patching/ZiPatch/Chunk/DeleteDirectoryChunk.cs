using System;
using System.IO;

using Serilog;
using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk
{
    /// <summary>
    /// A "DELD" (Delete Directory) chunk.
    /// </summary>
    public class DeleteDirectoryChunk : ZiPatchChunk
    {
        /// <summary>
        /// The chunk type.
        /// </summary>
        public static new string Type = "DELD";

        /// <summary>
        /// Gets the directory name.
        /// </summary>
        public string DirName { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteDirectoryChunk"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        /// <param name="offset">Chunk offset.</param>
        /// <param name="size">Chunk size.</param>
        public DeleteDirectoryChunk(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) { }

        /// <inheritdoc/>
        protected override void ReadChunk()
        {
            var start = this.Reader.BaseStream.Position;

            var dirNameLen = this.Reader.ReadUInt32BE();

            DirName = this.Reader.ReadFixedLengthString(dirNameLen);

            this.Reader.ReadBytes(Size - (int)(this.Reader.BaseStream.Position - start));
        }

        /// <inheritdoc/>
        public override void ApplyChunk(ZiPatchConfig config)
        {
            try
            {
                Directory.Delete(config.GamePath + DirName);
            }
            catch (Exception e)
            {
                Log.Debug(e, "Ran into {This}, failed at deleting the dir", this);
                throw;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Type}:{DirName}";
        }
    }
}
