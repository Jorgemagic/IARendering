using System;
using System.IO;
using Evergine.Common.Graphics;
using Evergine.Framework.Graphics;
using SkiaSharp;

namespace IARendering.Features.Screenshots
{
    public static class TextureScreenshotExtensions
    {
        public static void SaveToFile(this Texture texture, GraphicsContext context, string outputPath, SKEncodedImageFormat imageFormat = SKEncodedImageFormat.Png, int quality = 100)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Output path is required.", nameof(outputPath));
            }

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var capturer = new TextureSnapShoter();
            using (var image = capturer.GetSnapShot(texture, context))
            using (var data = image.Encode(imageFormat, quality))
            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                data.SaveTo(fs);
            }
        }

        public static void SaveDisplayToFile(this Display display, GraphicsContext context, string outputPath, SKEncodedImageFormat imageFormat = SKEncodedImageFormat.Png, int quality = 100)
        {
            if (display == null)
            {
                throw new ArgumentNullException(nameof(display));
            }

            var frameBuffer = display.FrameBuffer ?? throw new InvalidOperationException("Display does not have a frame buffer.");
            if (frameBuffer.ColorTargets == null || frameBuffer.ColorTargets.Length == 0)
            {
                throw new InvalidOperationException("Display frame buffer does not contain color targets.");
            }

            frameBuffer.ColorTargets[0].Texture.SaveToFile(context, outputPath, imageFormat, quality);
        }
    }
}
