using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace IARendering.Avalonia.Controls
{
    public partial class LoadingSpinner : UserControl
    {
        private readonly DispatcherTimer timer;
        private Path? spinnerArc;

        public LoadingSpinner()
        {
            InitializeComponent();

            this.timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16),
            };

            this.timer.Tick += this.TimerTick;
            this.AttachedToVisualTree += this.OnAttachedToVisualTree;
            this.DetachedFromVisualTree += this.OnDetachedFromVisualTree;

            this.spinnerArc = this.FindControl<Path>("SpinnerArc");
        }

        private void OnAttachedToVisualTree(object? sender, global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            this.timer.Start();
        }

        private void OnDetachedFromVisualTree(object? sender, global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            this.timer.Stop();
        }

        private void TimerTick(object? sender, EventArgs e)
        {
            if (this.spinnerArc?.RenderTransform is RotateTransform rotateTransform)
            {
                rotateTransform.Angle = (rotateTransform.Angle + 6) % 360;
            }
        }
    }
}
