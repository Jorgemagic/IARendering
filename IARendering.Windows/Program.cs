using Evergine.Common.Graphics;
using Evergine.Common.Helpers;
using Evergine.Forms;
using Evergine.Framework.Graphics;
using Evergine.Framework.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Application = Evergine.Framework.Application;

namespace IARendering.Windows
{
    class Program
    {
        static uint width = 1280;
        static uint height = 720;
        static bool Windowed = true;
        static bool VSync = true;
        private static MyApplication application;
        private static Window window;

        [STAThread]
        static void Main(string[] args)
        {
            // Commandline parser
            if (args.Length > 0)
            {
                CmdParser cmd = new CmdParser()
                    .AddOption(new CmdParser.Option("-Width", (string v) => { return uint.TryParse(v, out width); }, "Set width size in pixels."))
                    .AddOption(new CmdParser.Option("-Height", (string v) => { return uint.TryParse(v, out height); }, "Set height size in pixels."))
                    .AddOption(new CmdParser.Option("-Vsync", (string _) => { VSync = true; return true; }, "Active vertical sync."))
                    .AddOption(new CmdParser.Option("-NoVsync", (string _) => { VSync = false; return true; }, "Desactive vertical sync."))
                    .AddOption(new CmdParser.Option("-Windowed", (string _) => { Windowed = true; return true; }, "Set application to run in windowed mode."))
                    .AddOption(new CmdParser.Option("-FullScreen", (string _) => { Windowed = false; return true; }, "Set application to run in fullscreen mode."));

                var success = cmd.Parse(args);

                if (!success)
                {
                    Console.Write(cmd.ErrorMessage);
                    return;
                }
            }
            else
            {
                var handle = Evergine.Forms.Win32Native.GetConsoleWindow();
                Evergine.Forms.Win32Native.ShowWindow(handle, false);
            }

            // Create app
            application = new MyApplication();

            // Create Services
            var windowsSystem = new Evergine.Forms.FormsWindowsSystem();
            application.Container.RegisterInstance<WindowsSystem>(windowsSystem);
            window = windowsSystem.CreateWindow("AI Rendering", width, height);

            ConfigureGraphicsContext(application, window);

            SetupDrag();

            // Creates XAudio device
            var xaudio = new global::Evergine.XAudio2.XAudioDevice();
            application.Container.RegisterInstance(xaudio);

            Stopwatch clockTimer = Stopwatch.StartNew();
            windowsSystem.Run(
            () =>
            {
                application.Initialize();
            },
            () =>
            {
                var gameTime = clockTimer.Elapsed;
                clockTimer.Restart();

                application.UpdateFrame(gameTime);
                application.DrawFrame(gameTime);
            });


            DisposeDrag();

            application.Dispose();
        }

        private static void SetupDrag()
        {
            var formsWindow = window as FormsWindow;
            var form = formsWindow.NativeWindow;
            form.AllowDrop = true;

            form.DragEnter += Form_DragEnter;
            form.DragDrop += Form_DragDrop;
        }

        private static void DisposeDrag()
        {
            var formsWindow = window as FormsWindow;
            var form = formsWindow.NativeWindow;

            form.DragEnter -= Form_DragEnter;
            form.DragDrop -= Form_DragDrop;
        }

        private static void Form_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }

            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0)
            {
                return;
            }

            var filePath = files[0];

            if (MyApplication.IsValidRuntimeAsset(filePath))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private static void Form_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
            try
            {
                if (e.Data == null)
                {
                    return;
                }

                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null || files.Length == 0)
                {
                    return;
                }

                var filePath = files[0];
                if (File.Exists(filePath))
                {
                    MyApplication.NewRuntimeAssetToLoad(filePath);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"DragDrop error: {ex}");
            }
        }

        private static void ConfigureGraphicsContext(Application application, Window window)
        {
            GraphicsContext graphicsContext = new global::Evergine.DirectX11.DX11GraphicsContext();
            graphicsContext.CreateDevice();
            SwapChainDescription swapChainDescription = new SwapChainDescription()
            {
                SurfaceInfo = window.SurfaceInfo,
                Width = window.Width,
                Height = window.Height,
                ColorTargetFormat = PixelFormat.R8G8B8A8_UNorm_SRgb,
                ColorTargetFlags = TextureFlags.RenderTarget | TextureFlags.ShaderResource,
                DepthStencilTargetFormat = PixelFormat.D24_UNorm_S8_UInt,
                DepthStencilTargetFlags = TextureFlags.DepthStencil,
                SampleCount = TextureSampleCount.None,
                IsWindowed = Windowed,
                RefreshRate = 60
            };
            var swapChain = graphicsContext.CreateSwapChain(swapChainDescription);
            swapChain.VerticalSync = VSync;

            var graphicsPresenter = application.Container.Resolve<GraphicsPresenter>();
            var firstDisplay = new Display(window, swapChain);
            graphicsPresenter.AddDisplay("DefaultDisplay", firstDisplay);

            application.Container.RegisterInstance(graphicsContext);
        }
    }
}

