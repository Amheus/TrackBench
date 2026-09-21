using System;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.Core.Audio
{
    public interface IChannelAudioSource : IDisposable
    {
        int ChannelCount { get; }
        uint SampleRate { get; }
        long TotalFrameCount { get; }

        int ReadChannel(long startFrame, int frameCount, int channelIndex, Span<float> destination);
        int ReadAllChannels(long startFrame, int frameCount, float[][] destination);
    }
}
