using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Helpers;

namespace XIVLauncher.PatchInstaller.ZiPatch.Structures.Commands.SqPack
{
    class SqPackZiPatchCommand : IZiPatchCommand
    {
        private const ulong SQPK_FILE_BLOCK_SIZE = 128;

        public SqPackCommandType CommandType { get; private set; }

        void IZiPatchCommand.Handle(BinaryReader reader, long commandSize, ZiPatchExecute execute)
        {
            var sqpkCommandSize = reader.ReadUInt32BE();
            var sqpkCommandType = (char) reader.ReadByte();

            switch (sqpkCommandType)
            {
                case 'A':
                {
                    CommandType = SqPackCommandType.Add;
                    reader.BaseStream.Position += 3;

                    var file = execute.ResolveSqPackFile(reader);
                    
                    var blockInfo = ReadBlockInfo(reader);

                    var data = reader.ReadBytes((int) (blockInfo.BlockCount * SQPK_FILE_BLOCK_SIZE));
                    //data.Dump();

                    if (!ZiPatchExecute.IsDryRun)
                        file.Write(data, (int) blockInfo.BlockOffset, data.Length);

                    file.Close();

                    Log.Verbose("   -> SQPK ADD command executed for offset {0} with data of {1}", blockInfo.BlockOffset.ToString("X"), data.Length.ToString("X"));
                    break;
                }



                case 'D':
                {
                    CommandType = SqPackCommandType.Delete;
                    reader.BaseStream.Position += 3;

                    var file = execute.ResolveSqPackFile(reader);

                    var blockInfo = ReadBlockInfo(reader);

                    file.Close();

                    break;
                }



                case 'E':
                {
                    CommandType = SqPackCommandType.Expand;

                    var operation = reader.ReadByte();

                    reader.BaseStream.Position += 2; // Skipped

                    var file = execute.ResolveSqPackFile(reader);
                    
                    var blockInfo = ReadBlockInfo(reader);

                    reader.BaseStream.Position++; // Skipped

                    file.Close();

                    break;
                }



                // HEADER command - Overwrite the header of the specified SqPack DAT file with the 1024 byte payload
                case 'H':
                {
                    CommandType = SqPackCommandType.Header;

                    var targetType = (char) reader.ReadByte();
                    var headerType = (char) reader.ReadByte();
                    reader.BaseStream.Position++; // Skipped

                    var file = execute.ResolveSqPackFile(reader);

                    var headerData = reader.ReadBytes(1024);
                    headerData.Dump();

                    if (targetType == 'D')
                    {
                        var offset = 0;

                        if (sqpkCommandSize != 0x410)
                            throw new Exception("HEADER command has invalid size: " + sqpkCommandSize);

                        if (headerType != 'V')
                        {
                            if (headerType != 'D')
                                throw new Exception("Unknown HEADER headerType: " + headerType);

                            offset = 0x400;
                        }

                        if (!ZiPatchExecute.IsDryRun)
                            file.Write(headerData, offset, headerData.Length);

                        file.Close();

                        Log.Verbose("   -> SQPK HEADER command executed for offset {0}", offset.ToString("X"));
                    }
                    else
                        throw new NotImplementedException("Unimplemented HEADER target type: " + targetType);

                    break;
                }



                // FILE command - Extract file data at specified offset
                case 'F':
                    CommandType = SqPackCommandType.File;
                    reader.BaseStream.Position += 3;

                    var fileCommandInfo = ReadFileCommandInfo(reader);

                    HandleFileWrite(reader, null, fileCommandInfo.OutputOffset, fileCommandInfo.OutputLength);
                    break;



                // PATCHINFO? - Unknown
                case 'T':
                    CommandType = SqPackCommandType.PatchInfo;
                    reader.BaseStream.Position += commandSize - 5;
                    break;



                // INDEX - 
                case 'I':
                    CommandType = SqPackCommandType.Index;
                    reader.BaseStream.Position += commandSize - 5;
                    break;



                // Not quite sure?
                case 'X':
                    CommandType = SqPackCommandType.Unknown;
                    reader.BaseStream.Position += commandSize - 5;
                    break;

                default:
                    throw new NotImplementedException("Unimplemented ZiPatch SQPK subcommand type: " + sqpkCommandType);
            }

            Log.Verbose("   -> SQPK: type:{0}", CommandType);
        }

        private (ulong OutputOffset, ulong OutputLength, string path) ReadFileCommandInfo(BinaryReader reader)
        {
            var outputOffset = reader.ReadUInt64BE();
            var outputLength = reader.ReadUInt64BE();

            var fileNameLength = reader.ReadUInt32BE();

            var expansionId = reader.ReadUInt16BE();

            reader.BaseStream.Position += 2;

            var path = $"{ZiPatchExecute.ResolveExId((byte) expansionId)}\\{Encoding.ASCII.GetString(reader.ReadBytes((int) fileNameLength))}";

            // Cut null byte from end of path
            path = path.Substring(0, path.Length - 1);

            //Log.Verbose("   -> SQPK FILECMDINFO: outputOffset:{0} outputLen:{1}     {2}", outputOffset, outputLength, path);

            return (outputOffset, outputLength, path);
        }

        uint AlignFileBlockLength(uint length)
        {
            // block is aligned to 128 bytes
            return (uint) ((length + 0x8F) & 0xFFFFFF80);
        }

        private (uint Length, uint Version, uint CompressedLength, uint DecompressedLength, bool IsCompressed) ReadFileBlockInfo(BinaryReader reader)
        {
            var length = reader.ReadUInt32();
            var version = reader.ReadUInt32();
            var compressedLength = reader.ReadUInt32();
            var decompressedLength = reader.ReadUInt32();

            //Log.Verbose("   -> SQPK FILEBLOCKINFO: length:{0} version:{1} compressed:{2} uncompressed:{3} ", length, version, compressedLength, decompressedLength);

            return (length, version, compressedLength, decompressedLength, compressedLength != 32000);
        }

        private void HandleFileWrite(BinaryReader reader, FileStream file, ulong outputOffset, ulong outputLength)
        {
            var lengthRemaining = outputLength;

            while (lengthRemaining > 0)
            {
                var currentPos = reader.BaseStream.Position;

                var fileBlock = ReadFileBlockInfo(reader);

                lengthRemaining -= fileBlock.DecompressedLength;

                var payloadSize = AlignFileBlockLength(fileBlock.IsCompressed
                    ? fileBlock.CompressedLength
                    : fileBlock.DecompressedLength);

                var payload = reader.ReadBytes((int) payloadSize);

                //Log.Verbose("   -> FILE PAYLOAD READ: {0}", payloadSize.ToString("X"));

                // Skip the padded bytes
                reader.BaseStream.Position = currentPos + payloadSize;
            }
        }

        public (uint BlockOffset, uint BlockCount, uint PaddingBlockCount) ReadBlockInfo(BinaryReader reader)
        {
            var blockOffset = reader.ReadUInt32BE();
            var blockCount = reader.ReadUInt32BE();
            var paddingBlockCount = reader.ReadUInt32BE();

            Log.Verbose("   -> SQPK BLOCKINFO: off:{0} count:{1} paddingCount:{2}", blockOffset, blockCount, paddingBlockCount);

            return (blockOffset, blockCount, paddingBlockCount);
        }
    }
}
