using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace CerysLewis.TrackBench.App.ViewModels
{
    public partial class TimelineViewModel : ObservableObject
    {
        private const double MinSamplesPerPixel = 1;

        [ObservableProperty] private long _visibleStartFrame;
        [ObservableProperty] private double _samplesPerPixel = 64;
        [ObservableProperty] private long _totalFrameCount;

        [ObservableProperty] private long? _playheadFrame;

        public double FrameToPixel(long frame) => (frame - VisibleStartFrame) / SamplesPerPixel;
        public long PixelToFrame(double pixel) => VisibleStartFrame + (long)(pixel * SamplesPerPixel);

        public void ZoomAt(double anchorPixel, double factor, double viewportPixelWidth)
        {
            long anchorFrame = PixelToFrame(anchorPixel);
            double maxSamplesPerPixel = TotalFrameCount <= 0
                ? SamplesPerPixel
                : Math.Max(
                    MinSamplesPerPixel,
                    TotalFrameCount / Math.Max(
                        1,
                        viewportPixelWidth
                    )
                );

            SamplesPerPixel = Math.Clamp(
                SamplesPerPixel / factor, MinSamplesPerPixel,
                maxSamplesPerPixel
                );
            VisibleStartFrame = Math.Max(
                0,
                anchorFrame - (long)(anchorPixel * SamplesPerPixel)
            );
        }
    }
}
