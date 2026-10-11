using CerysLewis.TrackBench.App.Common.Audio;
using CerysLewis.TrackBench.Core.Audio;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.App.ViewModels
{
    public partial class TrackViewModel : ObservableObject
    {
        public int ChannelIndex { get; }
        public IChannelAudioSource Audio { get; }

        [ObservableProperty] private string _name;
        [ObservableProperty] private WaveformPeakCache? _peakCache;

        public TrackViewModel(int channelIndex, string name, IChannelAudioSource audio)
        {
            ChannelIndex = channelIndex;
            _name = name;
            Audio = audio;
        }

        /// <summary>Forces WaveformView to repaint even though PeakCache is still the
        /// same object reference - its arrays are being mutated in place by a
        /// background build, so no ordinary change notification fires on its own.</summary>
        public void NotifyWaveformChanged() => OnPropertyChanged(nameof(PeakCache));
    }
}
