using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Text;
using NAudio.Wave;

namespace CerysLewis.TrackBench.Core.Audio
{
    public sealed class ChannelMixSampleProvider : ISampleProvider
    {
        private readonly IChannelAudioSource _source;
        private readonly int[] _channelIndices;
        private readonly float _gain;
        private long _positionFrame;
        private float[][] _channelBuffers = [];

        public ChannelMixSampleProvider(IChannelAudioSource source, IReadOnlyList<int>? channelIndices = null)
        {
            _source = source;
            _channelIndices = (channelIndices ?? Enumerable.Range(0, source.ChannelCount)).ToArray();
            _gain = _channelIndices.Length == 0 ? 0f : 1f / _channelIndices.Length;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat((int)source.SampleRate, 2);
        }

        public WaveFormat WaveFormat { get; }

        public long PositionFrame
        {
            get => Interlocked.Read(ref _positionFrame);
            set => Interlocked.Exchange(ref _positionFrame, Math.Clamp(value, 0, _source.TotalFrameCount));
        }

        public int Read(float[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public int Read(Span<float> buffer)
        {
            int framesRequested = buffer.Length / 2;
            EnsureBuffers(framesRequested);

            long startFrame = PositionFrame;
            int framesAvailable = (int)Math.Max(0, Math.Min(framesRequested, _source.TotalFrameCount - startFrame));
            if (framesAvailable == 0)
            {
                return 0;
            }

            int got = _source.ReadAllChannels(startFrame, framesAvailable, _channelBuffers);

            for (int i = 0; i < got; i++)
            {
                float sum = 0f;
                foreach (int ch in _channelIndices)
                {
                    sum += _channelBuffers[ch][i];
                }
                float mixed = sum * _gain;
                buffer[i * 2] = mixed;
                buffer[i * 2 + 1] = mixed;
            }

            PositionFrame = startFrame + got;
            return got * 2;
        }

        private void EnsureBuffers(int framesNeeded)
        {
            if (_channelBuffers.Length == _source.ChannelCount && _channelBuffers.Length > 0 && _channelBuffers[0].Length >= framesNeeded)
            {
                return;
            }

            _channelBuffers = new float[_source.ChannelCount][];
            for (int c = 0; c < _source.ChannelCount; c++)
            {
                _channelBuffers[c] = new float[framesNeeded];
            }
        }
    }
}
