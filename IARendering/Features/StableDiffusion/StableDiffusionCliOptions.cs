using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace IARendering.Features.StableDiffusion
{
    public class StableDiffusionCliOptions
    {
        public string ExecutablePath { get; set; }

        public string WorkingDirectory { get; set; }

        public string DiffusionModelPath { get; set; }

        public string VaePath { get; set; }

        public string LlmPath { get; set; }

        public string InputImagePath { get; set; }

        public string OutputImagePath { get; set; }

        public string Prompt { get; set; }

        public int Width { get; set; } = 1024;

        public int Height { get; set; } = 1024;

        public float CfgScale { get; set; } = 1.0f;

        public int Steps { get; set; } = 4;

        public string SamplingMethod { get; set; } = "euler";

        public bool EnableDiffusionFlashAttention { get; set; } = true;

        public List<string> AdditionalArguments { get; } = new List<string>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(this.ExecutablePath))
            {
                throw new InvalidOperationException("Stable Diffusion executable path is required.");
            }

            if (!File.Exists(this.ExecutablePath))
            {
                throw new FileNotFoundException("Stable Diffusion executable not found.", this.ExecutablePath);
            }

            if (string.IsNullOrWhiteSpace(this.DiffusionModelPath))
            {
                throw new InvalidOperationException("Diffusion model path is required.");
            }

            if (!File.Exists(this.DiffusionModelPath))
            {
                throw new FileNotFoundException("Diffusion model not found.", this.DiffusionModelPath);
            }

            if (string.IsNullOrWhiteSpace(this.VaePath))
            {
                throw new InvalidOperationException("VAE path is required.");
            }

            if (!File.Exists(this.VaePath))
            {
                throw new FileNotFoundException("VAE file not found.", this.VaePath);
            }

            if (string.IsNullOrWhiteSpace(this.LlmPath))
            {
                throw new InvalidOperationException("LLM path is required.");
            }

            if (!File.Exists(this.LlmPath))
            {
                throw new FileNotFoundException("LLM file not found.", this.LlmPath);
            }

            if (string.IsNullOrWhiteSpace(this.InputImagePath))
            {
                throw new InvalidOperationException("Input image path is required.");
            }

            if (!File.Exists(this.InputImagePath))
            {
                throw new FileNotFoundException("Input image not found.", this.InputImagePath);
            }

            if (string.IsNullOrWhiteSpace(this.OutputImagePath))
            {
                throw new InvalidOperationException("Output image path is required.");
            }

            if (string.IsNullOrWhiteSpace(this.Prompt))
            {
                throw new InvalidOperationException("Prompt is required.");
            }

            if (this.Width <= 0)
            {
                throw new InvalidOperationException("Width must be greater than zero.");
            }

            if (this.Height <= 0)
            {
                throw new InvalidOperationException("Height must be greater than zero.");
            }

            if (this.Steps <= 0)
            {
                throw new InvalidOperationException("Steps must be greater than zero.");
            }

            var outputDirectory = Path.GetDirectoryName(this.OutputImagePath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
        }

        internal string GetCfgScaleArgument() => this.CfgScale.ToString(CultureInfo.InvariantCulture);
    }
}
