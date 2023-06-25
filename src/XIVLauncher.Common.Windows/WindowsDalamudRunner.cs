using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json;
using PInvoke;
using Serilog;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Windows;

public class WindowsDalamudRunner : IDalamudRunner
{
    public unsafe Process? Run(FileInfo runner, bool fakeLogin, bool noPlugins, bool noThirdPlugins, FileInfo gameExe, string gameArgs, IDictionary<string, string> environment, DalamudLoadMethod loadMethod, DalamudStartInfo dalamudStartInfo)
    {
        var inheritableCurrentProcess = GetInheritableCurrentProcessHandle();

        if (gameExe == null)
            throw new ArgumentNullException(nameof(gameExe), "Game path was null");

        if (dalamudStartInfo == null)
            throw new ArgumentNullException(nameof(dalamudStartInfo), "StartInfo was null");

        if (dalamudStartInfo.TroubleshootingPackData == null)
            throw new ArgumentNullException(nameof(dalamudStartInfo.TroubleshootingPackData), "TS data was null");

        var launchArguments = new List<string>
        {
            DalamudInjectorArgs.LAUNCH,
            DalamudInjectorArgs.Mode(loadMethod == DalamudLoadMethod.EntryPoint ? "entrypoint" : "inject"),
            DalamudInjectorArgs.Game(gameExe.FullName),
            DalamudInjectorArgs.WorkingDirectory(dalamudStartInfo.WorkingDirectory),
            DalamudInjectorArgs.ConfigurationPath(dalamudStartInfo.ConfigurationPath),
            DalamudInjectorArgs.LoggingPath(dalamudStartInfo.LoggingPath),
            DalamudInjectorArgs.PluginDirectory(dalamudStartInfo.PluginDirectory),
            DalamudInjectorArgs.AssetDirectory(dalamudStartInfo.AssetDirectory),
            DalamudInjectorArgs.ClientLanguage((int)dalamudStartInfo.Language),
            DalamudInjectorArgs.DelayInitialize(dalamudStartInfo.DelayInitializeMs),
            DalamudInjectorArgs.TsPackB64(Convert.ToBase64String(Encoding.UTF8.GetBytes(dalamudStartInfo.TroubleshootingPackData))),
        };

        if (inheritableCurrentProcess != null)
            launchArguments.Add(DalamudInjectorArgs.HandleOwner((long)inheritableCurrentProcess.Handle));

        if (loadMethod == DalamudLoadMethod.ACLonly)
            launchArguments.Add(DalamudInjectorArgs.WITHOUT_DALAMUD);

        if (fakeLogin)
            launchArguments.Add(DalamudInjectorArgs.FAKE_ARGUMENTS);

        if (noPlugins)
            launchArguments.Add(DalamudInjectorArgs.NO_PLUGIN);

        if (noThirdPlugins)
            launchArguments.Add(DalamudInjectorArgs.NO_THIRD_PARTY);

        launchArguments.Add("--");
        launchArguments.Add(gameArgs);

        var joinedArguments = string.Join(" ", launchArguments);
        var fullCommandLine = $"\"{runner.FullName}\" {joinedArguments}";
        var envVars = SafeGetEnvVars();

        // Merge specified env vars into existing env var dict
        foreach (var keyValuePair in environment)
        {
            if (envVars.ContainsKey(keyValuePair.Key))
                envVars[keyValuePair.Key] = keyValuePair.Value;
            else
                envVars.Add(keyValuePair.Key, keyValuePair.Value);
        }

        try
        {
            var environmentBlock = GetEnvironmentVariablesBlock(envVars);

            Log.Verbose("Starting launch Dalamud with\n\tCmdLine: {CommandLine}\n\tEnvBlock: {EnvironmentBlock}",
                fullCommandLine,
                environmentBlock.Replace("\0", "\\0"));

            var kernelStartupInfo = Kernel32.STARTUPINFO.Create();
            Kernel32.PROCESS_INFORMATION kernelProcessInfo;

            var pipeSecAttr = Kernel32.SECURITY_ATTRIBUTES.Create();
            pipeSecAttr.bInheritHandle = 1;

            // Create the pipe used to capture stdout
            if (!Kernel32.CreatePipe(
                    out var tempOutputHandle,
                    out var childOutputPipeHandle,
                    pipeSecAttr, 0))
            {
                throw new Win32Exception();
            }

            Log.Verbose("=> Acquired pipe");

            var currentProcHandle = Kernel32.GetCurrentProcess();

            // Duplicate the pipe's handle, so that we can still access it even if the child process closes it
            if (!DuplicateHandle(currentProcHandle.DangerousGetHandle(),
                    tempOutputHandle.DangerousGetHandle(),
                    currentProcHandle.DangerousGetHandle(),
                    out IntPtr parentOutputPipeHandle,
                    0,
                    false,
                    DuplicateOptions.SameAccess))
            {
                throw new Win32Exception();
            }

            Log.Verbose("=> Duplicated pipe handle");

            kernelStartupInfo.dwFlags = Kernel32.StartupInfoFlags.STARTF_USESTDHANDLES;
            kernelStartupInfo.hStdOutput = childOutputPipeHandle.DangerousGetHandle();

            // Start process
            fixed (char* environmentBlockPtr = environmentBlock)
            {
                const Kernel32.CreateProcessFlags FLAGS = Kernel32.CreateProcessFlags.CREATE_NO_WINDOW |
                                                          Kernel32.CreateProcessFlags.CREATE_UNICODE_ENVIRONMENT;

                var retVal = Kernel32.CreateProcess(
                    null,
                    fullCommandLine,
                    (Kernel32.SECURITY_ATTRIBUTES*)0,
                    (Kernel32.SECURITY_ATTRIBUTES*)0,
                    true,
                    FLAGS,
                    environmentBlockPtr,
                    Environment.CurrentDirectory,
                    ref kernelStartupInfo,
                    out kernelProcessInfo);

                if (!retVal)
                    throw new Win32Exception();
            }

            Log.Verbose("=> Started process");

            // Close thread handle, it will leak otherwise
            if (kernelProcessInfo.hThread != IntPtr.Zero && kernelProcessInfo.hThread != new IntPtr(-1))
                Kernel32.CloseHandle(kernelProcessInfo.hThread);

            // Create stdout stream reader with our pipe
            var stdoutEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            using var stdoutStream = new StreamReader(new FileStream(new SafeFileHandle(parentOutputPipeHandle, false), FileAccess.Read, 4096, false), stdoutEncoding, true, 4096);

            // Wait for the process to exit
            const int WAIT_INJECTOR_TIMEOUT_MS = 60 * 1000;
            var res = Kernel32.WaitForSingleObject(new SafeProcessHandle(kernelProcessInfo.hProcess, false), WAIT_INJECTOR_TIMEOUT_MS);

            if (res != Kernel32.WaitForSingleObjectResult.WAIT_OBJECT_0)
            {
                if (res == Kernel32.WaitForSingleObjectResult.WAIT_FAILED)
                    throw new Win32Exception();

                throw new DalamudRunnerException("Injector did not exit in the expected timeout period");
            }

            Log.Verbose("=> WaitForSingleObject() complete");

            // Check the exit code
            if (!Kernel32.GetExitCodeProcess(kernelProcessInfo.hProcess, out var exitCode))
                throw new Win32Exception();

            if (exitCode != 0)
                throw new DalamudRunnerException($"Injector exit code was {exitCode}");

            // Check if the stream is empty, if not, read a line from it(json with pid/handle)
            if (stdoutStream.EndOfStream)
                throw new DalamudRunnerException("Injector output stream was empty");

            var output = stdoutStream.ReadLine();
            if (string.IsNullOrEmpty(output))
                throw new DalamudRunnerException("No injector output");

            Log.Verbose("=> Reading result");

            Process gameProcess;

            try
            {
                Log.Verbose("=> Dalamud.Injector output: {Output}", output);
                var dalamudConsoleOutput = JsonConvert.DeserializeObject<DalamudConsoleOutput>(output);

                if (dalamudConsoleOutput.Handle == 0)
                {
                    Log.Warning($"=> Dalamud returned NULL process handle, attempting to recover by creating a new one from pid {dalamudConsoleOutput.Pid}...");
                    gameProcess = Process.GetProcessById(dalamudConsoleOutput.Pid);
                }
                else
                {
                    gameProcess = new ExistingProcess((IntPtr)dalamudConsoleOutput.Handle);
                }

                try
                {
                    Log.Verbose($"=> Got game process handle {gameProcess.Handle} with pid {gameProcess.Id}");
                }
                catch (InvalidOperationException ex)
                {
                    Log.Error(ex, $"=> Dalamud returned invalid process handle {gameProcess.Handle}, attempting to recover by creating a new one from pid {dalamudConsoleOutput.Pid}...");
                    gameProcess = Process.GetProcessById(dalamudConsoleOutput.Pid);
                    Log.Warning($"=> Recovered with process handle {gameProcess.Handle}");
                }

                if (gameProcess.Id != dalamudConsoleOutput.Pid)
                    Log.Warning($"=> Internal Process ID {gameProcess.Id} does not match Dalamud provided one {dalamudConsoleOutput.Pid}");
            }
            catch (JsonReaderException ex)
            {
                Log.Error(ex, $"=> Couldn't parse Dalamud output: {output}");
                return null;
            }

            Log.Verbose("=> Closing handles");

            // Close our own pipe, the child will have closed its
            Kernel32.CloseHandle(parentOutputPipeHandle);

            // Close the child process handle
            Kernel32.CloseHandle(kernelProcessInfo.hProcess);

            return gameProcess;
        }
        catch (Exception ex)
        {
            throw new DalamudRunnerException("Error trying to start Dalamud.", ex);
        }
    }

