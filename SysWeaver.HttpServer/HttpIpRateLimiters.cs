using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;

namespace SysWeaver.Net
{
    /// <summary>
    /// One rate limiter per client IP address (IPv6 addresses are grouped by their /64 prefix, a single client usually has a whole /64).
    /// The client address is <see cref="HttpServerRequest.GetIpAddress"/>, forwarded addresses are only used from trusted proxies.
    /// At most <see cref="MaxAddresses"/> addresses are tracked, idle limiters are removed.
    /// </summary>
    /// <remarks>
    /// Per address limits slow down a single attacker, not a distributed one (tor, bot nets), combine with a (permissive) service limit.
    /// Thread safe.
    /// </remarks>
    public sealed class HttpIpRateLimiters
    {
        /// <summary>
        /// Create per address limiters
        /// </summary>
        /// <param name="p">The parameters of every per address limiter</param>
        public HttpIpRateLimiters(HttpRateLimiterParams p)
        {
            ArgumentNullException.ThrowIfNull(p);
            p.Validate();
            P = p;
            IdleMs = Math.Max(60_000L, p.Duration * 2_000L + p.MaxDelay * 1_000L);
        }

        readonly HttpRateLimiterParams P;

        /// <summary>
        /// The maximum number of tracked addresses (when exceeded, idle limiters are removed, and if that isn't enough the oldest half)
        /// </summary>
        public int MaxAddresses = 50_000;

        readonly long IdleMs;

        sealed class Entry
        {
            public Entry(HttpRateLimiter l) => Limiter = l;
            public readonly HttpRateLimiter Limiter;
            public long LastUsed;
        }

        readonly ConcurrentDictionary<String, Entry> Limiters = new(StringComparer.Ordinal);

        long NextPrune;

        /// <summary>
        /// Get the key of an address: the address itself, or the /64 prefix of an IPv6 address
        /// </summary>
        /// <param name="address">The address as text</param>
        /// <returns>The key</returns>
        public static String GetKey(String address)
        {
            if (String.IsNullOrEmpty(address))
                return "?";
            if ((address.IndexOf(':') < 0) || !IPAddress.TryParse(address, out var ip))
                return address;
            if (ip.IsIPv4MappedToIPv6)
                return ip.MapToIPv4().ToString();
            Span<Byte> b = stackalloc Byte[16];
            if (!ip.TryWriteBytes(b, out var w) || (w != 16))
                return address;
            b.Slice(8).Clear();
            return new IPAddress(b).ToString() + "/64";
        }

        /// <summary>
        /// Get the limiter of the client of a request
        /// </summary>
        /// <param name="request">The request</param>
        /// <returns>The limiter</returns>
        public HttpRateLimiter Get(HttpServerRequest request)
        {
            var key = GetKey(request?.GetIpAddress());
            var now = Environment.TickCount64;
            var l = Limiters;
            if (!l.TryGetValue(key, out var e))
            {
                if (l.Count >= MaxAddresses)
                    Prune(now);
                e = l.GetOrAdd(key, _ => new Entry(new HttpRateLimiter(P)));
            }
            Volatile.Write(ref e.LastUsed, now);
            if (now >= Interlocked.Read(ref NextPrune))
            {
                Interlocked.Exchange(ref NextPrune, now + IdleMs);
                Prune(now);
            }
            return e.Limiter;
        }

        /// <summary>
        /// The number of tracked addresses
        /// </summary>
        public int Count => Limiters.Count;

        void Prune(long now)
        {
            var l = Limiters;
            var limit = now - IdleMs;
            foreach (var x in l)
            {
                var e = x.Value;
                if ((Volatile.Read(ref e.LastUsed) < limit) && (e.Limiter.Waiting <= 0))
                    l.TryRemove(x);
            }
            if (l.Count < MaxAddresses)
                return;
            //  Still too many (a large distributed attack): drop the least recently used half
            var all = l.ToArray();
            Array.Sort(all, (a, b) => Volatile.Read(ref a.Value.LastUsed).CompareTo(Volatile.Read(ref b.Value.LastUsed)));
            for (int i = 0; i < all.Length / 2; ++i)
                l.TryRemove(all[i]);
        }
    }
}
