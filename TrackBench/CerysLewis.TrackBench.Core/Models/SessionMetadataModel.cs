using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CerysLewis.TrackBench.Core.Models
{
    public sealed class SessionMetadataModel
    {
        public string SessionDisplayName { get; set; } = "";
        public List<TrackMetaModel> Tracks { get; set; } = [];
        public List<MarkerMetaModel> Markers { get; set; } = [];
        public List<RangeMetaModel> Ranges { get; set; } = [];

        public bool NativeMarkersImported { get; set; }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = {
                new JsonStringEnumConverter()
            }
        };

        public static SessionMetadataModel LoadOrCreate(string path, string sessionDisplayName, int channelCount)
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<SessionMetadataModel>(json, JsonOptions);
                if (loaded is not null)
                {
                    return loaded;
                }
            }

            return new SessionMetadataModel
            {
                SessionDisplayName = sessionDisplayName,
                Tracks = Enumerable.Range(0, channelCount)
                    .Select(
                        i => new TrackMetaModel {
                            ChannelIndex = i,
                            Name = $"Track {i + 1:00}"
                        }
                    )
                    .ToList()
            };
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
    }
}
