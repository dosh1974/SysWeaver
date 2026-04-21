using System;

namespace SysWeaver.MicroService
{
    public sealed class ThumbnailWebParams
    {
        /// <summary>
        /// Cache duration
        /// </summary>
        public int ClientCacheDuration = 30;
        public int RequestCacheDuration = 25;
        public String Auth = Roles.Service;
        public int MaxConcurrency = 32;

        public ApiKeyParams GoogleMapsKey = new ApiKeyParams
        {
            CredFile = "$(KeyFolder)/GoogleMaps_Embed.txt"
        };
    }


    public class GetGoogleMapJpegRequest : GetGoogleMapRequest
    {
        /// <summary>
        /// Quality
        /// </summary>
        [EditSlider]
        [EditDefault(70.0)]
        [EditRange(10.0, 100.0)]
        public int Quality = 70;
    }


}
