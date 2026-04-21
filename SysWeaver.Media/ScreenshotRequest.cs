using System;

namespace SysWeaver.MicroService
{
    public class ScreenshotRequest : GetDataRequestBase
    {
        public override string ToString() => String.Concat(Width, 'x', Height, " from ", Url);
        /// <summary>
        /// If true, a js object is registered so that the page can control dimensions and when to take the screenshot
        /// </summary>
        public bool Control;

        /// <summary>
        /// The scale factor to use (dpi / device scale)
        /// </summary>
        [EditSlider]
        [EditDefault(100.0)]
        [EditRange(10.0, 1000.0)]
        public double Scale = 100;

        /// <summary>
        /// Optimize for speed rather than quality
        /// </summary>
        [EditDefault(false)]
        public bool OptimizeForSpeed;
    }




}
