using System;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.Core.Sessions
{
    public sealed class WavChunkHeader
    {
        public required string FilePath { get; init; }
        public required ushort ChannelCount { get; init; }
        public required uint SampleRate { get; init; }
        public required ushort BitsPerSample { get; init; }
        public required ushort BlockAlign { get; init; }

        public required long DataStartOffset { get; init; }

        public required long DataLength { get; init; }

        public long FrameCount =>
            BlockAlign == 0 ? 0 : DataLength / BlockAlign;

        public static WavChunkHeader Read(string path)
        {
            using var fileSteam = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var binaryReader = new BinaryReader(fileSteam);

            if (new string(binaryReader.ReadChars(4)) != "RIFF")
            {
                throw new InvalidDataException($"'{path}' is not a RIFF file.");
            }
            binaryReader.ReadUInt32();
            if (new string(binaryReader.ReadChars(4)) != "WAVE")
            {
                throw new InvalidDataException($"'{path}' is not a WAVE file.");
            }

            ushort channelCount = 0, bitsPerSample = 0, blockAlign = 0;
            uint sampleRate = 0;
            long dataStart = -1, dataLength = 0;
            bool haveFmt = false;

            while (fileSteam.Position <= fileSteam.Length - 8)
            {
                var chunkId = new string(binaryReader.ReadChars(4));
                uint chunkSize = binaryReader.ReadUInt32();
                long chunkDataStart = fileSteam.Position;

                if (chunkId == "fmt ")
                {
                    ushort formatTag = binaryReader.ReadUInt16();
                    channelCount = binaryReader.ReadUInt16();
                    sampleRate = binaryReader.ReadUInt32();
                    binaryReader.ReadUInt32();
                    blockAlign = binaryReader.ReadUInt16();
                    bitsPerSample = binaryReader.ReadUInt16();

                    haveFmt = true;
                }
                else if (chunkId == "data")
                {
                    dataStart = chunkDataStart;
                    dataLength = chunkSize;
                }

                long next = chunkDataStart + chunkSize + (chunkSize % 2);
                if (next < chunkDataStart)
                {
                    break;
                }
                fileSteam.Seek(next, SeekOrigin.Begin);
            }

            if (!haveFmt || dataStart < 0)
            {
                throw new InvalidDataException($"'{path}' is missing fmt or data chunk.");
            }

            long fileLength = new FileInfo(path).Length;
            if (dataLength <= 0 || dataStart + dataLength > fileLength)
            {
                dataLength = fileLength - dataStart;
            }

            return new WavChunkHeader
            {
                FilePath = path,
                ChannelCount = channelCount,
                SampleRate = sampleRate,
                BitsPerSample = bitsPerSample,
                BlockAlign = blockAlign == 0 ? (ushort)(channelCount * (bitsPerSample / 8)) : blockAlign,
                DataStartOffset = dataStart,
                DataLength = dataLength
            };
        }
    }
}
