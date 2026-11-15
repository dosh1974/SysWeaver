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

        /// <summary>
        /// Image options
        /// </summary>
        public MediaImage Options;
    }


}
