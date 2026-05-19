using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IARendering.Features.StableDiffusion
{
    public class StableDiffusionCli
    {
        private readonly string repositoryRoot;

        public StableDiffusionCli()
            : this(ResolveRepositoryRoot())
        {
        }

        public StableDiffusionCli(string repositoryRoot)
        {
            this.repositoryRoot = repositoryRoot ?? throw new ArgumentNullException(nameof(repositoryRoot));
        }

        public StableDiffusionCliOptions CreateDefaultFluxKleinOptions(string inputImagePath, string outputImagePath, string prompt)
        {
            var stableDiffusionDirectory = this.GetStableDiffusionDirectory();
            var modelDirectory = Path.Combine(stableDiffusionDirectory, "models", "FluxKlein");

            return new StableDiffusionCliOptions
            {
                ExecutablePath = Path.Combine(stableDiffusionDirectory, "sd-cli.exe"),
                WorkingDirectory = stableDiffusionDirectory,
                DiffusionModelPath = Path.Combine(modelDirectory, "flux-2-klein-4b-Q8_0.gguf"),
                VaePath = Path.Combine(modelDirectory, "full_encoder_small_decoder.safetensors"),
                LlmPath = Path.Combine(modelDirectory, "Qwen3-4B-Q4_K_M.gguf"),
                InputImagePath = inputImagePath,
                OutputImagePath = outputImagePath,
                Prompt = prompt,
                Width = 1024,
                Height = 1024,
                CfgScale = 1.0f,
                Steps = 4,
                SamplingMethod = "euler",
                EnableDiffusionFlashAttention = true,
            };
        }

        public async Task<StableDiffusionCliResult> RunAsync(StableDiffusionCliOptions options, CancellationToken cancellationToken = default)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            options.Validate();

            var startInfo = new ProcessStartInfo
            {
                FileName = options.ExecutablePath,
                WorkingDirectory = string.IsNullOrWhiteSpace(options.WorkingDirectory)
                    ? Path.GetDirectoryName(options.ExecutablePath) ?? this.repositoryRoot
                    : options.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            AddArguments(startInfo, options);

            using var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };

            var standardOutput = new StringBuilder();
            var standardError = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    standardOutput.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    standardError.AppendLine(e.Data);
                }
            };

            if (!process.Start())
            {
                throw new InvalidOperationException($"Unable to start process '{options.ExecutablePath}'.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            return new StableDiffusionCliResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = standardOutput.ToString(),
                StandardError = standardError.ToString(),
                OutputImagePath = options.OutputImagePath,
            };
        }

        public string GetStableDiffusionDirectory()
        {
            return Path.Combine(this.repositoryRoot, "StableDiffusion");
        }

        private static void AddArguments(ProcessStartInfo startInfo, StableDiffusionCliOptions options)
        {
            startInfo.ArgumentList.Add("--diffusion-model");
            startInfo.ArgumentList.Add(options.DiffusionModelPath);

            startInfo.ArgumentList.Add("--vae");
            startInfo.ArgumentList.Add(options.VaePath);

            startInfo.ArgumentList.Add("--llm");
            startInfo.ArgumentList.Add(options.LlmPath);

            startInfo.ArgumentList.Add("-r");
            startInfo.ArgumentList.Add(options.InputImagePath);

            startInfo.ArgumentList.Add("-p");
            startInfo.ArgumentList.Add(options.Prompt);

            startInfo.ArgumentList.Add("-W");
            startInfo.ArgumentList.Add(options.Width.ToString());

            startInfo.ArgumentList.Add("-H");
            startInfo.ArgumentList.Add(options.Height.ToString());

            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(options.OutputImagePath);

            startInfo.ArgumentList.Add("--cfg-scale");
            startInfo.ArgumentList.Add(options.GetCfgScaleArgument());

            startInfo.ArgumentList.Add("--steps");
            startInfo.ArgumentList.Add(options.Steps.ToString());

            if (!string.IsNullOrWhiteSpace(options.SamplingMethod))
            {
                startInfo.ArgumentList.Add("--sampling-method");
                startInfo.ArgumentList.Add(options.SamplingMethod);
            }

            if (options.EnableDiffusionFlashAttention)
            {
                startInfo.ArgumentList.Add("--diffusion-fa");
            }

            foreach (var argument in options.AdditionalArguments)
            {
                if (!string.IsNullOrWhiteSpace(argument))
                {
                    startInfo.ArgumentList.Add(argument);
                }
            }
        }

        private static string ResolveRepositoryRoot()
        {
            var currentDirectory = AppContext.BaseDirectory;
            var directory = new DirectoryInfo(currentDirectory);

            while (directory != null)
            {
                var projectMarker = Path.Combine(directory.FullName, "IARendering.weproj");
                if (File.Exists(projectMarker))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Unable to resolve the repository root from the current application directory.");
        }
    }
}
