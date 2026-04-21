using System;
using SysWeaver.MicroService.Media;

namespace SysWeaver.MicroService
{
    public sealed class GetMediaVideoRequest : GetMediaRequest
    {
        internal override int Type => (int)MediaTypes.Video;
        internal override Object Params => Options;

        /// <summary>
        /// Image options
        /// </summary>
        public MediaVideo Options;
    }


}
