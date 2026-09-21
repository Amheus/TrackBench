using CerysLewis.TrackBench.Core.Audio;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using static System.Collections.Specialized.BitVector32;

namespace CerysLewis.TrackBench.Core.Sessions
{
    public abstract class SessionInfo
    {
        public required string DisplayName { get; init; }
        public required string FolderPath { get; init; }
        public required int ChannelCount { get; init; }
        public required uint SampleRate { get; init; }
        public required ushort BitsPerSample { get; init; }
        public required long TotalFrameCount { get; init; }

        public TimeSpan Duration =>
            SampleRate == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)TotalFrameCount / SampleRate);

        public string MetadataCachePath =>
            Path.Combine(AppPaths.Root, "Sessions", Fingerprint, "Markers.json");

        public string Fingerprint
        {
            get
            {
                string raw = $"{DisplayName}|{ChannelCount}|{SampleRate}|{BitsPerSample}|{TotalFrameCount}";
                byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
                return Convert.ToHexString(hash)[..16].ToUpper();
            }
        }

        public abstract IChannelAudioSource OpenAudioSource();
    }
}
