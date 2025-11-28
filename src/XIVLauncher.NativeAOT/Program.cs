using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarshalUTF8Extensions;
using Serilog;
using Serilog.Events;
using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.Game.Patch.PatchList;
using XIVLauncher.Common.Patching;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Unix;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Common.Util;
using XIVLauncher.Common.Windows;
using XIVLauncher.NativeAOT.Configuration;
using XIVLauncher.NativeAOT.Support;
using XIVLauncher.PlatformAbstractions;
using static XIVLauncher.Common.Game.Launcher;
using static XIVLauncher.Common.Unix.Compatibility.Dxvk;

namespace XIVLauncher.NativeAOT;

[JsonSerializable(typeof(LoginResult))]
[JsonSerializable(typeof(RepairProgress))]
[JsonSerializable(typeof(DalamudConsoleOutput))]
[JsonSerializable(typeof(PatchListEntry[]))]
internal partial class ProgramJsonContext : JsonSerializerContext
{
}

public class Program
{
    public static string? AppName { get; private set; }
    public static Storage? Storage { get; private set; }
    public static LauncherConfig? Config { get; private set; }
    public static CommonSettings? CommonSettings => CommonSettings.Instance;
    public static DirectoryInfo DotnetRuntime => Storage!.GetFolder("runtime");
    public static string? FrontierUrl { get; private set; }
    public static ISteam? Steam { get; private set; }
    public static DalamudUpdater? DalamudUpdater { get; private set; }
    public static CompatibilityTools? CompatibilityTools { get; private set; }
    public static Launcher? Launcher { get; set; }
    public static CommonUniqueIdCache? UniqueIdCache;

    private const uint STEAM_APP_ID = 39210;
    private const uint STEAM_APP_ID_FT = 312060;

    // Temporary disable Dalamud auto-update due to compatibility issues
    private static bool isDalamudAutoUpdateDisabled = true;

