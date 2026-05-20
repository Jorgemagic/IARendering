using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using IARendering.Avalonia.ViewModels;
using IARendering.Features.Launcher;
using System;
using System.IO;
using System.Linq;

namespace IARendering.Avalonia
{
    /// <summary>
    /// The main application window that hosts the Evergine render control.
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly string[] SupportedDropExtensions = [".glb", ".obj", ".stl"];

        /// <summary>
        /// The Evergine render control used for rendering within this window.
        /// </summary>
        private EvergineControl? renderControl;
        private readonly MainWindowViewModel viewModel;

        /// <summary>
        /// Gets the Evergine render control instance associated with this window.
        /// </summary>
        internal EvergineControl? EvergineRenderControl => renderControl;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// Finds and assigns the <see cref="EvergineControl"/> defined in the AXAML layout.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            var app = (App)(Application.Current ?? throw new InvalidOperationException("Avalonia application is not available."));
            var launcherState = app.EvergineApplication?.Container.Resolve<LauncherStateService>()
                ?? throw new InvalidOperationException("Launcher state service is not available.");

            this.viewModel = new MainWindowViewModel(launcherState);
            this.DataContext = this.viewModel;
            this.renderControl = this.FindControl<EvergineControl>("RenderControl");
        }

        private void GenerateAiRender_Click(object? sender, RoutedEventArgs e)
        {
            this.viewModel.RequestGenerateAiRender();
        }

        private void ViewportDragOver(object? sender, DragEventArgs e)
        {
            if (TryGetSupportedFilePath(e.Data, out _))
            {
                e.DragEffects = DragDropEffects.Copy;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void ViewportDrop(object? sender, DragEventArgs e)
        {
            if (TryGetSupportedFilePath(e.Data, out var filePath))
            {
                this.viewModel.LoadRuntimeAsset(filePath);
            }

            e.Handled = true;
        }

        /// <summary>
        /// Called when the window is unloaded. Ensures the Evergine render control
        /// is properly unloaded to release resources.
        /// </summary>
        /// <param name="e">The routed event arguments.</param>
        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            this.viewModel.Dispose();
            this.renderControl?.Unload();
        }

        private static bool TryGetSupportedFilePath(IDataObject dataObject, out string filePath)
        {
            filePath = string.Empty;

            var files = dataObject.GetFiles();
            if (files == null)
            {
                return false;
            }

            foreach (var file in files.OfType<IStorageItem>())
            {
                var localPath = file.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
                {
                    continue;
                }

                var extension = Path.GetExtension(localPath);
                if (SupportedDropExtensions.Any(supportedExtension => string.Equals(supportedExtension, extension, StringComparison.OrdinalIgnoreCase)))
                {
                    filePath = localPath;
                    return true;
                }
            }

            return false;
        }
    }
}
