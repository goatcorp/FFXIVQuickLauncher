using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Patching;
using XIVLauncher.Common.Patching.IndexedZiPatch;
using XIVLauncher.Common.Patching.Rpc.Implementations;

namespace XIVLauncher.PatchInstaller
{
    public class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                Log.Logger = new LoggerConfiguration()
                             .WriteTo.Console()
                             .WriteTo.File(Path.Combine(Paths.RoamingPath, "patcher.log"))
                             .WriteTo.Debug()
                             .MinimumLevel.Verbose()
                             .CreateLogger();

                if (args.Length > 1 && args[0] == "install")
                {
                    try
                    {
                        foreach (var file in args.Skip(1).Take(args.Length - 2).ToList())
                            RemotePatchInstaller.InstallPatch(file, args[args.Length - 1]);
                        Log.Information("OK");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Patch installation failed");
                        Environment.Exit(-1);
                    }

                    Environment.Exit(0);
                    return;
                }

                if (args.Length > 1 && args[0] == "index-create")
                {
                    try
                    {
                        IndexedZiPatchOperations.CreateZiPatchIndices(int.Parse(args[1]), args.Skip(2).ToList()).Wait();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to create patch index files.");
                        Environment.Exit(-1);
                    }

                    Environment.Exit(0);
                    return;
                }

                if (args.Length > 2 && args[0] == "index-verify")
                {
                    try
                    {
                        IndexedZiPatchOperations.VerifyFromZiPatchIndex(args[1], args[2]).Wait();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to verify from patch index file.");
                        Environment.Exit(-1);
                    }

                    Environment.Exit(0);
                    return;
                }

                if (args.Length > 2 && args[0] == "index-repair")
                {
                    try
                    {
                        IndexedZiPatchOperations.RepairFromPatchFileIndexFromFile(args[1], args[2], args[3], 8).Wait();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to repair from patch index file.");
                        Environment.Exit(-1);
                    }

                    Environment.Exit(0);
                    return;
                }

                if (args.Length > 2 && args[0] == "index-rpc")
                {
                    new IndexedZiPatchIndexRemoteInstaller.WorkerSubprocessBody(int.Parse(args[1]), args[2]).RunToDisposeSelf();
                    return;
                }

                if (args.Length > 0 && args[0] == "index-rpc-test")
                {
                    IndexedZiPatchIndexRemoteInstaller.Test();
                    return;
                }

                if (args.Length == 0 || args[0] != "rpc")
                {
                    Log.Information("usage:\n" +
                                    "* XIVLauncher.PatchInstaller.exe install <oldest>.patch <oldest2>.patch ... <newest>.patch <game dir>\n" +
                                    "  * Install patch files in the given order.\n" +
                                    "* XIVLauncher.PatchInstaller.exe index-create <expac version; -1 for boot> <oldest>.patch <oldest2>.patch ... <newest>.patch\n" +
                                    "  * Index game patch files in the given order.\n" +
                                    "* XIVLauncher.PatchInstaller.exe index-verify <patch index file> <game dir>\n" +
                                    "  * Verify game installation from patch file index.\n" +
                                    "* XIVLauncher.PatchInstaller.exe index-repair <patch index file> <game dir> <patch file directory>\n" +
                                    "  * Verify and repair game installation from patch file index, looking for patch files in given patch file directory.\n" +
                                    "* XIVLauncher.PatchInstaller.exe <server port> <client port>");

                    Environment.Exit(-1);
                    return;
                }

                var installer = new RemotePatchInstaller(new SharedMemoryRpc(args[1]));
                installer.Start();

                while (true)
                {
                    if ((Process.GetProcesses().All(x => x.ProcessName != "XIVLauncher") && !installer.HasQueuedInstalls) || installer.IsDone)
                    {
                        Environment.Exit(0);
                        return;
                    }

                    Thread.Sleep(1000);

                    if (installer.IsFailed)
                    {
                        Log.Information("Exited due to failure");
                        Environment.Exit(-1);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Patcher init failed.\n\n" + ex, "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}