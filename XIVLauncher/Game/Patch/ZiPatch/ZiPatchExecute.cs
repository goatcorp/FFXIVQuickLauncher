using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using XIVLauncher.Game.Patch.ZiPatch.Structures;
using XIVLauncher.Helpers;

namespace XIVLauncher.Game.Patch.ZiPatch
{
    class ZiPatchExecute
    {
        private const UInt32 ZIPATCH_MAGIC_1 = 0x915A4950; // ‘ZIP
        private const UInt32 ZIPATCH_MAGIC_2 = 0x41544348; // ATCH
        private const UInt32 ZIPATCH_MAGIC_3 = 0x0D0A1A0A; 

        public void Execute(string patchPath, string gamePath)
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