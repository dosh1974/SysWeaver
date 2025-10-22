using System;
using SysWeaver.MicroService.Media;

namespace SysWeaver.MicroService
{
    public sealed class ThumbnailWebParams
    {
        /// <summary>
        /// Cache duration
        /// </summary>
        public int ClientCacheDuration = 30;
        public int RequestCacheDuration = 30;
        public String Auth = Roles.Debug;
        public int MaxConcurrency = 32;
    }


    public abstract class GetMediaRequest : GetDataRequestBase
    {
        /// <summary>
        /// "When" to take the screen shot (time in seconds)
        /// </summary>
        [EditMin(0)]
        [EditDefault(0.5)]
        public double Pos = 0.5;

        internal abstract int Type { get; }
        internal abstract Object Params { get; }
    }

    public sealed class GetMediaImageRequest : GetMediaRequest
    {
        internal override int Type => (int)MediaTypes.Image;
        internal override Object Params => Options;

        /// <summary>
        /// Image options
        /// </summary>
        public MediaImage Options;
    }

    public sealed class GetMediaVideoRequest : GetMediaRequest
    {
        internal override int Type => (int)MediaTypes.Video;
        internal override Object Params => Options;

        /// <summary>
        /// Image options
        /// </summary>
        public MediaVideo Options;
    }

    public sealed class GetMediaEffectRequest : GetMediaRequest
    {
        internal override int Type => (int)MediaTypes.Effect;
        internal override Object Params => Options;

        /// <summary>
        /// Image options
        /// </summary>
        public MediaEffect Options;
    }


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
