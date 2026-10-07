using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SysWeaver.Net
{
    /// <summary>
    /// Resolves the host of a request url (called for every request).
    /// A server typically has a few hosts (1 to 3), so these are kept in a small array that is searched linearly (no hashing, no allocations).
    /// </summary>
    /// <remarks>
    /// Also url decodes the whole url (path and query, see <see cref="HttpServerTools.UrlDecode(string)"/>) and inserts "index.html" for directory requests.
    /// The decoding happens after any normalization done by the listener, so the resulting local url may contain "../" segments (from "%2F" / "%2E") and "?" chars (from "%3F").
    /// Thread safe, new hosts are added under a lock (copy on write lookup array).
    /// Hosts are never removed, a new <see cref="HttpServerHostInfo"/> is kept for every distinct "scheme://host:port" that matches a listener prefix,
    /// up to <see cref="MaxCachedHosts"/> hosts. Further hosts (the Host header is client controlled) are resolved on every request without being cached,
    /// so that random Host headers can't grow the memory without bound.
    /// </remarks>
    public sealed class HttpServerHosts
    {
        /// <summary>
        /// Up to this number of hosts are kept in the lookup array, if there are more the dictionary is used
        /// </summary>
        const int MaxArrayHosts = 8;

        /// <summary>
        /// The maximum number of hosts that are cached, more hosts are resolved (a new <see cref="HttpServerHostInfo"/> is created) for every request.
        /// </summary>
        public const int MaxCachedHosts = 1024;

        /// <summary>
        /// Urls up to this length are decoded on the stack (longer urls use a pooled buffer)
        /// </summary>
        const int MaxStackChars = 512;

        /// <summary>
        /// Host names up to this length are lower cased on the stack
        /// </summary>
        const int MaxStackHostChars = 256;

        const String IndexFile = "index.html";

        /// <summary>
        /// Set the prefixes the server is listening on, used to create new hosts.
        /// Should be called before any request is resolved, hosts that are already known are not affected.
        /// </summary>
        /// <param name="prefixes">The (normalized) listener prefixes</param>
        public void SetPrefixes(HttpServerPrefix[] prefixes) => Prefixes = prefixes;

        HttpServerPrefix[] Prefixes = [];

        readonly LowAllocConcurrentDictionary<String, HttpServerHostInfo> Hosts = new LowAllocConcurrentDictionary<string, HttpServerHostInfo>(64, StringComparer.Ordinal);

        /// <summary>
        /// The known hosts (key is the lower cased "scheme://host:port"), replaced (never modified) when a host is added, null if there are too many hosts
        /// </summary>
        HostEntry[] HostArray = [];

        sealed class HostEntry
        {
            public HostEntry(String key, HttpServerHostInfo host)
            {
                Key = key;
                Host = host;
                RootIndexUrl = String.Concat(key, "/", IndexFile);
            }
            public readonly String Key;
            public readonly HttpServerHostInfo Host;
            /// <summary>
            /// The url of a root request ("scheme://host:port/") with index.html inserted, so that root requests doesn't allocate a new url
            /// </summary>
            public readonly String RootIndexUrl;
        }

        /// <summary>
        /// Find a known host
        /// </summary>
        /// <param name="hostName">The "scheme://host:port" part of the url (may contain upper case chars)</param>
        /// <returns>The host entry or null if it's not known (or there are too many hosts)</returns>
        HostEntry FindHost(ReadOnlySpan<char> hostName)
        {
            var hosts = Volatile.Read(ref HostArray);
            if (hosts == null)
                return null;
            // Host names are typically lower case already
            foreach (var h in hosts)
                if (hostName.SequenceEqual(h.Key))
                    return h;
            var l = hostName.Length;
            if (l > MaxStackHostChars)
                return null;
            Span<char> lower = stackalloc char[l];
            bool changed = false;
            for (int i = 0; i < l; ++i)
            {
                var c = hostName[i];
                var lc = c.FastToLower();
                lower[i] = lc;
                changed |= c != lc;
            }
            if (!changed)
                return null;
            foreach (var h in hosts)
                if (lower.SequenceEqual(h.Key))
                    return h;
            return null;
        }

        /// <summary>
        /// Create (or get) the host for a lower cased "scheme://host:port" (slow path, takes a lock).
        /// With a single listener prefix any host name is accepted, with multiple prefixes the first prefix that the url starts with (wildcard replaced by the host name) is used.
        /// If <see cref="MaxCachedHosts"/> hosts are already cached, a new (uncached) host is returned.
        /// </summary>
        /// <param name="hostName">The lower cased "scheme://host:port" part of the url</param>
        /// <param name="url">The full url</param>
        /// <returns>The host</returns>
        /// <exception cref="Exception">Thrown if there are multiple prefixes and none of them matches the url</exception>
        HttpServerHostInfo CreateHost(String hostName, String url)
        {
            var hosts = Hosts;
            if (hosts.Count >= MaxCachedHosts)
                return NewHost(hostName, url);
            lock (hosts)
            {
                if (hosts.TryGetValue(hostName, out var host))
                    return host;
                host = NewHost(hostName, url);
                if (hosts.Count >= MaxCachedHosts)
                    return host;
                hosts[hostName] = host;
                // Update the lookup array (copy on write)
                var arr = HostArray;
                if (arr != null)
                {
                    if (arr.Length < MaxArrayHosts)
                    {
                        var n = new HostEntry[arr.Length + 1];
                        arr.CopyTo(n, 0);
                        n[arr.Length] = new HostEntry(hostName, host);
                        arr = n;
                    }
                    else
                    {
                        arr = null;
                    }
                    Volatile.Write(ref HostArray, arr);
                }
                return host;
            }
        }

        /// <summary>
        /// Create a new host for a lower cased "scheme://host:port" (no caching).
        /// </summary>
        /// <param name="hostName">The lower cased "scheme://host:port" part of the url</param>
        /// <param name="url">The full url</param>
        /// <returns>The host</returns>
        /// <exception cref="Exception">Thrown if there are multiple prefixes and none of them matches the url</exception>
        HttpServerHostInfo NewHost(String hostName, String url)
        {
            var pr = Prefixes;
            var start = hostName.FastIndexOf("://") + 3;
            // An IPv6 host is enclosed in brackets (ex: "[::1]"), the port starts after the ']'
            var end = ((start < hostName.Length) && (hostName[start] == '['))
                ? hostName.IndexOf(']', start) + 1
                : hostName.IndexOf(':', start);
            if (end <= 0)
                end = hostName.Length;
            String wild = hostName.Substring(start, end - start);
            if (pr.Length == 1)
            {
                var prefix = pr[0];
                var t = prefix.Prefix.Replace("*", wild);
                return new HttpServerHostInfo(t, prefix);
            }
            // The host name part must be compared lower cased (the prefixes are)
            var lowerUrl = String.Concat(hostName, url.AsSpan(Math.Min(hostName.Length, url.Length)));
            foreach (var prefix in pr)
            {
                var t = prefix.Prefix.Replace("*", wild);
                if (lowerUrl.FastStartsWith(t))
                    return new HttpServerHostInfo(t, prefix);
            }
            throw new Exception("Unknown host name!");
        }

        /// <summary>
        /// Resolve the host of a url, decodes the url and inserts index.html for directory requests.
        /// </summary>
        /// <param name="prefix">The prefix (the <see cref="HttpServerHostInfo.Name"/> of the host)</param>
        /// <param name="queryStart">The index of the '?' that starts the query string in the returned url, -1 if there is no query string</param>
        /// <param name="didIndex">True if "index.html" was added to the url</param>
        /// <param name="url">The absolute url ("scheme://host:port/path?query"), replaced with the decoded url (with index.html inserted if needed)</param>
        /// <returns>The host</returns>
        /// <exception cref="Exception">Thrown if there are multiple listener prefixes and none of them matches a new host</exception>
        [SkipLocalsInit]
        public HttpServerHostInfo GetHost(out String prefix, out int queryStart, out bool didIndex, ref String url)
        {
            var src = url;
            var l = src.Length;
            HttpServerHostInfo host;
            String newUrl;
            // Most urls doesn't need decoding (vectorized check)
            if (!src.AsSpan().ContainsAny('%', '+'))
            {
                host = Resolve(src, src, false, out prefix, out queryStart, out didIndex, out newUrl);
            }
            else if (l <= MaxStackChars)
            {
                // Decode into a temporary buffer in a single pass (the decoded url is never longer), only the final url is allocated
                Span<char> buffer = stackalloc char[l];
                var n = HttpServerTools.UrlDecode(src, buffer, out var needsDecoding);
                host = needsDecoding
                    ? Resolve(buffer[..n], null, true, out prefix, out queryStart, out didIndex, out newUrl)
                    : Resolve(src, src, false, out prefix, out queryStart, out didIndex, out newUrl);
            }
            else
            {
                var rented = ArrayPool<char>.Shared.Rent(l);
                try
                {
                    var n = HttpServerTools.UrlDecode(src, rented, out var needsDecoding);
                    host = needsDecoding
                        ? Resolve(rented.AsSpan(0, n), null, true, out prefix, out queryStart, out didIndex, out newUrl)
                        : Resolve(src, src, false, out prefix, out queryStart, out didIndex, out newUrl);
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(rented);
                }
            }
            if (newUrl != null)
                url = newUrl;
            return host;
        }

        /// <summary>
        /// Resolve the host of a decoded url
        /// </summary>
        /// <param name="u">The decoded url</param>
        /// <param name="urlString">The url as a string (if available, else null)</param>
        /// <param name="decoded">True if the url was decoded (so a new url string must be returned)</param>
        /// <param name="prefix">The prefix (the host name)</param>
        /// <param name="queryStart">The index of the '?' that starts the query string, -1 if there is no query string</param>
        /// <param name="didIndex">True if "index.html" was added to the url</param>
        /// <param name="newUrl">The url to use, null if the urlString can be used as is</param>
        /// <returns>The host</returns>
        HttpServerHostInfo Resolve(ReadOnlySpan<char> u, String urlString, bool decoded, out String prefix, out int queryStart, out bool didIndex, out String newUrl)
        {
            var ul = u.Length;
            var start = u.IndexOf("://");
            start = start < 0 ? 0 : start + 3;
            var end = u[start..].IndexOfAny('/', '?');
            end = end < 0 ? ul : end + start;
            var entry = FindHost(u[..end]);
            var host = entry?.Host;
            if (host == null)
            {
                // Slow path (new host or many hosts)
                urlString ??= new String(u);
                var hostName = urlString.FastStartToLower(end);
                if (!Hosts.TryGetValue(hostName, out host))
                    host = CreateHost(hostName, urlString);
            }
            prefix = host.Name;
            // Skip the '/' or '?' after the host name
            ++end;
            int qs = end < ul ? u[end..].IndexOf('?') : -1;
            int insertAt;
            if (qs < 0)
            {
                queryStart = -1;
                insertAt = ((ul > 0) && (u[ul - 1] == '/')) ? ul : -1;
            }
            else
            {
                qs += end;
                queryStart = qs;
                insertAt = u[qs - 1] == '/' ? qs : -1;
                if (insertAt >= 0)
                    queryStart += IndexFile.Length;
            }
            didIndex = insertAt >= 0;
            if (didIndex)
            {
                // A root request (no query) to a known host with the same casing: the cached url (no allocation)
                if ((entry != null) && (insertAt == ul) && (ul == entry.Key.Length + 1) && u.StartsWith(entry.Key))
                    newUrl = entry.RootIndexUrl;
                else
                    // A single allocation, even if the url was decoded
                    newUrl = u[..insertAt].ConcatToString(IndexFile, u[insertAt..]);
            }
            else
            {
                // The decoded url (null if the url is unchanged)
                newUrl = decoded ? (urlString ?? new String(u)) : null;
            }
            return host;
        }
    }
}
