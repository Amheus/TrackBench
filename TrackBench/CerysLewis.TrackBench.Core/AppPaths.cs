using System;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.Core
{
    public static class AppPaths
    {
        public static string Root { get; } = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            ),
            "TrackBench"
        );
    }
}
