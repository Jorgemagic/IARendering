using Evergine.Common.IO;
using Evergine.Framework;
using Evergine.Framework.Services;
using Evergine.Framework.Threading;
using System;

namespace IARendering
{
    public partial class MyApplication : Application
    {
        public static event EventHandler<string> OnNewRuntimeAsset;

        public delegate bool IsRuntimeAssetValidDelegate(string filePath);

        public static IsRuntimeAssetValidDelegate IsRuntimeAssetValid;

        public MyApplication()
        {
            this.Container.Register<Settings>();
            this.Container.Register<Clock>();
            this.Container.Register<TimerFactory>();
            this.Container.Register<Evergine.Framework.Services.Random>();
            this.Container.Register<ErrorHandler>();
            this.Container.Register<ScreenContextManager>();
            this.Container.Register<GraphicsPresenter>();
            this.Container.Register<AssetsDirectory>();
            this.Container.Register<AssetsService>();
            this.Container.Register<ForegroundTaskSchedulerService>();
            this.Container.Register<WorkActionScheduler>();
        }

        public override void Initialize()
        {
            base.Initialize();

            // Get ScreenContextManager
            var screenContextManager = this.Container.Resolve<ScreenContextManager>();
            var assetsService = this.Container.Resolve<AssetsService>();

            // Navigate to scene
            var scene = assetsService.Load<MyScene>(EvergineContent.Scenes.MyScene_wescene);
            ScreenContext screenContext = new ScreenContext(scene);
            screenContextManager.To(screenContext);
        }

        public static void NewRuntimeAssetToLoad(string path)
        {
            OnNewRuntimeAsset?.Invoke(null, path);
        }

        public static bool IsValidRuntimeAsset(string filePath)
        {
            return IsRuntimeAssetValid?.Invoke(filePath) ?? false;
        }
    }
}