    // ReSharper disable SuggestVarOrType_SimpleTypes

    /*
    * .NET Framework BUG: ProcessStartInfo.EnvironmentVariables can't handle
    * env vars with the same name, but different casing. This is usually forbidden
    * on Windows but we all know what that means.
    *
    * New code taken from .NET Core
    * https://github.com/dotnet/runtime/blob/2c62994efb2495dcaef2312de3ab25ea4792b23a/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/ProcessStartInfo.cs#L97-L110
    */
    private static IDictionary<string, string> SafeGetEnvVars()
    {
        IDictionary envVars = System.Environment.GetEnvironmentVariables();

        var envDict = new DictionaryWrapper(new Dictionary<string, string?>(
            envVars.Count,
            StringComparer.OrdinalIgnoreCase));

        // Manual use of IDictionaryEnumerator instead of foreach to avoid DictionaryEntry box allocations.
        IDictionaryEnumerator e = envVars.GetEnumerator();

        Debug.Assert(!(e is IDisposable), "Environment.GetEnvironmentVariables should not be IDisposable.");

        while (e.MoveNext())
        {
            DictionaryEntry entry = e.Entry;
            envDict.Add((string)entry.Key, (string?)entry.Value);
        }

        return envDict;
    }

    // https://github.com/dotnet/runtime/blob/2c62994efb2495dcaef2312de3ab25ea4792b23a/src/libraries/System.Diagnostics.Process/src/System/Collections/Specialized/DictionaryWrapper.cs#L8
    private sealed class DictionaryWrapper : IDictionary<string, string?>, IDictionary
    {
        private readonly Dictionary<string, string?> _contents;

