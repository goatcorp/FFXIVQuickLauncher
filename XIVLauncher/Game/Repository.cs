using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Serilog;
using XIVLauncher.Game.Patch;

namespace XIVLauncher.Game
{
    public enum Repository
    {
        Boot,
        Ffxiv,
        Ex1,
        Ex2,
        Ex3
    }

    public static class RepoExtensions
    {
        private static readonly ConcurrentDictionary<FileInfo, FileStream> OpenVerFileStreams = new ConcurrentDictionary<FileInfo, FileStream>();

        private static DirectoryInfo GetRepoPath(this Repository repo, DirectoryInfo gamePath)
        {
            switch (repo)
            {
                case Repository.Boot:
                    return new DirectoryInfo(Path.Combine(gamePath.FullName, "boot"));
                case Repository.Ffxiv:
                    return new DirectoryInfo(Path.Combine(gamePath.FullName, "game"));
                case Repository.Ex1:
                    return new DirectoryInfo(Path.Combine(gamePath.FullName, "game", "sqpack", "ex1"));
                case Repository.Ex2:
                    return new DirectoryInfo(Path.Combine(gamePath.FullName, "game", "sqpack", "ex2"));
                case Repository.Ex3:
                    return new DirectoryInfo(Path.Combine(gamePath.FullName, "game", "sqpack", "ex3"));
                default:
                    throw new ArgumentOutOfRangeException(nameof(repo), repo, null);
            }
        }

        public static FileInfo GetVerFile(this Repository repo, DirectoryInfo gamePath, bool isBck = false)
        {
            var repoPath = repo.GetRepoPath(gamePath).FullName;
            switch (repo)
            {
                case Repository.Boot:
                    return new FileInfo(Path.Combine(repoPath, "ffxivboot" + (isBck ? ".bck" : ".ver")));
                case Repository.Ffxiv:
                    return new FileInfo(Path.Combine(repoPath, "ffxivgame" + (isBck ? ".bck" : ".ver")));
                case Repository.Ex1:
                    return new FileInfo(Path.Combine(repoPath, "ex1" + (isBck ? ".bck" : ".ver")));
                case Repository.Ex2:
                    return new FileInfo(Path.Combine(repoPath, "ex2" + (isBck ? ".bck" : ".ver")));
                case Repository.Ex3:
                    return new FileInfo(Path.Combine(repoPath, "ex3" + (isBck ? ".bck" : ".ver")));
                default:
                    throw new ArgumentOutOfRangeException(nameof(repo), repo, null);
            }
        }

        public static string GetVer(this Repository repo, DirectoryInfo gamePath, bool isBck = false)
        {
            var ver = PatchManager.BASE_GAME_VERSION;
            var verFile = repo.GetVerFile(gamePath, isBck);

            if (!verFile.Exists) 
                return ver;

            using var reader = new StreamReader(AcquireStream(verFile), Encoding.UTF8);
            reader.BaseStream.Position = 0;
            ver = reader.ReadToEnd();

            return ver;
        }

        public static void CloseAllStreams()
        {
            foreach (var openVerFileStream in OpenVerFileStreams)
            {
                openVerFileStream.Value.Close();
            }

            OpenVerFileStreams.Clear();
        }

        public static void SetVer(this Repository repo, DirectoryInfo gamePath, string newVer, bool isBck = false)
        {
            var verFile = repo.GetVerFile(gamePath, isBck);
            using var writer = new StreamWriter(AcquireStream(verFile), Encoding.UTF8);
            writer.BaseStream.Position = 0;
            writer.Write(newVer);
        }

        private static FileStream AcquireStream(FileInfo file)
        {
            if (OpenVerFileStreams.TryGetValue(file, out var val))
                return val;

            var tries = 0;
            while (tries <= 5)
            {
                tries++;

                try
                {
                    var stream = file.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    OpenVerFileStreams.GetOrAdd(file, stream);

                    return stream;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Could not open ver stream");
                }

                Thread.Sleep(200);
            }

            throw new Exception("Could not acquire lock for ver file.");
        }

        // TODO
        public static string GetRepoHash(this Repository repo)
        {
            switch (repo)
            {
                case Repository.Boot:
                    return null;
                case Repository.Ffxiv:
                    return null;
                case Repository.Ex1:
                    return null;
                case Repository.Ex2:
                    return null;
                case Repository.Ex3:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(repo), repo, null);
            }
        }
    }
}