using System.Numerics;
using CheapLoc;
using Config.Net;
using ImGuiNET;
using XIVLauncher.Core.Style;
using Serilog;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Windows;
using XIVLauncher.Common.Unix;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Core.Accounts.Secrets;
using XIVLauncher.Core.Accounts.Secrets.Providers;
using XIVLauncher.Core.Components.LoadingPage;
using XIVLauncher.Core.Configuration;
using XIVLauncher.Core.Configuration.Parsers;

namespace XIVLauncher.Core;

class Program
{
    private static Sdl2Window window;
    private static CommandList cl;
    private static GraphicsDevice gd;
    private static ImGuiBindings bindings;

    public static GraphicsDevice GraphicsDevice => gd;
    public static ImGuiBindings ImGuiBindings => bindings;
    public static ILauncherConfig Config { get; private set; }
    public static CommonSettings CommonSettings => new(Config);
    public static ISteam? Steam { get; private set; }
    public static DalamudUpdater DalamudUpdater { get; private set; }
    public static DalamudOverlayInfoProxy DalamudLoadInfo { get; private set; }
    public static CompatibilityTools CompatibilityTools { get; private set; }
    public static ISecretProvider Secrets { get; private set; }

    private static readonly Vector3 clearColor = new(0.1f, 0.1f, 0.1f);
    private static bool showImGuiDemoWindow = true;

    private static LauncherApp launcherApp;
    private static Storage storage;
    public static DirectoryInfo DotnetRuntime => storage.GetFolder("runtime");

    private const string APP_NAME = "xlcore";

    private static uint invalidationFrames = 0;
    private static Vector2 lastMousePosition;

    private static bool lastFrameTextInput = false;
    public static bool DoAutoSoftwareKbd { get; set; } = true;

    public static void Invalidate(uint frames = 100)
    {
        invalidationFrames = frames;
    }

    private static void SetupLogging()
    {
        Log.Logger = new LoggerConfiguration()
                     .WriteTo.Async(a =>
                         a.File(Path.Combine(storage.GetFolder("logs").FullName, "launcher.log")))
                     .WriteTo.Console()
                     .WriteTo.Debug()
                     .MinimumLevel.Verbose()
                     .CreateLogger();
    }

    private static void LoadConfig(Storage storage)
    {
        Config = new ConfigurationBuilder<ILauncherConfig>()
                 .UseCommandLineArgs()
                 .UseIniFile(storage.GetFile("launcher.ini").FullName)
                 .UseTypeParser(new DirectoryInfoParser())
                 .UseTypeParser(new AddonListParser())
                 .Build();

        if (string.IsNullOrEmpty(Config.AcceptLanguage))
        {
            Config.AcceptLanguage = Util.GenerateAcceptLanguage();
        }

        Config.GamePath ??= storage.GetFolder("ffxiv");
        Config.GameConfigPath ??= storage.GetFolder("ffxivConfig");
        Config.ClientLanguage ??= ClientLanguage.English;
        Config.DpiAwareness ??= DpiAwareness.Unaware;
        Config.IsAutologin ??= false;

        Config.IsDx11 ??= true;
        Config.IsEncryptArgs ??= true;
        Config.IsFt ??= false;
        Config.IsOtpServer ??= false;

        Config.PatchPath ??= storage.GetFolder("patch");
        Config.PatchAcquisitionMethod ??= AcquisitionMethod.Aria;

        Config.DalamudEnabled ??= true;
        Config.DalamudLoadMethod = !OperatingSystem.IsWindows() ? DalamudLoadMethod.DllInject : DalamudLoadMethod.EntryPoint;

        Config.GlobalScale ??= 1.0f;

        Config.GameModeEnabled ??= false;
        Config.DxvkAsyncEnabled ??= true;
        Config.ESyncEnabled ??= true;
        Config.FSyncEnabled ??= false;

        Config.WineStartupType ??= WineStartupType.Managed;
        Config.WineBinaryPath ??= "/usr/bin";
        Config.WineDebugVars ??= "-all";
    }

    public const int STEAM_APP_ID = 39210;

    private static void Main(string[] args)
    {
        storage = new Storage(APP_NAME);
        SetupLogging();
        LoadConfig(storage);

        Secrets = GetSecretProvider(storage);

        Loc.SetupWithFallbacks();

        try
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    Steam = new WindowsSteam();
                    break;

                case PlatformID.Unix:
                    Steam = new UnixSteam();
                    break;

                default:
                    throw new PlatformNotSupportedException();
            }

            Steam.Initialize(STEAM_APP_ID);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Steam couldn't load");
        }

        DalamudLoadInfo = new DalamudOverlayInfoProxy();
        DalamudUpdater = new DalamudUpdater(storage.GetFolder("dalamud"), storage.GetFolder("runtime"), storage.GetFolder("dalamudAssets"), storage.Root, null)
        {
            Overlay = DalamudLoadInfo
        };
        DalamudUpdater.Run();

        UpdateCompatibilityTools();

        Log.Debug("Creating Veldrid devices...");

