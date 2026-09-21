using System;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.Core.Sessions
{
    public sealed class WavFileHeader
    {
        public required string FilePath { get; init; }
        public required ushort ChannelCount { get; init; }
        public required uint SampleRate { get; init; }
        public required ushort BitsPerSample { get; init; }
        public required ushort BlockAlign { get; init; }
        public required long DataStartOffset { get; init; }
        public required long DataLength { get; init; }

        public string? TrackName { get; init; }

        public long FrameCount => BlockAlign == 0 ? 0 : DataLength / BlockAlign;

        public static WavFileHeader Read(string path)
        {
            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var binaryReader = new BinaryReader(fileStream);

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
            string? trackName = null;
            bool haveFmt = false;

            while (fileStream.Position <= fileStream.Length - 8)
            {
                var chunkId = new string(binaryReader.ReadChars(4));
                uint chunkSize = binaryReader.ReadUInt32();
                long chunkDataStart = fileStream.Position;

                if (chunkId == "fmt ")
                {
                    binaryReader.ReadUInt16();
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
                else if (chunkId == "LIST" && chunkSize >= 4)
                {
                    trackName ??= TryReadInfoName(binaryReader, chunkDataStart, chunkSize);
                }

                long next = chunkDataStart + chunkSize + (chunkSize % 2);
                if (next < chunkDataStart)
                {
                    break;
                }
                fileStream.Seek(next, SeekOrigin.Begin);
            }

            if (!haveFmt || dataStart < 0)
            {
                throw new InvalidDataException($"'{path}' is missing fmt or data chunk.");
            }

            if (bitsPerSample is not (8 or 16 or 24 or 32))
            {
                throw new InvalidDataException(
                    $"'{path}' reports an unsupported bit depth ({bitsPerSample}-bit, {channelCount}ch, {sampleRate}Hz). "
                    + "Either the fmt chunk parsed incorrectly, or the file uses a format TrackBench doesn't handle yet."
                );
            }

            long fileLength = new FileInfo(path).Length;
            if (dataLength <= 0 || dataStart + dataLength > fileLength)
            {
                dataLength = fileLength - dataStart;
            }

            return new WavFileHeader
            {
                FilePath = path,
                ChannelCount = channelCount,
                SampleRate = sampleRate,
                BitsPerSample = bitsPerSample,
                BlockAlign = blockAlign == 0 ? (ushort)(channelCount * (bitsPerSample / 8)) : blockAlign,
                DataStartOffset = dataStart,
                DataLength = dataLength,
                TrackName = trackName
            };
        }

        private static string? TryReadInfoName(BinaryReader binaryReader, long listDataStart, uint listSize)
        {
            var listType = new string(binaryReader.ReadChars(4));
            if (listType != "INFO")
            {
                return null;
            }

            long end = listDataStart + listSize;
            while (binaryReader.BaseStream.Position <= end - 8)
            {
                var subId = new string(binaryReader.ReadChars(4));
                uint subSize = binaryReader.ReadUInt32();
                long subDataStart = binaryReader.BaseStream.Position;

                if (subId == "INAM")
                {
                    var bytes = binaryReader.ReadBytes((int)subSize)
                        .TakeWhile(b => b != 0)
                        .ToArray();

                    return Encoding.UTF8.GetString(bytes);
                }

                long next = subDataStart + subSize + (subSize % 2);
                if (next <= subDataStart)
                {
                    break; 
                }
                binaryReader.BaseStream.Seek(next, SeekOrigin.Begin);
            }
            return null;
        }
    }
}
