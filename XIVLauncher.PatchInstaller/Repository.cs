using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Serilog;

namespace XIVLauncher.PatchInstaller
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
            var verFile = repo.GetVerFile(gamePath, isBck);

            if (!verFile.Exists) 
                return PatcherMain.BASE_GAME_VERSION;

            return File.ReadAllText(verFile.FullName);
        }

        public static void SetVer(this Repository repo, DirectoryInfo gamePath, string newVer, bool isBck = false)
        {
            var verFile = GetVerFile(repo, gamePath, isBck);

            if (!verFile.Directory.Exists)
                verFile.Directory.Create();

            File.WriteAllText(verFile.FullName, newVer, Encoding.ASCII);
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