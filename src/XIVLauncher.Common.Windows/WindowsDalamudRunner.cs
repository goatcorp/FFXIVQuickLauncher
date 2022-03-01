using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Windows;

public class WindowsDalamudRunner : IDalamudRunner
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    [DllImport("Dalamud.Boot.dll")]
    private static extern int RewriteRemoteEntryPointW(IntPtr hProcess, [MarshalAs(UnmanagedType.LPWStr)] string gamePath, [MarshalAs(UnmanagedType.LPWStr)] string loadInfoJson);

    public void Run(Process gameProcess, FileInfo runner, DalamudStartInfo startInfo, DirectoryInfo gamePath, DalamudLoadMethod loadMethod)
    {
        switch (loadMethod)
        {
            case DalamudLoadMethod.EntryPoint:
                SetDllDirectory(runner.DirectoryName);

                try
                {
                    if (0 != RewriteRemoteEntryPointW(gameProcess.Handle,
                            Path.Combine(gamePath.FullName, "game", gameProcess.ProcessName + ".exe"),
                            JsonConvert.SerializeObject(startInfo)))
                    {
                        Log.Error("[HOOKS] RewriteRemoteEntryPointW failed");
                        throw new DalamudRunnerException("RewriteRemoteEntryPointW failed");
                    }
                }
                catch (DllNotFoundException ex)
                {
                    Log.Error(ex, "[HOOKS] Dalamud entrypoint DLL not found");
                    throw new DalamudRunnerException("DLL not found");
                }

                break;

            case DalamudLoadMethod.DllInject:
            {
                var parameters = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(startInfo)));

                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = runner.FullName, WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true,
                        Arguments = gameProcess.Id + " " + parameters, WorkingDirectory = runner.DirectoryName!
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