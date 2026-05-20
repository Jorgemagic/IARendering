using System;

namespace IARendering.Features.Launcher
{
    public sealed class LauncherRenderRequest : EventArgs
    {
        public LauncherRenderRequest(string filePath)
        {
            this.FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public string FilePath { get; }
    }
}
