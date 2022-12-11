#nullable enable
using System.IO;
using System.Text.Json.Serialization;

namespace XIVLauncher.Common.PatcherIpc
{
    public class PatcherIpcStartInstall
    {
        public Repository Repo { get; set; }
        public string VersionId { get; set; } = null!;
        public bool KeepPatch { get; set; }
        public string? PatchFilePath { get; set; }
        public string? GameDirectoryPath { get; set; }

        [JsonIgnore]
        public FileInfo? PatchFile
        {
            get => new(PatchFilePath!);
            set => PatchFilePath = value?.FullName;
        }

        [JsonIgnore]
        public DirectoryInfo? GameDirectory
        {
            get => new(GameDirectoryPath!);
            set => GameDirectoryPath = value?.FullName;
        }
    }
}