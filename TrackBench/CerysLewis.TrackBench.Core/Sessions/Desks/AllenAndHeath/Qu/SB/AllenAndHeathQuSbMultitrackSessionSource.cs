using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CerysLewis.TrackBench.Core.Sessions.Desks.AllenAndHeath.Qu.SB
{
    public sealed class AllenAndHeathQuSbMultitrackSessionSource : ISessionSource
    {
        public string DeskName => "Allen & Heath (Qu/SB multitrack)";

        private static readonly Regex TrackFilePattern = new(@"^TRK\d{2,3}\.wav$", RegexOptions.IgnoreCase);
        private static readonly Regex SessionFolderPattern = new(@"^([A-Za-z]+)-MT\d+$", RegexOptions.IgnoreCase);

        public bool CanRead(string rootPath) =>
            Directory.Exists(rootPath) &&
            (
                IsSessionFolder(rootPath)
                ||
                FindUsbmtkRoots(rootPath)
                    .Any(
                        u => Directory.EnumerateDirectories(u)
                        .Any(
                            IsSessionFolder
                        )
                    )
            );

        public IReadOnlyList<SessionInfo> ScanSessions(string rootPath)
        {
            if (IsSessionFolder(rootPath))
            {
                return TryBuildSession(rootPath) is { } single ? [single] : [];
            }

            var sessions = new List<SessionInfo>();
            foreach (var usbmtk in FindUsbmtkRoots(rootPath))
            {
                foreach (var dir in Directory.EnumerateDirectories(usbmtk))
                {
                    if (IsSessionFolder(dir) && TryBuildSession(dir) is { } session)
                    {
                        sessions.Add(session);
                    }
                }
            }
            return sessions;
        }

        private static bool IsSessionFolder(string dir) =>
            Directory.EnumerateFiles(dir)
                .Any(
                    f => TrackFilePattern.IsMatch(
                        Path.GetFileName(f)
                    )
                );

        private static IEnumerable<string> FindUsbmtkRoots(string rootPath, int maxDepth = 3)
        {
            var queue = new Queue<(string Path, int Depth)>();
            queue.Enqueue((rootPath, 0));

            while (queue.Count > 0)
            {
                var (path, depth) = queue.Dequeue();

                if (
                    string.Equals(
                        Path.GetFileName(path),
                        "USBMTK",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    yield return path;
                    continue;
                }
                if (depth >= maxDepth)
                {
                    continue;
                }

                IEnumerable<string> subdirs;
                try
                {
                    subdirs = Directory.EnumerateDirectories(path);
                }
                catch
                {
                    continue;
                }

                foreach (var d in subdirs) queue.Enqueue((d, depth + 1));
            }
        }

        private static AllenAndHeathQuSbSessionInfo? TryBuildSession(string sessionDir)
        {
            var files = Directory.EnumerateFiles(sessionDir)
                .Where(
                    f => TrackFilePattern.IsMatch(
                        Path.GetFileName(f)
                    )
                )
                .OrderBy(
                    f => f, StringComparer.OrdinalIgnoreCase
                )
                .ToList();
            if (files.Count == 0)
            {
                return null;
            }

            var channelFiles = files.Select(WavFileHeader.Read).ToList();
            var first = channelFiles[0];
            if (channelFiles.Any(c => c.SampleRate != first.SampleRate || c.BitsPerSample != first.BitsPerSample))
            {
                return null;
            }

            var folderName = Path.GetFileName(sessionDir.TrimEnd('/', '\\'));
            var match = SessionFolderPattern.Match(folderName);
            var deskModel = match.Success ? match.Groups[1].Value.ToUpperInvariant() : "unknown";

            return new AllenAndHeathQuSbSessionInfo
            {
                DisplayName = folderName,
                FolderPath = sessionDir,
                DeskModel = deskModel,
                ChannelCount = channelFiles.Count,
                SampleRate = first.SampleRate,
                BitsPerSample = first.BitsPerSample,
                TotalFrameCount = channelFiles.Min(c => c.FrameCount),
                ChannelFiles = channelFiles
            };
        }
    }
}
