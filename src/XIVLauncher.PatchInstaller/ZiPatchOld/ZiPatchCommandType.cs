using System;
using XIVLauncher.PatchInstaller.ZiPatch.Structures.Commands;
using XIVLauncher.PatchInstaller.ZiPatch.Structures.Commands.SqPack;

namespace XIVLauncher.PatchInstaller.ZiPatch
{
    enum ZiPatchCommandType
    {
        FileHeader,     // FHDR
        ApplyOption,    // APLY
        ApplyFreeSpace, // APFS
        EntryFile,      // ETRY
        AddDirectory,   // ADIR
        DeleteDirectory,// DELD
        SQPK,           // SQPK
        EndOfFile,      // EOF_
        XXXX            // XXXX
    }

    static class ZiPatchCommandTypeExtensions
    {
        public static (ZiPatchCommandType, IZiPatchCommand) GetZiPatchCommand(this string command)
        {
            switch (command)
            {
                case "FHDR":
                    return (ZiPatchCommandType.FileHeader, new FileHeaderZiPatchCommand());
                case "APLY":
                case "APFS":
                case "ETRY":
                case "ADIR":
                case "DELD":
                    return (ZiPatchCommandType.XXXX, null);
                case "SQPK":
                    return (ZiPatchCommandType.SQPK, new SqPackZiPatchCommand());
                case "EOF_":
                case "XXXX":
                    return (ZiPatchCommandType.XXXX, null);
                default:
                    throw new Exception("Unknown ZiPatch command type: " + command);
            }
        }
    }
}
