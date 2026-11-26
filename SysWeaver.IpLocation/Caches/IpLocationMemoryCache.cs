using System;
using System.Threading.Tasks;

namespace SysWeaver.IpLocation.Caches
{
    /// <summary>
    /// An ip location cache that keeps the locations in memory
    /// </summary>
    public sealed class IpLocationMemoryCache : IIpLocationCache
    {
        /// <summary>
        /// Create the cache
        /// </summary>
        /// <param name="p">Parameters, null to use the defaults</param>
        public IpLocationMemoryCache(IpLocationMemoryCacheParams p = null)
        {
            p = p ?? new IpLocationMemoryCacheParams();
            Cache = new FastMemCache<string, Net.Location>(TimeSpan.FromMinutes(Math.Max(1, p.MaxCachedMinutes)), StringComparer.Ordinal);
        }
            
        /// <inheritdoc />
        public ValueTask<Net.Location> Get(string ip, Func<string, Task<Net.Location>> getFromSource)
            => Cache.GetOrUpdateAsync(ip, getFromSource);

        readonly FastMemCache<String, Net.Location> Cache;
    }
}
