using CerysLewis.TrackBench.Core;
using CerysLewis.TrackBench.Core.Sessions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CerysLewis.TrackBench.App.Common.Audio
{
    public static class WaveformCacheStore
    {
        public static string GetCachePath(SessionInfo session, int bucketSizeInFrames) =>
            Path.Combine(AppPaths.Root, "Sessions", session.Fingerprint, $"Waveforms_{bucketSizeInFrames}.cache");
    }
}
