using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Windows;

public class WindowsDalamudRunner : IDalamudRunner
{
    public Process? Run(FileInfo runner, bool fakeLogin, FileInfo gameExe, string gameArgs, IDictionary<string, string> environment, DalamudLoadMethod loadMethod, DalamudStartInfo startInfo)
    {
        var inheritableCurrentProcess = GetInheritableCurrentProcessHandle();

        var launchArguments = new List<string>
        {
            "launch",
            $"--mode={(loadMethod == DalamudLoadMethod.EntryPoint ? "entrypoint" : "inject")}",
            $"--handle-owner={(long)inheritableCurrentProcess.Handle}",
            $"--game=\"{gameExe.FullName}\"",
            $"--dalamud-working-directory=\"{startInfo.WorkingDirectory}\"",
            $"--dalamud-configuration-path=\"{startInfo.ConfigurationPath}\"",
            $"--dalamud-plugin-directory=\"{startInfo.PluginDirectory}\"",
            $"--dalamud-dev-plugin-directory=\"{startInfo.DefaultPluginDirectory}\"",
            $"--dalamud-asset-directory=\"{startInfo.AssetDirectory}\"",
            $"--dalamud-client-language={(int)startInfo.Language}",
            $"--dalamud-delay-initialize={startInfo.DelayInitializeMs}"
        };

        if (loadMethod == DalamudLoadMethod.ACLonly)
            launchArguments.Add("--without-dalamud");

        if (fakeLogin)
            launchArguments.Add("--fake-arguments");

        launchArguments.Add("--");
        launchArguments.Add(gameArgs);

        var psi = new ProcessStartInfo(runner.FullName) {
            Arguments = string.Join(" ", launchArguments),
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var keyValuePair in environment)
        {
            if (psi.EnvironmentVariables.ContainsKey(keyValuePair.Key))
                psi.EnvironmentVariables[keyValuePair.Key] = keyValuePair.Value;
            else
                psi.EnvironmentVariables.Add(keyValuePair.Key, keyValuePair.Value);
        }

        try
        {
            var dalamudProcess = Process.Start(psi);
            var output = dalamudProcess.StandardOutput.ReadLine();

            if (output == null)
                throw new DalamudRunnerException("An internal Dalamud error has occured");

            try
            {
                var dalamudConsoleOutput = JsonConvert.DeserializeObject<DalamudConsoleOutput>(output);
                Process gameProcess;

                if (dalamudConsoleOutput.Handle == 0)
                {
                    Log.Warning($"Dalamud returned NULL process handle, attempting to recover by creating a new one from pid {dalamudConsoleOutput.Pid}...");
                    gameProcess = Process.GetProcessById(dalamudConsoleOutput.Pid);
                }
                else
                {
                    gameProcess = new ExistingProcess((IntPtr)dalamudConsoleOutput.Handle);
                }

                try
                {
                    Log.Verbose($"Got game process handle {gameProcess.Handle} with pid {gameProcess.Id}");
                }
                catch (InvalidOperationException ex)
                {
                    Log.Error(ex, $"Dalamud returned invalid process handle {gameProcess.Handle}, attempting to recover by creating a new one from pid {dalamudConsoleOutput.Pid}...");
                    gameProcess = Process.GetProcessById(dalamudConsoleOutput.Pid);
                    Log.Warning($"Recovered with process handle {gameProcess.Handle}");
                }

                if (gameProcess.Id != dalamudConsoleOutput.Pid)
                    Log.Warning($"Internal Process ID {gameProcess.Id} does not match Dalamud provided one {dalamudConsoleOutput.Pid}");

                return gameProcess;
            }
            catch (JsonReaderException ex)
            {
                Log.Error(ex, $"Couldn't parse Dalamud output: {output}");
                return null;
            }
        }
        catch (Exception ex)
        {
            throw new DalamudRunnerException("Error trying to start Dalamud.", ex);
        }
    }

    /// <summary>
    /// DUPLICATE_* values for DuplicateHandle's dwDesiredAccess.
    /// </summary>
    [Flags]
    private enum DuplicateOptions : uint {
        /// <summary>
        /// Closes the source handle. This occurs regardless of any error status returned.
        /// </summary>
        CloseSource = 0x00000001,

        /// <summary>
        /// Ignores the dwDesiredAccess parameter. The duplicate handle has the same access as the source handle.
        /// </summary>
        SameAccess = 0x00000002,
    }

    /// <summary>
    /// Duplicates an object handle.
    /// </summary>
    /// <param name="hSourceProcessHandle">
    /// A handle to the process with the handle to be duplicated.
    ///
    /// The handle must have the PROCESS_DUP_HANDLE access right.
    /// </param>
    /// <param name="hSourceHandle">
    /// The handle to be duplicated. This is an open object handle that is valid in the context of the source process.
    /// For a list of objects whose handles can be duplicated, see the following Remarks section.
    /// </param>
    /// <param name="hTargetProcessHandle">
    /// A handle to the process that is to receive the duplicated handle.
    ///
    /// The handle must have the PROCESS_DUP_HANDLE access right.
    /// </param>
    /// <param name="lpTargetHandle">
    /// A pointer to a variable that receives the duplicate handle. This handle value is valid in the context of the target process.
    ///
    /// If hSourceHandle is a pseudo handle returned by GetCurrentProcess or GetCurrentThread, DuplicateHandle converts it to a real handle to a process or thread, respectively.
    ///
    /// If lpTargetHandle is NULL, the function duplicates the handle, but does not return the duplicate handle value to the caller. This behavior exists only for backward compatibility with previous versions of this function. You should not use this feature, as you will lose system resources until the target process terminates.
    ///
    /// This parameter is ignored if hTargetProcessHandle is NULL.
    /// </param>
    /// <param name="dwDesiredAccess">
    /// The access requested for the new handle. For the flags that can be specified for each object type, see the following Remarks section.
    ///
    /// This parameter is ignored if the dwOptions parameter specifies the DUPLICATE_SAME_ACCESS flag. Otherwise, the flags that can be specified depend on the type of object whose handle is to be duplicated.
    ///
    /// This parameter is ignored if hTargetProcessHandle is NULL.
    /// </param>
    /// <param name="bInheritHandle">
    /// A variable that indicates whether the handle is inheritable. If TRUE, the duplicate handle can be inherited by new processes created by the target process. If FALSE, the new handle cannot be inherited.
    ///
    /// This parameter is ignored if hTargetProcessHandle is NULL.
    /// </param>
    /// <param name="dwOptions">
    /// Optional actions.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is nonzero.
    ///
    /// If the function fails, the return value is zero. To get extended error information, call GetLastError.
    /// </returns>
    /// <remarks>
    /// See https://docs.microsoft.com/en-us/windows/win32/api/handleapi/nf-handleapi-duplicatehandle.
    /// </remarks>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateHandle(
        IntPtr hSourceProcessHandle,
        IntPtr hSourceHandle,
        IntPtr hTargetProcessHandle,
        out IntPtr lpTargetHandle,
        uint dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        DuplicateOptions dwOptions);

    private static Process GetInheritableCurrentProcessHandle() {
        if (!DuplicateHandle(Process.GetCurrentProcess().Handle, Process.GetCurrentProcess().Handle, Process.GetCurrentProcess().Handle, out var inheritableCurrentProcessHandle, 0, true, DuplicateOptions.SameAccess)) {
            Log.Error("Failed to call DuplicateHandle: Win32 error code {0}", Marshal.GetLastWin32Error());
            return null;
        }

        return new ExistingProcess(inheritableCurrentProcessHandle);
    }
}