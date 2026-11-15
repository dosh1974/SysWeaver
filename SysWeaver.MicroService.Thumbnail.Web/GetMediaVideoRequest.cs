using System;
using SysWeaver.MicroService.Media;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A request for a screenshot of a video
    /// </summary>
    public sealed class GetMediaVideoRequest : GetMediaRequest
    {
        internal override int Type => (int)MediaTypes.Video;
        internal override Object Params => Options;

        /// <summary>
        /// Video options
        /// </summary>
        public MediaVideo Options;
    }


}
