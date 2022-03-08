using System.Numerics;
using Config.Net;
using ImGuiNET;
using XIVLauncher.Core.Style;
using Serilog;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Windows;
using XIVLauncher.Core.Configuration;
using XIVLauncher.Core.Configuration.Parsers;

namespace XIVLauncher.Core
{
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
        public static ISteam Steam { get; private set; }
        public static Launcher Launcher { get; private set; }

        private static readonly Vector3 clearColor = new(0.1f, 0.1f, 0.1f);
        private static bool showImGuiDemoWindow = true;

        private static LauncherApp launcherApp;
        private static Storage storage;

        private const string APP_NAME = "xlcore";

        private static void SetupLogging()
        {
            Log.Logger = new LoggerConfiguration()
                         .WriteTo.Async(a =>
                             a.File(Path.Combine(storage.GetFolder("logs").FullName, "launcher.log")))
#if DEBUG
                         .WriteTo.Console()
                         .WriteTo.Debug()
                         .MinimumLevel.Verbose()
#else
                         .MinimumLevel.Information()
#endif
                         .CreateLogger();
        }

        private static void LoadConfig(Storage storage)
        {
            Config = new ConfigurationBuilder<ILauncherConfig>()
                     .UseCommandLineArgs()
                     .UseIniFile(storage.GetFile("launcher.ini").FullName)
                     .UseTypeParser(new DirectoryInfoParser())
                     .Build();

            if (string.IsNullOrEmpty(Config.AcceptLanguage))
            {
                Config.AcceptLanguage = Util.GenerateAcceptLanguage();
            }

            Config.GamePath ??= storage.GetFolder("ffxiv");
            Config.ClientLanguage ??= ClientLanguage.English;
            Config.DpiAwareness ??= DpiAwareness.Unaware;

            Config.DalamudLoadMethod = !OperatingSystem.IsWindows() ? DalamudLoadMethod.DllInject : DalamudLoadMethod.EntryPoint;

            Config.GlobalScale ??= 1.0f;
        }

        public const int STEAM_APP_ID = 39210;

        private static void Main(string[] args)
        {
            storage = new Storage(APP_NAME);
            SetupLogging();
            LoadConfig(storage);

            Steam = new WindowsSteam();
            Steam.Initialize(STEAM_APP_ID);

            Launcher = new Launcher(Steam, new UniqueIdCache(), CommonSettings);

            Log.Debug("Creating veldrid devices...");

            // Create window, GraphicsDevice, and all resources necessary for the demo.
            VeldridStartup.CreateWindowAndGraphicsDevice(
                new WindowCreateInfo(50, 50, 1280, 720, WindowState.Normal, "XIVLauncher"),
                new GraphicsDeviceOptions(true, null, true, ResourceBindingModel.Improved, true, true),
                out window,
                out gd);

            window.Resized += () =>
            {
                gd.MainSwapchain.Resize((uint)window.Width, (uint)window.Height);
                bindings.WindowResized(window.Width, window.Height);
            };
            cl = gd.ResourceFactory.CreateCommandList();
            Log.Debug("Veldrid OK!");

            bindings = new ImGuiBindings(gd, gd.MainSwapchain.Framebuffer.OutputDescription, window.Width, window.Height);
            Log.Debug("ImGui OK!");

            StyleModelV1.DalamudStandard.Apply();
            ImGui.GetIO().FontGlobalScale = Config.GlobalScale ?? 1.0f;

            launcherApp = new LauncherApp(storage);

            // Main application loop
            while (window.Exists)
            {
                InputSnapshot snapshot = window.PumpEvents();

                if (!window.Exists)
                    break;

                bindings.Update(1f / 60f, snapshot);

                launcherApp.Draw();

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
    }
}