using CommunityToolkit.Mvvm.ComponentModel;
using CerysLewis.TrackBench.Core.Enumerators;

namespace CerysLewis.TrackBench.App.ViewModels
{
    public partial class MarkerViewModel : ObservableObject
    {
        [ObservableProperty] private long _framePosition;
        [ObservableProperty] private string _label;

        public MarkerSourceEnum Source { get; }

        public MarkerViewModel(long framePosition, string label, MarkerSourceEnum source)
        {
            _framePosition = framePosition;
            _label = label;
            Source = source;
        }
    }
}
