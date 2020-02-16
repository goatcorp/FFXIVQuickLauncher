using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using SharpCompress.Archives.Zip;

namespace XIVLauncher.Dalamud.PluginUpdate
{
    static class PluginUpdateMaster
    {
        private const string UPDATE_BASE = "https://goaaats.github.io/DalamudPlugins/";

        public static void Run(string pluginDirectory)
        {
            var pluginDirInfo = new DirectoryInfo(pluginDirectory);
            var eligibleFile = pluginDirInfo.GetFiles("*.dll", SearchOption.AllDirectories);

            foreach (var fileInfo in eligibleFile)
            {
                var defJsonFile = new FileInfo(Path.Combine(fileInfo.Directory.FullName, $"{Path.GetFileNameWithoutExtension(fileInfo.Name)}.json"));

                if (defJsonFile.Exists)
                {
                    Log.Information("Loading definition for plugin DLL {0}", fileInfo.FullName);

                    var pluginDef =
                        JsonConvert.DeserializeObject<PluginDefinition>(File.ReadAllText(defJsonFile.FullName));

                    try
                    {
                        var remoteDef = GetRemoteDefinition(pluginDef.InternalName);

                        Log.Information("Plugin {0} update check: local:{1} remote:{2}", pluginDef.InternalName, pluginDef.AssemblyVersion, remoteDef.AssemblyVersion);

                        if (pluginDef.AssemblyVersion != remoteDef.AssemblyVersion)
                        {
                            UpdatePlugin(fileInfo, pluginDef);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Remote plugin update failed.");
                    }
                }
                else
                {
                    Log.Information("Plugin DLL {0} has no definition.", fileInfo.FullName);
                }
            }
        }

        private static void UpdatePlugin(FileInfo pluginDll, PluginDefinition localDef)
        {
            using (var client = new WebClient())
            {
                var dlPath = Path.GetTempFileName();
                client.DownloadFile($"{UPDATE_BASE}/plugins/{localDef.InternalName}/latest.zip", dlPath);

                ZipFile.ExtractToDirectory(dlPath, pluginDll.Directory.FullName);

                File.Delete(dlPath);
            }
        }

        private static PluginDefinition GetRemoteDefinition(string internalName)
        {
            using (var client = new WebClient())
            {
                return JsonConvert.DeserializeObject<PluginDefinition>(
                    client.DownloadString($"{UPDATE_BASE}/plugins/{internalName}/{internalName}.json"));
            }
        }
    }
}
