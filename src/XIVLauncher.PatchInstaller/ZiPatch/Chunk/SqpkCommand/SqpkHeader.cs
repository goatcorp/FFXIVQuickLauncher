using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.PatchInstaller.Util;
using XIVLauncher.PatchInstaller.ZiPatch.Util;

namespace XIVLauncher.PatchInstaller.ZiPatch.Chunk.SqpkCommand
{
    /// <summary>
    /// An "H" (Header) command chunk.
    /// </summary>
    class SqpkHeader : SqpkChunk
    {
        /// <summary>
        /// Gets the command type.
        /// </summary>
        public new static string Command = "H";

        /// <summary>
        /// Target file kinds.
        /// </summary>
        public enum TargetFileKind : byte
        {
            /// <summary>
            /// Dat file.
            /// </summary>
            Dat = (byte)'D',

            /// <summary>
            /// Index file.
            /// </summary>
            Index = (byte)'I',
        }

        /// <summary>
        /// Target header kinds.
        /// </summary>
        public enum TargetHeaderKind : byte
        {
            /// <summary>
            /// Version header.
            /// </summary>
            Version = (byte)'V',

            /// <summary>
            /// Index header.
            /// </summary>
            Index = (byte)'I',

            /// <summary>
            /// Data header.
            /// </summary>
            Data = (byte)'D',
        }

        private const int HEADER_SIZE = 1024;

        public TargetFileKind FileKind { get; protected set; }
        public TargetHeaderKind HeaderKind { get; protected set; }
        public SqpackFile TargetFile { get; protected set; }

        public byte[] HeaderData { get; protected set; }

        public SqpkHeader(ChecksumBinaryReader reader, int size) : base(reader, size) {}

        protected override void ReadChunk()
        {
            var start = reader.BaseStream.Position;

            FileKind = (TargetFileKind)reader.ReadByte();
            HeaderKind = (TargetHeaderKind)reader.ReadByte();
            reader.ReadByte(); // Alignment

            if (FileKind == TargetFileKind.Dat)
                TargetFile = new SqpackDatFile(reader);
            else
                TargetFile = new SqpackIndexFile(reader);

            HeaderData = reader.ReadBytes(HEADER_SIZE);

            reader.ReadBytes(Size - (int)(reader.BaseStream.Position - start));
        }

        public override void ApplyChunk(ZiPatchConfig config)
        {
            TargetFile.ResolvePath(config.Platform);

            var file = config.Store == null ?
                TargetFile.OpenStream(config.GamePath, FileMode.OpenOrCreate) :
                TargetFile.OpenStream(config.Store, config.GamePath, FileMode.OpenOrCreate);

            file.WriteFromOffset(HeaderData, HeaderKind == TargetHeaderKind.Version ? 0 : HEADER_SIZE);
        }

        public override string ToString()
        {
            return $"{Type}:{Command}:{FileKind}:{HeaderKind}:{TargetFile}";
        }
    }
}
