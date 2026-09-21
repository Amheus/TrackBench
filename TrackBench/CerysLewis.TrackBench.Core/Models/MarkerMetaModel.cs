using CerysLewis.TrackBench.Core.Enumerators;
using System;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.Core.Models
{
    public sealed class MarkerMetaModel
    {
        public long FramePosition { get; set; }
        public string Label { get; set; } = "";
        public MarkerSourceEnum Source { get; set; } = MarkerSourceEnum.User;
    }
}
