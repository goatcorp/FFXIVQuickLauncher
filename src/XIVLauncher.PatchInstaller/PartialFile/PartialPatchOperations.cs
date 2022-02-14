using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.PatchInstaller.ZiPatch;

namespace XIVLauncher.PatchInstaller.PartialFile
{
    public class PartialPatchOperations
    {
        public static PartialFileDef CreatePatchFileIndices(IList<string> patchFilePaths)
        {
            var sources = new List<Stream>();
            var patchFiles = new List<ZiPatchFile>();
            PartialFileDef fileDef = new();
            try
            {
                var firstPatchFileIndex = patchFilePaths.Count - 1;
                while (firstPatchFileIndex > 0)
                {
                    if (File.Exists(patchFilePaths[firstPatchFileIndex] + ".index"))
                        break;
                    firstPatchFileIndex--;
                }
                for (var i = 0; i < patchFilePaths.Count; ++i)
                {
                    var patchFilePath = patchFilePaths[i];
                    sources.Add(new FileStream(patchFilePath, FileMode.Open, FileAccess.Read));
                    patchFiles.Add(new ZiPatchFile(sources[sources.Count - 1]));

                    if (i < firstPatchFileIndex)
                        continue;

                    if (File.Exists(patchFilePath + ".index"))
                    {
                        Log.Information("Reading patch index file {0}...", patchFilePath);
                        using var reader = new BinaryReader(new DeflateStream(new FileStream(patchFilePath + ".index", FileMode.Open, FileAccess.Read), CompressionMode.Decompress));
                        fileDef.ReadFrom(reader);
                        continue;
                    }

                    Log.Information("Indexing patch file {0}...", patchFilePath);
                    fileDef.ApplyZiPatch(Path.GetFileName(patchFilePath), patchFiles[patchFiles.Count - 1]);

                    Log.Information("Calculating CRC32 for files resulted from patch file {0}...", patchFilePath);
                    fileDef.CalculateCrc32(sources);

                    using (var writer = new BinaryWriter(new DeflateStream(new FileStream(patchFilePath + ".index.tmp", FileMode.Create), CompressionLevel.Optimal)))
                        fileDef.WriteTo(writer);

                    File.Move(patchFilePath + ".index.tmp", patchFilePath + ".index");
                }

                return fileDef;
            }
            finally
            {
                foreach (var source in sources)
                    source.Dispose();
            }
        }

        public static Dictionary<string, List<Tuple<long, int>>> VerifyFromPatchFileIndex(IList<string> patchFilePaths, string gameRootPath)
        {
            Dictionary<string, List<Tuple<long, int>>> corruptedParts = new();

            var def = CreatePatchFileIndices(patchFilePaths);
            var sources = new List<Stream>();
            try
            {
                foreach (var patchFilePath in patchFilePaths)
                    sources.Add(new FileStream(patchFilePath, FileMode.Open, FileAccess.Read));

                byte[] buf = new byte[16000];
                foreach (var file in def.GetFiles())
                {
                    Log.Information("Checking file {0}...", file);
                    var parts = def.GetFile(file);
                    try
                    {
                        using var local = new FileStream(Path.Combine(gameRootPath, file), FileMode.Open, FileAccess.Read);
                        var prematureEof = false;
                        foreach (var part in parts)
                        {
                            local.Seek(part.TargetOffset, SeekOrigin.Begin);
                            if (buf.Length < part.TargetSize)
                                buf = new byte[part.TargetSize];
                            if (local.Read(buf, 0, part.TargetSize) == part.TargetSize)
                            {
                                if (part.VerifyData(buf, 0, part.TargetSize))
                                    continue;

                                Log.Warning("{0}:{1}:{2}: Corrupt data", file, part.TargetOffset, part.TargetEnd);
                            }
                            else if (!prematureEof)
                            {
                                prematureEof = true;
                                Log.Warning("{0}:{1}:{2}: Premature EOF", file, part.TargetOffset, def.GetFileSize(file));
                            }

                            if (!corruptedParts.ContainsKey(file))
                                corruptedParts[file] = new();
                            corruptedParts[file].Add(Tuple.Create(part.TargetOffset, part.TargetSize));
                        }
                        if (local.Length > def.GetFileSize(file) && !prematureEof)
                        {
                            Log.Warning("{0}:{1}:{2}: File too long", file, def.GetFileSize(file), local.Length);
                            if (!corruptedParts.ContainsKey(file))
                                corruptedParts[file] = new();
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        Log.Warning("{0}:{1}:{2}: File does not exist", file, 0, def.GetFileSize(file));
                        if (!corruptedParts.ContainsKey(file))
                            corruptedParts[file] = new();
                    }
                }
            }
            finally
            {
                foreach (var source in sources)
                    source.Dispose();
            }

            return corruptedParts;
        }

        public static void RepairFromPatchFileIndex(IList<string> patchFilePaths, string gameRootPath)
        {
            var def = CreatePatchFileIndices(patchFilePaths);
            var sources = new List<Stream>();
            foreach (var patchFilePath in patchFilePaths)
                sources.Add(new FileStream(patchFilePath, FileMode.Open, FileAccess.Read));
            
            try
            {
                var buf = new byte[16000];
                foreach (var file in def.GetFiles())
                {
                    Log.Information("Checking file {0}...", file);
                    var reconstructed = def.GetFileStream(file, sources);
                    var parts = def.GetFile(file);
                    using var local = new FileStream(Path.Combine(gameRootPath, file), FileMode.OpenOrCreate, FileAccess.ReadWrite);

                    var prematureEof = false;
                    foreach (var part in parts)
                    {
                        local.Seek(part.TargetOffset, SeekOrigin.Begin);
                        if (buf.Length < part.TargetSize)
                            buf = new byte[part.TargetSize];
                        if (local.Read(buf, 0, part.TargetSize) == part.TargetSize)
                        {
                            if (part.VerifyData(buf, 0, part.TargetSize))
                                continue;

                            Log.Warning("{0}:{1}:{2}: Corrupt data; repairing", file, part.TargetOffset, part.TargetEnd);
                        }
                        else if (!prematureEof)
                        {
                            prematureEof = true;
                            Log.Warning("{0}:{1}:{2}: Premature EOF; repairing", file, part.TargetOffset, reconstructed.Length);
                        }

                        reconstructed.Seek(part.TargetOffset, SeekOrigin.Begin);
                        reconstructed.Read(buf, 0, part.TargetSize);
                        local.Seek(part.TargetOffset, SeekOrigin.Begin);
                        local.Write(buf, 0, part.TargetSize);
                    }
                    local.SetLength(reconstructed.Length);
                }
            }
            finally
            {
                foreach (var source in sources)
                    source.Dispose();
            }
        }
    }
}
