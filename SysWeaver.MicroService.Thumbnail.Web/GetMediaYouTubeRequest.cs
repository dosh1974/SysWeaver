using System;
using SysWeaver.MicroService.Media;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A request for a screenshot of a YouTube video
    /// </summary>
    public sealed class GetMediaYouTubeRequest : GetMediaRequest
    {
        internal override int Type => (int)MediaTypes.YouTube;
        internal override Object Params => Options;

        /// <summary>
        /// YouTube options
        /// </summary>
        public MediaYouTube Options;
    }


}
