using System;
using System.Threading.Tasks;

namespace SysWeaver.IpLocation.Caches
{
    public sealed class IpLocationMemoryCache : IIpLocationCache
    {
        public IpLocationMemoryCache(IpLocationMemoryCacheParams p = null)
        {
            p = p ?? new IpLocationMemoryCacheParams();
            Cache = new FastMemCache<string, IpLocation>(TimeSpan.FromMinutes(Math.Max(1, p.MaxCachedMinutes)), StringComparer.Ordinal);
        }
            
        public ValueTask<IpLocation> Get(string ip, Func<string, Task<IpLocation>> getFromSource)
            => Cache.GetOrUpdateAsync(ip, getFromSource);

        readonly FastMemCache<String, IpLocation> Cache;
    }
}
