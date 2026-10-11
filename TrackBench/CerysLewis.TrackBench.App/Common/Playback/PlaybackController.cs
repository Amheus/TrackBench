using CerysLewis.TrackBench.Core.Audio;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Threading;

namespace CerysLewis.TrackBench.App.Common.Playback
{
    public sealed class PlaybackController : IDisposable
    {
        private readonly WaveOut _output = new();
        private readonly ChannelMixSampleProvider _mix;
        private readonly DispatcherTimer _positionTimer;

        public event Action<long>? PositionChanged;

        public PlaybackController(IChannelAudioSource source)
        {
            _mix = new ChannelMixSampleProvider(source);
            _output.Init(_mix);

            _positionTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(25)
            };
            _positionTimer.Tick += (_, _) => PositionChanged?.Invoke(_mix.PositionFrame);
        }

        public bool IsPlaying => _output.PlaybackState == PlaybackState.Playing;

        public void Play()
        {
            _output.Play();
            _positionTimer.Start();
        }

        public void Pause()
        {
            _output.Pause();
            _positionTimer.Stop();
        }

        public void Seek(long frame)
        {
            _mix.PositionFrame = frame;
            PositionChanged?.Invoke(frame);
        }

        public void Dispose()
        {
            _positionTimer.Stop();
            _output.Dispose();
        }
    }
}
