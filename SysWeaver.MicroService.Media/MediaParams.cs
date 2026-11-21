namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters of the <see cref="MediaService"/>
    /// </summary>
    public sealed class MediaParams
    {

        /// <summary>
        /// Optional API key for the maps API, only visible for Dev's
        /// </summary>
        public ApiKeyParams GoogleMapsKey = new ApiKeyParams
        {
            CredFile = "$(KeyFolder)/GoogleMaps_Embed.txt"
        };
    }
    
}
