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

        public DalamudBranchMeta.Branch SelectedBranch
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public string AppliedBetaKey
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public bool IsLoading
        {
            get;
            private set
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanInteract));
            }
        }

        public bool CanInteract => !this.IsLoading;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public async Task FetchBranchesAsync()
        {
            IsLoading = true;

            try
            {
                var allBranches = await DalamudBranchMeta.FetchBranchesAsync(App.HttpClient);

                Branches.Clear();

                foreach (var branch in allBranches)
                {
                    if (!branch.Hidden || (branch.Hidden && branch.Key == this.AppliedBetaKey))
                        Branches.Add(branch);
                }

                SelectedBranch = this.Branches.FirstOrDefault(x => x.Track == App.Settings.DalamudBetaKind && x.Key == App.Settings.DalamudBetaKey) ??
                                 this.Branches.FirstOrDefault(x => x.Track == "release");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
