using Avalonia.Media.Imaging;
using Avalonia.Threading;
using IARendering.Features.Launcher;
using IARendering.Features.StableDiffusion;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace IARendering.Avalonia.ViewModels
{
    public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly LauncherStateService launcherState;
        private readonly DispatcherTimer renderDurationTimer;
        private Bitmap? viewportCaptureImage;
        private Bitmap? resultImage;
        private string? loadedViewportCaptureImagePath;
        private string? loadedImagePath;
        private string supportedExtensionsText = "GLB, STL or OBJ";
        private string statusMessage = "Drag and drop a model to begin.";
        private string lastRenderDurationText = "Last Render Time: --";
        private string aiRenderPrompt = string.Empty;
        private string aiRenderCfgScaleText = string.Empty;
        private string aiRenderStepsText = string.Empty;
        private string aiRenderSettingsErrorMessage = string.Empty;
        private StableDiffusionRuntime selectedAiRenderRuntime;
        private bool canGenerateRender;
        private bool isAiSettingsPanelOpen;
        private bool showViewportSurface;
        private bool showViewportPlaceholder = true;
        private bool showViewportSpinner;
        private bool showRenderSpinner;

        private static readonly IReadOnlyList<StableDiffusionRuntime> aiRenderRuntimeOptions =
        [
            StableDiffusionRuntime.GPU,
            StableDiffusionRuntime.CPU,
        ];

        public MainWindowViewModel(LauncherStateService launcherState)
        {
            this.launcherState = launcherState ?? throw new ArgumentNullException(nameof(launcherState));
            this.renderDurationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100),
            };
            this.renderDurationTimer.Tick += this.OnRenderDurationTimerTick;
            this.launcherState.PropertyChanged += this.OnLauncherStateChanged;
            this.RefreshFromState();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string SupportedExtensionsText
        {
            get => this.supportedExtensionsText;
            private set => this.SetProperty(ref this.supportedExtensionsText, value);
        }

        public string StatusMessage
        {
            get => this.statusMessage;
            private set => this.SetProperty(ref this.statusMessage, value);
        }

        public string LastRenderDurationText
        {
            get => this.lastRenderDurationText;
            private set => this.SetProperty(ref this.lastRenderDurationText, value);
        }

        public string AiRenderPrompt
        {
            get => this.aiRenderPrompt;
            set
            {
                if (this.SetProperty(ref this.aiRenderPrompt, value))
                {
                    this.CommitAiRenderSettings();
                }
            }
        }

        public string AiRenderCfgScaleText
        {
            get => this.aiRenderCfgScaleText;
            set
            {
                if (this.SetProperty(ref this.aiRenderCfgScaleText, value))
                {
                    this.CommitAiRenderSettings();
                }
            }
        }

        public string AiRenderStepsText
        {
            get => this.aiRenderStepsText;
            set
            {
                if (this.SetProperty(ref this.aiRenderStepsText, value))
                {
                    this.CommitAiRenderSettings();
                }
            }
        }

        public string AiRenderSettingsErrorMessage
        {
            get => this.aiRenderSettingsErrorMessage;
            private set
            {
                if (this.SetProperty(ref this.aiRenderSettingsErrorMessage, value))
                {
                    this.OnPropertyChanged(nameof(this.HasAiRenderSettingsError));
                }
            }
        }

        public IReadOnlyList<StableDiffusionRuntime> AiRenderRuntimeOptions => aiRenderRuntimeOptions;

        public StableDiffusionRuntime SelectedAiRenderRuntime
        {
            get => this.selectedAiRenderRuntime;
            set
            {
                if (this.SetProperty(ref this.selectedAiRenderRuntime, value))
                {
                    this.launcherState.SetAiRenderRuntime(value);
                }
            }
        }

        public bool HasAiRenderSettingsError => !string.IsNullOrWhiteSpace(this.AiRenderSettingsErrorMessage);

        public bool CanGenerateRender
        {
            get => this.canGenerateRender;
            private set => this.SetProperty(ref this.canGenerateRender, value);
        }

        public bool IsAiSettingsPanelOpen
        {
            get => this.isAiSettingsPanelOpen;
            set
            {
                if (this.SetProperty(ref this.isAiSettingsPanelOpen, value))
                {
                    this.launcherState.SetAiSettingsPanelOpen(value);
                }
            }
        }

        public bool ShowViewportPlaceholder
        {
            get => this.showViewportPlaceholder;
            private set => this.SetProperty(ref this.showViewportPlaceholder, value);
        }

        public bool ShowViewportSurface
        {
            get => this.showViewportSurface;
            private set => this.SetProperty(ref this.showViewportSurface, value);
        }

        public bool ShowViewportSpinner
        {
            get => this.showViewportSpinner;
            private set => this.SetProperty(ref this.showViewportSpinner, value);
        }

        public bool ShowRenderSpinner
        {
            get => this.showRenderSpinner;
            private set
            {
                if (this.SetProperty(ref this.showRenderSpinner, value))
                {
                    this.OnPropertyChanged(nameof(this.ShowResultImage));
                    this.OnPropertyChanged(nameof(this.ShowRenderPlaceholder));
                }
            }
        }

        public Bitmap? ResultImage
        {
            get => this.resultImage;
            private set
            {
                if (!ReferenceEquals(this.resultImage, value))
                {
                    this.resultImage?.Dispose();
                    this.resultImage = value;
                    this.OnPropertyChanged();
                    this.OnPropertyChanged(nameof(this.HasResultImage));
                    this.OnPropertyChanged(nameof(this.ShowResultImage));
                    this.OnPropertyChanged(nameof(this.ShowRenderPlaceholder));
                }
            }
        }

        public Bitmap? ViewportCaptureImage
        {
            get => this.viewportCaptureImage;
            private set
            {
                if (!ReferenceEquals(this.viewportCaptureImage, value))
                {
                    this.viewportCaptureImage?.Dispose();
                    this.viewportCaptureImage = value;
                    this.OnPropertyChanged();
                }
            }
        }

        public bool HasResultImage => this.ResultImage != null;

        public bool ShowResultImage => this.HasResultImage && !this.ShowRenderSpinner;

        public bool ShowRenderPlaceholder => !this.ShowRenderSpinner && !this.HasResultImage;

        public void RequestGenerateAiRender()
        {
            if (this.CanGenerateRender)
            {
                MyApplication.RequestAiRenderGeneration();
            }
        }

        public void LoadRuntimeAsset(string filePath)
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                MyApplication.NewRuntimeAssetToLoad(filePath);
            }
        }

        public void Dispose()
        {
            this.renderDurationTimer.Stop();
            this.renderDurationTimer.Tick -= this.OnRenderDurationTimerTick;
            this.launcherState.PropertyChanged -= this.OnLauncherStateChanged;
            this.ViewportCaptureImage = null;
            this.ResultImage = null;
        }

        private void OnLauncherStateChanged(object? sender, PropertyChangedEventArgs e)
        {
            _ = Dispatcher.UIThread.InvokeAsync(this.RefreshFromState);
        }

        private void RefreshFromState()
        {
            this.SupportedExtensionsText = this.launcherState.SupportedExtensionsText;
            this.StatusMessage = this.launcherState.StatusMessage;
            this.LastRenderDurationText = this.launcherState.GetRenderDurationText();
            this.AiRenderPrompt = this.launcherState.AiRenderPrompt;
            this.AiRenderCfgScaleText = this.launcherState.AiRenderCfgScale.ToString("0.##", CultureInfo.InvariantCulture);
            this.AiRenderStepsText = this.launcherState.AiRenderSteps.ToString(CultureInfo.InvariantCulture);
            this.SelectedAiRenderRuntime = this.launcherState.AiRenderRuntime;
            this.IsAiSettingsPanelOpen = this.launcherState.IsAiSettingsPanelOpen;
            this.ShowViewportSurface = this.launcherState.ShowViewportSurface;
            this.ShowViewportPlaceholder = this.launcherState.ShowViewportPlaceholder;
            this.ShowViewportSpinner = this.launcherState.ShowViewportSpinner;
            this.ShowRenderSpinner = this.launcherState.ShowRenderSpinner;
            this.UpdateViewportCaptureImage(this.launcherState.ViewportCaptureImagePath);
            this.UpdateResultImage(this.launcherState.ResultImagePath);
            this.UpdateRenderDurationTimer();
            this.RefreshGenerateAvailability();
        }

        private void CommitAiRenderSettings()
        {
            var prompt = this.AiRenderPrompt ?? string.Empty;
            if (string.IsNullOrWhiteSpace(prompt))
            {
                this.AiRenderSettingsErrorMessage = "Prompt cannot be empty.";
                this.RefreshGenerateAvailability();
                return;
            }

            if (!TryParseFloat(this.AiRenderCfgScaleText, out var cfgScale) || cfgScale <= 0)
            {
                this.AiRenderSettingsErrorMessage = "Cfg-scale must be a number greater than 0.";
                this.RefreshGenerateAvailability();
                return;
            }

            if (!TryParseInt(this.AiRenderStepsText, out var steps) || steps <= 0)
            {
                this.AiRenderSettingsErrorMessage = "Steps must be an integer greater than 0.";
                this.RefreshGenerateAvailability();
                return;
            }

            this.launcherState.SetAiRenderPrompt(prompt);
            this.launcherState.SetAiRenderCfgScale(cfgScale);
            this.launcherState.SetAiRenderSteps(steps);
            this.AiRenderSettingsErrorMessage = string.Empty;
            this.RefreshGenerateAvailability();
        }

        private void RefreshGenerateAvailability()
        {
            this.CanGenerateRender = this.launcherState.CanGenerateRender && !this.HasAiRenderSettingsError;
        }

        private void UpdateRenderDurationTimer()
        {
            if (this.launcherState.IsGeneratingRender)
            {
                if (!this.renderDurationTimer.IsEnabled)
                {
                    this.renderDurationTimer.Start();
                }
            }
            else
            {
                this.renderDurationTimer.Stop();
            }
        }

        private void OnRenderDurationTimerTick(object? sender, EventArgs e)
        {
            this.LastRenderDurationText = this.launcherState.GetRenderDurationText();
        }

        private static bool TryParseFloat(string? text, out float value)
        {
            text = text?.Trim();
            return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                || float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseInt(string? text, out int value)
        {
            text = text?.Trim();
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
                || int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private void UpdateViewportCaptureImage(string? imagePath)
        {
            if (string.Equals(this.loadedViewportCaptureImagePath, imagePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.loadedViewportCaptureImagePath = imagePath;

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                this.ViewportCaptureImage = null;
                return;
            }

            using var stream = File.OpenRead(imagePath);
            this.ViewportCaptureImage = new Bitmap(stream);
        }

        private void UpdateResultImage(string? imagePath)
        {
            if (string.Equals(this.loadedImagePath, imagePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.loadedImagePath = imagePath;

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                this.ResultImage = null;
                return;
            }

            using var stream = File.OpenRead(imagePath);
            this.ResultImage = new Bitmap(stream);
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            this.OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
