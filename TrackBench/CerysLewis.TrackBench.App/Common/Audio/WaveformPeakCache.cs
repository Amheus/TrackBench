using CerysLewis.TrackBench.Core.Audio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CerysLewis.TrackBench.App.Common.Audio
{
    public sealed class WaveformPeakCache
    {
        public int BucketSizeInFrames { get; }
        public float[] Min { get; }
        public float[] Max { get; }

        internal WaveformPeakCache(int bucketSizeInFrames, float[] min, float[] max)
        {
            BucketSizeInFrames = bucketSizeInFrames;
            Min = min;
            Max = max;
        }

        public readonly record struct BuildProgress(long FramesProcessed, long TotalFrames, WaveformPeakCache[] Caches);

        public static WaveformPeakCache[] BuildAll(
            IChannelAudioSource source,
            int bucketSizeInFrames,
            IProgress<BuildProgress>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            int channelCount = source.ChannelCount;
            long total = source.TotalFrameCount;
            int bucketCount = total == 0 ? 0 : (int)((total + bucketSizeInFrames - 1) / bucketSizeInFrames);

            var caches = new WaveformPeakCache[channelCount];
            for (int c = 0; c < channelCount; c++)
            {
                caches[c] = new WaveformPeakCache(bucketSizeInFrames, new float[bucketCount], new float[bucketCount]);
            }

            var bucketMin = new float[channelCount];
            var bucketMax = new float[channelCount];
            Array.Fill(bucketMin, float.MaxValue);
            Array.Fill(bucketMax, float.MinValue);
            int framesInBucket = 0;
            int bucket = 0;

            const int readChunkFrames = 1 << 16;
            var buffers = new float[channelCount][];
            for (int c = 0; c < channelCount; c++)
            {
                buffers[c] = new float[readChunkFrames];
            }

            Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            TimeSpan lastReport = TimeSpan.Zero;

            long frame = 0;
            while (frame < total)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int toRead = (int)Math.Min(readChunkFrames, total - frame);
                int got = source.ReadAllChannels(frame, toRead, buffers);
                if (got == 0)
                {
                    break;
                }

                for (int i = 0; i < got; i++)
                {
                    for (int channel = 0; channel < channelCount; channel++)
                    {
                        float s = buffers[channel][i];
                        if (s < bucketMin[channel])
                        {
                            bucketMin[channel] = s;
                        }
                        if (s > bucketMax[channel])
                        {
                            bucketMax[channel] = s;
                        }
                    }
                    framesInBucket++;

                    if (framesInBucket == bucketSizeInFrames && bucket < bucketCount)
                    {
                        for (int channel = 0; channel < channelCount; channel++)
                        {
                            caches[channel].Min[bucket] = bucketMin[channel];
                            caches[channel].Max[bucket] = bucketMax[channel];
                            bucketMin[channel] = float.MaxValue;
                            bucketMax[channel] = float.MinValue;
                        }
                        bucket++;
                        framesInBucket = 0;
                    }
                }

                frame += got;

                if (progress is not null && stopwatch.Elapsed - lastReport > TimeSpan.FromMilliseconds(100))
                {
                    lastReport = stopwatch.Elapsed;
                    progress.Report(
                        new BuildProgress(
                            frame,
                            total,
                            caches)
                    );
                }
            }

            if (framesInBucket > 0 && bucket < bucketCount)
            {
                for (int channel = 0; channel < channelCount; channel++)
                {
                    caches[channel].Min[bucket] = bucketMin[channel];
                    caches[channel].Max[bucket] = bucketMax[channel];
                }
            }

            progress?.Report(new BuildProgress(total, total, caches));
            return caches;
        }

        public static void SaveAllTo(string path, WaveformPeakCache[] caches)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tempPath = path + ".tmp";
            using (FileStream fileStream = new(tempPath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter binaryWriter = new(fileStream))
            {
                binaryWriter.Write(caches.Length);
                binaryWriter.Write(caches.Length > 0 ? caches[0].BucketSizeInFrames : 0);
                foreach (var c in caches)
                {
                    binaryWriter.Write(c.Min.Length);
                    foreach (var v in c.Min) binaryWriter.Write(v);
                    foreach (var v in c.Max) binaryWriter.Write(v);
                }
            }
            File.Move(tempPath, path, overwrite: true);
        }

        public static WaveformPeakCache[]? LoadAllFrom(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
                using var binaryReader = new BinaryReader(fileStream);
                int channelCount = binaryReader.ReadInt32();
                int bucketSize = binaryReader.ReadInt32();

                var result = new WaveformPeakCache[channelCount];
                for (int channel = 0; channel < channelCount; channel++)
                {
                    int count = binaryReader.ReadInt32();
                    float[] min = new float[count];
                    float[] max = new float[count];
                    for (int i = 0; i < count; i++)
                    {
                        min[i] = binaryReader.ReadSingle();
                    }
                    for (int i = 0; i < count; i++)
                    {
                        max[i] = binaryReader.ReadSingle();
                    }
                    result[channel] = new WaveformPeakCache(bucketSize, min, max);
                }
                return result;
            }
            catch (Exception ex) when (ex is IOException or EndOfStreamException)
            {
                return null;
            }
        }
    }
}
