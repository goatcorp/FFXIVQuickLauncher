using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using XIVLauncher.Game.Patch.ZiPatch.Structures;
using XIVLauncher.Game.Patch.ZiPatch.Structures.Commands;
using XIVLauncher.Helpers;

namespace XIVLauncher.Game.Patch.ZiPatch
{
    /// <summary>
    /// ZiPatch command parser and executor
    /// Big thanks to Mino and perchbird for their research on this!
    /// </summary>
    class ZiPatchExecute
    {
        private const uint ZIPATCH_MAGIC_1 = 0x915A4950; // ‘ZIP
        private const uint ZIPATCH_MAGIC_2 = 0x41544348; // ATCH
        private const uint ZIPATCH_MAGIC_3 = 0x0D0A1A0A;

        private readonly string _gamePath;
        private readonly string _repository;

        public ZiPatchExecute(string gamePath, string repository)
        {
            _gamePath = gamePath;
            _repository = repository;
        }

        private static string ResolveExId(byte exId)
        {
            if (exId == 0)
                return "ffxiv";

            return $"ex{exId}";
        }

        public FileStream ResolveSqPackFile(BinaryReader reader)
        {
            var datCat = reader.ReadUInt16BE();
            var exId = reader.ReadByte();
            var datChunk = reader.ReadByte();

            var datNum = reader.ReadUInt32BE();

            var path = $"sqpack/{ResolveExId(exId)}/{datCat:X2}{exId:X2}{datChunk:X2}.dat{datNum}";

            Log.Verbose("Resolved SqPack dat file path at {0} to {1}", reader.BaseStream.Position.ToString("X"), path);

            return File.Open(Path.Combine(_gamePath, path), FileMode.Open, FileAccess.ReadWrite);
        }

        public void Execute(string patchPath)
        {
            using (var stream = new FileStream(patchPath, FileMode.Open))
            {
                Log.Verbose("Patch at {0} opened", patchPath);

                using (var reader = new BinaryReader(stream))
                {
                    // Check ZiPatch magic
                    if (reader.ReadUInt32BE() != ZIPATCH_MAGIC_1 || reader.ReadUInt32BE() != ZIPATCH_MAGIC_2 || reader.ReadUInt32BE() != ZIPATCH_MAGIC_3)
                        throw new ArgumentException("Patch file is invalid or not ZiPatch.");

                    // Read all commands one by one
                    while (stream.CanRead)
                    {
                        var chunk = new ZiPatchCommandPack(reader, this);

                        switch (chunk.CommandType)
                        {
                            case ZiPatchCommandType.FileHeader:
                            {
                                var fileHeaderCommand = chunk.Command as FileHeaderZiPatchCommand;

                                if (fileHeaderCommand.PatchVersion != 3)
                                    throw new ArgumentException("Unsupported ZiPatch version: " + fileHeaderCommand.PatchVersion);

                                Log.Verbose("PATCH HEADER: type:{0} version:{1}", fileHeaderCommand.PatchVersion, fileHeaderCommand.PatchType);
                                break;
                            }

                            case ZiPatchCommandType.EndOfFile:
                            {
                                MessageBox.Show("Patch EOF!");
                                return;
                            }
                        }
                    }
                }
            }
        }
    }
}