        public DictionaryWrapper(Dictionary<string, string?> contents)
        {
            _contents = contents;
        }

        public string? this[string key]
        {
            get => _contents[key];
            set => _contents[key] = value;
        }

        public object? this[object key]
        {
            get => this[(string)key];
            set => this[(string)key] = (string?)value;
        }

        public ICollection<string> Keys => _contents.Keys;
        public ICollection<string?> Values => _contents.Values;

        ICollection IDictionary.Keys => _contents.Keys;
        ICollection IDictionary.Values => _contents.Values;

        public int Count => _contents.Count;

        public bool IsReadOnly => ((IDictionary)_contents).IsReadOnly;
        public bool IsSynchronized => ((IDictionary)_contents).IsSynchronized;
        public bool IsFixedSize => ((IDictionary)_contents).IsFixedSize;
        public object SyncRoot => ((IDictionary)_contents).SyncRoot;

        public void Add(string key, string? value) => this[key] = value;

        public void Add(KeyValuePair<string, string?> item) => Add(item.Key, item.Value);

        public void Add(object key, object? value) => Add((string)key, (string?)value);

        public void Clear() => _contents.Clear();

        public bool Contains(KeyValuePair<string, string?> item)
        {
            return _contents.ContainsKey(item.Key) && _contents[item.Key] == item.Value;
        }

