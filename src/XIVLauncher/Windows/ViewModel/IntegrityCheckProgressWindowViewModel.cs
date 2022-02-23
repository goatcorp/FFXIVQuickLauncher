using CheapLoc;

namespace XIVLauncher.Windows.ViewModel
{
    class IntegrityCheckProgressWindowViewModel
    {
        public IntegrityCheckProgressWindowViewModel()
        {
            SetupLoc();
        }

        private void SetupLoc()
        {
            IntegrityCheckRunningLoc = Loc.Localize("IntegrityCheckRunning", "Running integrity check...");
        }

        public string IntegrityCheckRunningLoc { get; private set; }
    }
}
