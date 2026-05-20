using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using System;

namespace IARendering.Avalonia.Controls
{
    public partial class LoadingSpinner : UserControl
    {
        private static readonly double[] BaseOpacities = [1.00, 0.88, 0.76, 0.64, 0.52, 0.40, 0.28, 0.16];

        private readonly DispatcherTimer timer;
        private Ellipse[] dots = [];
        private int frameOffset;

        public LoadingSpinner()
        {
            InitializeComponent();

            this.timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(80),
            };

            this.timer.Tick += this.TimerTick;
            this.AttachedToVisualTree += this.OnAttachedToVisualTree;
            this.DetachedFromVisualTree += this.OnDetachedFromVisualTree;
            this.dots =
            [
                this.FindControl<Ellipse>("Dot0"),
                this.FindControl<Ellipse>("Dot1"),
                this.FindControl<Ellipse>("Dot2"),
                this.FindControl<Ellipse>("Dot3"),
                this.FindControl<Ellipse>("Dot4"),
                this.FindControl<Ellipse>("Dot5"),
                this.FindControl<Ellipse>("Dot6"),
                this.FindControl<Ellipse>("Dot7"),
            ];
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
            if (this.dots.Length != BaseOpacities.Length)
            {
                return;
            }

            this.frameOffset = (this.frameOffset + 1) % this.dots.Length;

            for (int index = 0; index < this.dots.Length; index++)
            {
                int opacityIndex = (index + this.frameOffset) % BaseOpacities.Length;
                this.dots[index].Opacity = BaseOpacities[opacityIndex];
            }
        }
    }
}
