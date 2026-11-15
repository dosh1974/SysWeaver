using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Base class for requests that take a screenshot of some media
    /// </summary>
    public abstract class GetMediaRequest : GetDataRequestBase
    {
        /// <summary>
        /// "When" to take the screen shot (time in seconds).
        /// Defaults to 0.5.
        /// </summary>
        [EditMin(0)]
        [EditDefault(0.5)]
        public double Pos = 0.5;

        internal abstract int Type { get; }
        internal abstract Object Params { get; }

        /// <summary>The position as a part of the media's duration (MediaPreview.html rpos, ex: 0.5 = the middle), null = Pos</summary>
        internal virtual double? RelPos => null;

        /// <summary>
        /// The urls the browser loads as WebGL textures or with fetch (they must be of the page's origin, else the service proxies them):
        /// get / set pairs. Default: none (the media is shown as it is, ex: an image or a video element)
        /// </summary>
        internal virtual (Func<String> Get, Action<String> Set)[] TextureUrls => [];
    }


}
