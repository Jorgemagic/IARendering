using Evergine.Bullet;
using Evergine.Framework;
using Evergine.Framework.Graphics;
using Evergine.Mathematics;
using Evergine.UI;
using IARendering.Features.Camera;
using IARendering.Features.RuntimeAssets;
using IARendering.Features.UI;

namespace IARendering
{
    public class MyScene : Scene
    {
        public override void RegisterManagers()
        {
            base.RegisterManagers();
            
            this.Managers.AddManager(new BulletPhysicManager3D());
            this.Managers.AddManager(new ImGuiManager());
            this.Managers.AddManager(new RuntimeAssetManager());
        }

        protected override void CreateScene()
        {
            // Create camera
            Entity cameraRoot = new Entity()
                .AddComponent(new Transform3D()
                {
                    LocalOrientation = Quaternion.CreateFromYawPitchRoll(
                        MathHelper.ToRadians(25),
                        MathHelper.ToRadians(-25),
                        0),
                })
                .AddComponent(new OrbitCameraBehavior()
                {
                    MaxZoom = 0.01f,
                    MinZoom = 1000f,
                    ZoomFactor = 0.15f,
                    UpdateOrder = 0.5f,
                });

            Entity camera = new Entity()
                .AddComponent(new Transform3D() { Position = new Vector3(0, 0, 2) })
                .AddComponent(new Camera3D() { NearPlane = 0.1f, FarPlane = 1000 });

            cameraRoot.AddChild(camera);

            this.Managers.EntityManager.Add(cameraRoot);

            // Create UI
            Entity ui = new Entity()
                .AddComponent(new Transform3D())
                .AddComponent(new UIComponent()
                {
                    UpdateOrder = 0.5f,
                });

            this.Managers.EntityManager.Add(ui);


        }
    }
}


