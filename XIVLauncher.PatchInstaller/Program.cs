using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.PatchInstaller.ZiPatch;

namespace XIVLauncher.PatchInstaller
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Async(a =>
                    a.File(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "XIVLauncher", "patcher.log")))
                .WriteTo.Console()
#if DEBUG
                .WriteTo.Debug()
                .MinimumLevel.Verbose()
#endif
                .CreateLogger();

            if (args.Length == 3)
            {
                InstallPatch(args[0], args[1], args[2]);
                return;
            }

            if (args.Length == 1)
            {
                return;
            }

            Console.WriteLine("XIVLauncher.PatchInstaller\n\nUsage:\nXIVLauncher.PatchInstaller.exe <patch path> <game path> <repository>\nOR\nXIVLauncher.PatchInstaller.exe <pipe name>");
        }

        private static void InstallPatch(string path, string gamePath, string repository)
        {
            var execute = new ZiPatchExecute(gamePath, repository);

            try
            {
                execute.Execute(path);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to execute ZiPatch.");
                throw;
            }
        }
    }
}
