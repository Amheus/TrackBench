using CerysLewis.TrackBench.Core.Sessions;
using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.Core.Audio
{
    public sealed class InterleavedMultiWaveStream : IChannelAudioSource
    {
        private readonly IReadOnlyList<WavFileHeader> _chunks;
        private readonly SafeFileHandle?[] _openHandles;
        private readonly long[] _chunkStartFrame;

        public InterleavedMultiWaveStream(IReadOnlyList<WavFileHeader> chunks)
        {
            _chunks = chunks;
            _openHandles = new SafeFileHandle?[chunks.Count];
            _chunkStartFrame = new long[chunks.Count];

            long running = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                _chunkStartFrame[i] = running;
                running += chunks[i].FrameCount;
            }
        }

        public long TotalFrameCount
            => _chunks.Sum(c => c.FrameCount);
        public int ChannelCount
            => _chunks.Count > 0 ? _chunks[0].ChannelCount : 0;
        public uint SampleRate
            => _chunks.Count > 0 ? _chunks[0].SampleRate : 0;

        public int ReadChannel(
            long startFrame,
            int frameCount,
            int channelIndex,
            Span<float> destination
        )
        {
            if (channelIndex < 0 || channelIndex >= this.ChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(channelIndex), $"channelIndex {channelIndex} is out of range - this stream has {ChannelCount} channel(s).");
            }

            int written = 0;
            long frame = startFrame;

            while (written < frameCount && frame < TotalFrameCount)
            {
                int chunkIndex = FindChunkIndex(frame);
                var header = _chunks[chunkIndex];
                var handle = GetOpenHandle(chunkIndex);

                long frameWithinChunk = frame - _chunkStartFrame[chunkIndex];
                long framesLeftInChunk = header.FrameCount - frameWithinChunk;
                int framesToReadHere = (int)Math.Min(framesLeftInChunk, frameCount - written);
                if (framesToReadHere <= 0)
                {
                    break;
                }

                int got = ReadChannelFromChunk(
                    handle,
                    header,
                    frameWithinChunk,
                    framesToReadHere,
                    channelIndex,
                    destination.Slice(
                        written,
                        framesToReadHere
                    )
                );

                written += got;
                frame += got;
                if (got < framesToReadHere)
                {
                    break;
                }
            }

            return written;
        }

        public int ReadAllChannels(long startFrame, int frameCount, float[][] destination)
        {
            if (destination.Length != ChannelCount)
            {
                throw new ArgumentException($"destination must have exactly {ChannelCount} channel buffers, got {destination.Length}.", nameof(destination));
            }

            int written = 0;
            long frame = startFrame;

            while (written < frameCount && frame < TotalFrameCount)
            {
                int chunkIndex = FindChunkIndex(frame);
                var header = _chunks[chunkIndex];
                var handle = GetOpenHandle(chunkIndex);

                long frameWithinChunk = frame - _chunkStartFrame[chunkIndex];
                long framesLeftInChunk = header.FrameCount - frameWithinChunk;
                int framesToReadHere = (int)Math.Min(framesLeftInChunk, frameCount - written);
                if (framesToReadHere <= 0)
                {
                    break;
                }

                int got = ReadAllChannelsFromChunk(
                    handle,
                    header,
                    frameWithinChunk,
                    framesToReadHere,
                    destination,
                    written
                );

                written += got;
                frame += got;
                if (got < framesToReadHere)
                {
                    break;
                }
            }

            return written;
        }

        private int FindChunkIndex(long globalFrame)
        {
            for (int i = _chunkStartFrame.Length - 1; i >= 0; i--)
            {
                if (globalFrame >= _chunkStartFrame[i])
                {
                    return i;
                }
            }
            return 0;
        }

        private SafeFileHandle GetOpenHandle(int chunkIndex) =>
            _openHandles[chunkIndex]
            ??= File.OpenHandle(_chunks[chunkIndex].FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);

        private static int ReadChannelFromChunk(
            SafeFileHandle handle,
            WavFileHeader header,
            long frameWithinChunk,
            int frameCount,
            int channelIndex,
            Span<float> destination
        )
        {
            int bytesPerSample = header.BitsPerSample / 8;
            int frameStride = header.BlockAlign;
            int byteCount = frameCount * frameStride;

            byte[] block = AudioBufferPool.Instance.Rent(byteCount);
            try
            {
                long blockOffset = header.DataStartOffset + frameWithinChunk * frameStride;
                int gotBytes = RandomAccess.Read(handle, block.AsSpan(0, byteCount), blockOffset);
                int gotFrames = gotBytes / frameStride;

                int channelByteOffset = channelIndex * bytesPerSample;
                for (int i = 0; i < gotFrames; i++)
                {
                    destination[i] = PcmSampleCodec.Decode(
                        block.AsSpan(
                            i * frameStride + channelByteOffset,
                            bytesPerSample
                        ),
                        bytesPerSample
                    );
                }

                return gotFrames;
            }
            finally
            {
                AudioBufferPool.Instance.Return(block);
            }
        }


        private static int ReadAllChannelsFromChunk(
            SafeFileHandle handle,
            WavFileHeader header,
            long frameWithinChunk,
            int frameCount,
            float[][] destination,
            int destOffset
        )
        {
            int bytesPerSample = header.BitsPerSample / 8;
            int frameStride = header.BlockAlign;
            int channelCount = header.ChannelCount;
            int byteCount = frameCount * frameStride;

            byte[] block = AudioBufferPool.Instance.Rent(byteCount);
            try
            {
                long blockOffset = header.DataStartOffset + frameWithinChunk * frameStride;
                int gotBytes = RandomAccess.Read(handle, block.AsSpan(0, byteCount), blockOffset);
                int gotFrames = gotBytes / frameStride;

                for (int i = 0; i < gotFrames; i++)
                {
                    int frameByteOffset = i * frameStride;
                    for (int c = 0; c < channelCount; c++)
                    {
                        destination[c][destOffset + i] = PcmSampleCodec.Decode(block.AsSpan(frameByteOffset + c * bytesPerSample, bytesPerSample), bytesPerSample);
                    }
                }

                return gotFrames;
            }
            finally
            {
                AudioBufferPool.Instance.Return(block);
            }
        }

        public void Dispose()
        {
            foreach (var h in _openHandles)
            {
                h?.Dispose();
            }
        }
    }
}