    [UnmanagedCallersOnly(EntryPoint = "initXL")]
    public static void Init(nint appName, nint storagePath, bool verboseLogging, nint frontierUrl)
    {
        // TODO: TC伺服器的初始設定
        AppName = Marshal.PtrToStringUTF8(appName)!;
        Storage = new Storage(AppName, Marshal.PtrToStringUTF8(storagePath)!);
        FrontierUrl = Marshal.PtrToStringUTF8(frontierUrl)!;

        var logLevel = verboseLogging ? LogEventLevel.Verbose : LogEventLevel.Information;
        Log.Logger = new LoggerConfiguration()
                     .WriteTo.Async(a =>
                                        a.File(Path.Combine(Storage.GetFolder("logs").FullName, "launcher.log")))
                     .WriteTo.Console()
                     .MinimumLevel.Is(logLevel)
                     .CreateLogger();

        Log.Information("========================================================");
        Log.Information("Starting a session({AppName})", AppName);
        Task.Run(Troubleshooting.LogTroubleshooting);

        try
        {
            Steam = Environment.OSVersion.Platform switch
            {
                PlatformID.Win32NT => new WindowsSteam(),
                PlatformID.Unix => new UnixSteam(),
                _ => throw new PlatformNotSupportedException()
            };

            try
            {
                var appId = Config!.IsFt == true ? STEAM_APP_ID_FT : STEAM_APP_ID;
                Steam.Initialize(appId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Couldn't init Steam with game AppIds");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Steam couldn't load");
            Troubleshooting.LogException(ex, "Steam couldn't load");
        }

        var dalamudLoadInfo = new DalamudOverlayInfoProxy();
        DalamudUpdater = new DalamudUpdater(Storage.GetFolder("dalamud"), Storage.GetFolder("runtime"), Storage.GetFolder("dalamudAssets"), null, null)
        {
            Overlay = dalamudLoadInfo
        };
        if (!isDalamudAutoUpdateDisabled)
        {
            DalamudUpdater.Run(null, null, true);
        }

        UniqueIdCache = new CommonUniqueIdCache(Storage.GetFile("uidCache.json"));
        Launcher = new Launcher(steam: null,UniqueIdCache, CommonSettings, FrontierUrl);
        LaunchServices.EnsureLauncherAffinity((XIVLauncher.NativeAOT.Configuration.License)Config!.License!);
    }

    [UnmanagedCallersOnly(EntryPoint = "addEnvironmentVariable")]
    public static void AddEnvironmentVariable(nint key, nint value)
    {
        var kvp = new KeyValuePair<string, string>(Marshal.PtrToStringUTF8(key)!, Marshal.PtrToStringUTF8(value)!);
        Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
    }

    [UnmanagedCallersOnly(EntryPoint = "createCompatToolsInstance")]
    public static void CreateCompatToolsInstance(nint winePath, nint wineDebugVars, bool esync)
    {
        var wineLogFile = new FileInfo(Path.Combine(Storage!.GetFolder("logs").FullName, "wine.log"));
        var winePrefix = Storage.GetFolder("wineprefix");
        var wineSettings = new WineSettings(WineStartupType.Custom, Marshal.PtrToStringUTF8(winePath), Marshal.PtrToStringUTF8(wineDebugVars), wineLogFile, winePrefix, esync, false);
        var toolsFolder = Storage.GetFolder("compatibilitytool");
        CompatibilityTools = new CompatibilityTools(wineSettings, DxvkHudType.None, false, true, toolsFolder);
    }

    [UnmanagedCallersOnly(EntryPoint = "ensurePrefix")]
    public static void EnsurePrefix()
    {
        try
        {
            CompatibilityTools?.EnsurePrefix();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Couldn't ensure Prefix");
            Troubleshooting.LogException(ex, "Couldn't ensure Prefix");
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "generateAcceptLanguage")]
    public static nint GenerateAcceptLanguage(int seed)
    {
        //TODO: TC伺服器的Accept-Language限制
        // Needs to be freed by the caller
        return MarshalUtf8.StringToHGlobal(ApiHelpers.GenerateAcceptLanguage(seed));
    }

    [UnmanagedCallersOnly(EntryPoint = "loadConfig")]
    public static void LoadConfig(nint acceptLanguage, nint gamePath, nint gameConfigPath, byte clientLanguage, bool isEncryptArgs, bool isFt, byte license, nint patchPath,
                                  byte patchAcquisitionMethod, long patchSpeedLimit, byte dalamudLoadMethod, int dalamudLoadDelay, bool isAutoLogin, bool isHiDpi)
    {
        // TODO: TC伺服器的初始設定
        Config = new LauncherConfig
        {
            AcceptLanguage = Marshal.PtrToStringUTF8(acceptLanguage),

            GamePath = new DirectoryInfo(Marshal.PtrToStringUTF8(gamePath)!),
            GameConfigPath = new DirectoryInfo(Marshal.PtrToStringUTF8(gameConfigPath)!),
            ClientLanguage = (ClientLanguage)clientLanguage,

            IsEncryptArgs = isEncryptArgs,
            License = (License)license,
            IsFt = isFt,
            IsAutoLogin = isAutoLogin,
            DpiAwareness = isHiDpi ? DpiAwareness.Aware : DpiAwareness.Unaware,

            PatchPath = new DirectoryInfo(Marshal.PtrToStringUTF8(patchPath)!),
            PatchAcquisitionMethod = (AcquisitionMethod)patchAcquisitionMethod,
            PatchSpeedLimit = patchSpeedLimit,

            DalamudLoadMethod = (DalamudLoadMethod)dalamudLoadMethod,
            DalamudLoadDelay = dalamudLoadDelay
        };
    }

    [UnmanagedCallersOnly(EntryPoint = "fakeLogin")]
    public static void FakeLogin()
    {
        // TODO: TC伺服器遊戲啟動確認 - 遊戲啟動模擬
        LaunchServices.EnsureLauncherAffinity((XIVLauncher.NativeAOT.Configuration.License)Config!.License!);
        IGameRunner gameRunner;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            gameRunner = new WindowsGameRunner(null, false);
        else
            gameRunner = new UnixGameRunner(Program.CompatibilityTools, null, false);

        Launcher!.LaunchGame(gameRunner, "0", 1, 2, false, "", Program.Config!.GamePath!, ClientLanguage.Japanese, true, DpiAwareness.Unaware);
    }

    [UnmanagedCallersOnly(EntryPoint = "tryLoginToGame")]
    public static nint TryLoginToGame(nint username, nint password, nint otp, nint recaptchaToken, bool repair)
    {
        //TODO: 使用RecapchaToken登入
        try
        {
            return MarshalUtf8.StringToHGlobal(LaunchServices.TryLoginToGame(Marshal.PtrToStringUTF8(username)!, Marshal.PtrToStringUTF8(password)!, Marshal.PtrToStringUTF8(otp)!, Marshal.PtrToStringUTF8(recaptchaToken)!, repair).Result);
        }
        catch (AggregateException ex)
        {
            var lastException = "";

            foreach (var iex in ex.InnerExceptions)
            {
                Log.Error(iex, "An error during login occured");
                Troubleshooting.LogException(ex, "An error during login occured");
                lastException = iex.Message;
            }

            return MarshalUtf8.StringToHGlobal(lastException);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error during login occured");
            Troubleshooting.LogException(ex, "An error during login occured");
            return MarshalUtf8.StringToHGlobal(ex.Message);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "getUserAgent")]
    public static nint GetUserAgent()
    {
        // TODO: TC伺服器的User-Agent
        // return MarshalUtf8.StringToHGlobal(Launcher!.GenerateUserAgent());

        return MarshalUtf8.StringToHGlobal("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/");
    }

    [UnmanagedCallersOnly(EntryPoint = "getPatcherUserAgent")]
    public static nint GetPatcherUserAgent()
    {
        // TODO: TC伺服器的Patcher User-Agent
        return MarshalUtf8.StringToHGlobal(Constants.PatcherUserAgent);
    }

    [UnmanagedCallersOnly(EntryPoint = "getBootPatches")]
    public static nint GetBootPatches()
    {
        // TC伺服器的Boot更新 maybe skip?
        return MarshalUtf8.StringToHGlobal(LaunchServices.GetBootPatches().Result);
    }

    [UnmanagedCallersOnly(EntryPoint = "installPatch")]
    public static nint InstallPatch(nint patch, nint repo)
    {
        try
        {
            RemotePatchInstaller.InstallPatch(Marshal.PtrToStringUTF8(patch)!, Marshal.PtrToStringUTF8(repo)!);
            Log.Information("OK");
            return MarshalUtf8.StringToHGlobal("OK");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Patch installation failed");
            Troubleshooting.LogException(ex, "Patch installation failed");
            return MarshalUtf8.StringToHGlobal(ex.Message);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "checkPatchValidity")]
    public static bool CheckPatchValidity(nint path, long patchLength, long hashBlockSize, nint hashType, nint hashes)
    {
        try
        {
            var pathInfo = new FileInfo(Marshal.PtrToStringUTF8(path)!);
            var splitHashes = Marshal.PtrToStringUTF8(hashes)!.Split(',');
            return LaunchServices.CheckPatchValidity(pathInfo, patchLength, hashBlockSize, Marshal.PtrToStringUTF8(hashType)!, splitHashes);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Patch verification failed");
            Troubleshooting.LogException(ex, "Patch verification failed");
            return false;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "repairGame")]
    public static nint RepairGame(nint loginResultJson)
    {
        try
        {
            var loginResult = JsonSerializer.Deserialize(Marshal.PtrToStringUTF8(loginResultJson)!, ProgramJsonContext.Default.LoginResult);
            return MarshalUtf8.StringToHGlobal(LaunchServices.RepairGame(loginResult!).Result);
        }
        catch (AggregateException ex)
        {
            var lastException = "";

            foreach (var iex in ex.InnerExceptions)
            {
                Log.Error(iex, "An error during game repair has occured");
                Troubleshooting.LogException(ex, "An error during game repair has occured");
                lastException = iex.Message;
            }

            return MarshalUtf8.StringToHGlobal(lastException);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error during game repair has occured");
            Troubleshooting.LogException(ex, "An error during game repair has occured");
            return MarshalUtf8.StringToHGlobal(ex.Message);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "queryRepairProgress")]
    public static nint QueryRepairProgress()
    {
        try
        {
            var progress = new RepairProgress(LaunchServices.CurrentPatchVerifier);
            return MarshalUtf8.StringToHGlobal(JsonSerializer.Serialize(progress, ProgramJsonContext.Default.RepairProgress));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Querying Repair Progress Info failed");
            Troubleshooting.LogException(ex, "Querying Repair Progress Info failed");
            return MarshalUtf8.StringToHGlobal(JsonSerializer.Serialize(new RepairProgress(), ProgramJsonContext.Default.RepairProgress));
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "getDalamudInstallState")]
    public static byte GetDalamudInstallState()
    {
        try
        {
            return (byte)LaunchServices.GetDalamudInstallState();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error getting the dalamud state has occured");
            Troubleshooting.LogException(ex, "An error getting the dalamud state has occured");
            return (byte)DalamudLauncher.DalamudInstallState.OutOfDate;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "startGame")]
    public static nint StartGame(nint loginResultJson, bool dalamudOk)
    {
        // TODO: TC伺服器遊戲啟動
        try
        {
            var loginResult = JsonSerializer.Deserialize(Marshal.PtrToStringUTF8(loginResultJson)!, ProgramJsonContext.Default.LoginResult);
            var process = LaunchServices.StartGameAndAddon(loginResult!, dalamudOk);
            var ret = new DalamudConsoleOutput
            {
                Handle = (long)process.Handle,
                Pid = process.Id
            };
            return MarshalUtf8.StringToHGlobal(JsonSerializer.Serialize(ret, ProgramJsonContext.Default.DalamudConsoleOutput));
        }
        catch (AggregateException ex)
        {
            foreach (var iex in ex.InnerExceptions)
            {
                Log.Error(iex, "An error during game startup has occured");
                Troubleshooting.LogException(ex, "An error during game startup has occured");
            }

            return MarshalUtf8.StringToHGlobal("An error during game startup has occured");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error during game startup has occured");
            Troubleshooting.LogException(ex, "An error during game startup has occured");
            return MarshalUtf8.StringToHGlobal("An error during game startup has occured");
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "getExitCode")]
    public static int GetExitCode(int pid)
    {
        try
        {
            return LaunchServices.GetExitCode(pid).Result;
        }
        catch (AggregateException ex)
        {
            foreach (var iex in ex.InnerExceptions)
            {
                Log.Error(iex, "An error occured getting the exit code of pid {Pid}", pid);
                Troubleshooting.LogException(iex, "An error occured getting the exit code");
            }

            return -42069;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occured getting the exit code of pid {Pid}", pid);
            Troubleshooting.LogException(ex, "An error occured getting the exit code");
            return -69;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "writeLogLine")]
    public static void WriteLogLine(byte logLevel, nint message)
    {
        Log.Write((Serilog.Events.LogEventLevel)logLevel, Marshal.PtrToStringUTF8(message)!);
    }

    [UnmanagedCallersOnly(EntryPoint = "runInPrefix")]
    public static void RunInPrefix(nint command, bool blocking, bool wineD3D)
    {
        try
        {
            var commandStr = Marshal.PtrToStringUTF8(command)!;
            var process = CompatibilityTools!.RunInPrefix(commandStr, wineD3D: wineD3D);

            if (blocking)
            {
                process.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An internal wine error occured");
            Troubleshooting.LogException(ex, "An internal wine error occured");
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "addRegistryKey")]
    public static void AddRegistryKey(nint key, nint value, nint data)
    {
        try
        {
            CompatibilityTools!.AddRegistryKey(Marshal.PtrToStringUTF8(key)!, Marshal.PtrToStringUTF8(value)!, Marshal.PtrToStringUTF8(data)!);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occured adding the registry key");
            Troubleshooting.LogException(ex, "An error occured adding the registry key");
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "getProcessIds")]
    public static nint GetProcessIds(nint executableName)
    {
        try
        {
            var pids = CompatibilityTools!.GetProcessIds(Marshal.PtrToStringUTF8(executableName)!);
            return MarshalUtf8.StringToHGlobal(string.Join(" ", pids));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occured getting the process ids");
            Troubleshooting.LogException(ex, "An error occured getting the process ids");
            return MarshalUtf8.StringToHGlobal("");
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "getUnixProcessId")]
    public static int GetUnixProcessId(int winePid)
    {
        try
        {
            return CompatibilityTools!.GetUnixProcessId(winePid);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occured getting the unix process id");
            Troubleshooting.LogException(ex, "An error occured getting the unix process id");
            return 0;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "killWine")]
    public static void KillWine()
    {
        try
        {
            CompatibilityTools!.Kill();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occured terminating wine");
            Troubleshooting.LogException(ex, "An error occured terminating wine");
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "checkRosetta")]
    public static bool CheckRosetta()
    {
        try
        {
            var proc = CompatibilityTools!.RunInPrefix("--version")!;
            proc.WaitForExit();
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Rosetta does not appear to be installed");
            return false;
        }
    }
}