        public bool Contains(object key) => ContainsKey((string)key);
        public bool ContainsKey(string key) => _contents.ContainsKey(key);
        public bool ContainsValue(string? value) => _contents.ContainsValue(value);

        public void CopyTo(KeyValuePair<string, string?>[] array, int arrayIndex)
        {
            ((IDictionary<string, string?>)_contents).CopyTo(array, arrayIndex);
        }

        public void CopyTo(Array array, int index) => ((IDictionary)_contents).CopyTo(array, index);

        public bool Remove(string key) => _contents.Remove(key);
        public void Remove(object key) => Remove((string)key);

        public bool Remove(KeyValuePair<string, string?> item)
        {
            if (!Contains(item))
            {
                return false;
            }

            return Remove(item.Key);
        }

        public bool TryGetValue(string key, out string? value) => _contents.TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<string, string?>> GetEnumerator() => _contents.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _contents.GetEnumerator();
        IDictionaryEnumerator IDictionary.GetEnumerator() => _contents.GetEnumerator();
    }

    // https://github.com/dotnet/runtime/blob/2c62994efb2495dcaef2312de3ab25ea4792b23a/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Windows.cs#L860-L879
    private static string GetEnvironmentVariablesBlock(IDictionary<string, string> sd)
    {
        // https://docs.microsoft.com/en-us/windows/win32/procthread/changing-environment-variables
        // "All strings in the environment block must be sorted alphabetically by name. The sort is
        //  case-insensitive, Unicode order, without regard to locale. Because the equal sign is a
        //  separator, it must not be used in the name of an environment variable."

        var keys = new string[sd.Count];
        sd.Keys.CopyTo(keys, 0);
        Array.Sort(keys, StringComparer.OrdinalIgnoreCase);

        // Join the null-terminated "key=val\0" strings
        var result = new StringBuilder(8 * keys.Length);

        foreach (string key in keys)
        {
            result.Append(key).Append('=').Append(sd[key]).Append('\0');
        }

        return result.ToString();
    }
    // ReSharper restore SuggestVarOrType_SimpleTypes

    /// <summary>
    /// DUPLICATE_* values for DuplicateHandle's dwDesiredAccess.
    /// </summary>
    [Flags]
    private enum DuplicateOptions : uint
    {
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

    private static Process GetInheritableCurrentProcessHandle()
    {
        if (!DuplicateHandle(Process.GetCurrentProcess().Handle, Process.GetCurrentProcess().Handle, Process.GetCurrentProcess().Handle, out var inheritableCurrentProcessHandle, 0, true, DuplicateOptions.SameAccess))
        {
            Log.Error("Failed to call DuplicateHandle: Win32 error code {0}", Marshal.GetLastWin32Error());
            return null;
        }

        return new ExistingProcess(inheritableCurrentProcessHandle);
    }
}