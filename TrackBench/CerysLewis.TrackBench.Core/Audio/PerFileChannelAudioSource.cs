using System.Buffers;
using Microsoft.Win32.SafeHandles;
using CerysLewis.TrackBench.Core.Sessions;

namespace CerysLewis.TrackBench.Core.Audio
{
    public sealed class PerFileChannelAudioSource : IChannelAudioSource
    {
        private readonly IReadOnlyList<WavFileHeader> _channelFiles;
        private readonly SafeFileHandle?[] _openHandles;

        public PerFileChannelAudioSource(IReadOnlyList<WavFileHeader> channelFiles)
        {
            _channelFiles = channelFiles;
            _openHandles = new SafeFileHandle?[channelFiles.Count];
        }

        public int ChannelCount
            => _channelFiles.Count;
        public uint SampleRate
            => _channelFiles.Count > 0 ? _channelFiles[0].SampleRate : 0;
        public long TotalFrameCount
            => _channelFiles.Count > 0 ? _channelFiles.Min(f => f.FrameCount) : 0;

        public int ReadChannel(long startFrame, int frameCount, int channelIndex, Span<float> destination)
        {
            if (channelIndex < 0 || channelIndex >= _channelFiles.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(channelIndex),
                    $"channelIndex {channelIndex} is out of range - this source has {_channelFiles.Count} channel file(s)."
                );
            }

            var header = _channelFiles[channelIndex];
            int bytesPerSample = header.BitsPerSample / 8;

            var handle = _openHandles[channelIndex]
                ??= File.OpenHandle(
                    header.FilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    FileOptions.RandomAccess
                );

            long framesAvailable = Math.Max(0, header.FrameCount - startFrame);
            int toRead = (int)Math.Min(frameCount, framesAvailable);
            if (toRead <= 0)
            {
                return 0;
            }

            int byteCount = toRead * bytesPerSample;
            byte[] raw = ArrayPool<byte>.Shared.Rent(byteCount);
            try
            {
                int gotBytes = RandomAccess.Read(
                    handle,
                    raw.AsSpan(0, byteCount),
                    header.DataStartOffset + startFrame * bytesPerSample
                );
                int gotFrames = gotBytes / bytesPerSample;

                for (int i = 0; i < gotFrames; i++)
                {
                    destination[i] = PcmSampleCodec.Decode(
                        raw.AsSpan(i * bytesPerSample, bytesPerSample),
                        bytesPerSample
                    );
                }

                return gotFrames;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(raw);
            }
        }

        public int ReadAllChannels(long startFrame, int frameCount, float[][] destination)
        {
            if (destination.Length != ChannelCount)
            {
                throw new ArgumentException($"destination must have exactly {ChannelCount} channel buffers, got {destination.Length}.", nameof(destination));
            }

            int minGot = frameCount;
            for (int c = 0; c < ChannelCount; c++)
            {
                minGot = Math.Min(
                    minGot,
                    ReadChannel(
                        startFrame,
                        frameCount,
                        c,
                        destination[c]
                    )
                );
            }
            return minGot;
        }

        public void Dispose()
        {
            foreach (var h in _openHandles) h?.Dispose();
        }
    }
}
