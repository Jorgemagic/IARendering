using Evergine.Components.Animation;
using Evergine.Runtimes.STL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IARendering.Features.RuntimeAssets.Loaders
{
    public class STLRuntimeLoader : BaseRuntimeLoader
    {
        public override RuntimeLoaderType LoaderType { get; } = RuntimeLoaderType.Model;

        public override string[] SupportedExtensions { get; } = new[] { ".stl" };

        public STLRuntimeLoader(RuntimeAssetManager runtimeAssetManager) 
            : base(runtimeAssetManager)
        {
        }

        public override async Task<RuntimeLoadResult> LoadAsset(string path)
        {
            RuntimeLoadResult result = new RuntimeLoadResult();

            using var fileStream = File.OpenRead(path);
            if (fileStream != null)
            {
                var model = await STLRuntime.Instance.Read(fileStream);

                if (model != null)
                {
                    var modelEntity = model.InstantiateModelHierarchy(this.runtimeAssetManager.AssetsService);

                    if (modelEntity != null)
                    {
                        result.IsValid = true;
                        result.Entity = modelEntity;
                        result.BoundingBox = model.BoundingBox;
                    }
                }
            }

            return result;
        }
    }
}
