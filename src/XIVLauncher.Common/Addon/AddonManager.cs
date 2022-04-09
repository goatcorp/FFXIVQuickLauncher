using System;
using System.Collections.Generic;
using System.Threading;
using Serilog;

namespace XIVLauncher.Common.Addon
{
    public class AddonManager
    {
        private List<Tuple<IAddon, Thread, CancellationTokenSource>> _runningAddons;

        public bool IsRunning { get; private set; }

        public void RunAddons(int gamePid, List<IAddon> addonEntries)
        {
            if (_runningAddons != null)
                throw new Exception("Addons still running?");

            _runningAddons = new List<Tuple<IAddon, Thread, CancellationTokenSource>>();

            foreach (var addonEntry in addonEntries)
            {
                addonEntry.Setup(gamePid);

                if (addonEntry is IPersistentAddon persistentAddon)
                {
                    Log.Information("Starting PersistentAddon {0}", persistentAddon.Name);
                    var cancellationTokenSource = new CancellationTokenSource();

                    var addonThread = new Thread(persistentAddon.DoWork);
                    addonThread.Start(cancellationTokenSource.Token);

                    _runningAddons.Add(new Tuple<IAddon, Thread, CancellationTokenSource>(persistentAddon, addonThread, cancellationTokenSource));
                }

                if (addonEntry is IRunnableAddon runnableAddon)
                {
                    Log.Information("Starting RunnableAddon {0}", runnableAddon.Name);
                    runnableAddon.Run();
                }

                if (addonEntry is INotifyAddonAfterClose notifiedAddon)
                    _runningAddons.Add(new Tuple<IAddon, Thread, CancellationTokenSource>(notifiedAddon, null, null));
            }

            IsRunning = true;
        }

        public void StopAddons()
        {
            Log.Information("Stopping addons...");

            if (_runningAddons != null)
            {
                foreach (var addon in _runningAddons)
                {
                    addon.Item3?.Cancel();
                    addon.Item2?.Join();

                    if (addon.Item1 is INotifyAddonAfterClose notifiedAddon)
                        notifiedAddon.GameClosed();
                }

                _runningAddons = null;
            }

            IsRunning = false;
        }
    }
}