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
using IARendering.Features.RuntimeAssets.Loaders;
using IARendering.Features.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IARendering.Features.UI.UIComponent;

namespace IARendering.Features.RuntimeAssets
{
    public class RuntimeAssetManager : UpdatableSceneManager
    {
        [BindService]
        internal AssetsService AssetsService;

        [BindService]
        internal AssetsDirectory AssetsDirectory;

        [BindSceneManager]
        private RenderManager renderManager;

        private List<BaseRuntimeLoader> runtimeLoaders = new List<BaseRuntimeLoader>();
        private Dictionary<RuntimeLoaderType, string[]> supportedExtensionsByType;
        private RuntimeLoadResult currentLoad;

        private OrbitCameraBehavior orbitCameraBehavior;

        private DirectionalLight light;

        private UIComponent uIComponent;
        private bool initialSceneSetupCompleted;

        public float CameraResetZoom = 2f;

        [RenderPropertyAsFInput(typeof(FloatRadianToDegreeConverter), MinLimit = -90, MaxLimit = 90, AsSlider = true, DesiredChange = 1, DesiredLargeChange = 5)]
        public float CameraResetLambda = MathHelper.ToRadians(25);

        [RenderPropertyAsFInput(typeof(FloatRadianToDegreeConverter), MinLimit = -180, MaxLimit = 180, AsSlider = true, DesiredChange = 1, DesiredLargeChange = 5)]
        public float cameraResetTheta = MathHelper.ToRadians(-25);

        public BoundingBox BBox { get; private set; }

        protected override bool OnAttached()
        {
            // Register runtimes
            this.runtimeLoaders.Add(new GLBRuntimeLoader(this));
            this.runtimeLoaders.Add(new STLRuntimeLoader(this));
            this.runtimeLoaders.Add(new OBJRuntimeLoader(this));

            this.supportedExtensionsByType = this.runtimeLoaders
                .GroupBy(l => l.LoaderType)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(l => l.SupportedExtensions)
                          .Distinct(StringComparer.OrdinalIgnoreCase)
                          .OrderBy(e => e)
                          .ToArray());

            // Register to application events
            MyApplication.OnNewRuntimeAsset += OnNewRuntimeAsset;
            MyApplication.IsRuntimeAssetValid = IsRuntimeAssetValid;

            return base.OnAttached();
        }

        protected override void Start()
        {
            base.Start();

            this.TryInitializeSceneReferences();
        }

        public override void Update(TimeSpan gameTime)
        {
            if (!this.initialSceneSetupCompleted || this.uIComponent == null)
            {
                this.TryInitializeSceneReferences();
            }

            var keyboard = this.Managers.RenderManager.ActiveCamera3D?.Display?.KeyboardDispatcher;
            if (keyboard?.ReadKeyState(Evergine.Common.Input.Keyboard.Keys.F1) == Evergine.Common.Input.ButtonState.Pressing)
            {
                this.renderManager.DebugLines = !this.renderManager.DebugLines;
            }

            if (renderManager.DebugLines && this.currentLoad?.IsValid == true && this.currentLoad.BoundingBox.HasValue)
            {
                var lb = renderManager.LineBatch3D;
                lb.DrawBoundingBox(this.BBox, Color.Red);
                lb.DrawPoint(this.BBox.Center, 0.5f, Color.Blue);
                lb.DrawPoint(Vector3.Zero, 1, Color.Black);
            }
        }

        protected override void OnDetached()
        {
            base.OnDetached();

            this.runtimeLoaders.Clear();

            // Unregister from application events
            MyApplication.OnNewRuntimeAsset -= OnNewRuntimeAsset;
            MyApplication.IsRuntimeAssetValid = null;

        }

        private bool IsRuntimeAssetValid(string filePath)
        {
            return this.runtimeLoaders.Any(loader => loader.CanProcess(filePath));
        }

        private void OnNewRuntimeAsset(object sender, string path)
        {
            Task.Run(async () =>
            {
                var loader = runtimeLoaders.FirstOrDefault(l => l.CanProcess(path));
                if (loader != null)
                {
                    try
                    {
                        this.SetUiMode(UIMode.Loading);
                        var result = await loader.LoadAsset(path);

                        if (result.IsValid && result.Entity != null)
                        {
                            await EvergineForegroundTask.Run(() =>
                            {
                                Debug.WriteLine($"[RuntimeAssetManager] Loaded asset from {path}");
                                this.RuntimeAssetLoaded(result);
                            });

                            this.SetUiMode(UIMode.Loaded);
                        }
                        else
                        {
                            Debug.WriteLine($"[RuntimeAssetManager] Failed to load asset from {path}");
                            this.SetUiMode(UIMode.Init);
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError($"[RuntimeAssetManager] Exception loading asset from {path}: {ex.Message}");
                        this.SetUiMode(UIMode.Init);
                    }
                }
                else
                {
                    Debug.WriteLine($"[RuntimeAssetManager] No loader available for asset from {path}");
                }
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
                this.BBox = BoundingBox.Transform(this.currentLoad.BoundingBox.Value, this.currentLoad.Entity.FindComponent<Transform3D>().WorldTransform);

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

            if (this.uIComponent == null)
            {
                this.uIComponent = this.Managers.EntityManager.FindFirstComponentOfType<UIComponent>();
                if (this.uIComponent != null)
                {
                    this.uIComponent.SetSuportedFiles(this.supportedExtensionsByType);
                    this.uIComponent.CurrentMode = this.currentLoad?.IsValid == true ? UIMode.Loaded : UIMode.Init;
                    this.uIComponent.IsEnabled = true;
                }
            }

            if (!this.initialSceneSetupCompleted && this.orbitCameraBehavior != null)
            {
                this.CenterCamera();
                this.initialSceneSetupCompleted = true;
            }
        }

        private void SetUiMode(UIMode mode)
        {
            this.uIComponent ??= this.Managers.EntityManager.FindFirstComponentOfType<UIComponent>();
            if (this.uIComponent != null)
            {
                this.uIComponent.CurrentMode = mode;
            }
        }
    }
}
