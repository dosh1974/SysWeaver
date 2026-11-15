using System;
using SysWeaver.MicroService.Media;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A request for a screenshot of an image
    /// </summary>
    public sealed class GetMediaImageRequest : GetMediaRequest
    {
        internal override int Type => (int)MediaTypes.Image;
        internal override Object Params => Options;

        /// <summary>With an effect the image is its WebGL texture</summary>
        internal override (Func<String> Get, Action<String> Set)[] TextureUrls => String.IsNullOrEmpty(Options?.Effect) ? [] : [(() => Url, v => Url = v)];

        /// <summary>
        /// Image options
        /// </summary>
        public MediaImage Options;
    }


}
