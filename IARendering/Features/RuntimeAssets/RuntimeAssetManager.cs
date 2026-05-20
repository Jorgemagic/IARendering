using Evergine.Common.Attributes;
using Evergine.Common.Attributes.Converters;
using Evergine.Common.Graphics;
using Evergine.Common.IO;
using Evergine.Components.Animation;
using Evergine.Framework;
using Evergine.Framework.Graphics;
using Evergine.Framework.Managers;
using Evergine.Framework.Services;
using Evergine.Framework.Threading;
using Evergine.Mathematics;
using IARendering.Features.Camera;
using IARendering.Features.Launcher;
using IARendering.Features.RuntimeAssets.Loaders;
using IARendering.Features.Screenshots;
using IARendering.Features.StableDiffusion;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IARendering.Features.RuntimeAssets
{
    public class RuntimeAssetManager : UpdatableSceneManager
    {
        private const string AiRenderPrompt = "convert this 3D viewport render into a photorealistic architectural visualization, preserve geometry, perspective, object placement and scale, replace flat 3D materials with realistic PBR materials, add realistic global illumination, contact shadows, fabric texture, wood grain, wall paint texture, realistic plant leaves, natural window light, physically accurate indoor lighting, high quality photorealistic render";

        [BindService]
        internal AssetsService AssetsService = null!;

        [BindService]
        internal AssetsDirectory AssetsDirectory = null!;

        [BindService]
        private LauncherStateService launcherState = null!;

        [BindSceneManager]
        private RenderManager renderManager = null!;

        private readonly List<BaseRuntimeLoader> runtimeLoaders = new List<BaseRuntimeLoader>();
        private Dictionary<RuntimeLoaderType, string[]> supportedExtensionsByType = null!;
        private RuntimeLoadResult? currentLoad;
        private StableDiffusionCli stableDiffusionCli = null!;
        private bool isAiGenerationInProgress;

        private OrbitCameraBehavior? orbitCameraBehavior;
        private DirectionalLight? light;
        private bool initialSceneSetupCompleted;

        public float CameraResetZoom = 2f;

        [RenderPropertyAsFInput(typeof(FloatRadianToDegreeConverter), MinLimit = -90, MaxLimit = 90, AsSlider = true, DesiredChange = 1, DesiredLargeChange = 5)]
        public float CameraResetLambda = MathHelper.ToRadians(25);

        [RenderPropertyAsFInput(typeof(FloatRadianToDegreeConverter), MinLimit = -180, MaxLimit = 180, AsSlider = true, DesiredChange = 1, DesiredLargeChange = 5)]
        public float cameraResetTheta = MathHelper.ToRadians(-25);

        public BoundingBox BBox { get; private set; }

        protected override bool OnAttached()
        {
            this.runtimeLoaders.Add(new GLBRuntimeLoader(this));
            this.runtimeLoaders.Add(new STLRuntimeLoader(this));
            this.runtimeLoaders.Add(new OBJRuntimeLoader(this));

            this.supportedExtensionsByType = this.runtimeLoaders
                .GroupBy(loader => loader.LoaderType)
                .ToDictionary(
                    group => group.Key,
                    group => group.SelectMany(loader => loader.SupportedExtensions)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(extension => extension)
                        .ToArray());

            this.stableDiffusionCli = new StableDiffusionCli();
            this.launcherState.SetSupportedExtensions(this.supportedExtensionsByType.SelectMany(entry => entry.Value));

            MyApplication.OnNewRuntimeAsset += this.OnNewRuntimeAsset;
            MyApplication.OnAiRenderRequested += this.OnAiRenderRequested;
            MyApplication.IsRuntimeAssetValid = this.IsRuntimeAssetValid;

            return base.OnAttached();
        }

        protected override void Start()
        {
            base.Start();
            this.TryInitializeSceneReferences();
        }

        public override void Update(TimeSpan gameTime)
        {
            if (!this.initialSceneSetupCompleted)
            {
                this.TryInitializeSceneReferences();
            }

            var keyboard = this.Managers.RenderManager.ActiveCamera3D?.Display?.KeyboardDispatcher;
            if (keyboard?.ReadKeyState(Evergine.Common.Input.Keyboard.Keys.F1) == Evergine.Common.Input.ButtonState.Pressing)
            {
                this.renderManager.DebugLines = !this.renderManager.DebugLines;
            }

            if (this.renderManager.DebugLines && this.currentLoad?.IsValid == true && this.currentLoad.BoundingBox.HasValue)
            {
                var lineBatch = this.renderManager.LineBatch3D;
                lineBatch.DrawBoundingBox(this.BBox, Color.Red);
                lineBatch.DrawPoint(this.BBox.Center, 0.5f, Color.Blue);
                lineBatch.DrawPoint(Vector3.Zero, 1, Color.Black);
            }
        }

        protected override void OnDetached()
        {
            base.OnDetached();

            this.runtimeLoaders.Clear();
            MyApplication.OnNewRuntimeAsset -= this.OnNewRuntimeAsset;
            MyApplication.OnAiRenderRequested -= this.OnAiRenderRequested;
            MyApplication.IsRuntimeAssetValid = null;
        }

        private bool IsRuntimeAssetValid(string filePath)
        {
            return this.runtimeLoaders.Any(loader => loader.CanProcess(filePath));
        }

        private void OnNewRuntimeAsset(object? sender, LauncherRenderRequest request)
        {
            var path = request.FilePath;
            var hadValidModel = this.currentLoad?.IsValid == true;

            Task.Run(async () =>
            {
                var loader = this.runtimeLoaders.FirstOrDefault(candidate => candidate.CanProcess(path));
                if (loader == null)
                {
                    Debug.WriteLine($"[RuntimeAssetManager] No loader available for asset from {path}");
                    return;
                }

                try
                {
                    this.launcherState.BeginModelLoad();
                    var result = await loader.LoadAsset(path);

                    if (result.IsValid && result.Entity != null)
                    {
                        await EvergineForegroundTask.Run(() =>
                        {
                            Debug.WriteLine($"[RuntimeAssetManager] Loaded asset from {path}");
                            this.RuntimeAssetLoaded(result);
                        });

                        this.launcherState.CompleteModelLoad();
                    }
                    else
                    {
                        Debug.WriteLine($"[RuntimeAssetManager] Failed to load asset from {path}");
                        this.RestoreStateAfterFailedLoad(hadValidModel, $"Unable to load model: {Path.GetFileName(path)}");
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"[RuntimeAssetManager] Exception loading asset from {path}: {ex.Message}");
                    this.RestoreStateAfterFailedLoad(hadValidModel, $"Unable to load model: {Path.GetFileName(path)}");
                }
            });
        }

        private void OnAiRenderRequested(object? sender, EventArgs e)
        {
            _ = EvergineForegroundTask.Run(() =>
            {
                this.StartAiRenderGeneration();
            });
        }

        private void RuntimeAssetLoaded(RuntimeLoadResult result)
        {
            if (this.currentLoad?.IsValid == true)
            {
                this.Managers.EntityManager.Remove(this.currentLoad.Entity);

                foreach (var disposable in this.currentLoad.ObjectsToRemove)
                {
                    disposable.Dispose();
                }

                this.currentLoad = null;
            }

            var animation = result.Entity.FindComponent<Animation3D>();
            if (animation != null)
            {
                animation.PlayAutomatically = true;
                animation.Loop = true;
            }

            this.currentLoad = result;
            this.Managers.EntityManager.Add(this.currentLoad.Entity);
            this.CenterCamera();
        }

        private void CenterCamera()
        {
            if (this.orbitCameraBehavior == null)
            {
                return;
            }

            this.orbitCameraBehavior.ResetCameraToInit();

            if (this.currentLoad?.BoundingBox.HasValue == true)
            {
                this.BBox = BoundingBox.Transform(
                    this.currentLoad.BoundingBox.Value,
                    this.currentLoad.Entity.FindComponent<Transform3D>().WorldTransform);

                var aspectRatio = Math.Max(1 / this.orbitCameraBehavior.Camera.AspectRatio, 1.5f);
                var zoom = this.BBox.HalfExtent.Length() * this.CameraResetZoom * aspectRatio;

                this.orbitCameraBehavior.ResetPosition(this.BBox.Center);
                this.orbitCameraBehavior.ResetZoom(zoom);
                if (this.light != null)
                {
                    this.light.ShadowDistance = zoom * 2;
                }
            }

            this.orbitCameraBehavior.ResetOrbit(this.cameraResetTheta, this.CameraResetLambda);
        }

        private void TryInitializeSceneReferences()
        {
            this.orbitCameraBehavior ??= this.Managers.EntityManager.FindFirstComponentOfType<OrbitCameraBehavior>();
            this.light ??= this.Managers.EntityManager.FindFirstComponentOfType<DirectionalLight>(isExactType: false);

            if (!this.initialSceneSetupCompleted && this.orbitCameraBehavior != null)
            {
                this.CenterCamera();
                this.initialSceneSetupCompleted = true;
            }
        }

        private void RestoreStateAfterFailedLoad(bool hadValidModel, string errorMessage)
        {
            if (hadValidModel)
            {
                this.launcherState.RestoreReadyState(errorMessage);
            }
            else
            {
                this.launcherState.FailModelLoad(errorMessage);
            }
        }

        private void StartAiRenderGeneration()
        {
            if (this.isAiGenerationInProgress || this.currentLoad?.IsValid != true)
            {
                return;
            }

            var activeCamera = this.Managers.RenderManager.ActiveCamera3D;
            var display = activeCamera?.Display;
            if (display == null)
            {
                this.launcherState.FailAiRenderGeneration("Unable to capture the viewport.");
                return;
            }

            try
            {
                var stableDiffusionDirectory = this.stableDiffusionCli.GetStableDiffusionDirectory();
                var capturesDirectory = Path.Combine(stableDiffusionDirectory, "captures");
                var resultsDirectory = Path.Combine(stableDiffusionDirectory, "results");
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                var viewportCapturePath = Path.Combine(capturesDirectory, $"viewport_{timestamp}.png");
                var aiRenderPath = Path.Combine(resultsDirectory, $"ai_render_{timestamp}.png");

                var graphicsContext = Application.Current.Container.Resolve<GraphicsContext>();
                display.SaveDisplayToFile(graphicsContext, viewportCapturePath);

                var frameBuffer = display.FrameBuffer ?? throw new InvalidOperationException("Display does not have a frame buffer.");
                var colorTarget = frameBuffer.ColorTargets?.FirstOrDefault()
                    ?? throw new InvalidOperationException("Display frame buffer does not contain color targets.");
                var captureWidth = Math.Max(1, (int)colorTarget.Texture.Description.Width);
                var captureHeight = Math.Max(1, (int)colorTarget.Texture.Description.Height);

                var options = this.stableDiffusionCli.CreateDefaultFluxKleinOptions(
                    viewportCapturePath,
                    aiRenderPath,
                    AiRenderPrompt,
                    captureWidth,
                    captureHeight);

                this.isAiGenerationInProgress = true;
                this.launcherState.BeginAiRenderGeneration();
                _ = this.RunAiRenderGenerationAsync(options, Stopwatch.StartNew());
            }
            catch (Exception ex)
            {
                this.isAiGenerationInProgress = false;
                this.launcherState.FailAiRenderGeneration($"Capture failed: {ex.Message}");
            }
        }

        private async Task RunAiRenderGenerationAsync(StableDiffusionCliOptions options, Stopwatch stopwatch)
        {
            try
            {
                var result = await this.stableDiffusionCli.RunAsync(options);

                await EvergineForegroundTask.Run(() =>
                {
                    this.isAiGenerationInProgress = false;

                    if (result.Success)
                    {
                        this.launcherState.CompleteAiRenderGeneration(result.OutputImagePath, stopwatch.Elapsed);
                    }
                    else
                    {
                        var error = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
                        this.launcherState.FailAiRenderGeneration($"Stable Diffusion failed ({result.ExitCode}): {TrimStatus(error)}");
                    }
                });
            }
            catch (Exception ex)
            {
                await EvergineForegroundTask.Run(() =>
                {
                    this.isAiGenerationInProgress = false;
                    this.launcherState.FailAiRenderGeneration($"IA render failed: {ex.Message}");
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
    }
}
