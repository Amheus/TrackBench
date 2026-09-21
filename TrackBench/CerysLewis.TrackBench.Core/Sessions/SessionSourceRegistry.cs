
using CerysLewis.TrackBench.Core.Sessions.Desks.AllenAndHeath.Qu.SB;
using CerysLewis.TrackBench.Core.Sessions.Desks.Behringer.X32;
using System;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.Core.Sessions
{
    public sealed class SessionSourceRegistry
    {
        private readonly List<ISessionSource> _sources;

        public SessionSourceRegistry(IEnumerable<ISessionSource>? sources = null)
        {
            _sources = sources?.ToList() ??
            [
                new BehringerX32SessionSource(),
                new AllenAndHeathQuSbMultitrackSessionSource(),
            ];
        }

        public IReadOnlyList<(ISessionSource Source, IReadOnlyList<SessionInfo> Sessions)> DetectAndScan(string rootPath)
        {
            var results = new List<(ISessionSource, IReadOnlyList<SessionInfo>)>();
            foreach (var source in _sources)
            {
                if (!source.CanRead(rootPath))
                {
                    continue;
                }
                var sessions = source.ScanSessions(rootPath);
                if (sessions.Count > 0)
                {
                    results.Add((source, sessions));
                }
            }
            return results;
        }
    }
}
