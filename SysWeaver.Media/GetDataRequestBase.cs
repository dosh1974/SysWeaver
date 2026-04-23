using System;

namespace SysWeaver.MicroService
{
    public abstract class GetDataRequestBase
    {
        public override string ToString() => String.Concat(Width, 'x', Height, " from ", Url);

        /// <summary>
        /// The url to take a snap shot for
        /// </summary>
        [EditMin(1)]
        public String Url;

        /// <summary>
        /// The width (can be modified if the page is "aware" and control is true)
        /// </summary>
        [EditDefault(1920)]
        [EditRange(1, 16384)]
        public int Width = 1920;

        /// <summary>
        /// The height (can be modified if the page is "aware" and control is true)
        /// </summary>
        [EditDefault(1080)]
        [EditRange(1, 16384)]
        public int Height = 1080;

        /// <summary>
        /// Wait an additional time before capturing the screen shot (in ms)
        /// </summary>
        [EditDefault(0)]
        [EditRange(0, 100_000)]
        public int ExtraDelayMs = 0;

    }




}
