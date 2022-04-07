using System;
using System.IO;
using System.Text;

namespace XIVLauncher.Common
{
    /// <summary>
    /// FFXIV base/expansion game repository types.
    /// </summary>
    public enum Repository
    {
        /// <summary>
        /// Boot files.
        /// </summary>
        Boot,

        /// <summary>
        /// ARR files.
        /// </summary>
        Ffxiv,

        /// <summary>
        /// Heavensward files.
        /// </summary>
        Ex1,

        /// <summary>
        /// Stormblood files.
        /// </summary>
        Ex2,

        /// <summary>
        /// Shadowbringer files.
        /// </summary>
        Ex3,

        /// <summary>
        /// Endwalker files.
        /// </summary>
        Ex4,
    }

    /// <summary>
    /// Extension methods for the <see cref="Repository"/> enum.
    /// </summary>
    public static class RepoExtensions
    {
        /// <summary>
        /// Get the repo path.
        /// </summary>
        /// <param name="repo">Repository.</param>
        /// <param name="gamePath">Root game path.</param>
        /// <returns>Path to the repo.</returns>
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(repo), repo, null);
            }
        }

        /// <summary>
        /// Get the path to a repo's version file.
        /// </summary>
        /// <param name="repo">Repository.</param>
        /// <param name="gamePath">Root game path.</param>
        /// <param name="isBck">A value indicating if the backup file should be chosen.</param>
        /// <returns>Path to the repo version file.</returns>
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(repo), repo, null);
            }
        }

        /// <summary>
        /// Gets the version within a repo's version file.
        /// </summary>
        /// <param name="repo">Repository.</param>
        /// <param name="gamePath">Root game path.</param>
        /// <param name="isBck">A value indicating if the backup file should be chosen.</param>
        /// <returns>Version string for the repo.</returns>
        public static string GetVer(this Repository repo, DirectoryInfo gamePath, bool isBck = false)
        {
            var verFile = repo.GetVerFile(gamePath, isBck);

            if (!verFile.Exists)
                return Constants.BASE_GAME_VERSION;

            var ver = File.ReadAllText(verFile.FullName);
            return string.IsNullOrWhiteSpace(ver) ? Constants.BASE_GAME_VERSION : ver;
        }

        /// <summary>
        /// Set the version within a repo's version file.
        /// </summary>
        /// <param name="repo">Repository.</param>
        /// <param name="gamePath">Root game path.</param>
        /// <param name="newVer">The new version.</param>
        /// <param name="isBck">A value indicating if the backup file should be chosen.</param>
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

        /// <summary>
        /// Determine if the repository is unpatched.
        /// </summary>
        /// <param name="repo">Repository.</param>
        /// <param name="gamePath">Root game path.</param>
        /// <returns>A value indicating whether the repository is unpatched.</returns>
        public static bool IsBaseVer(this Repository repo, DirectoryInfo gamePath)
        {
            return repo.GetVer(gamePath) == Constants.BASE_GAME_VERSION;
        }

        /// <summary>
        /// Get the hash of a repo's files.
        /// </summary>
        /// <param name="repo">Repository.</param>
        /// <returns>A hash calculated from all of the files within a repo.</returns>
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(repo), repo, null);
            }
        }
    }
}
