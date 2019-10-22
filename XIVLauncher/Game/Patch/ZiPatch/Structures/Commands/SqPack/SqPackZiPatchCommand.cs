using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Helpers;

namespace XIVLauncher.Game.Patch.ZiPatch.Structures.Commands.SqPack
{
    class SqPackZiPatchCommand : IZiPatchCommand
    {
        public SqPackCommandType CommandType { get; private set; }

        bool IZiPatchCommand.CanExecute => false;

        void IZiPatchCommand.Prepare(BinaryReader reader, long commandSize, ZiPatchExecute execute)
        {
            var sqpkBlockSize = reader.ReadUInt32BE();
            var sqpkType = (char) reader.ReadByte();

            switch (sqpkType)
            {
                case 'A':
                    CommandType = SqPackCommandType.Add;
                    break;
                case 'D':
                    CommandType = SqPackCommandType.Delete;
                    break;
                case 'X':
                    CommandType = SqPackCommandType.Expand;
                    break;
                case 'H':
                    CommandType = SqPackCommandType.Header;
                    break;
                case 'F':
                    CommandType = SqPackCommandType.File;
                    break;
                case 'T':
                    CommandType = SqPackCommandType.PatchInfo;
                    break;
                case 'I':
                    CommandType = SqPackCommandType.Index;
                    break;

                default:
                    throw new Exception("Unknown ZiPatch SQPK subcommand type: " + sqpkType);
            }

            reader.BaseStream.Position += commandSize - 5;

            Log.Verbose("   -> SQPK: type:{0}", CommandType);
        }

        void IZiPatchCommand.Execute()
        {
            throw new NotImplementedException();
        }
    }
}
