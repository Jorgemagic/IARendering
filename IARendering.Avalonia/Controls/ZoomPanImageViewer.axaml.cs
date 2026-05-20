using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using System;

namespace IARendering.Avalonia.Controls
{
    public partial class ZoomPanImageViewer : UserControl
    {
        public static readonly StyledProperty<Bitmap?> SourceProperty =
            AvaloniaProperty.Register<ZoomPanImageViewer, Bitmap?>(nameof(Source));

        private Border? viewport;
        private Image? imageHost;
        private bool isPanning;
        private Point lastPointerPosition;
        private Vector panOffset;
        private double zoomFactor = 1.0;

        public ZoomPanImageViewer()
        {
            InitializeComponent();

            this.viewport = this.FindControl<Border>("Viewport");
            this.imageHost = this.FindControl<Image>("ImageHost");

            this.AttachedToVisualTree += this.OnAttachedToVisualTree;
            this.DetachedFromVisualTree += this.OnDetachedFromVisualTree;
        }

        public Bitmap? Source
        {
            get => this.GetValue(SourceProperty);
            set => this.SetValue(SourceProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SourceProperty)
            {
                this.ResetView();
                this.UpdateImageLayout();
            }
        }

        private void OnAttachedToVisualTree(object? sender, global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            if (this.viewport == null)
            {
                return;
            }

            this.viewport.SizeChanged += this.ViewportSizeChanged;
            this.viewport.PointerWheelChanged += this.ViewportPointerWheelChanged;
            this.viewport.PointerPressed += this.ViewportPointerPressed;
            this.viewport.PointerMoved += this.ViewportPointerMoved;
            this.viewport.PointerReleased += this.ViewportPointerReleased;
            this.viewport.PointerCaptureLost += this.ViewportPointerCaptureLost;
            this.viewport.DoubleTapped += this.ViewportDoubleTapped;
        }

        private void OnDetachedFromVisualTree(object? sender, global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            if (this.viewport == null)
            {
                return;
            }

            this.viewport.SizeChanged -= this.ViewportSizeChanged;
            this.viewport.PointerWheelChanged -= this.ViewportPointerWheelChanged;
            this.viewport.PointerPressed -= this.ViewportPointerPressed;
            this.viewport.PointerMoved -= this.ViewportPointerMoved;
            this.viewport.PointerReleased -= this.ViewportPointerReleased;
            this.viewport.PointerCaptureLost -= this.ViewportPointerCaptureLost;
            this.viewport.DoubleTapped -= this.ViewportDoubleTapped;
        }

        private void ViewportSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            this.UpdateImageLayout();
        }

        private void ViewportDoubleTapped(object? sender, TappedEventArgs e)
        {
            this.ResetView();
            this.UpdateImageLayout();
        }

        private void ViewportPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (this.Source == null || this.viewport == null)
            {
                return;
            }

            if (!e.GetCurrentPoint(this.viewport).Properties.IsLeftButtonPressed)
            {
                return;
            }

