using System;
using Evergine.Common.Graphics;
using SkiaSharp;

namespace IARendering.Features.Screenshots
{
    internal class TextureSnapShoter
    {
        public SKImage GetSnapShot(Texture texture, GraphicsContext context)
        {
            Texture stagingTexture = texture;
            CommandQueue stagingCommandQueue = null;

            var isStaging = texture.Description.Usage == ResourceUsage.Staging;
            if (!isStaging)
            {
                var copyDescription = texture.Description;
                copyDescription.Flags = TextureFlags.None;
                copyDescription.CpuAccess = ResourceCpuAccess.Read;
                copyDescription.Usage = ResourceUsage.Staging;

                stagingTexture = context.Factory.CreateTexture(ref copyDescription);

                stagingCommandQueue = context.Factory.CreateCommandQueue();
                var command = stagingCommandQueue.CommandBuffer();

                command.Begin();
                command.CopyTextureDataTo(texture, stagingTexture);
                command.End();
                command.Commit();
                stagingCommandQueue.Submit();
            }

            SKImage image = stagingTexture.Description.Format switch
            {
                PixelFormat.R8G8B8A8_UNorm => CopyTexture(context, stagingTexture, SKColorType.Rgba8888),
                PixelFormat.B8G8R8A8_UNorm => CopyTexture(context, stagingTexture, SKColorType.Bgra8888),
                _ => null,
            };

            if (image == null)
            {
                throw new InvalidOperationException($"Texture format not valid {stagingTexture.Description.Format}, use R8G8B8A8_UNorm or B8G8R8A8_UNorm.");
            }

            if (!isStaging)
            {
                stagingCommandQueue.Dispose();
                stagingTexture.Dispose();
            }

            return image;
        }

        private static unsafe SKImage CopyTexture(GraphicsContext context, Texture stagingTexture, SKColorType colorType)
        {
            var mappedResource = context.MapMemory(stagingTexture, MapMode.Read);

            try
            {
                int width = (int)stagingTexture.Description.Width;
                int height = (int)stagingTexture.Description.Height;
                var info = new SKImageInfo
                {
                    Width = width,
                    Height = height,
                    ColorType = colorType,
                    AlphaType = SKAlphaType.Premul,
                };

                var resourceSpan = new Span<byte>(mappedResource.Data.ToPointer(), (int)mappedResource.RowPitch * height);
                var data = new byte[info.BytesSize];
                var dataSlice = new Span<byte>(data);

                for (int i = 0; i < height; i++)
                {
                    var rowDataSlice = dataSlice.Slice(info.RowBytes * i, info.RowBytes);
                    resourceSpan.Slice((int)mappedResource.RowPitch * i, info.RowBytes).CopyTo(rowDataSlice);
                }

                return SKImage.FromPixelCopy(info, data);
            }
            finally
            {
                context.UnmapMemory(stagingTexture);
            }
        }
    }
}
