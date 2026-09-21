using CerysLewis.TrackBench.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.Core.Sessions
{
    public interface INativeMarkerReader
    {
        IReadOnlyList<MarkerMetaModel> ReadNativeMarkers(SessionInfo session);
    }
}
