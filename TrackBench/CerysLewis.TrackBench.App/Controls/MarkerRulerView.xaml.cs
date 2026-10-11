using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using CerysLewis.TrackBench.App.ViewModels;
using CerysLewis.TrackBench.Core.Enumerators;

namespace CerysLewis.TrackBench.App.Controls
{
    public partial class MarkerRulerView : UserControl
    {
        public static readonly DependencyProperty TimelineProperty = DependencyProperty.Register(
            nameof(Timeline),
            typeof(TimelineViewModel),
            typeof(MarkerRulerView),
            new PropertyMetadata(null, OnTimelineChanged)
        );

        public static readonly DependencyProperty MarkersProperty = DependencyProperty.Register(
            nameof(Markers),
            typeof(ObservableCollection<MarkerViewModel>),
            typeof(MarkerRulerView),
            new PropertyMetadata(null, OnMarkersChanged)
        );

        public TimelineViewModel? Timeline
        {
            get => (TimelineViewModel?)GetValue(TimelineProperty);
            set => SetValue(TimelineProperty, value);
        }

        public ObservableCollection<MarkerViewModel>? Markers
        {
            get => (ObservableCollection<MarkerViewModel>?)GetValue(MarkersProperty);
            set => SetValue(MarkersProperty, value);
        }

        public event Action<long>? SeekRequested;

        public event Action<long>? MarkerAddRequested;

        public MarkerRulerView()
        {
            InitializeComponent();
            MouseLeftButtonDown += OnMouseLeftButtonDown;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Timeline is null)
            {
                return;
            }

            double x = e.GetPosition(this).X;
            long frame = Math.Max(0, Timeline.PixelToFrame(x));

            if (e.ClickCount >= 2)
            {
                MarkerAddRequested?.Invoke(frame);
            }
            else
            {
                SeekRequested?.Invoke(frame);
            }
        }

        private static void OnTimelineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (MarkerRulerView)d;
            if (e.OldValue is TimelineViewModel oldTl)
            {
                oldTl.PropertyChanged -= view.OnRepaintNeeded;
            }
            if (e.NewValue is TimelineViewModel newTl)
            {
                newTl.PropertyChanged += view.OnRepaintNeeded;
            }
            view.Repaint();
        }

        private static void OnMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (MarkerRulerView)d;

            if (e.OldValue is ObservableCollection<MarkerViewModel> oldMarkers)
            {
                oldMarkers.CollectionChanged -= view.OnMarkersCollectionChanged;
                foreach (var m in oldMarkers)
                {
                    m.PropertyChanged -= view.OnRepaintNeeded;
                }
            }
            if (e.NewValue is ObservableCollection<MarkerViewModel> newMarkers)
            {
                newMarkers.CollectionChanged += view.OnMarkersCollectionChanged;
                foreach (var m in newMarkers)
                {
                    m.PropertyChanged += view.OnRepaintNeeded;
                }
            }
            view.Repaint();
        }

        private void OnMarkersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems is not null)
            {
                foreach (MarkerViewModel m in e.OldItems)
                {
                    m.PropertyChanged -= OnRepaintNeeded;
                }
            }
            if (e.NewItems is not null)
            {
                foreach (MarkerViewModel m in e.NewItems)
                {
                    m.PropertyChanged += OnRepaintNeeded;
                }
            }
            Repaint();
        }

        private void OnRepaintNeeded(object? sender, PropertyChangedEventArgs e) => Repaint();

        private void Repaint()
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

        private static readonly SKColor UserMarkerColor = new(230, 180, 60);
        private static readonly SKColor NativeMarkerColor = new(90, 200, 140);

        private void Surface_PaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
        {
            SKCanvas canvas = e.Surface.Canvas;
            canvas.Clear(new SKColor(30, 30, 30));
            if (Timeline is null)
            {
                return;
            }

            var bounds = e.Surface.Canvas.DeviceClipBounds;
            using var font = new SKFont {
                Size = 12
            };

            if (Markers is not null)
            {
                foreach (var marker in Markers)
                {
                    double x = Timeline.FrameToPixel(marker.FramePosition);
                    if (x < -50 || x > bounds.Width + 50)
                    {
                        continue;
                    }

                    SKColor color = marker.Source == MarkerSourceEnum.Native ? NativeMarkerColor : UserMarkerColor;
                    using SKPaint linePaint = new () { Color = color, StrokeWidth = 1, IsAntialias = false };
                    using SKPaint textPaint = new () { Color = color, IsAntialias = true };

                    canvas.DrawLine((float)x, 0, (float)x, bounds.Height, linePaint);
                    canvas.DrawText(marker.Label, (float)x + 3, bounds.Height - 4, SKTextAlign.Left, font, textPaint);
                }
            }

            if (Timeline.PlayheadFrame is { } playheadFrame)
            {
                double px = Timeline.FrameToPixel(playheadFrame);
                if (px >= -2 && px <= bounds.Width + 2)
                {
                    using SKPaint playheadPaint = new() { Color = SKColors.Red, StrokeWidth = 2, IsAntialias = false };
                    canvas.DrawLine((float)px, 0, (float)px, bounds.Height, playheadPaint);
                }
            }
        }
    }
}
