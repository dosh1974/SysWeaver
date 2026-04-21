using System;
using SysWeaver.MicroService.Media;

namespace SysWeaver.MicroService
{
    public sealed class GetMediaEffectRequest : GetMediaRequest
    {
        internal override int Type => (int)MediaTypes.Effect;
        internal override Object Params => Options;

        /// <summary>
        /// Image options
        /// </summary>
        public MediaEffect Options;
    }


}
