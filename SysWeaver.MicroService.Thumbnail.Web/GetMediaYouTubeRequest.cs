using System;
using SysWeaver.MicroService.Media;

namespace SysWeaver.MicroService
{
    public sealed class GetMediaYouTubeRequest : GetMediaRequest
    {
        internal override int Type => (int)MediaTypes.YouTube;
        internal override Object Params => Options;

        /// <summary>
        /// Image options
        /// </summary>
        public MediaYouTube Options;
    }


}
