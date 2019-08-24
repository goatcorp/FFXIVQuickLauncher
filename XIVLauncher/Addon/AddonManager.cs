using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace XIVLauncher.Addon
{
    class AddonManager
    {
        private List<Tuple<IAddon, CancellationTokenSource>> _runningAddons;

        public void RunAddons(Process gameProcess, List<AddonEntry> addonEntries)
        {
            if (_runningAddons != null)
                throw new Exception("Addons still running?");

            _runningAddons = new List<Tuple<IAddon, CancellationTokenSource>>();

            foreach (var addonEntry in addonEntries)
            {
                if (addonEntry.Addon is IPersistentAddon persistentAddon)
                {
                    Log.Information("Starting PersistentAddon {0}", persistentAddon.Name);
                    var cancellationTokenSource = new CancellationTokenSource();

                    Task.Run(() => persistentAddon.DoWork(gameProcess, cancellationTokenSource.Token));
                    _runningAddons.Add(new Tuple<IAddon, CancellationTokenSource>(persistentAddon, cancellationTokenSource));
                }

                if (addonEntry.Addon is IRunnableAddon runnableAddon)
                {
                    Log.Information("Starting RunnableAddon {0}", runnableAddon.Name);
                    runnableAddon.Run(gameProcess);

                    if (runnableAddon is INotifyAddonAfterClose notifiedAddon)
                        _runningAddons.Add(new Tuple<IAddon, CancellationTokenSource>(notifiedAddon, null));
                }
            }
        }

        public void StopAddons()
        {
            Log.Information("Stopping addons...");

            foreach (var addon in _runningAddons)
            {
                addon.Item2?.Cancel();

                if (addon.Item1 is INotifyAddonAfterClose notifiedAddon)
                    notifiedAddon.GameClosed();
            }

            _runningAddons = null;
        }
    }
}
