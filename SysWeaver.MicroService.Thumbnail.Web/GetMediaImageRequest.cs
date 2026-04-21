using System;
using SysWeaver.MicroService.Media;

namespace SysWeaver.MicroService
{
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
