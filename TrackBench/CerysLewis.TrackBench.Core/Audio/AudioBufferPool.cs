
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.Core.Audio
{
    public static class AudioBufferPool
    {
        public static readonly ArrayPool<byte> Instance =
            ArrayPool<byte>.Create(maxArrayLength: 16 * 1024 * 1024, maxArraysPerBucket: 4);
    }
}
