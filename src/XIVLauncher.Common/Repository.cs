using System;
using System.IO;
using System.Text;

namespace XIVLauncher.Common
{
    public enum Repository
    {
        Boot,
        Ffxiv,
        Ex1,
        Ex2,
        Ex3,
        Ex4,
        Ex5,
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
                case Repository.Ex4:
                    return new DirectoryInfo(Path.Combine(gamePath.FullName, "game", "sqpack", "ex4"));
                case Repository.Ex5:
                    return new DirectoryInfo(Path.Combine(gamePath.FullName, "game", "sqpack", "ex5"));
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
                case Repository.Ex4:
                    return new FileInfo(Path.Combine(repoPath, "ex4" + (isBck ? ".bck" : ".ver")));
                case Repository.Ex5:
                    return new FileInfo(Path.Combine(repoPath, "ex5" + (isBck ? ".bck" : ".ver")));
                default:
                    throw new ArgumentOutOfRangeException(nameof(repo), repo, null);
            }
        }

        public static string GetVer(this Repository repo, DirectoryInfo gamePath, bool isBck = false)
        {
            var verFile = repo.GetVerFile(gamePath, isBck);

            if (!verFile.Exists)
                return Constants.BASE_GAME_VERSION;

            var ver =  File.ReadAllText(verFile.FullName);
            return string.IsNullOrWhiteSpace(ver) ? Constants.BASE_GAME_VERSION : ver;
        }

        public static void SetVer(this Repository repo, DirectoryInfo gamePath, string newVer, bool isBck = false)
        {
            var verFile = GetVerFile(repo, gamePath, isBck);

            if (!verFile.Directory.Exists)
                verFile.Directory.Create();

            using var fileStream = verFile.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = Encoding.ASCII.GetBytes(newVer);
            fileStream.Write(buffer, 0, buffer.Length);
            fileStream.Flush();
        }

        public static bool IsBaseVer(this Repository repo, DirectoryInfo gamePath)
        {
            return repo.GetVer(gamePath) == Constants.BASE_GAME_VERSION;
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
                case Repository.Ex4:
                    return null;
                case Repository.Ex5:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(repo), repo, null);
            }
        }
    }
}
