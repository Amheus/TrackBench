using CerysLewis.TrackBench.Core.Enumerators;
using System;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.Core.Models
{
    public sealed class RangeMetaModel
    {
        public long StartFrame { get; set; }
        public long EndFrame { get; set; }
        public RangeCategoryEnum Category { get; set; }
        public string? CustomCategoryName { get; set; }
        public string? Label { get; set; }
    }
}
