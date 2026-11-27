using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {
        /// <summary>
        /// The state of the probe trap (see <see cref="HttpProbeTrapParams"/>), null if disabled
        /// </summary>
        readonly ProbeTrapState ProbeTrap;

        /// <summary>
        /// Requests for a probe url (answered by the probe trap)
        /// </summary>
        readonly HttpRequestTracker ProbeRequests = new HttpRequestTracker();

        /// <summary>
        /// Requests of banned addresses and sessions (answered by the probe trap)
        /// </summary>
        readonly HttpRequestTracker BannedRequests = new HttpRequestTracker();

        /// <summary>
        /// Requests that hit no end point (a 404, not counting the probe urls)
        /// </summary>
        readonly HttpRequestTracker NotFoundRequests = new HttpRequestTracker();

        /// <summary>
        /// The resource name prefix of the pre-compressed trap answers (trapData/GetAJob.txt.*)
        /// </summary>
        const String TrapResourcePrefix = "SysWeaver.Net.trapData.GetAJob.txt.";

        /// <summary>
        /// The repeated message of a trap answer
        /// </summary>
        const String TrapMessage = "GET A JOB - FBI IS ON THE WAY! ";

        /// <summary>
        /// A banned client address of the probe trap (memory only, the bans don't survive a restart).
        /// The counters and urls are best effort (updated without locks, except the url list).
        /// </summary>
        internal sealed class BanEntry
        {
            /// <summary>
            /// The maximum number of different urls kept per ban
            /// </summary>
            public const int MaxUrls = 12;

            /// <summary>
            /// The maximum length of a kept url
            /// </summary>
            public const int MaxUrlLength = 160;

            public static String Cap(String url)
            {
                url ??= "";
                return url.Length > MaxUrlLength ? String.Concat(url.AsSpan(0, MaxUrlLength), "…") : url;
            }

            public BanEntry(String key, String ip, long startTicks, long untilTicks, String url, String userAgent, String session)
            {
                Key = key;
                LiftToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                FirstTicks = startTicks;
                StartTicks = startTicks;
                UntilTicks = untilTicks;
                LastSeenTicks = startTicks;
                Address = ip;
                TriggerUrl = Cap(url);
                UserAgent = userAgent;
                Session = session;
                Probes = 1;
                AddUrl(url);
            }

            /// <summary>
            /// The address key (<see cref="HttpIpRateLimiters.GetKey(string)"/>: the address, or the /64 network of an IPv6 address)
            /// </summary>
            public readonly String Key;

            /// <summary>
            /// The random token needed to lift the ban (sent with the ip bans table only, so a crafted link can't lift a ban)
            /// </summary>
            public readonly String LiftToken;

            /// <summary>
            /// When the address was banned the first time (UTC ticks, a ban that expired and was renewed keeps it)
            /// </summary>
            public readonly long FirstTicks;

            /// <summary>
            /// When the current ban started (UTC ticks)
            /// </summary>
            public long StartTicks;

            /// <summary>
            /// When the ban ends (UTC ticks)
            /// </summary>
            public long UntilTicks;

            /// <summary>
            /// The last request of the address (UTC ticks)
            /// </summary>
            public long LastSeenTicks;

            /// <summary>
            /// The latest client address (the exact address, the key may be a network)
            /// </summary>
            public String Address;

            /// <summary>
            /// The url that banned the address (the latest when renewed)
            /// </summary>
            public String TriggerUrl;

            /// <summary>
            /// The latest url
            /// </summary>
            public String LastUrl;

            /// <summary>
            /// The user agent of the request that banned the address
            /// </summary>
            public readonly String UserAgent;

            /// <summary>
            /// The session id (<see cref="HttpSession.TokenHash"/>) of the latest probe, null if it had none
            /// </summary>
            public String Session;

            /// <summary>
            /// The number of times the ban was started or extended by a probe url
            /// </summary>
            public long Probes;

            /// <summary>
            /// The number of requests of the address while banned
            /// </summary>
            public long Requests;

            /// <summary>
            /// The number of decoy files served
            /// </summary>
            public long Decoys;

            /// <summary>
            /// The number of decompression bombs served
            /// </summary>
            public long Bombs;

            /// <summary>
            /// The number of plain (uncompressed) trap answers served
            /// </summary>
            public long Plain;

            /// <summary>
            /// The number of trap answers without a body (too many trap answers in progress, or a HEAD request)
            /// </summary>
            public long Empty;

            /// <summary>
            /// The latest answer, ex: "Bomb (br)", "Decoy (EnvFile)"
            /// </summary>
            public String LastAnswer;

            /// <summary>
            /// The decoy identity of the address, -1 if no decoy was served
            /// </summary>
            public int DecoyIdentity = -1;

            /// <summary>
            /// The AWS key id of the decoy identity, null if no decoy was served
            /// </summary>
            public String DecoyAwsKey;

            readonly List<KeyValuePair<String, int>> UrlList = new();
            int OtherUrls;

            /// <summary>
            /// Count a requested url (the first <see cref="MaxUrls"/> different urls are kept)
            /// </summary>
            public void AddUrl(String url)
            {
                url = Cap(url);
                LastUrl = url;
                var l = UrlList;
                lock (l)
                {
                    for (int i = 0; i < l.Count; ++i)
                    {
                        if (String.Equals(l[i].Key, url, StringComparison.Ordinal))
                        {
                            l[i] = new KeyValuePair<String, int>(url, l[i].Value + 1);
                            return;
                        }
                    }
                    if (l.Count < MaxUrls)
                        l.Add(new KeyValuePair<String, int>(url, 1));
                    else
                        ++OtherUrls;
                }
            }

            /// <summary>
            /// The requested urls (with the number of requests when more than one)
            /// </summary>
            public String[] GetUrls(out int count)
            {
                var l = UrlList;
                lock (l)
                {
                    count = l.Count + OtherUrls;
                    var r = new String[l.Count];
                    for (int i = 0; i < r.Length; ++i)
                    {
                        var x = l[i];
                        r[i] = x.Value > 1 ? String.Concat(x.Key, " (", x.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), ")") : x.Key;
                    }
                    return r;
                }
            }

            /// <summary>
            /// Extend the ban with a new probe
            /// </summary>
            /// <param name="ip">The address</param>
            /// <param name="untilTicks">The new end</param>
            /// <param name="url">The probe url</param>
            /// <param name="session">The session id, null if none</param>
            /// <param name="startTicks">Non zero when an expired ban is renewed: the new start</param>
            public void Renew(String ip, long untilTicks, String url, String session, long startTicks)
            {
                Address = ip;
                if (startTicks != 0)
                {
                    Interlocked.Exchange(ref StartTicks, startTicks);
                    TriggerUrl = Cap(url);
                }
                Interlocked.Exchange(ref UntilTicks, untilTicks);
                Interlocked.Exchange(ref LastSeenTicks, DateTime.UtcNow.Ticks);
                if (session != null)
                    Session = session;
                Interlocked.Increment(ref Probes);
                AddUrl(url);
            }

            /// <summary>
            /// Count a request while banned
            /// </summary>
            public void OnRequest(String ip, String url)
            {
                Address = ip;
                Interlocked.Increment(ref Requests);
                Interlocked.Exchange(ref LastSeenTicks, DateTime.UtcNow.Ticks);
                AddUrl(url);
            }
        }

        sealed class ProbeTrapState
        {
            public ProbeTrapState(HttpServerBase server, HttpProbeTrapParams p)
            {
                P = p;
                var urls = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
                if (p.UseBuiltInUrls)
                    AddUrls(urls, Encoding.UTF8.GetString(typeof(HttpServerBase).GetUncompressedResourceData("data.ProbeUrls.txt").Span).Split('\n'));
                var extra = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
                AddUrls(extra, p.ExtraUrls);
                extra.ExceptWith(urls);
                ExtraUrls = extra;
                urls.UnionWith(extra);
                Urls = urls;
                UrlLookup = urls.GetAlternateLookup<ReadOnlySpan<Char>>();
                NeverBan = server.ParseTrustedProxies(p.NeverBan ?? []);
                NeverTrapHosts = new HashSet<String>((p.NeverTrapHosts ?? []).Select(x => x?.Trim()).Where(x => !String.IsNullOrEmpty(x)), StringComparer.OrdinalIgnoreCase);
                BanTicks = Math.Max(0, p.BanMinutes) * TimeSpan.TicksPerMinute;
                var min = Math.Max(0, p.MinDelayMs);
                MinDelay = min;
                MaxDelay = Math.Max(min, p.MaxDelayMs);
                MaxConcurrent = Math.Max(0, p.MaxConcurrent);
                Plain = CreatePlain(Math.Max(0, p.UncompressedSize));
                Headers = CreateHeaders(urls);
                Cookies = CreateCookies(p.BanMinutes > 0 ? p.BanMinutes * 60 : 3600);
                if (p.UseDecoys)
                {
                    Decoy = new ProbeDecoy(GetDecoySecret(), String.IsNullOrEmpty(p.DecoyName) ? EnvInfo.AppName : p.DecoyName, p.DecoyIdentities, p.CanaryAwsKeys);
                    var dmin = Math.Max(0, p.DecoyMinDelayMs);
                    DecoyMinDelay = dmin;
                    DecoyMaxDelay = Math.Max(dmin, p.DecoyMaxDelayMs);
                }
            }

            /// <summary>
            /// The decoy files, null if disabled
            /// </summary>
            public readonly ProbeDecoy Decoy;
            public readonly int DecoyMinDelay;
            public readonly int DecoyMaxDelay;

            /// <summary>
            /// The secret of the decoy values of this installation, stored in the application key value store (stable across restarts,
            /// so a value from an alert can be traced back), a random one per process if it can't be stored
            /// </summary>
            static Byte[] GetDecoySecret()
            {
                const String key = "HttpServer.ProbeDecoySecret";
                try
                {
                    var s = KeyValueStore.AllApp.TryGet<String>(key);
                    if (!String.IsNullOrEmpty(s))
                    {
                        var b = Convert.FromBase64String(s);
                        if (b.Length == 32)
                            return b;
                    }
                    var n = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
                    KeyValueStore.AllApp.Set(key, Convert.ToBase64String(n));
                    return n;
                }
                catch
                {
                    return System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
                }
            }

            public readonly HttpProbeTrapParams P;

            /// <summary>
            /// All probe urls (the built-in list and <see cref="HttpProbeTrapParams.ExtraUrls"/>)
            /// </summary>
            public readonly HashSet<String> Urls;

            /// <summary>
            /// The probe urls of <see cref="HttpProbeTrapParams.ExtraUrls"/> that aren't in the built-in list
            /// </summary>
            public readonly HashSet<String> ExtraUrls;
            readonly HashSet<String>.AlternateLookup<ReadOnlySpan<Char>> UrlLookup;
            readonly System.Net.IPNetwork[] NeverBan;

            /// <summary>
            /// The host names of <see cref="HttpProbeTrapParams.NeverTrapHosts"/>
            /// </summary>
            readonly HashSet<String> NeverTrapHosts;

            /// <summary>
            /// True if a request must not be trapped: the host is in <see cref="HttpProbeTrapParams.NeverTrapHosts"/> and the client address in <see cref="HttpProbeTrapParams.NeverBan"/>
            /// </summary>
            /// <param name="req">The request</param>
            /// <param name="ip">The client address</param>
            public bool IsNeverTrapped(HttpServerRequest req, String ip)
            {
                if (NeverTrapHosts.Count == 0)
                    return false;
                var host = req.GetReqHeader("Host");
                if (String.IsNullOrEmpty(host))
                    return false;
                host = host.Trim();
                //  Remove the port ("[::1]:443", "dev.sysweaver.com:443")
                var e = host.StartsWith('[') ? host.IndexOf(']') + 1 : host.LastIndexOf(':');
                if ((e > 0) && (e < host.Length) && (host[e] == ':'))
                    host = host.Substring(0, e);
                host = host.TrimEnd('.');
                return NeverTrapHosts.Contains(host) && IsNeverBanned(ip);
            }
            public readonly long BanTicks;
            public readonly int MinDelay;
            public readonly int MaxDelay;
            public readonly int MaxConcurrent;
            public readonly Byte[] Plain;
            public readonly KeyValuePair<String, String>[] Headers;
            public readonly String[] Cookies;

            /// <summary>
            /// The number of trap answers in progress
            /// </summary>
            public int InProgress;

            /// <summary>
            /// Banned addresses (<see cref="HttpIpRateLimiters.GetKey(string)"/>)
            /// </summary>
            readonly ConcurrentDictionary<String, BanEntry> Bans = new(StringComparer.Ordinal);

            /// <summary>
            /// The bans (including expired ones that haven't been removed yet)
            /// </summary>
            public IEnumerable<BanEntry> GetBans() => Volatile.Read(ref BanCount) <= 0 ? [] : Bans.Values;

            /// <summary>
            /// Get the active ban of an address key, null if it isn't banned
            /// </summary>
            public BanEntry Find(String key)
            {
                if ((Volatile.Read(ref BanCount) <= 0) || (key == null) || !Bans.TryGetValue(key, out var e))
                    return null;
                return e.UntilTicks > DateTime.UtcNow.Ticks ? e : null;
            }

            /// <summary>
            /// Get a ban from its lift token (<see cref="BanEntry.LiftToken"/>), null if not found
            /// </summary>
            public BanEntry FindByLiftToken(String token)
            {
                if (String.IsNullOrEmpty(token) || (Volatile.Read(ref BanCount) <= 0))
                    return null;
                var a = Encoding.ASCII.GetBytes(token);
                foreach (var x in Bans.Values)
                    if (System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, Encoding.ASCII.GetBytes(x.LiftToken)))
                        return x;
                return null;
            }

            /// <summary>
            /// Remove the ban of an address key
            /// </summary>
            /// <returns>The removed ban, null if the address wasn't banned</returns>
            public BanEntry Lift(String key)
            {
                if ((key == null) || !Bans.TryRemove(key, out var e))
                    return null;
                Interlocked.Decrement(ref BanCount);
                return e;
            }

            /// <summary>
            /// The number of bans (so that requests don't look anything up when no address is banned)
            /// </summary>
            int BanCount;

            /// <summary>
            /// The number of banned addresses
            /// </summary>
            public int BannedAddresses => Volatile.Read(ref BanCount);

            static void AddUrls(HashSet<String> to, IEnumerable<String> urls)
            {
                if (urls == null)
                    return;
                foreach (var x in urls)
                {
                    var u = x?.Trim();
                    if (String.IsNullOrEmpty(u) || (u[0] == '#'))
                        continue;
                    u = u.TrimStart('/');
                    if (u.Length > 0)
                        to.Add(u);
                }
            }

            /// <summary>
            /// True if a local url is a probe url: the whole url, or the url below any folder (ex: "app/.env" for ".env"), the query is ignored
            /// </summary>
            public bool IsProbe(String localUrl)
            {
                if (String.IsNullOrEmpty(localUrl))
                    return false;
                var s = localUrl.AsSpan();
                var q = s.IndexOf('?');
                if (q >= 0)
                    s = s.Slice(0, q);
                s = s.Trim('/');
                var l = UrlLookup;
                while (!s.IsEmpty)
                {
                    if (l.Contains(s))
                        return true;
                    var i = s.IndexOf('/');
                    if (i < 0)
                        return false;
                    s = s.Slice(i + 1);
                }
                return false;
            }

            /// <summary>
            /// True if the client address of a request is banned (nothing is looked up when no address is banned)
            /// </summary>
            public bool IsBanned(HttpServerRequest req)
            {
                if (Volatile.Read(ref BanCount) <= 0)
                    return false;
                var key = HttpIpRateLimiters.GetKey(req.GetIpAddress());
                if (!Bans.TryGetValue(key, out var e))
                    return false;
                if (e.UntilTicks > DateTime.UtcNow.Ticks)
                    return true;
                if (Bans.TryRemove(new KeyValuePair<String, BanEntry>(key, e)))
                    Interlocked.Decrement(ref BanCount);
                return false;
            }

            /// <summary>
            /// True if an address is in <see cref="HttpProbeTrapParams.NeverBan"/>
            /// </summary>
            public bool IsNeverBanned(String ip)
            {
                if (!System.Net.IPAddress.TryParse(ip, out var a))
                    return false;
                if (a.IsIPv4MappedToIPv6)
                    a = a.MapToIPv4();
                foreach (var n in NeverBan)
                    if (n.Contains(a))
                        return true;
                return false;
            }

            /// <summary>
            /// Ban the address (unless it's in <see cref="HttpProbeTrapParams.NeverBan"/>) and the session of a request
            /// </summary>
            /// <param name="req">The request</param>
            /// <param name="ip">The client address</param>
            /// <param name="session">The session of the request, null if none</param>
            /// <param name="url">The probe url</param>
            /// <param name="until">The end of the ban</param>
            /// <param name="entry">The ban of the address, null if the address isn't banned (never banned, or bans are disabled)</param>
            /// <returns>True if the address wasn't already banned</returns>
            public bool Ban(HttpServerRequest req, String ip, HttpSession session, String url, out DateTime until, out BanEntry entry)
            {
                var ticks = BanTicks;
                until = default;
                entry = null;
                if (ticks <= 0)
                    return false;
                var now = DateTime.UtcNow.Ticks;
                var u = now + ticks;
                until = new DateTime(u, DateTimeKind.Utc);
                if (session != null)
                {
                    session.BannedUrl = BanEntry.Cap(url);
                    Interlocked.Exchange(ref session.BannedUntilTicks, u);
                }
                if (IsNeverBanned(ip))
                    return false;
                var key = HttpIpRateLimiters.GetKey(ip);
                var b = Bans;
                var e = new BanEntry(key, ip, now, u, url, req?.GetReqHeader("User-Agent"), session?.TokenHash);
                while (true)
                {
                    if (b.TryAdd(key, e))
                    {
                        entry = e;
                        if (Interlocked.Increment(ref BanCount) > Math.Max(1, P.MaxBannedAddresses))
                            Prune();
                        return true;
                    }
                    if (b.TryGetValue(key, out var existing))
                    {
                        //  Already banned (or an expired ban that wasn't removed yet): extend it, keep the history
                        var renewed = existing.UntilTicks <= now;
                        existing.Renew(ip, u, url, session?.TokenHash, renewed ? now : 0);
                        entry = existing;
                        return renewed;
                    }
                }
            }

            void Prune()
            {
                var b = Bans;
                var now = DateTime.UtcNow.Ticks;
                foreach (var x in b)
                    if ((x.Value.UntilTicks <= now) && b.TryRemove(x))
                        Interlocked.Decrement(ref BanCount);
                if (Volatile.Read(ref BanCount) <= Math.Max(1, P.MaxBannedAddresses))
                    return;
                //  Still too many (a large distributed probe): drop the half that ends first
                var all = b.ToArray();
                Array.Sort(all, (x, y) => x.Value.UntilTicks.CompareTo(y.Value.UntilTicks));
                for (int i = 0; i < all.Length / 2; ++i)
                    if (b.TryRemove(all[i]))
                        Interlocked.Decrement(ref BanCount);
            }

            static Byte[] CreatePlain(int size)
            {
                var m = Encoding.ASCII.GetBytes(TrapMessage);
                var r = new Byte[size];
                for (int i = 0; i < size; i += m.Length)
                    m.AsSpan(0, Math.Min(m.Length, size - i)).CopyTo(r.AsSpan(i));
                return r;
            }

            static String Junk(int length)
            {
                var b = new Byte[(length * 3 + 3) / 4];
                Random.Shared.NextBytes(b);
                var s = Convert.ToBase64String(b);
                return s.Length > length ? s.Substring(0, length) : s;
            }

            /// <summary>
            /// The hostile headers: a fake (old, "vulnerable") server stack to send the probe after exploits that don't apply, huge validators, links and a refresh to
            /// more trap urls (crawlers follow them into the trap), a forever cache and a long retry, and many large junk headers (memory and parsing time of the client).
            /// All values are valid header text, so the server itself never fails to send them.
            /// </summary>
            static KeyValuePair<String, String>[] CreateHeaders(HashSet<String> urls)
            {
                var h = new List<KeyValuePair<String, String>>();
                void Add(String k, String v) => h.Add(new KeyValuePair<String, String>(k, v));
                Add("Server", "Apache/2.2.15 (CentOS) PHP/5.3.3 OpenSSL/1.0.1e-fips mod_ssl/2.2.15 mod_perl/2.0.4 Perl/v5.10.1");
                Add("X-Powered-By", "PHP/5.3.3");
                Add("X-AspNet-Version", "1.1.4322");
                Add("Cache-Control", "public, max-age=31536000, immutable");
                Add("Last-Modified", "Thu, 01 Jan 1970 00:00:00 GMT");
                Add("ETag", "\"" + Junk(4096) + "\"");
                Add("Retry-After", "2147483647");
                Add("Vary", "*");
                Add("Content-Disposition", "inline; filename=\"GET-A-JOB.txt\"");
                Add("Content-Language", "x-fbi, x-interpol, x-europol, en-GB, en-US, sv-SE, de-DE, fr-FR, es-ES, ja-JP, zh-CN, ru-RU");
                Add("X-Robots-Tag", "noindex, nofollow");
                var trap = urls.OrderBy(x => x, StringComparer.Ordinal).Take(64).ToArray();
                if (trap.Length > 0)
                {
                    Add("Refresh", "0; url=/" + trap[0]);
                    Add("Link", String.Join(", ", trap.Select(x => String.Concat("</", x, ">; rel=preload; as=fetch"))));
                }
                for (int i = 0; i < 32; ++i)
                    Add("X-Fbi-Case-" + (1000 + i).ToString(System.Globalization.CultureInfo.InvariantCulture), Junk(1024));
                return h.ToArray();
            }

            /// <summary>
            /// Large cookies that expire with the ban: a client that keeps them sends them back with every request (and with enough of them its requests are too large to serve)
            /// </summary>
            static String[] CreateCookies(int maxAgeSeconds)
            {
                var c = new String[20];
                for (int i = 0; i < c.Length; ++i)
                    c[i] = String.Concat("fbi_case_", i.ToString(System.Globalization.CultureInfo.InvariantCulture), "=", Junk(2048), "; Max-Age=", maxAgeSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture), "; Path=/; HttpOnly");
                return c;
            }

            /// <summary>
            /// The pre-compressed answer for the best encoding that the client accepts (brotli, zstd, gzip, deflate, in that order), null if none
            /// </summary>
            public static ReadOnlyMemory<Byte>? GetBomb(String acceptEncoding, out String encoding)
            {
                encoding = null;
                if (String.IsNullOrEmpty(acceptEncoding))
                    return null;
                bool br = false, zstd = false, gzip = false, deflate = false;
                foreach (var x in acceptEncoding.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    var name = x;
                    var semi = x.IndexOf(';');
                    if (semi >= 0)
                    {
                        name = x.Substring(0, semi).Trim();
                        var q = x.Substring(semi + 1).Replace(" ", "");
                        if (q.StartsWith("q=0", StringComparison.OrdinalIgnoreCase) && !q.AsSpan(3).ContainsAnyInRange('1', '9'))
                            continue;
                    }
                    switch (name.ToLowerInvariant())
                    {
                        case "br":
                            br = true;
                            break;
                        case "zstd":
                            zstd = true;
                            break;
                        case "gzip":
                        case "x-gzip":
                            gzip = true;
                            break;
                        case "deflate":
                            deflate = true;
                            break;
                        case "*":
                            br = true;
                            break;
                    }
                }
                String ext;
                if (br)
                {
                    encoding = "br";
                    ext = "br";
                }
                else if (zstd)
                {
                    encoding = "zstd";
                    ext = "zst";
                }
                else if (gzip)
                {
                    encoding = "gzip";
                    ext = "gz";
                }
                else if (deflate)
                {
                    encoding = "deflate";
                    ext = "deflate";
                }
                else
                    return null;
                return typeof(HttpServerBase).Assembly.GetResourceData(TrapResourcePrefix + ext);
            }
        }

        /// <summary>
        /// The probe trap answer (see <see cref="HttpProbeTrapParams"/>): a delay, status 200, hostile headers and a decompression bomb (or the plain message)
        /// </summary>
        /// <param name="data">The request</param>
        /// <param name="ban">The ban of the client address (the answer is counted on it), null if none</param>
        async Task ProbeTrapRespond(HttpServerRequest data, BanEntry ban)
        {
            var t = ProbeTrap;
            //  A probe for a known secret file gets a believable decoy (a normal answer), anything else the hostile answer
            var decoy = t.Decoy;
            if (decoy != null)
            {
                var type = ProbeDecoy.Detect(data.LocalUrl);
                if (type != ProbePayloadTypes.None)
                {
                    await DecoyRespond(data, t, decoy, type, ban).ConfigureAwait(false);
                    return;
                }
            }
            //  Above the limit: answer at once and without a body (a flood of banned requests mustn't tie up the server or its bandwidth)
            bool full = Interlocked.Increment(ref t.InProgress) <= t.MaxConcurrent;
            try
            {
                if (full && (t.MaxDelay > 0))
                    await Task.Delay(Random.Shared.Next(t.MinDelay, t.MaxDelay + 1)).ConfigureAwait(false);
                //  Not an error for the intrusion detection (the address is banned anyway)
                data.IntrusionTracked = true;
                data.SetResStatusCode(200);
                if (!full)
                {
                    if (ban != null)
                    {
                        Interlocked.Increment(ref ban.Empty);
                        ban.LastAnswer = "Empty (too many in progress)";
                    }
                    return;
                }
                foreach (var h in t.Headers)
                {
                    try
                    {
                        data.SetResHeader(h.Key, h.Value);
                    }
                    catch
                    {
                        //  A header the listener doesn't allow (ex: http.sys owns "Server"), skip it
                    }
                }
                foreach (var c in t.Cookies)
                {
                    try
                    {
                        data.UpdateCookie(c);
                    }
                    catch
                    {
                    }
                }
                data.SetResMime("text/plain; charset=utf-8");
                if (data.IsHead)
                {
                    if (ban != null)
                    {
                        Interlocked.Increment(ref ban.Empty);
                        ban.LastAnswer = "Headers (HEAD)";
                    }
                    return;
                }
                var bomb = ProbeTrapState.GetBomb(data.AcceptEncoding, out var enc);
                if (bomb != null)
                {
                    data.SetResHeader("Content-Encoding", enc);
                    data.SetResBody(bomb.Value.Span);
                    if (ban != null)
                    {
                        Interlocked.Increment(ref ban.Bombs);
                        ban.LastAnswer = String.Concat("Bomb (", enc, ")");
                    }
                    return;
                }
                data.SetResBody(t.Plain);
                if (ban != null)
                {
                    Interlocked.Increment(ref ban.Plain);
                    ban.LastAnswer = "Plain text";
                }
            }
            finally
            {
                Interlocked.Decrement(ref t.InProgress);
            }
        }

        /// <summary>
        /// Requests that got a decoy file
        /// </summary>
        readonly HttpRequestTracker DecoyRequests = new HttpRequestTracker();

        /// <summary>
        /// Answer with a decoy file (see <see cref="ProbeDecoy"/>): like a normal static file (a short delay, the usual headers, no strange content),
        /// with the identity of the client address. Every served decoy is logged (identity, AWS key id, client address, url) so that a use of the
        /// values can be traced back.
        /// </summary>
        async Task DecoyRespond(HttpServerRequest data, ProbeTrapState t, ProbeDecoy decoy, ProbePayloadTypes type, BanEntry ban)
        {
            if (t.DecoyMaxDelay > 0)
                await Task.Delay(Random.Shared.Next(t.DecoyMinDelay, t.DecoyMaxDelay + 1)).ConfigureAwait(false);
            var url = data.LocalUrl;
            var ip = data.GetIpAddress();
            var payload = decoy.GetPayload(out var mime, out var identity, url, type, HttpIpRateLimiters.GetKey(ip));
            data.IntrusionTracked = true;
            data.SetResStatusCode(200);
            data.SetResMime(mime);
            var time = decoy.GetTime(identity);
            try
            {
                data.SetResHeader("Last-Modified", time.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                //  An nginx style etag (hex time - hex length)
                data.SetResHeader("ETag", String.Concat("\"", new DateTimeOffset(time).ToUnixTimeSeconds().ToString("x", System.Globalization.CultureInfo.InvariantCulture), "-", payload.Length.ToString("x", System.Globalization.CultureInfo.InvariantCulture), "\""));
                data.SetResHeader("Accept-Ranges", "bytes");
            }
            catch
            {
            }
            DecoyRequests.OnRequest(url, ip);
            var awsKey = decoy.GetAwsKeyId(identity);
            if (ban != null)
            {
                Interlocked.Increment(ref ban.Decoys);
                ban.DecoyIdentity = identity;
                ban.DecoyAwsKey = awsKey;
                ban.LastAnswer = String.Concat("Decoy (", type.ToString(), ")");
            }
            Msg?.AddMessage(String.Concat(Prefix, "Probe decoy: identity ", identity.ToString(System.Globalization.CultureInfo.InvariantCulture), " (AWS key ", awsKey, ") served to ", ip, " for \"", url, "\""), MessageLevels.Info);
            if (!data.IsHead)
                data.SetResBody(payload.Span);
        }

        /// <summary>
        /// Handle a request that hit no end point: the probe trap for a probe url (see <see cref="HttpProbeTrapParams"/>), else counted as a 404
        /// </summary>
        /// <param name="data">The request</param>
        /// <returns>True if the probe trap answered</returns>
        async Task<bool> HandleProbe(HttpServerRequest data)
        {
            var url = data.LocalUrl;
            var ip = data.GetIpAddress();
            var t = ProbeTrap;
            //  A probe url of the list, or a known secret file (see ProbeDecoy.Detect)
            //  (a development host on this computer, ex: dev.sysweaver.com, gets a normal 404, see HttpProbeTrapParams.NeverTrapHosts)
            if ((t == null) || !(t.IsProbe(url) || (ProbeDecoy.Detect(url) != ProbePayloadTypes.None)) || t.IsNeverTrapped(data, ip))
            {
                NotFoundRequests.OnRequest(url, ip);
                return false;
            }
            ProbeRequests.OnRequest(url, ip);
            if (t.Ban(data, ip, data.Session, url, out var until, out var ban))
                Msg?.AddMessage(String.Concat(Prefix, "Probe trap: banned ", ip, " until ", until.ToString("u"), ", it asked for \"", url, "\""), MessageLevels.Warning);
            await ProbeTrapRespond(data, ban).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Handle a request of a banned address (no session) or of a banned session: the probe trap answers
        /// </summary>
        /// <param name="data">The request</param>
        Task HandleBanned(HttpServerRequest data)
        {
            var url = data.LocalUrl;
            var ip = data.GetIpAddress();
            BannedRequests.OnRequest(url, ip);
            var ban = ProbeTrap?.Find(HttpIpRateLimiters.GetKey(ip));
            ban?.OnRequest(ip, url);
            return ProbeTrapRespond(data, ban);
        }

        /// <summary>
        /// The statistics of the probe trap and of the requests that hit no end point
        /// </summary>
        IEnumerable<Stats> GetProbeTrapStats(String sys)
        {
            foreach (var e in ProbeRequests.GetStats(sys, "Probe.", "requests for a probe url (answered by the probe trap)"))
                yield return e;
            foreach (var e in BannedRequests.GetStats(sys, "Banned.", "requests of a banned address or session (answered by the probe trap)"))
                yield return e;
            foreach (var e in DecoyRequests.GetStats(sys, "Decoy.", "requests that got a decoy secret file (from the probe trap)"))
                yield return e;
            var t = ProbeTrap;
            if (t != null)
                yield return new Stats(sys, "Banned.Addresses", t.BannedAddresses, "The number of client addresses that are banned by the probe trap right now");
            foreach (var e in NotFoundRequests.GetStats(sys, "NotFound.", "requests that hit no end point (404)"))
                yield return e;
            foreach (var e in CrossSiteRequests.GetStats(sys, "CrossSite.", "refused cross site requests of signed in sessions (CSRF protection)"))
                yield return e;
        }
    }
}
