using CerysLewis.TrackBench.App.Common.Audio;
using CerysLewis.TrackBench.App.Common.Playback;
using CerysLewis.TrackBench.App.ViewModels;
using CerysLewis.TrackBench.Core.Audio;
using CerysLewis.TrackBench.Core.Enumerators;
using CerysLewis.TrackBench.Core.Models;
using CerysLewis.TrackBench.Core.Sessions;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CerysLewis.TrackBench.App
{
    public partial class MainWindow : Window
    {
        private const int BucketSizeInFrames = 256;

        private const double TrackLabelColumnWidth = 148;

        private readonly SessionSourceRegistry _registry = new();
        private CancellationTokenSource? _peakBuildCts;
        private PlaybackController? _playback;

        private SessionInfo? _currentSession;
        private SessionMetadataModel? _currentMetadata;

        private MarkerViewModel? _draggedMarker;
        private Point _dragStartPoint;

        public TimelineViewModel Timeline { get; } = new();
        public ObservableCollection<MarkerViewModel> Markers { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            MarkerRuler.MarkerAddRequested += OnMarkerAddRequested;
            MarkerRuler.SeekRequested += OnSeekRequested;
        }

        private double WaveformViewportWidth
            => Math.Max(1, TrackList.ActualWidth - TrackLabelColumnWidth);

        private void OnSeekRequested(long framePosition)
        {
            Timeline.PlayheadFrame = framePosition;
            _playback?.Seek(framePosition);
        }

        private void OnMarkerAddRequested(long framePosition)
        {
            if (_currentSession is null || _currentMetadata is null)
            {
                return;
            }

            MarkerMetaModel markerMeta = new()
            {
                FramePosition = framePosition,
                Label = $"Marker {_currentMetadata.Markers.Count + 1}",
                Source = MarkerSourceEnum.User
            };
            _currentMetadata.Markers.Add(markerMeta);
            _currentMetadata.Save(_currentSession.MetadataCachePath);

            Markers.Add(new MarkerViewModel(markerMeta.FramePosition, markerMeta.Label, markerMeta.Source));
        }


        private void MarkerListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedMarker = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as MarkerViewModel;
        }

        private void MarkerListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedMarker is null || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            Point current = e.GetPosition(null);
            if (
                Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance
            )
            {
                return;
            }

            DragDrop.DoDragDrop(MarkerListBox, _draggedMarker, DragDropEffects.Move);
        }

        private void MarkerListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(MarkerViewModel)) is not MarkerViewModel dragged)
            {
                return;
            }

            MarkerViewModel? target = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as MarkerViewModel;

            int oldIndex = Markers.IndexOf(dragged);
            int newIndex = target is null ? Markers.Count - 1 : Markers.IndexOf(target);

            if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
            {
                Markers.Move(oldIndex, newIndex);
                SaveMarkerOrder();
            }

            _draggedMarker = null;
        }

        private void SaveMarkerOrder()
        {
            if (_currentSession is null || _currentMetadata is null)
            {
                return;
            }

            _currentMetadata.Markers = Markers
                .Select(m => new MarkerMetaModel {
                    FramePosition = m.FramePosition,
                    Label = m.Label,
                    Source = m.Source,
                })
                .ToList();
            _currentMetadata.Save(_currentSession.MetadataCachePath);
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current is not null)
            {
                if (current is T match)
                {
                    return match;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_playback is null)
            {
                return;
            }

            if (_playback.IsPlaying)
            {
                _playback.Pause();
                PlayPauseButton.Content = "Play ▶";
            }
            else
            {
                _playback.Play();
                PlayPauseButton.Content = "Pause ⏸";
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new() {
                Title = "Select the SD card / USB drive root or a session folder"
            };
            if (dialog.ShowDialog() != true) return;

            var match = _registry.DetectAndScan(dialog.FolderName)
                .SelectMany(r => r.Sessions.Select(s => (Source: r.Source, Session: s)))
                .FirstOrDefault();

            if (match.Session is null)
            {
                SessionSummaryText.Text = "No sessions found in that folder.";
                return;
            }

            LoadSession(match.Source, match.Session);
        }

        private void LoadSession(ISessionSource source, SessionInfo session)
        {
            _peakBuildCts?.Cancel();
            CancellationTokenSource cancellationTokenSource = new();
            _peakBuildCts = cancellationTokenSource;

            _playback?.Dispose();
            _playback = null;
            PlayPauseButton.Content = "Play ▶";
            Timeline.PlayheadFrame = null;

            IChannelAudioSource audio = session.OpenAudioSource();
            SessionMetadataModel metadata = SessionMetadataModel.LoadOrCreate(session.MetadataCachePath, session.DisplayName, session.ChannelCount);

            if (!metadata.NativeMarkersImported)
            {
                if (source is INativeMarkerReader nativeReader)
                {
                    metadata.Markers.AddRange(nativeReader.ReadNativeMarkers(session));
                }
                metadata.NativeMarkersImported = true;
                metadata.Save(session.MetadataCachePath);
            }

            _currentSession = session;
            _currentMetadata = metadata;

            Timeline.TotalFrameCount = session.TotalFrameCount;
            Timeline.VisibleStartFrame = 0;

            Timeline.SamplesPerPixel = Math.Max(1, session.TotalFrameCount / WaveformViewportWidth);

            Markers.Clear();
            foreach (var m in metadata.Markers)
            {
                Markers.Add(new MarkerViewModel(m.FramePosition, m.Label, m.Source));
            }

            List<TrackViewModel> tracks = new();
            for (int channel = 0; channel < session.ChannelCount; channel++)
            {
                var trackMeta = metadata.Tracks.FirstOrDefault(t => t.ChannelIndex == channel);
                tracks.Add(
                    new TrackViewModel(
                        channel,
                        trackMeta?.Name ?? $"Track {channel + 1:00}",
                        audio
                    )
                );
            }
            TrackList.ItemsSource = tracks;

            HorizontalScroll.Minimum = 0;
            HorizontalScroll.Maximum = session.TotalFrameCount;
            HorizontalScroll.Value = 0;

            _playback = new PlaybackController(audio);
            _playback.PositionChanged += frame => Dispatcher.BeginInvoke(() => Timeline.PlayheadFrame = frame);

            string cachePath = WaveformCacheStore.GetCachePath(session, BucketSizeInFrames);
            WaveformPeakCache[]? cached = WaveformPeakCache.LoadAllFrom(cachePath);

            if (cached is not null && cached.Length == tracks.Count)
            {
                for (int channel = 0; channel < tracks.Count; channel++)
                {
                    tracks[channel].PeakCache = cached[channel];
                }
                SessionSummaryText.Text = $"{session.DisplayName} - {session.ChannelCount}ch @ {session.SampleRate}Hz, {session.Duration:hh\\:mm\\:ss}";
                return;
            }

            SessionSummaryText.Text = $"{session.DisplayName} - {session.ChannelCount}ch @ {session.SampleRate}Hz, {session.Duration:hh\\:mm\\:ss} (analysing waveform...)";
            BuildProgressBar.Visibility = Visibility.Visible;
            BuildProgressBar.Value = 0;

            DateTime lastRedraw = DateTime.MinValue;
            var progress = new Progress<WaveformPeakCache.BuildProgress>(p =>
            {
                BuildProgressBar.Value = p.TotalFrames == 0
                    ? 0
                    : (double)p.FramesProcessed / p.TotalFrames;

                DateTime now = DateTime.UtcNow;
                if (
                    now - lastRedraw < TimeSpan.FromMilliseconds(500)
                    && p.FramesProcessed < p.TotalFrames
                )
                {
                    return;
                }
                lastRedraw = now;

                for (int channel = 0; channel < tracks.Count; channel++)
                {
                    tracks[channel].PeakCache = p.Caches[channel];
                    tracks[channel].NotifyWaveformChanged();
                }
            });

            Task.Factory.StartNew(() =>
            {
                try
                {
                    var caches = WaveformPeakCache.BuildAll(audio, BucketSizeInFrames, progress, cancellationTokenSource.Token);
                    WaveformPeakCache.SaveAllTo(cachePath, caches);

                    Dispatcher.Invoke(() =>
                    {
                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            return;
                        }
                        BuildProgressBar.Visibility = Visibility.Collapsed;
                        SessionSummaryText.Text = $"{session.DisplayName} - {session.ChannelCount}ch @ {session.SampleRate}Hz, {session.Duration:hh\\:mm\\:ss}";
                    });
                }
                catch (OperationCanceledException)
                {
                }
            }, cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
            => Timeline.ZoomAt(0, 2, WaveformViewportWidth);

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
            => Timeline.ZoomAt(0, 0.5, WaveformViewportWidth);

        private void HorizontalScroll_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
            Timeline.VisibleStartFrame = (long)e.NewValue;
    }
}
