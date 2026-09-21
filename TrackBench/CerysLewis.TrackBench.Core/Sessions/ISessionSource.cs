using System;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.Core.Sessions
{
    public interface ISessionSource
    {
        string DeskName { get; }
        bool CanRead(string rootPath);
        IReadOnlyList<SessionInfo> ScanSessions(string rootPath);
    }
}
