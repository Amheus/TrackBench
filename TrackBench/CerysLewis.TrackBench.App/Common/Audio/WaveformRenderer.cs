using CerysLewis.TrackBench.App.Common.Audio;

namespace CerysLewis.TrackBench.Core.Audio;

public readonly record struct WaveformColumn(float Min, float Max);

public static class WaveformRenderer
{
    private const double MaxLiveFallbackSamplesPerPixel = 4096;

    public static WaveformColumn[] Render(
        IChannelAudioSource source,
        int channelIndex,
        WaveformPeakCache? cache,
        long visibleStartFrame,
        double samplesPerPixel,
        int pixelWidth
    )
    {
        WaveformColumn[] columns = new WaveformColumn[pixelWidth];

        if (cache is not null && samplesPerPixel >= cache.BucketSizeInFrames)
        {
            RenderFromCache(cache, visibleStartFrame, samplesPerPixel, columns);
        }
        else if (samplesPerPixel <= MaxLiveFallbackSamplesPerPixel)
        {
            RenderFromLiveSamples(source, channelIndex, visibleStartFrame, samplesPerPixel, columns);
        }
        // else: zoomed out far enough that only the cache is cheap enough to use, and the cache is built - render from cache

        return columns;
    }

    private static void RenderFromCache(WaveformPeakCache cache, long visibleStartFrame, double samplesPerPixel, WaveformColumn[] columns)
    {
        for (int column = 0; column < columns.Length; column++)
        {
            long frameStart = visibleStartFrame + (long)(column * samplesPerPixel);
            long frameEnd = visibleStartFrame + (long)((column + 1) * samplesPerPixel);

            int bucketStart = (int)(frameStart / cache.BucketSizeInFrames);
            int bucketEnd = (int)(frameEnd / cache.BucketSizeInFrames);

            float min = float.MaxValue, max = float.MinValue;
            bool any = false;
            for (int b = Math.Max(0, bucketStart); b <= bucketEnd && b < cache.Min.Length; b++)
            {
                min = Math.Min(min, cache.Min[b]);
                max = Math.Max(max, cache.Max[b]);
                any = true;
            }

            columns[column] = any ? new WaveformColumn(min, max) : new WaveformColumn(0, 0);
        }
    }

    private static void RenderFromLiveSamples(IChannelAudioSource source, int channelIndex, long visibleStartFrame, double samplesPerPixel, WaveformColumn[] columns)
    {
        int framesPerColumn = Math.Max(1, (int)Math.Ceiling(samplesPerPixel));
        var buffer = new float[framesPerColumn];

        for (int x = 0; x < columns.Length; x++)
        {
            long frameStart = visibleStartFrame + (long)(x * samplesPerPixel);
            if (frameStart >= source.TotalFrameCount)
            {
                columns[x] = new WaveformColumn(0, 0);
                continue;
            }

            int got = source.ReadChannel(frameStart, framesPerColumn, channelIndex, buffer);
            if (got == 0)
            {
                columns[x] = new WaveformColumn(0, 0);
                continue;
            }

            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < got; i++)
            {
                if (buffer[i] < min)
                {
                    min = buffer[i];
                }
                if (buffer[i] > max)
                {
                    max = buffer[i];
                }
            }
            columns[x] = new WaveformColumn(min, max);
        }
    }
}
