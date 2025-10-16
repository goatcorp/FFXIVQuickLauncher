using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using XIVLauncher.Common.Dalamud;

namespace XIVLauncher.Windows.ViewModel
{
    public class DalamudBranchSwitcherViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<DalamudBranchMeta.Branch> Branches { get; set; } = [];

        private DalamudBranchMeta.Branch selectedBranch;

        public DalamudBranchMeta.Branch SelectedBranch
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
            Branches.Clear();
            var allBranches = await DalamudBranchMeta.FetchBranchesAsync();

            foreach (var branch in allBranches)
            {
                if (!branch.Hidden || (branch.Hidden && branch.Key == this.AppliedBetaKey))
                    Branches.Add(branch);
            }

            SelectedBranch = this.Branches.FirstOrDefault(x => x.Track == App.Settings.DalamudBetaKind && x.Key == App.Settings.DalamudBetaKey) ??
                             this.Branches.FirstOrDefault(x => x.Track == "release");
        }
    }
}