            this.isPanning = true;
            this.lastPointerPosition = e.GetPosition(this.viewport);
            e.Pointer.Capture(this.viewport);
            e.Handled = true;
        }

        private void ViewportPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!this.isPanning || this.viewport == null)
            {
                return;
            }

            var currentPosition = e.GetPosition(this.viewport);
            var delta = currentPosition - this.lastPointerPosition;
            this.lastPointerPosition = currentPosition;

            this.panOffset += new Vector(delta.X, delta.Y);
            this.UpdateImageLayout();
            e.Handled = true;
        }

        private void ViewportPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (this.isPanning)
            {
                e.Pointer.Capture(null);
                this.EndPan();
            }

            e.Handled = true;
        }

        private void ViewportPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            this.EndPan();
        }

        private void ViewportPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (this.Source == null || this.viewport == null)
            {
                return;
            }

            var viewportBounds = this.viewport.Bounds;
            if (viewportBounds.Width <= 0 || viewportBounds.Height <= 0)
            {
                return;
            }

            var oldScale = this.GetEffectiveScale();
            var oldOrigin = this.GetImageOrigin(oldScale);
            var pointerPosition = e.GetPosition(this.viewport);

            var imageSpaceX = (pointerPosition.X - oldOrigin.X) / oldScale;
            var imageSpaceY = (pointerPosition.Y - oldOrigin.Y) / oldScale;

            var zoomDelta = e.Delta.Y > 0 ? 1.15 : 1 / 1.15;
            this.zoomFactor = Math.Clamp(this.zoomFactor * zoomDelta, 1.0, 12.0);

            var newScale = this.GetEffectiveScale();
            var centeredOrigin = this.GetCenteredOrigin(newScale);

            this.panOffset = new Vector(
                pointerPosition.X - (imageSpaceX * newScale) - centeredOrigin.X,
                pointerPosition.Y - (imageSpaceY * newScale) - centeredOrigin.Y);

            this.UpdateImageLayout();
            e.Handled = true;
        }

        private void EndPan()
        {
            if (!this.isPanning || this.viewport == null)
            {
                return;
            }

            this.isPanning = false;
        }

        private void ResetView()
        {
            this.zoomFactor = 1.0;
            this.panOffset = default;
        }

        private void UpdateImageLayout()
        {
            if (this.imageHost == null || this.viewport == null)
            {
                return;
            }

            this.imageHost.Source = this.Source;

            if (this.Source == null)
            {
                this.imageHost.IsVisible = false;
                return;
            }

            this.imageHost.IsVisible = true;

            var scale = this.GetEffectiveScale();
            var origin = this.GetImageOrigin(scale);
            var scaledWidth = this.Source.PixelSize.Width * scale;
            var scaledHeight = this.Source.PixelSize.Height * scale;

            this.imageHost.Width = scaledWidth;
            this.imageHost.Height = scaledHeight;

            Canvas.SetLeft(this.imageHost, origin.X);
            Canvas.SetTop(this.imageHost, origin.Y);
        }

        private double GetEffectiveScale()
        {
            if (this.Source == null || this.viewport == null)
            {
                return 1.0;
            }

            var viewportBounds = this.viewport.Bounds;
            if (viewportBounds.Width <= 0 || viewportBounds.Height <= 0)
            {
                return 1.0;
            }

            var fitScale = Math.Min(
                viewportBounds.Width / this.Source.PixelSize.Width,
                viewportBounds.Height / this.Source.PixelSize.Height);

            return fitScale * this.zoomFactor;
        }

        private Point GetImageOrigin(double scale)
        {
            if (this.Source == null || this.viewport == null)
            {
                return default;
            }

            var centeredOrigin = this.GetCenteredOrigin(scale);
            var scaledWidth = this.Source.PixelSize.Width * scale;
            var scaledHeight = this.Source.PixelSize.Height * scale;
            var viewportBounds = this.viewport.Bounds;

            var maxPanX = Math.Max(0, (scaledWidth - viewportBounds.Width) * 0.5);
            var maxPanY = Math.Max(0, (scaledHeight - viewportBounds.Height) * 0.5);

            var clampedPanX = Math.Clamp(this.panOffset.X, -maxPanX, maxPanX);
            var clampedPanY = Math.Clamp(this.panOffset.Y, -maxPanY, maxPanY);
            this.panOffset = new Vector(clampedPanX, clampedPanY);

            return new Point(centeredOrigin.X + clampedPanX, centeredOrigin.Y + clampedPanY);
        }

        private Point GetCenteredOrigin(double scale)
        {
            if (this.Source == null || this.viewport == null)
            {
                return default;
            }

            var viewportBounds = this.viewport.Bounds;
            var scaledWidth = this.Source.PixelSize.Width * scale;
            var scaledHeight = this.Source.PixelSize.Height * scale;

            return new Point(
                (viewportBounds.Width - scaledWidth) * 0.5,
                (viewportBounds.Height - scaledHeight) * 0.5);
        }
    }
}
