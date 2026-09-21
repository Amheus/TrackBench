using CerysLewis.TrackBench.Core.Audio;
using System;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.Core.Sessions.Desks.AllenAndHeath.Qu.SB
{
    public sealed class AllenAndHeathQuSbSessionInfo : SessionInfo
    {
        public required string DeskModel { get; init; }
        public required IReadOnlyList<WavFileHeader> ChannelFiles { get; init; }
        public override IChannelAudioSource OpenAudioSource() => new PerFileChannelAudioSource(ChannelFiles);
    }
}
