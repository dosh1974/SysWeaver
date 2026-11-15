using System;
using SysWeaver.MicroService.Media;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// A request for a screenshot of an effect (a shader)
    /// </summary>
    public sealed class GetMediaEffectRequest : GetMediaRequest
    {
        internal override int Type => (int)MediaTypes.Effect;
        internal override Object Params => Options;

        /// <summary>
        /// Effect options
        /// </summary>
        public MediaEffect Options;
    }


}
