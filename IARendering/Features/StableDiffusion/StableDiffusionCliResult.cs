namespace IARendering.Features.StableDiffusion
{
    public class StableDiffusionCliResult
    {
        public int ExitCode { get; init; }

        public string StandardOutput { get; init; }

        public string StandardError { get; init; }

        public string OutputImagePath { get; init; }

        public bool Success => this.ExitCode == 0;
    }
}
