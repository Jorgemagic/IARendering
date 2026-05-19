using Evergine.Framework;
using Evergine.Framework.Graphics;
using Evergine.Runtimes.GLB;
using System.IO;
using System.Threading.Tasks;

namespace IARendering.Features.RuntimeAssets.Loaders
{
    public class GLBRuntimeLoader : BaseRuntimeLoader
    {
        public override RuntimeLoaderType LoaderType { get; } = RuntimeLoaderType.Model;

        public override string[] SupportedExtensions { get; } = new[] { ".glb" };

        public GLBRuntimeLoader(RuntimeAssetManager runtimeAssetManager)
            : base(runtimeAssetManager)
        {
        }

        public override async Task<RuntimeLoadResult> LoadAsset(string path)
        {
            RuntimeLoadResult result = new RuntimeLoadResult();

            using var fileStream = File.OpenRead(path);
            if (fileStream != null)
            {
                var model = await GLBRuntime.Instance.Read(fileStream);

                if (model != null)
                {
                    var modelEntity = model.InstantiateModelHierarchy(this.runtimeAssetManager.AssetsService);
                    if (modelEntity != null)
                    {
                        result.IsValid = true;
                        result.Entity = modelEntity;
                        result.BoundingBox = model.BoundingBox;
                        result.ObjectsToRemove.Add(model);

                        this.AddRelatedAssets(result, model);
                    }
                }
            }

            return result;
        }

        private void AddRelatedAssets(RuntimeLoadResult result, Model model)
        {
            foreach (var materialPair in model.Materials)
            {
                var matId = materialPair.Item2;
                var material = this.runtimeAssetManager.AssetsService.Load<Material>(matId);
                result.ObjectsToRemove.Add(material);

                foreach (var textureSlot in material.TextureSlots)
                {
                    var texture = textureSlot.Texture;
                    if (texture != null)
                    {
                        result.ObjectsToRemove.Add(texture);
                        var sampler = texture.Sampler;
                        if (sampler != null && !(sampler.Id == DefaultResourcesIDs.LinearClampSamplerID || sampler.Id == DefaultResourcesIDs.LinearWrapSamplerID))
                        {
                            result.ObjectsToRemove.Add(sampler);
                        }
                    }
                }
            }
        }
    }
}
