using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XIVLauncher.Windows.ViewModel
{
    public class Branch
    {
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }
        [JsonPropertyName("description")]
        public string Description { get; set; }
        [JsonPropertyName("track")]
        public string Track { get; set; }
        //[JsonPropertyName("changelog")]
        //public string Changelog { get; set; }

        [JsonPropertyName("hidden")]
        public bool Hidden { get; set; }
        [JsonPropertyName("key")]
        public string Key { get; set; }
        [JsonPropertyName("assemblyVersion")]
        public string AssemblyVersion { get; set; }
        [JsonPropertyName("runtimeVersion")]
        public string RuntimeVersion { get; set; }
        [JsonPropertyName("runtimeRequired")]
        public bool RuntimeRequired { get; set; }
        [JsonPropertyName("supportedGameVer")]
        public string SupportedGameVer { get; set; }
        [JsonPropertyName("isApplicableForCurrentGameVer")]
        public bool IsApplicableForCurrentGameVer { get; set; }
    }

    public class BranchSwitcherViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Branch> Branches { get; set; } = new ObservableCollection<Branch>();

        private Branch selectedBranch;

        public Branch SelectedBranch
        {
            get => selectedBranch;
            set
            {
                selectedBranch = value;
                OnPropertyChanged();
            }
        }

        private string appliedBetaKey;

        public string AppliedBetaKey
        {
            get => this.appliedBetaKey;
            set
            {
                this.appliedBetaKey = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public async Task FetchBranchesAsync()
        {
            using var client = new HttpClient();
            var json = await client.GetStringAsync("https://kamori.goats.dev/Dalamud/Release/Meta");
            Branches.Clear();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dict = JsonSerializer.Deserialize<Dictionary<string, Branch>>(json, options);
            if (dict != null)
            {
                foreach (var branch in dict.Values)
                {
                    if ((!branch.Hidden || (branch.Hidden && branch.Key == this.AppliedBetaKey)) && branch.IsApplicableForCurrentGameVer)
                        Branches.Add(branch);
                }
            }
        }
    }
}
