using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace IARendering.Avalonia.Controls
{
    public partial class ImageCompareViewer : UserControl
    {
        public static readonly StyledProperty<Bitmap?> LeftSourceProperty =
            AvaloniaProperty.Register<ImageCompareViewer, Bitmap?>(nameof(LeftSource));

        public static readonly StyledProperty<Bitmap?> RightSourceProperty =
            AvaloniaProperty.Register<ImageCompareViewer, Bitmap?>(nameof(RightSource));

        private Border? viewport;
        private Image? leftImage;
        private Image? rightImage;
        private Border? dividerLine;
        private double dividerRatio = 0.5;
        private bool isPointerInside;

        public ImageCompareViewer()
        {
            InitializeComponent();

            this.viewport = this.FindControl<Border>("Viewport");
            this.leftImage = this.FindControl<Image>("LeftImage");
            this.rightImage = this.FindControl<Image>("RightImage");
            this.dividerLine = this.FindControl<Border>("DividerLine");

            this.AttachedToVisualTree += this.OnAttachedToVisualTree;
            this.DetachedFromVisualTree += this.OnDetachedFromVisualTree;
        }

        public Bitmap? LeftSource
        {
            get => this.GetValue(LeftSourceProperty);
            set => this.SetValue(LeftSourceProperty, value);
        }

        public Bitmap? RightSource
        {
            get => this.GetValue(RightSourceProperty);
            set => this.SetValue(RightSourceProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == LeftSourceProperty || change.Property == RightSourceProperty)
            {
                this.UpdateLayoutForImages();
            }
        }

        private void OnAttachedToVisualTree(object? sender, global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            if (this.viewport == null)
            {
                return;
            }

            this.viewport.SizeChanged += this.ViewportSizeChanged;
            this.viewport.PointerEntered += this.ViewportPointerEntered;
            this.viewport.PointerExited += this.ViewportPointerExited;
            this.viewport.PointerMoved += this.ViewportPointerMoved;
        }

        private void OnDetachedFromVisualTree(object? sender, global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            if (this.viewport == null)
            {
                return;
            }

            this.viewport.SizeChanged -= this.ViewportSizeChanged;
            this.viewport.PointerEntered -= this.ViewportPointerEntered;
            this.viewport.PointerExited -= this.ViewportPointerExited;
            this.viewport.PointerMoved -= this.ViewportPointerMoved;
        }

        private void ViewportSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            this.UpdateLayoutForImages();
        }

        private void ViewportPointerEntered(object? sender, PointerEventArgs e)
        {
            this.isPointerInside = true;
            this.UpdateDividerFromPointer(e);
            this.UpdateLayoutForImages();
        }

        private void ViewportPointerExited(object? sender, PointerEventArgs e)
        {
            this.isPointerInside = false;
            this.UpdateLayoutForImages();
        }

        private void ViewportPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!this.isPointerInside)
            {
                return;
            }

            this.UpdateDividerFromPointer(e);
            this.UpdateLayoutForImages();
        }

        private void UpdateDividerFromPointer(PointerEventArgs e)
        {
            if (this.viewport == null)
            {
                return;
            }

            var bounds = this.viewport.Bounds;
            if (bounds.Width <= 0)
            {
                return;
            }

            var position = e.GetPosition(this.viewport);
            this.dividerRatio = System.Math.Clamp(position.X / bounds.Width, 0, 1);
        }

        private void UpdateLayoutForImages()
        {
            if (this.viewport == null || this.leftImage == null || this.rightImage == null || this.dividerLine == null)
            {
                return;
            }

            this.leftImage.Source = this.LeftSource;
            this.rightImage.Source = this.RightSource;

            if (this.LeftSource == null || this.RightSource == null)
            {
                this.leftImage.IsVisible = false;
                this.rightImage.IsVisible = false;
                this.dividerLine.IsVisible = false;
                return;
            }

            this.leftImage.IsVisible = true;
            this.rightImage.IsVisible = true;

            var source = this.LeftSource;
            var viewportBounds = this.viewport.Bounds;
            if (viewportBounds.Width <= 0 || viewportBounds.Height <= 0)
            {
                return;
            }

            var scale = System.Math.Min(
                viewportBounds.Width / source.PixelSize.Width,
                viewportBounds.Height / source.PixelSize.Height);

            var imageWidth = source.PixelSize.Width * scale;
            var imageHeight = source.PixelSize.Height * scale;
            var originX = (viewportBounds.Width - imageWidth) * 0.5;
            var originY = (viewportBounds.Height - imageHeight) * 0.5;
            var dividerX = this.dividerRatio * viewportBounds.Width;
            dividerX = System.Math.Clamp(dividerX, originX, originX + imageWidth);
            var clipWidth = dividerX - originX;

            this.rightImage.Width = imageWidth;
            this.rightImage.Height = imageHeight;
            Canvas.SetLeft(this.rightImage, originX);
            Canvas.SetTop(this.rightImage, originY);

            this.leftImage.Width = imageWidth;
            this.leftImage.Height = imageHeight;
            Canvas.SetLeft(this.leftImage, originX);
            Canvas.SetTop(this.leftImage, originY);
            this.leftImage.Clip = new RectangleGeometry(new Rect(0, 0, clipWidth, imageHeight));

            this.dividerLine.Height = imageHeight;
            Canvas.SetLeft(this.dividerLine, dividerX - (this.dividerLine.Width * 0.5));
            Canvas.SetTop(this.dividerLine, originY);
            this.dividerLine.IsVisible = this.isPointerInside;
        }
    }
}
