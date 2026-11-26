using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Serialization;

namespace SysWeaver.Net
{
    /// <summary>
    /// Parameters of the simple intrusion detection of the http server (see <see cref="IntrusionDetection"/>).
    /// </summary>
    public sealed class IntrusionDetectionParams
    {
        /// <summary>
        /// Set to false to disable the intrusion detection
        /// </summary>
        public bool Enabled = true;

        /// <summary>
        /// The tracking window in seconds: the state of a client address is kept this long after its last tracked request,
        /// and an intrusion is reported at most once per window and address.
        /// </summary>
        public int WindowSeconds = 300;

        /// <summary>
        /// The response status codes that count as an error (ex: scanning a site for API's or files)
        /// </summary>
        public int[] ErrorStatusCodes = [400, 401, 403, 404, 405];

        /// <summary>
        /// An intrusion is reported when a client address got errors for this many different urls (within the window)
        /// </summary>
        public int ErrorThreshold = 50;

        /// <summary>
        /// An intrusion is reported when a client address made this many calls to tracked API's (within the window).
        /// Tracked API's are the audited ones (<see cref="SysWeaver.MicroService.WebApiAuditAttribute"/>, if <see cref="TrackAuditedApis"/> is true)
        /// and the ones with a <see cref="SysWeaver.MicroService.WebApiIntrusionTrackAttribute"/>.
        /// </summary>
        public int ApiThreshold = 50;

        /// <summary>
        /// If true, all audited API's are tracked (ex: sign in attempts, administration, data exports)
        /// </summary>
        public bool TrackAuditedApis = true;

        /// <summary>
        /// The minimum random delay (in ms) added to error responses (and to tracked API calls of an address that triggered an intrusion within the window)
        /// </summary>
        public int MinDelayMs = 200;

        /// <summary>
        /// The maximum random delay (in ms), 0 or less disables the delay
        /// </summary>
        public int MaxDelayMs = 2000;

        /// <summary>
        /// The maximum number of chars of the url list in an intrusion report
        /// </summary>
        public int MaxReportChars = 2048;

        /// <summary>
        /// The maximum number of tracked addresses (bounds the memory use), when reached new addresses share one state (reported as address "*", ex: a distributed scan)
        /// </summary>
        public int MaxAddresses = 20_000;
    }

    /// <summary>
    /// The details of a detected intrusion attempt
    /// </summary>
    public sealed class IntrusionInfo
    {
        /// <summary>
        /// The kind: "errors" (many error responses, ex: scanning for API's / files) or "api" (many calls to tracked API's, ex: password guessing, data exfiltration)
        /// </summary>
        public String Kind;

        /// <summary>
        /// The client address (an IPv6 address is reported as its /64 network)
        /// </summary>
        public String Address;

        /// <summary>
        /// The number of tracked requests since the last report of this address (or since tracking started)
        /// </summary>
        public long Requests;

        /// <summary>
        /// The number of different urls
        /// </summary>
        public int UrlCount;

        /// <summary>
        /// The urls (with the number of requests when more than one), limited to <see cref="IntrusionDetectionParams.MaxReportChars"/> chars
        /// </summary>
        public String Urls;

        /// <inheritdoc/>
        public override string ToString() => String.Concat("Possible intrusion (", Kind, ") from ", Address, ": ", Requests.ToString(), " requests, ", UrlCount.ToString(), " urls: ", Urls);

        /// <summary>
        /// Get the output of the audit entry: json (like the output of any other audited call).
        /// The urls are not included (they are in the input of the audit entry), so it is short enough to never be capped (capped json is not valid).
        /// </summary>
        /// <returns>The json text of an <see cref="IntrusionAuditOutput"/></returns>
        public String GetAuditJson() => SerManager.GetText("json").ToStringWithoutType(new IntrusionAuditOutput
        {
            Message = String.Concat("Possible intrusion (", Kind, ") from ", Address),
            Requests = Requests,
            UrlCount = UrlCount,
        });
    }

