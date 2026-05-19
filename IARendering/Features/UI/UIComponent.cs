using Evergine.Bindings.Imgui;
using Evergine.Common.Attributes;
using Evergine.Common.Graphics;
using Evergine.Framework;
using Evergine.Framework.Graphics;
using Evergine.Framework.Services;
using Evergine.Framework.Threading;
using Evergine.Mathematics;
using Evergine.UI;
using IARendering.Features.Screenshots;
using IARendering.Features.RuntimeAssets.Loaders;
using IARendering.Features.StableDiffusion;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace IARendering.Features.UI
{
    public class UIComponent : Behavior
    {
        private const string AiRenderPrompt = "convert this 3D viewport render into a photorealistic architectural visualization, preserve geometry, perspective, object placement and scale, replace flat 3D materials with realistic PBR materials, add realistic global illumination, contact shadows, fabric texture, wood grain, wall paint texture, realistic plant leaves, natural window light, physically accurate indoor lighting, high quality photorealistic render";

        public enum UIMode
        {
            Init = 0,
            Loading,
            Loaded
        }

        [BindSceneManager]
        private ImGuiManager imGuiManager;

        [BindService]
        private Clock clock;

        private Texture evergineLogoTex;
        private ImTextureRef evergineLogo;
        private StableDiffusionCli stableDiffusionCli;
        private bool isAiGenerationInProgress;
        private string aiStatusText = "Load a model to enable AI render generation.";
        private string lastViewportCapturePath;
        private string lastAiRenderPath;

        public string Text = string.Empty;

        [IgnoreEvergine]
        [DontRenderProperty]
        public UIMode CurrentMode { get; set; } = UIMode.Init;

        protected override bool OnAttached()
        {
            this.evergineLogoTex = this.Managers.AssetSceneManager.Load<Texture>(EvergineContent.Textures.EvergineLogo_png);
            this.evergineLogo = this.imGuiManager.CreateImGuiBinding(this.evergineLogoTex);
            this.stableDiffusionCli = new StableDiffusionCli();


            return base.OnAttached();
        }

        protected unsafe override void Update(TimeSpan gameTime)
        {
            var io = ImguiNative.igGetIO_Nil();

            bool open = true;
            switch (this.CurrentMode)
            {
                case UIMode.Init:
                    {
                        var bgColor = new Color(49, 49, 49);
                        ImguiNative.igPushStyleColor_Vec4(ImGuiCol.WindowBg, bgColor.ToVector4());
                        ImguiNative.igSetNextWindowPos(Vector2.Zero, ImGuiCond.Always, Vector2.Zero);
                        ImguiNative.igSetNextWindowSize(io->DisplaySize, ImGuiCond.Always);

                        ImguiNative.igBegin("MainWindow", open.Pointer(), ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar);


                        var textureSize = new Vector2(evergineLogoTex.Description.Width, evergineLogoTex.Description.Height);
                        var position = new Vector2(
                            (io->DisplaySize.X - textureSize.X) * 0.5f,
                            (io->DisplaySize.Y - textureSize.Y) * 0.3f);

                        ImguiNative.igSetCursorPos(position);
                        ImguiNative.igImage(
                            this.evergineLogo,
                            textureSize,
                            uv0: Vector2.Zero,
                            uv1: Vector2.One);


                        var size = ImguiNative.igCalcTextSize(this.Text, null, false, -1.0f);

                        position = new Vector2(
                            (io->DisplaySize.X - size.X) * 0.5f,
                            (io->DisplaySize.Y - size.Y) * 0.5f);


                        ImguiNative.igSetCursorPos(position);
                        ImguiNative.igTextWrapped(this.Text);

                        ImguiNative.igEnd();

                        ImguiNative.igPopStyleColor(1);
                    }

                    break;
                case UIMode.Loading:
                    {
                        Vector2 loadingSize = new Vector2(300, 40);
                        var windowPosition = new Vector2(
                            (io->DisplaySize.X - loadingSize.X) * 0.5f,
                            (io->DisplaySize.Y - loadingSize.Y) * 0.5f);


                        var loadingBgColor = new Color(0, 0, 0, 0.4f);
                        ImguiNative.igPushStyleColor_Vec4(ImGuiCol.WindowBg, loadingBgColor.ToVector4());
                        ImguiNative.igSetNextWindowPos(windowPosition, ImGuiCond.Always, Vector2.Zero);
                        ImguiNative.igSetNextWindowSize(loadingSize, ImGuiCond.Always);

                        ImguiNative.igBegin("MainWindow", open.Pointer(), ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar);


                        var loadingPosition = new Vector2(
                            (io->DisplaySize.X) * 0.5f,
                            (io->DisplaySize.Y) * 0.3f);

                        //ImguiNative.igSetCursorPos(loadingPosition);
                        ImguiNative.igProgressBar((float)(clock.TotalTime.TotalSeconds * -0.4), new Vector2(loadingSize.X - 20, 0), "Loading...");

                        ImguiNative.igEnd();

                        ImguiNative.igPopStyleColor(1);
                    }

                    break;
                case UIMode.Loaded:
                    {
                        ImguiNative.igPushStyleColor_Vec4(ImGuiCol.WindowBg, Vector4.Zero);
                        ImguiNative.igSetNextWindowPos(Vector2.Zero, ImGuiCond.Always, Vector2.Zero);
                        ImguiNative.igSetNextWindowSize(io->DisplaySize, ImGuiCond.Always);

                        ImguiNative.igBegin("MainWindow", open.Pointer(), ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar);


                        var size = ImguiNative.igCalcTextSize(this.Text, null, false, -1.0f);

                        var position = new Vector2(
                            20,
                            (io->DisplaySize.Y - size.Y) - 20);


                        ImguiNative.igSetCursorPos(position);
                        ImguiNative.igTextWrapped(this.Text);

                        ImguiNative.igEnd();

                        ImguiNative.igPopStyleColor(1);
                    }
                    break;
            }

            this.DrawAiRenderPanel();
        }

        internal void SetSuportedFiles(Dictionary<RuntimeLoaderType, string[]> extensionsByType)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Drag and drop the following files:");
            foreach (var kvp in extensionsByType)
            {
                var description = GetDescription(kvp.Key) ?? kvp.Key.ToString();
                var extensions = string.Join(", ", kvp.Value);
                sb.AppendLine($"· {description}: {extensions}");
            }
            this.Text = sb.ToString();
        }

        private unsafe void DrawAiRenderPanel()
        {
            var io = ImguiNative.igGetIO_Nil();
            float panelWidth = 360;
            float panelHeight = 220;
            float margin = 16;

            var panelPosition = new Vector2(io->DisplaySize.X - panelWidth - margin, margin);
            var panelSize = new Vector2(panelWidth, panelHeight);
            var flags =
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoSavedSettings;

            ImguiNative.igSetNextWindowPos(panelPosition, ImGuiCond.Always, Vector2.Zero);
            ImguiNative.igSetNextWindowSize(panelSize, ImGuiCond.Always);

            if (ImguiNative.igBegin("AI Render", null, flags))
            {
                ImguiNative.igTextWrapped("Generate a photorealistic AI render from the current viewport.");
                ImguiNative.igTextWrapped($"Runtime asset state: {this.CurrentMode}");

                if (this.CurrentMode == UIMode.Loaded)
                {
                    if (!this.isAiGenerationInProgress)
                    {
                        if (ImguiNative.igButton("Generate AI Render", new Vector2(panelWidth - 32, 0)))
                        {
                            this.StartAiRenderGeneration();
                        }
                    }
                    else
                    {
                        ImguiNative.igTextWrapped("AI render generation is running...");
                    }
                }
                else
                {
                    ImguiNative.igTextWrapped("Load a runtime asset first to capture the viewport and send it to Stable Diffusion.");
                }

                ImguiNative.igTextWrapped($"Status: {this.aiStatusText}");

                if (!string.IsNullOrWhiteSpace(this.lastViewportCapturePath))
                {
                    ImguiNative.igTextWrapped($"Last capture: {Path.GetFileName(this.lastViewportCapturePath)}");
                }

                if (!string.IsNullOrWhiteSpace(this.lastAiRenderPath))
                {
                    ImguiNative.igTextWrapped($"Last output: {Path.GetFileName(this.lastAiRenderPath)}");
                }
            }

            ImguiNative.igEnd();
        }

        private void StartAiRenderGeneration()
        {
            if (this.isAiGenerationInProgress)
            {
                return;
            }

            var activeCamera = this.Managers.RenderManager.ActiveCamera3D;
            var display = activeCamera?.Display;
            if (display == null)
            {
                this.aiStatusText = "Unable to capture the viewport: active display not found.";
                return;
            }

            try
            {
                var stableDiffusionDirectory = this.stableDiffusionCli.GetStableDiffusionDirectory();
                var capturesDirectory = Path.Combine(stableDiffusionDirectory, "captures");
                var resultsDirectory = Path.Combine(stableDiffusionDirectory, "results");
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                this.lastViewportCapturePath = Path.Combine(capturesDirectory, $"viewport_{timestamp}.png");
                this.lastAiRenderPath = Path.Combine(resultsDirectory, $"ai_render_{timestamp}.png");

                var graphicsContext = Application.Current.Container.Resolve<GraphicsContext>();
                display.SaveDisplayToFile(graphicsContext, this.lastViewportCapturePath);

                var options = this.stableDiffusionCli.CreateDefaultFluxKleinOptions(
                    this.lastViewportCapturePath,
                    this.lastAiRenderPath,
                    AiRenderPrompt);

                this.isAiGenerationInProgress = true;
                this.aiStatusText = "Viewport captured. Running Stable Diffusion...";

                _ = this.RunAiRenderGenerationAsync(options);
            }
            catch (Exception ex)
            {
                this.isAiGenerationInProgress = false;
                this.aiStatusText = $"Capture failed: {ex.Message}";
            }
        }

        private async Task RunAiRenderGenerationAsync(StableDiffusionCliOptions options)
        {
            try
            {
                var result = await this.stableDiffusionCli.RunAsync(options);

                await EvergineForegroundTask.Run(() =>
                {
                    this.isAiGenerationInProgress = false;

                    if (result.Success)
                    {
                        this.lastAiRenderPath = result.OutputImagePath;
                        this.aiStatusText = $"AI render generated successfully: {Path.GetFileName(result.OutputImagePath)}";
                    }
                    else
                    {
                        var error = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
                        this.aiStatusText = $"Stable Diffusion failed ({result.ExitCode}): {TrimStatus(error)}";
                    }
                });
            }
            catch (Exception ex)
            {
                await EvergineForegroundTask.Run(() =>
                {
                    this.isAiGenerationInProgress = false;
                    this.aiStatusText = $"AI render failed: {ex.Message}";
                });
            }
        }

        private static string TrimStatus(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "No output returned by sd-cli.";
            }

            text = text.Replace(Environment.NewLine, " ").Trim();
            const int maxLength = 180;
            if (text.Length > maxLength)
            {
                return text.Substring(0, maxLength) + "...";
            }

            return text;
        }



        public static string GetDescription(Enum value)
        {
            Type type = value.GetType();
            string name = Enum.GetName(type, value);
            if (name != null)
            {
                FieldInfo field = type.GetField(name);
                if (field != null)
                {
                    DescriptionAttribute attr =
                           Attribute.GetCustomAttribute(field,
                             typeof(DescriptionAttribute)) as DescriptionAttribute;
                    if (attr != null)
                    {
                        return attr.Description;
                    }
                }
            }
            return null;
        }
    }
}
