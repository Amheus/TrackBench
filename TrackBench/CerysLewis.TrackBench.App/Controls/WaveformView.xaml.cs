using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using CerysLewis.TrackBench.App.ViewModels;
using CerysLewis.TrackBench.Core.Audio;

namespace CerysLewis.TrackBench.App.Controls
{
    public partial class WaveformView : UserControl
    {
        public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
            nameof(Track),
            typeof(TrackViewModel),
            typeof(WaveformView),
            new PropertyMetadata(null, OnBindingChanged)
        );

        public static readonly DependencyProperty TimelineProperty = DependencyProperty.Register(
            nameof(Timeline),
            typeof(TimelineViewModel),
            typeof(WaveformView),
            new PropertyMetadata(null, OnBindingChanged)
        );

        public TrackViewModel? Track
        {
            get => (TrackViewModel?)GetValue(TrackProperty);
            set => SetValue(TrackProperty, value);
        }

        public TimelineViewModel? Timeline
        {
            get => (TimelineViewModel?)GetValue(TimelineProperty);
            set => SetValue(TimelineProperty, value);
        }

        public WaveformView() => InitializeComponent();

        private static void OnBindingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (WaveformView)d;

            if (e.OldValue is TimelineViewModel oldTimeline)
            {
                oldTimeline.PropertyChanged -= view.OnRepaintNeeded;
            }
            if (e.NewValue is TimelineViewModel newTimeline)
            {
                newTimeline.PropertyChanged += view.OnRepaintNeeded;
            }
            if (e.OldValue is TrackViewModel oldTrack)
            {
                oldTrack.PropertyChanged -= view.OnRepaintNeeded;
            }
            if (e.NewValue is TrackViewModel newTrack)
            {
                newTrack.PropertyChanged += view.OnRepaintNeeded;
            }

            view.Surface.InvalidateVisual();
        }

        private void OnRepaintNeeded(object? sender, PropertyChangedEventArgs e)
        {
            if (Surface.Dispatcher.CheckAccess())
            {
                Surface.InvalidateVisual();
            }
            else
            {
                Surface.Dispatcher.BeginInvoke(Surface.InvalidateVisual);
            }
        }

        private void Surface_PaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
        {
            SKCanvas? canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            if (Track is null || Timeline is null)
            {
                return;
            }

            int width = e.Surface.Canvas.DeviceClipBounds.Width;
            float midY = e.Surface.Canvas.DeviceClipBounds.Height / 2f;

            WaveformColumn[] columns = WaveformRenderer.Render(
                source: Track.Audio,
                channelIndex: Track.ChannelIndex,
                cache: Track.PeakCache,
                visibleStartFrame: Timeline.VisibleStartFrame,
                samplesPerPixel: Timeline.SamplesPerPixel,
                pixelWidth: width
            );

            using SKPaint paint = new(){
                Color = new SKColor(90, 170, 230),
                StrokeWidth = 1,
                IsAntialias = false
            };
            for (int x = 0; x < columns.Length; x++)
            {
                float yMin = midY - columns[x].Max * midY;
                float yMax = midY - columns[x].Min * midY;
                canvas.DrawLine(x, yMin, x, yMax, paint);
            }

            if (Timeline.PlayheadFrame is { } playheadFrame)
            {
                double px = Timeline.FrameToPixel(playheadFrame);
                if (px >= -2 && px <= width + 2)
                {
                    using SKPaint playheadPaint = new() {
                        Color = SKColors.Red,
                        StrokeWidth = 2,
                        IsAntialias = false
                    };
                    canvas.DrawLine((float)px, 0, (float)px, e.Surface.Canvas.DeviceClipBounds.Height, playheadPaint);
                }
            }
        }
    }
}