    /// <summary>
    /// The output of the audit entry of an intrusion attempt, see <see cref="IntrusionInfo.GetAuditJson"/>
    /// </summary>
    public sealed class IntrusionAuditOutput
    {
        /// <summary>
        /// What was detected
        /// </summary>
        public String Message;

        /// <summary>
        /// The number of tracked requests, see <see cref="IntrusionInfo.Requests"/>
        /// </summary>
        public long Requests;

        /// <summary>
        /// The number of different urls, see <see cref="IntrusionInfo.UrlCount"/>
        /// </summary>
        public int UrlCount;
    }

    /// <summary>
    /// A simple intrusion detection: tracks error responses and calls to key API's per client address, and reports an intrusion attempt
    /// (<see cref="Detected"/>) when an address passes a threshold within a time window. Error responses get a random delay (slows down scanning).
    /// </summary>
    /// <remarks>
    /// Per address detection doesn't catch a distributed attack (each address stays below the thresholds), it catches a single scanner or guesser.
    /// Thread safe.
    /// </remarks>
    public sealed class IntrusionDetection
    {
        /// <summary>
        /// Create the detection
        /// </summary>
        /// <param name="p">Parameters, null for the defaults</param>
        public IntrusionDetection(IntrusionDetectionParams p)
        {
            p = p ?? new IntrusionDetectionParams();
            P = p;
            var w = TimeSpan.FromSeconds(Math.Max(10, p.WindowSeconds));
            WindowTicks = w.Ticks;
            States = new FastMemCache<String, AddressState>(w, StringComparer.Ordinal);
            var codes = new bool[600];
            foreach (var c in p.ErrorStatusCodes ?? [])
                if ((c >= 0) && (c < codes.Length))
                    codes[c] = true;
            ErrorCodes = codes;
        }

        readonly IntrusionDetectionParams P;
        readonly long WindowTicks;
        readonly bool[] ErrorCodes;
        readonly FastMemCache<String, AddressState> States;

        /// <summary>
        /// True if audited API's are tracked
        /// </summary>
        public bool TrackAuditedApis => P.TrackAuditedApis;

        /// <summary>
        /// Called when an intrusion attempt is detected (at most once per window and address and kind)
        /// </summary>
        public event Action<IntrusionInfo, HttpServerRequest> Detected;

        sealed class Category
        {
            public readonly ConcurrentDictionary<String, int> Urls = new(StringComparer.Ordinal);
            public long Count;
            public long LastReport;
        }

        sealed class AddressState
        {
            public readonly Category Errors = new();
            public readonly Category Api = new();
        }

        /// <summary>
        /// True if a status code counts as an error
        /// </summary>
        /// <param name="statusCode">The status code</param>
        /// <returns>True if tracked</returns>
        public bool IsError(int statusCode) => (statusCode >= 0) && (statusCode < ErrorCodes.Length) && ErrorCodes[statusCode];

        /// <summary>
        /// The address of the shared state used when <see cref="IntrusionDetectionParams.MaxAddresses"/> is reached
        /// </summary>
        public const String OverflowAddress = "*";

        /// <summary>
        /// The approximate number of tracked addresses (incremented on create, synced with the cache when the limit is reached)
        /// </summary>
        int ApproxCount;
        long NextCountSync;

        AddressState GetState(HttpServerRequest request, out String address)
        {
            var a = HttpIpRateLimiters.GetKey(request.GetIpAddress());
            var max = Math.Max(1, P.MaxAddresses);
            if ((Volatile.Read(ref ApproxCount) >= max) && !States.TryGet(a, out _))
            {
                //  Full: sync the count (expired entries are pruned by the cache) at most once per second, else use the shared state
                var now = Environment.TickCount64;
                if (now >= Interlocked.Read(ref NextCountSync))
                {
                    Interlocked.Exchange(ref NextCountSync, now + 1000);
                    Volatile.Write(ref ApproxCount, States.GetCount());
                }
                if (Volatile.Read(ref ApproxCount) >= max)
                    a = OverflowAddress;
            }
            address = a;
            //  Updating the entry extends its life time (the window restarts with every tracked request)
            return States.GetOrUpdateWithExisting(a, (k, existing) =>
            {
                if (existing != null)
                    return existing;
                Interlocked.Increment(ref ApproxCount);
                return new AddressState();
            });
        }