#if DEBUG
        var version = AppUtil.GetGitHash();
#else
        var version = $"{AppUtil.GetAssemblyVersion()} ({AppUtil.GetGitHash()})";
#endif

        // Create window, GraphicsDevice, and all resources necessary for the demo.
        VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(50, 50, 1280, 720, WindowState.Normal, $"XIVLauncher {version}"),
            new GraphicsDeviceOptions(false, null, true, ResourceBindingModel.Improved, true, true),
            out window,
            out gd);

        window.Resized += () =>
        {
            gd.MainSwapchain.Resize((uint)window.Width, (uint)window.Height);
            bindings.WindowResized(window.Width, window.Height);
            Invalidate();
        };
        cl = gd.ResourceFactory.CreateCommandList();
        Log.Debug("Veldrid OK!");

        bindings = new ImGuiBindings(gd, gd.MainSwapchain.Framebuffer.OutputDescription, window.Width, window.Height);
        Log.Debug("ImGui OK!");

        StyleModelV1.DalamudStandard.Apply();
        ImGui.GetIO().FontGlobalScale = Config.GlobalScale ?? 1.0f;

        launcherApp = new LauncherApp(storage);

        Invalidate(20);

        // Main application loop
        while (window.Exists)
        {
            Thread.Sleep(50);

            InputSnapshot snapshot = window.PumpEvents();

            if (!window.Exists)
                break;

            var overlayNeedsPresent = false;

            if (Steam != null && Steam.IsValid)
                overlayNeedsPresent = Steam.BOverlayNeedsPresent;

            if (!snapshot.KeyEvents.Any() && !snapshot.MouseEvents.Any() && !snapshot.KeyCharPresses.Any() && invalidationFrames == 0 && lastMousePosition == snapshot.MousePosition
                && !overlayNeedsPresent)
            {
                continue;
            }

            if (invalidationFrames == 0)
            {
                invalidationFrames = 10;
            }

            if (invalidationFrames > 0)
            {
                invalidationFrames--;
            }

            lastMousePosition = snapshot.MousePosition;

            bindings.Update(1f / 60f, snapshot);

            launcherApp.Draw();

            var wantTextInput = ImGui.GetIO().WantTextInput;

            if (wantTextInput && !lastFrameTextInput && DoAutoSoftwareKbd && Steam.IsRunningOnSteamDeck())
            {
                Steam.ShowFloatingGamepadTextInput(ISteam.EFloatingGamepadTextInputMode.EnterDismisses, 0, 0, 100, 100);
                Log.Verbose("Show kbd");
                lastFrameTextInput = true;
            }
            else if (wantTextInput)
            {
                lastFrameTextInput = true;
            }
            else
            {
                lastFrameTextInput = false;
            }

            cl.Begin();
            cl.SetFramebuffer(gd.MainSwapchain.Framebuffer);
            cl.ClearColorTarget(0, new RgbaFloat(clearColor.X, clearColor.Y, clearColor.Z, 1f));
            bindings.Render(gd, cl);
            cl.End();
            gd.SubmitCommands(cl);
            gd.SwapBuffers(gd.MainSwapchain);
        }

        // Clean up Veldrid resources
        gd.WaitForIdle();
        bindings.Dispose();
        cl.Dispose();
        gd.Dispose();
    }

    public static void UpdateCompatibilityTools()
    {
        var wineLogFile = new FileInfo(Path.Combine(storage.GetFolder("logs").FullName, "wine.log"));
        var winePrefix = storage.GetFolder("wineprefix");
        var wineSettings = new WineSettings(Config.WineStartupType, Config.WineBinaryPath, Config.WineDebugVars, wineLogFile, winePrefix, Config.ESyncEnabled, Config.FSyncEnabled);
        var toolsFolder = storage.GetFolder("compatibilitytool");
        CompatibilityTools = new CompatibilityTools(wineSettings, Config.DxvkHudType, Config.GameModeEnabled, Config.DxvkAsyncEnabled, toolsFolder);
    }

    public static void ShowWindow()
    {
        window.Visible = true;
    }

    public static void HideWindow()
    {
        window.Visible = false;
    }

    private static ISecretProvider GetSecretProvider(Storage storage)
    {
        var secretsFilePath = Environment.GetEnvironmentVariable("XL_SECRETS_FILE_PATH") ?? "secrets.json";

        var envVar = Environment.GetEnvironmentVariable("XL_SECRET_PROVIDER") ?? "KEYRING";
        envVar = envVar.ToUpper();

        switch (envVar)
        {
            case "FILE":
                return new FileSecretProvider(storage.GetFile(secretsFilePath));

            case "KEYRING":
            {
                var keyChain = new KeychainSecretProvider();

                if (!keyChain.IsAvailable)
                {
                    Log.Error("An org.freedesktop.secrets provider is not available - no secrets will be stored");
                    return new DummySecretProvider();
                }

                return keyChain;
            }

            case "NONE":
                return new DummySecretProvider();

            default:
                throw new ArgumentException($"Invalid secret provider: {envVar}");
        }
    }
}