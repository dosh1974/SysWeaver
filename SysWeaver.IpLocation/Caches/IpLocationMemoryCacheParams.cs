namespace SysWeaver.IpLocation.Caches
{
    /// <summary>
    /// Parameters for the <see cref="IpLocationMemoryCache"/>
    /// </summary>
    public sealed class IpLocationMemoryCacheParams
    {
        /// <summary>
        /// The number of minutes that a location is cached (at least 1).
        /// Defaults to 1440 (24 hours).
        /// </summary>
        public int MaxCachedMinutes = 60 * 24;
    }
}