        /// <summary>
        /// Track a finished request with an error status code, a random delay is returned (await it before the response is completed)
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="url">The requested url</param>
        /// <returns>A task that completes after the delay</returns>
        public Task OnError(HttpServerRequest request, String url)
        {
            var s = GetState(request, out var address);
            Track(s.Errors, s, "errors", address, request, url, true);
            return Delay();
        }

        /// <summary>
        /// Track a call to a key API (ex: sign in), an address that triggered an intrusion within the window gets a random delay
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="url">The API url</param>
        /// <returns>A task that completes after the delay (completed if no delay)</returns>
        public Task OnApi(HttpServerRequest request, String url)
        {
            var s = GetState(request, out var address);
            Track(s.Api, s, "api", address, request, url, false);
            var e = s.Errors;
            var a = s.Api;
            var now = DateTime.UtcNow.Ticks;
            var flagged = (now - Interlocked.Read(ref e.LastReport) < WindowTicks) || (now - Interlocked.Read(ref a.LastReport) < WindowTicks);
            return flagged ? Delay() : Task.CompletedTask;
        }

        Task Delay()
        {
            var max = P.MaxDelayMs;
            if (max <= 0)
                return Task.CompletedTask;
            var min = Math.Clamp(P.MinDelayMs, 0, max);
            return Task.Delay(Random.Shared.Next(min, max + 1));
        }

        void Track(Category c, AddressState s, String kind, String address, HttpServerRequest request, String url, bool distinctUrls)
        {
            var threshold = Math.Max(1, distinctUrls ? P.ErrorThreshold : P.ApiThreshold);
            var count = Interlocked.Increment(ref c.Count);
            var urls = c.Urls;
            url = url ?? "";
            if (url.Length > 256)
                url = url.Substring(0, 256);
            //  Bounded: a rate limited address keeps counting, but the url set doesn't grow without limit
            if ((urls.Count < threshold * 2) || urls.ContainsKey(url))
                urls.AddOrUpdate(url, 1, static (_, n) => n + 1);
            var reached = distinctUrls ? urls.Count >= threshold : count >= threshold;
            if (!reached)
                return;
            IntrusionInfo info;
            lock (c)
            {
                var now = DateTime.UtcNow.Ticks;
                //  At most one report per window
                if (now - c.LastReport < WindowTicks)
                    return;
                var list = urls.ToArray();
                if (distinctUrls ? list.Length < threshold : Interlocked.Read(ref c.Count) < threshold)
                    return;
                c.LastReport = now;
                var requests = Interlocked.Exchange(ref c.Count, 0);
                urls.Clear();
                var max = Math.Max(64, P.MaxReportChars);
                var sb = new StringBuilder();
                foreach (var x in list.OrderByDescending(x => x.Value))
                {
                    var t = x.Value > 1 ? String.Concat(x.Key, " (", x.Value.ToString(), ")") : x.Key;
                    if (sb.Length + t.Length + 2 > max)
                    {
                        sb.Append(", ...");
                        break;
                    }
                    if (sb.Length > 0)
                        sb.Append(", ");
                    sb.Append(t);
                }
                info = new IntrusionInfo
                {
                    Kind = kind,
                    Address = address,
                    Requests = requests,
                    UrlCount = list.Length,
                    Urls = sb.ToString(),
                };
            }
            try
            {
                Detected?.Invoke(info, request);
            }
            catch
            {
            }
        }
    }
}

namespace SysWeaver.Net
{
    /// <summary>
    /// A detected intrusion attempt (used for the problems list and the audit entry, never thrown)
    /// </summary>
    public sealed class IntrusionDetectedException : Exception
    {
        /// <summary>
        /// Create the exception
        /// </summary>
        /// <param name="message">The details</param>
        public IntrusionDetectedException(String message) : base(message)
        {
        }
    }
}
