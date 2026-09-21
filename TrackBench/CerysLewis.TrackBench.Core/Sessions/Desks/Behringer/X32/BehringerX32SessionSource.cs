using CerysLewis.TrackBench.Core.Enumerators;
using CerysLewis.TrackBench.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CerysLewis.TrackBench.Core.Sessions.Desks.Behringer.X32
{
    public sealed class BehringerX32SessionSource : ISessionSource, INativeMarkerReader
    {
        public string DeskName => "Behringer X32 (X-Live Expansion Card)";

        private static readonly Regex ChunkFilePattern = new(@"^\d{8}\.wav$", RegexOptions.IgnoreCase);

        private const int MarkerCountOffset = 0x14;
        private const int MarkerArrayOffset = 0x41C;
        private const int MaxSupportedMarkers = 32;

        public bool CanRead(string rootPath) =>
            Directory.Exists(rootPath) &&
            (
                IsSessionFolder(rootPath)
                || Directory.EnumerateDirectories(rootPath)
                    .Any(IsSessionFolder)
            );

        public IReadOnlyList<SessionInfo> ScanSessions(string rootPath)
        {
            if (IsSessionFolder(rootPath))
            {
                return TryBuildSession(rootPath) is { } single ? [single] : [];
            }

            var sessions = new List<SessionInfo>();
            foreach (var dir in Directory.EnumerateDirectories(rootPath))
            {
                if (IsSessionFolder(dir) && TryBuildSession(dir) is { } session)
                {
                    sessions.Add(session);
                }
            }
            return sessions;
        }

        public IReadOnlyList<MarkerMetaModel> ReadNativeMarkers(SessionInfo session)
        {
            string logPath = Path.Combine(session.FolderPath, "SE_LOG.BIN");
            if (!File.Exists(logPath))
            {
                return [];
            }

            using var fileStream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var binaryReader = new BinaryReader(fileStream);

            if (fileStream.Length < MarkerArrayOffset + 4)
            {
                return [];
            }

            fileStream.Seek(MarkerCountOffset, SeekOrigin.Begin);
            uint count = binaryReader.ReadUInt32();
            if (count == 0 || count > MaxSupportedMarkers)
            {
                return [];
            }
            if (fileStream.Length < MarkerArrayOffset + count * 4)
            {
                return [];
            }

            fileStream.Seek(MarkerArrayOffset, SeekOrigin.Begin);
            var markers = new List<MarkerMetaModel>((int)count);
            for (int i = 0; i < count; i++)
            {
                markers.Add(new MarkerMetaModel {
                    FramePosition = binaryReader.ReadUInt32(),
                    Label = $"Marker {i + 1}",
                    Source = MarkerSourceEnum.Native,
                });
            }


            return markers;
        }

        private static bool IsSessionFolder(string dir) =>
            Directory.EnumerateFiles(dir).Any(f => ChunkFilePattern.IsMatch(Path.GetFileName(f)));

        private static BehringerX32SessionInfo? TryBuildSession(string sessionDir)
        {
            var files = Directory.EnumerateFiles(sessionDir)
                .Where(f => ChunkFilePattern.IsMatch(Path.GetFileName(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (files.Count == 0)
            {
                return null;
            }

            var chunks = files.Select(WavFileHeader.Read).ToList();
            var first = chunks[0];
            if (chunks.Any(
                c => c.ChannelCount != first.ChannelCount
                    || c.SampleRate != first.SampleRate
            ))
            {
                return null;
            }

            return new BehringerX32SessionInfo
            {
                DisplayName = Path.GetFileName(sessionDir.TrimEnd('/', '\\')),
                FolderPath = sessionDir,
                ChannelCount = first.ChannelCount,
                SampleRate = first.SampleRate,
                BitsPerSample = first.BitsPerSample,
                TotalFrameCount = chunks.Sum(c => c.FrameCount),
                Chunks = chunks
            };
        }
    }
}
