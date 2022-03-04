using System.Numerics;
using ImGuiNET;
using Serilog;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace XIVLauncher.Core
{
    class Program
    {
        private static Sdl2Window window;
        private static GraphicsDevice gd;
        private static CommandList cl;
        private static ImGuiBindings bindings;

        private static readonly Vector3 clearColor = new(0.1f, 0.1f, 0.1f);
        private static bool showImGuiDemoWindow = true;

        private static App app;
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

        private static void Main(string[] args)
        {
            storage = new Storage(APP_NAME);
            SetupLogging();

            app = new App(storage);

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
            bindings = new ImGuiBindings(gd, gd.MainSwapchain.Framebuffer.OutputDescription, window.Width, window.Height);

            Log.Debug("Veldrid OK!");

            // Main application loop
            while (window.Exists)
            {
                InputSnapshot snapshot = window.PumpEvents();

                if (!window.Exists)
                    break;

                bindings.Update(1f / 60f, snapshot);

                app.Draw();
                ImGui.ShowDemoWindow();

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