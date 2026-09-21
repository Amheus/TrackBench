using System;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.Core.Models
{
    public sealed class TrackMetaModel
    {
        public int ChannelIndex { get; set; }
        public string Name { get; set; } = "";
        public bool Excluded { get; set; }
    }
}
