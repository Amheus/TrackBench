using System;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.Core.Audio
{
    public static class PcmSampleCodec
    {
        public static float Decode(ReadOnlySpan<byte> bytes, int bytesPerSample) => bytesPerSample switch
        {
            2 => BitConverter.ToInt16(bytes) / 32768f,
            3 => ((bytes[2] << 24 | bytes[1] << 16 | bytes[0] << 8) >> 8) / 8388608f, // 24-bit signed, sign-extended
            4 => BitConverter.ToInt32(bytes) / 2147483648f,
            _ => throw new NotSupportedException($"Unsupported bit depth: {bytesPerSample * 8}-bit.")
        };
    }
}
