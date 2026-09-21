using CerysLewis.TrackBench.Core.Audio;
using System;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.Core.Sessions.Desks.Behringer.X32
{
    public sealed class BehringerX32SessionInfo : SessionInfo
    {
        public required IReadOnlyList<WavFileHeader> Chunks { get; init; }
        public override IChannelAudioSource OpenAudioSource() => new InterleavedMultiWaveStream(Chunks);
    }
}
