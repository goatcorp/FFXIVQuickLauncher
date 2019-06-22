using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Serilog;

namespace XIVLauncher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Async(a => a.File(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "output.log")))
#if DEBUG
                   .MinimumLevel.Verbose()
#else
                .MinimumLevel.Information()
#endif
                .CreateLogger();

            Log.Information($"XIVLauncher started with version {Util.GetAssemblyVersion()}, commit {Util.GetGitHash()}");
        }
    }
}
