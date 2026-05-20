using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace IARendering.Features.Launcher
{
    public enum LauncherWorkflowState
    {
        Empty = 0,
        LoadingModel,
        Ready,
        GeneratingRender,
    }

    public sealed class LauncherStateService : INotifyPropertyChanged
    {
        private const string DefaultAiRenderPrompt = "convert this 3D viewport render into a photorealistic architectural visualization, preserve geometry, perspective, object placement and scale, replace flat 3D materials with realistic PBR materials, add realistic global illumination, contact shadows, fabric texture, wood grain, wall paint texture, realistic plant leaves, natural window light, physically accurate indoor lighting, high quality photorealistic render";

        private LauncherWorkflowState workflowState;
        private string supportedExtensionsText = "GLB, STL or OBJ";
        private string? viewportCaptureImagePath;
        private string? resultImagePath;
        private string lastRenderDurationText = "Last Render Time: --";
        private string statusMessage = "Drag and drop a model to begin.";
        private bool isAiSettingsPanelOpen;
        private string aiRenderPrompt = DefaultAiRenderPrompt;
        private float aiRenderCfgScale = 1.0f;
        private int aiRenderSteps = 4;

        public event PropertyChangedEventHandler? PropertyChanged;

        public LauncherWorkflowState WorkflowState => this.workflowState;

        public string SupportedExtensionsText => this.supportedExtensionsText;

        public string? ViewportCaptureImagePath => this.viewportCaptureImagePath;

        public string? ResultImagePath => this.resultImagePath;

        public string LastRenderDurationText => this.lastRenderDurationText;

        public string StatusMessage => this.statusMessage;

        public bool IsAiSettingsPanelOpen => this.isAiSettingsPanelOpen;

        public string AiRenderPrompt => this.aiRenderPrompt;

        public float AiRenderCfgScale => this.aiRenderCfgScale;

        public int AiRenderSteps => this.aiRenderSteps;

        public bool IsModelLoading => this.workflowState == LauncherWorkflowState.LoadingModel;

        public bool IsModelReady =>
            this.workflowState == LauncherWorkflowState.Ready ||
            this.workflowState == LauncherWorkflowState.GeneratingRender;

        public bool IsGeneratingRender => this.workflowState == LauncherWorkflowState.GeneratingRender;

        public bool CanGenerateRender => this.workflowState == LauncherWorkflowState.Ready;

        public bool ShowViewportSurface => this.workflowState == LauncherWorkflowState.Ready || this.workflowState == LauncherWorkflowState.GeneratingRender;

        public bool ShowViewportPlaceholder => this.workflowState == LauncherWorkflowState.Empty;

        public bool ShowViewportSpinner => this.workflowState == LauncherWorkflowState.LoadingModel;

        public bool ShowRenderSpinner => this.workflowState == LauncherWorkflowState.GeneratingRender;

        public bool HasRenderResult => !string.IsNullOrWhiteSpace(this.resultImagePath);

        public void SetAiSettingsPanelOpen(bool value)
        {
            if (this.isAiSettingsPanelOpen != value)
            {
                this.isAiSettingsPanelOpen = value;
                this.OnPropertyChanged(nameof(this.IsAiSettingsPanelOpen));
            }
        }

        public void SetAiRenderPrompt(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));
            }

            if (!string.Equals(this.aiRenderPrompt, prompt, StringComparison.Ordinal))
            {
                this.aiRenderPrompt = prompt;
                this.OnPropertyChanged(nameof(this.AiRenderPrompt));
            }
        }

        public void SetAiRenderCfgScale(float value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cfg scale must be greater than zero.");
            }

            if (Math.Abs(this.aiRenderCfgScale - value) > 0.0001f)
            {
                this.aiRenderCfgScale = value;
                this.OnPropertyChanged(nameof(this.AiRenderCfgScale));
            }
        }

        public void SetAiRenderSteps(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Steps must be greater than zero.");
            }

            if (this.aiRenderSteps != value)
            {
                this.aiRenderSteps = value;
                this.OnPropertyChanged(nameof(this.AiRenderSteps));
            }
        }

        public void SetSupportedExtensions(IEnumerable<string> extensions)
        {
            if (extensions == null)
            {
                throw new ArgumentNullException(nameof(extensions));
            }

            var normalized = extensions
                .Where(extension => !string.IsNullOrWhiteSpace(extension))
                .Select(extension => extension.Trim().TrimStart('.').ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(extension => extension)
                .ToArray();

            var newValue = normalized.Length > 0
                ? string.Join(", ", normalized)
                : "GLB, STL or OBJ";

            if (this.supportedExtensionsText != newValue)
            {
                this.supportedExtensionsText = newValue;
                this.OnPropertyChanged();
            }
        }

        public void BeginModelLoad()
        {
            this.viewportCaptureImagePath = null;
            this.resultImagePath = null;
            this.lastRenderDurationText = "Last Render Time: --";
            this.statusMessage = "Loading model...";
            this.workflowState = LauncherWorkflowState.LoadingModel;
            this.NotifyVisualStateChanged();
        }

        public void CompleteModelLoad()
        {
            this.statusMessage = "Model ready.";
            this.workflowState = LauncherWorkflowState.Ready;
            this.NotifyVisualStateChanged();
        }

        public void RestoreReadyState(string? statusMessage = null)
        {
            this.statusMessage = string.IsNullOrWhiteSpace(statusMessage) ? "Model ready." : statusMessage;
            this.workflowState = LauncherWorkflowState.Ready;
            this.NotifyVisualStateChanged();
        }

        public void FailModelLoad(string? statusMessage = null)
        {
            this.viewportCaptureImagePath = null;
            this.resultImagePath = null;
            this.lastRenderDurationText = "Last Render Time: --";
            this.statusMessage = string.IsNullOrWhiteSpace(statusMessage) ? "Unable to load the model." : statusMessage;
            this.workflowState = LauncherWorkflowState.Empty;
            this.NotifyVisualStateChanged();
        }

        public void BeginAiRenderGeneration(string viewportCaptureImagePath)
        {
            if (string.IsNullOrWhiteSpace(viewportCaptureImagePath))
            {
                throw new ArgumentException("Viewport capture image path is required.", nameof(viewportCaptureImagePath));
            }

            this.viewportCaptureImagePath = viewportCaptureImagePath;
            this.statusMessage = "Generating IA render...";
            this.workflowState = LauncherWorkflowState.GeneratingRender;
            this.NotifyVisualStateChanged();
        }

        public void CompleteAiRenderGeneration(string outputImagePath, TimeSpan duration)
        {
            if (string.IsNullOrWhiteSpace(outputImagePath))
            {
                throw new ArgumentException("Output image path is required.", nameof(outputImagePath));
            }

            this.resultImagePath = outputImagePath;
            this.lastRenderDurationText = $"Last Render Time: {FormatDuration(duration)}";
            this.statusMessage = "IA render generated successfully.";
            this.workflowState = LauncherWorkflowState.Ready;
            this.NotifyVisualStateChanged();
        }

        public void FailAiRenderGeneration(string? statusMessage = null)
        {
            this.statusMessage = string.IsNullOrWhiteSpace(statusMessage) ? "IA render generation failed." : statusMessage;
            this.workflowState = LauncherWorkflowState.Ready;
            this.NotifyVisualStateChanged();
        }

        private void NotifyVisualStateChanged()
        {
            this.OnPropertyChanged(nameof(this.WorkflowState));
            this.OnPropertyChanged(nameof(this.ViewportCaptureImagePath));
            this.OnPropertyChanged(nameof(this.ResultImagePath));
            this.OnPropertyChanged(nameof(this.LastRenderDurationText));
            this.OnPropertyChanged(nameof(this.StatusMessage));
            this.OnPropertyChanged(nameof(this.IsModelLoading));
            this.OnPropertyChanged(nameof(this.IsModelReady));
            this.OnPropertyChanged(nameof(this.IsGeneratingRender));
            this.OnPropertyChanged(nameof(this.CanGenerateRender));
            this.OnPropertyChanged(nameof(this.ShowViewportSurface));
            this.OnPropertyChanged(nameof(this.ShowViewportPlaceholder));
            this.OnPropertyChanged(nameof(this.ShowViewportSpinner));
            this.OnPropertyChanged(nameof(this.ShowRenderSpinner));
            this.OnPropertyChanged(nameof(this.HasRenderResult));
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return duration.ToString(@"hh\:mm\:ss");
            }

            if (duration.TotalMinutes >= 1)
            {
                return duration.ToString(@"mm\:ss");
            }

            return $"{duration.TotalSeconds:F1}s";
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
