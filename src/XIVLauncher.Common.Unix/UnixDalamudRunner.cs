using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Unix;

public class UnixDalamudRunner : IDalamudRunner
{

    public void Run(Int32 gameProcessID, FileInfo runner, DalamudStartInfo startInfo, DirectoryInfo gamePath, DalamudLoadMethod loadMethod)
    {
        switch (loadMethod)
        {
            case DalamudLoadMethod.EntryPoint:
                throw new NotImplementedException();
                break;

            case DalamudLoadMethod.DllInject:
            {
                var parameters = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(startInfo)));

                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = runner.FullName, WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true,
                        Arguments = gameProcessID + " " + parameters, WorkingDirectory = runner.DirectoryName!
                    }
                };

                process.Start();
                break;
            }

            default:
                // should not reach
                throw new ArgumentOutOfRangeException();
        }
    }
}