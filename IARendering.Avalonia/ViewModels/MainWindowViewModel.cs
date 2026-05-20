using Avalonia.Media.Imaging;
using Avalonia.Threading;
using IARendering.Features.Launcher;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace IARendering.Avalonia.ViewModels
{
    public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly LauncherStateService launcherState;
        private Bitmap? viewportCaptureImage;
        private Bitmap? resultImage;
        private string? loadedViewportCaptureImagePath;
        private string? loadedImagePath;
        private string supportedExtensionsText = "GLB, STL or OBJ";
        private string statusMessage = "Drag and drop a model to begin.";
        private string lastRenderDurationText = "Last Render Time: --";
        private bool canGenerateRender;
        private bool showViewportSurface;
        private bool showViewportPlaceholder = true;
        private bool showViewportSpinner;
        private bool showRenderSpinner;

        public MainWindowViewModel(LauncherStateService launcherState)
        {
            this.launcherState = launcherState ?? throw new ArgumentNullException(nameof(launcherState));
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

        public bool CanGenerateRender
        {
            get => this.canGenerateRender;
            private set => this.SetProperty(ref this.canGenerateRender, value);
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
            this.LastRenderDurationText = this.launcherState.LastRenderDurationText;
            this.CanGenerateRender = this.launcherState.CanGenerateRender;
            this.ShowViewportSurface = this.launcherState.ShowViewportSurface;
            this.ShowViewportPlaceholder = this.launcherState.ShowViewportPlaceholder;
            this.ShowViewportSpinner = this.launcherState.ShowViewportSpinner;
            this.ShowRenderSpinner = this.launcherState.ShowRenderSpinner;
            this.UpdateViewportCaptureImage(this.launcherState.ViewportCaptureImagePath);
            this.UpdateResultImage(this.launcherState.ResultImagePath);
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
