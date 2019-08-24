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
        private List<Tuple<IPersistentAddon, CancellationTokenSource>> _persistentAddons;

        public void RunAddons(Process gameProcess, List<AddonEntry> addonEntries)
        {
            if (_persistentAddons != null)
                throw new Exception("Addons still running?");

            _persistentAddons = new List<Tuple<IPersistentAddon, CancellationTokenSource>>();

            foreach (var addonEntry in addonEntries)
            {
                if (addonEntry.Addon is IPersistentAddon persistentAddon)
                {
                    Log.Information("Starting PersistentAddon {0}", persistentAddon.Name);
                    var cancellationTokenSource = new CancellationTokenSource();

                    Task.Run(() => persistentAddon.DoWork(gameProcess, cancellationTokenSource.Token));
                    _persistentAddons.Add(new Tuple<IPersistentAddon, CancellationTokenSource>(persistentAddon, cancellationTokenSource));
                }

                if (addonEntry.Addon is IRunnableAddon runnableAddon)
                {
                    Log.Information("Starting RunnableAddon {0}", runnableAddon.Name);
                    runnableAddon.Run(gameProcess);
                }
            }
        }

        public void StopPersistentAddons()
        {
            Log.Information("Stopping persistent addons...");

            foreach (var persistentAddon in _persistentAddons)
            {
                persistentAddon.Item2.Cancel();
            }

            _persistentAddons = null;
        }
    }
}
