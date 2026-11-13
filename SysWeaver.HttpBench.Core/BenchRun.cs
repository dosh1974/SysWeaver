using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Compression;

namespace SysWeaver.HttpBench
{
    /// <summary>
    /// One test: the workers that call the end points and the metrics they collect.
    /// Every worker is a client of its own (a connection pool of one connection and a cookie jar, so one server session per worker) that
    /// calls the selected end points in order, one request at a time, until the time is up.
    /// The metrics are kept per end point and second, so every table and chart is built from the same data.
    /// </summary>
    public sealed class BenchRun : IDisposable
    {
        /// <summary>
        /// An end point, ready to be called.
        /// </summary>
        sealed class Prepared
        {
            public BenchEndPoint EndPoint;
            public HttpMethod Method;
            public Uri Uri;
            public String Accept;
            /// <summary>
            /// The (compressed) body, null for none.
            /// </summary>
            public Byte[] Body;
            /// <summary>
            /// The size of the api value as sent: the (compressed) POST body or the GET query string, 0 for none.
            /// </summary>
            public long ValueSize;
            public int Index;
        }

        /// <summary>
        /// The http method each end point is called with ("GET" / "POST", by <see cref="BenchStartRequest.SendAsGet"/> for apis with a value).
        /// </summary>
        public readonly String[] Methods;

        /// <summary>
        /// The query string letter of a compression ("_" + letter + base64url data, see the HttpServer README "Calling APIs").
        /// </summary>
        static Char QueryLetter(String httpCode) => httpCode switch
        {
            "deflate" => 'd',
            "gzip" => 'g',
            "br" => 'b',
            "zstd" => 'z',
            _ => throw new Exception("The compression " + httpCode.ToQuoted() + " can't be sent in a query string"),
        };

        /// <summary>
        /// Encode an api value (json) as a GET query string: the url encoded json without compression, else "_" + the letter of the
        /// compression + the compressed json as base64url (no padding).
        /// </summary>
        static String QueryValue(Byte[] json, ICompType comp)
        {
            if (comp == null)
                return Uri.EscapeDataString(Encoding.UTF8.GetString(json));
            var data = comp.GetCompressed(json, CompEncoderLevels.Balanced).ToArray();
            var b64 = Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return String.Concat("_", QueryLetter(comp.HttpCode).ToString(), b64);
        }

        /// <summary>
        /// One worker (a client that sends one request at a time).
        /// </summary>
        public sealed class Worker
        {
            public int Index;
            public HttpClient Client;
            public readonly WireCounters Wire = new WireCounters();
            /// <summary>
            /// The totals of this worker (only the worker writes them).
            /// </summary>
            public readonly BenchCounters Totals = new BenchCounters(false);
            public readonly Byte[] Buffer = new Byte[64 * 1024];
            public BenchSample[] Samples;
            public long SamplesSeen;
            public Random Rng;
            public long Started;

            /// <summary>
            /// The counters of the requests that completed in <see cref="LocalSecond"/>, per end point (with histograms).
            /// Only the worker writes them, they are merged into the shared counters when the second changes and when the worker ends.
            /// </summary>
            internal BenchCounters[] Local;

            /// <summary>
            /// The second of <see cref="Local"/>, -1 for none.
            /// </summary>
            internal int LocalSecond = -1;

            /// <summary>
            /// The responses per end point and status of <see cref="LocalSecond"/>.
            /// </summary>
            internal readonly Dictionary<(int EndPoint, int Status), long> LocalStatuses = new Dictionary<(int, int), long>();

            /// <summary>
            /// The time out of a request (linked to the abort of the test), reused for every request until it fires.
            /// </summary>
            internal CancellationTokenSource TimeoutCts;
        }

        public BenchRun(int id, BenchSetup setup, BenchLimits limits = null)
        {
            limits = limits ?? new BenchLimits();
            var settings = setup.Settings;
            var target = setup.Target;
            var endPoints = setup.EndPoints;
            var acceptEncoding = setup.AcceptEncoding;
            var bodyCompression = setup.BodyCompression;
            var p = limits;
            Id = id;
            Settings = settings;
            Target = target;
            EndPoints = endPoints;
            Concurrency = settings.Concurrency;
            DurationSeconds = settings.DurationSeconds;
            AcceptEncoding = String.IsNullOrEmpty(acceptEncoding) ? null : acceptEncoding;
            ContentEncoding = bodyCompression?.HttpCode;
            Timeout = TimeSpan.FromSeconds(Math.Max(1, p.RequestTimeoutSeconds));
            Drain = TimeSpan.FromSeconds(Math.Max(0, p.DrainSeconds));
            var epl = endPoints.Length;
            var pr = new Prepared[epl];
            var methods = new String[epl];
            for (int i = 0; i < epl; ++i)
            {
                var e = endPoints[i];
                Byte[] body = null;
                var path = e.Path;
                var method = e.Method;
                long valueSize = 0;
                if (e.Body != null)
                {
                    var json = Encoding.UTF8.GetBytes(e.Body);
                    //  Encoded and compressed once here: a test measures the server, not the compression of the client
                    if (settings.SendAsGet)
                    {
                        //  The value in the query string (HttpServer README, "Calling APIs"): url encoded json, or "_" + compression letter + base64url
                        var q = QueryValue(json, bodyCompression);
                        path = String.Concat(path, "?", q);
                        method = "GET";
                        valueSize = q.Length;
                    }
                    else
                    {
                        body = bodyCompression != null ? bodyCompression.GetCompressed(json, CompEncoderLevels.Balanced).ToArray() : json;
                        method = "POST";
                        valueSize = body.Length;
                    }
                }
                methods[i] = method;
                pr[i] = new Prepared
                {
                    EndPoint = e,
                    Index = i,
                    Method = method == "POST" ? HttpMethod.Post : HttpMethod.Get,
                    Uri = new Uri(target, path),
                    Accept = e.IsApi ? "application/json" : "*/*",
                    Body = body,
                    ValueSize = valueSize,
                };
            }
            Prep = pr;
            Methods = methods;
            EpTotals = new BenchCounters[epl];
            for (int i = 0; i < epl; ++i)
                EpTotals[i] = new BenchCounters(true);
            var secs = DurationSeconds + (int)Math.Ceiling(Drain.TotalSeconds + Timeout.TotalSeconds) + 2;
            Seconds = new BenchCounters[secs][];
            SecondHists = new long[secs][];
            var workers = new Worker[Concurrency];
            var sampleCap = Math.Max(100, p.MaxRequestSamples / Math.Max(1, Concurrency));
            for (int i = 0; i < Concurrency; ++i)
            {
                var local = new BenchCounters[epl];
                for (int j = 0; j < epl; ++j)
                    local[j] = new BenchCounters(true);
                workers[i] = new Worker
                {
                    Index = i,
                    Samples = new BenchSample[sampleCap],
                    Rng = new Random(unchecked(id * 7919 + i * 104729)),
                    Local = local,
                };
            }
            Workers = workers;
            //  Set again by Start (the clients are created first)
            SetStartTime();
        }

        void SetStartTime()
        {
            Started = DateTime.UtcNow;
            StartTs = Stopwatch.GetTimestamp();
            EndTs = StartTs + (long)(DurationSeconds * (double)Stopwatch.Frequency);
        }

        public readonly int Id;
        public readonly BenchStartRequest Settings;
        public readonly Uri Target;
        public readonly BenchEndPoint[] EndPoints;
        public readonly int Concurrency;
        public readonly int DurationSeconds;
        public readonly String AcceptEncoding;
        public readonly String ContentEncoding;
        /// <summary>
        /// The utc time the workers started (set by <see cref="Start"/>).
        /// </summary>
        public DateTime Started { get; private set; }
        public DateTime? Ended { get; private set; }
        public volatile BenchStates State = BenchStates.Running;
        public String Message { get; private set; }
        public readonly Worker[] Workers;

        readonly Prepared[] Prep;
        readonly TimeSpan Timeout;
        readonly TimeSpan Drain;
        long StartTs;
        long EndTs;
        long EndedTs;

        /// <summary>
        /// Cancelled by a stop (at once) and at the end of the test (after the drain time): requests that are cancelled by it are not counted.
        /// </summary>
        readonly CancellationTokenSource AbortCts = new CancellationTokenSource();
        volatile bool UserStopped;

        /// <summary>
        /// The totals per end point (with latency histograms).
        /// </summary>
        public readonly BenchCounters[] EpTotals;

        /// <summary>
        /// The counters per second (of the completion of the request) and end point, created when needed.
        /// </summary>
        readonly BenchCounters[][] Seconds;

        /// <summary>
        /// The latency histogram of all end points per second, created when needed.
        /// </summary>
        readonly long[][] SecondHists;

        /// <summary>
        /// The responses per end point and status code (0 = no response).
        /// </summary>
        public readonly ConcurrentDictionary<(int EndPoint, int Status), long> Statuses = new ConcurrentDictionary<(int, int), long>();

        /// <summary>
        /// The errors per end point, status and message.
        /// </summary>
        public readonly ConcurrentDictionary<(int EndPoint, int Status, String Message), BenchError> Errors = new ConcurrentDictionary<(int, int, String), BenchError>();

        Task RunTask;

        /// <summary>
        /// Called once when the test is done (any end state).
        /// </summary>
        public Action<BenchRun> OnDone;

        public bool IsDone => State >= BenchStates.Completed;

        /// <summary>
        /// Start the workers.
        /// The http clients are created first, so the test time (and the per second counters) starts with the first request.
        /// </summary>
        public void Start()
        {
            foreach (var w in Workers)
                w.Client = CreateClient(w);
            SetStartTime();
            RunTask = Task.Run(RunAsync);
        }

        /// <summary>
        /// Wait for the test to end (any end state), completes at once if it was never started.
        /// </summary>
        public Task WaitAsync() => RunTask ?? Task.CompletedTask;

        /// <summary>
        /// Stop the test now (requests in flight are cancelled and not counted).
        /// </summary>
        public bool Stop()
        {
            if (IsDone)
                return false;
            UserStopped = true;
            try
            {
                AbortCts.Cancel();
            }
            catch
            {
            }
            return true;
        }

        public void Dispose()
        {
            Stop();
            try
            {
                RunTask?.Wait(TimeSpan.FromSeconds(10));
            }
            catch
            {
            }
        }

        static double TicksToUs(long ticks) => ticks * 1_000_000.0 / Stopwatch.Frequency;

        /// <summary>
        /// The time since the start (until the end when done).
        /// </summary>
        public double ElapsedSeconds
        {
            get
            {
                var e = IsDone ? Volatile.Read(ref EndedTs) : Stopwatch.GetTimestamp();
                return (e - StartTs) / (double)Stopwatch.Frequency;
            }
        }

        /// <summary>
        /// The time that requests were sent (at most the duration).
        /// </summary>
        public double MeasuredSeconds => Math.Max(0.001, Math.Min(ElapsedSeconds, DurationSeconds));

        /// <summary>
        /// The number of seconds that have counters (complete seconds while running, all seconds when done).
        /// </summary>
        public int SecondCount
        {
            get
            {
                var e = ElapsedSeconds;
                var n = IsDone ? (int)Math.Ceiling(e) : (int)Math.Floor(e);
                return Math.Clamp(n, 0, Seconds.Length);
            }
        }

        /// <summary>
        /// The counters of one second (null if nothing completed in it).
        /// </summary>
        public BenchCounters[] GetSecond(int second) => (second < 0) || (second >= Seconds.Length) ? null : Volatile.Read(ref Seconds[second]);

        /// <summary>
        /// The latency histogram of one second (null if nothing completed in it).
        /// </summary>
        public long[] GetSecondHist(int second) => (second < 0) || (second >= SecondHists.Length) ? null : Volatile.Read(ref SecondHists[second]);

        /// <summary>
        /// The utc time of the start of a second of the test.
        /// </summary>
        public DateTime SecondTime(int second) => Started.AddSeconds(second);

        /// <summary>
        /// The totals of all end points of one second (no histogram).
        /// </summary>
        public BenchCounters SecondTotal(int second)
        {
            var c = new BenchCounters(false);
            var s = GetSecond(second);
            if (s != null)
                foreach (var x in s)
                    c.AddFrom(x);
            return c;
        }

        /// <summary>
        /// The totals of all end points (with a histogram).
        /// </summary>
        public BenchCounters Total(Func<int, bool> include = null)
        {
            var c = new BenchCounters(true);
            var ep = EpTotals;
            for (int i = 0; i < ep.Length; ++i)
                if ((include == null) || include(i))
                    c.AddFrom(ep[i]);
            return c;
        }

        int PeakScanned;
        double PeakRps;
        bool PeakFinal;

        /// <summary>
        /// The highest number of requests completed in one second (complete seconds only).
        /// </summary>
        public double GetPeakRps()
        {
            lock (this)
            {
                //  Workers merge a second when their next request completes: once done, every second is scanned again (they are all merged then)
                if (IsDone && !PeakFinal)
                {
                    PeakFinal = true;
                    PeakScanned = 0;
                    PeakRps = 0;
                }
                var n = IsDone ? SecondCount : Math.Max(0, SecondCount - 1);
                n = Math.Min(n, (int)Math.Ceiling(MeasuredSeconds));
                for (int i = PeakScanned; i < n; ++i)
                {
                    var v = SecondTotal(i).Count;
                    if (v > PeakRps)
                        PeakRps = v;
                }
                if (IsDone)
                    PeakScanned = n;
                else
                    PeakScanned = Math.Max(PeakScanned, n - 1);
                return PeakRps;
            }
        }

        /// <summary>
        /// The live state and totals.
        /// </summary>
        public BenchStatus GetStatus()
        {
            var t = Total();
            var elapsed = ElapsedSeconds;
            var measured = MeasuredSeconds;
            var cur = SecondTotal(Math.Max(0, (int)Math.Floor(Math.Min(elapsed, DurationSeconds)) - 1));
            long wireSent = 0, wireRecv = 0, conns = 0;
            foreach (var w in Workers)
            {
                wireSent += Volatile.Read(ref w.Wire.Sent);
                wireRecv += Volatile.Read(ref w.Wire.Recv);
                conns += Volatile.Read(ref w.Wire.Connections);
            }
            return new BenchStatus
            {
                Id = Id,
                State = State,
                Done = IsDone,
                Target = Target.ToString(),
                Started = Started,
                Ended = Ended,
                ElapsedSeconds = elapsed,
                DurationSeconds = DurationSeconds,
                Progress = IsDone ? 100 : Math.Clamp(elapsed * 100.0 / DurationSeconds, 0, 100),
                Concurrency = Concurrency,
                EndPointCount = EndPoints.Length,
                RequestCompression = ContentEncoding,
                ResponseCompression = AcceptEncoding,
                ValueMethod = Settings.SendAsGet ? "GET" : "POST",
                Requests = t.Count,
                Errors = t.Errors,
                ErrorPercent = t.ErrorPercent,
                Rps = t.Count / measured,
                CurrentRps = IsDone ? 0 : cur.Count,
                PeakRps = GetPeakRps(),
                AvgMs = t.AvgMs,
                CurrentAvgMs = IsDone ? 0 : cur.AvgMs,
                MinMs = t.MinMs,
                MaxMs = t.MaxMs,
                P50Ms = t.PercentileMs(0.5),
                P90Ms = t.PercentileMs(0.9),
                P95Ms = t.PercentileMs(0.95),
                P99Ms = t.PercentileMs(0.99),
                AvgTtfbMs = t.AvgTtfbMs,
                WireSent = wireSent,
                WireRecv = wireRecv,
                BodySent = t.BodySent,
                BodyRecv = t.BodyRecv,
                Connections = conns,
                Message = Message,
            };
        }

        #region Running

        async Task RunAsync()
        {
            var abort = AbortCts.Token;
            var end = BenchStates.Failed;
            try
            {
                var tasks = new Task[Concurrency];
                for (int i = 0; i < Concurrency; ++i)
                {
                    var w = Workers[i];
                    tasks[i] = Task.Run(() => WorkerLoop(w, abort));
                }
                var all = Task.WhenAll(tasks);
                var left = EndTs - Stopwatch.GetTimestamp();
                if (left > 0)
                {
                    try
                    {
                        await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(left / (double)Stopwatch.Frequency), abort)).ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }
                if (!UserStopped)
                {
                    State = BenchStates.Finishing;
                    //  Requests that are still running get some time to complete, then they are cancelled (and not counted)
                    AbortCts.CancelAfter(Drain);
                }
                await all.ConfigureAwait(false);
                end = UserStopped ? BenchStates.Stopped : BenchStates.Completed;
            }
            catch (Exception ex)
            {
                Message = ex.Message;
                end = UserStopped ? BenchStates.Stopped : BenchStates.Failed;
            }
            finally
            {
                //  The end time first: IsDone (the state) makes the elapsed time read it
                Volatile.Write(ref EndedTs, Stopwatch.GetTimestamp());
                Ended = DateTime.UtcNow;
                State = end;
                foreach (var w in Workers)
                {
                    try
                    {
                        w.Client?.Dispose();
                    }
                    catch
                    {
                    }
                    w.Client = null;
                }
                try
                {
                    OnDone?.Invoke(this);
                }
                catch
                {
                }
            }
        }

        HttpClient CreateClient(Worker w)
        {
            var h = new SocketsHttpHandler
            {
                //  One connection: the worker sends one request at a time, so the wire bytes of a request are the change of its counters
                MaxConnectionsPerServer = 1,
                AutomaticDecompression = DecompressionMethods.None,
                AllowAutoRedirect = false,
                UseCookies = true,
                CookieContainer = new CookieContainer(),
                UseProxy = false,
                PooledConnectionLifetime = System.Threading.Timeout.InfiniteTimeSpan,
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                ConnectTimeout = Timeout,
                //  No distributed tracing headers (traceparent) and no activity work per request
                ActivityHeadersPropagator = null,
                ConnectCallback = async (ctx, ct) =>
                {
                    var s = new Socket(SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true,
                    };
                    try
                    {
                        await s.ConnectAsync(ctx.DnsEndPoint, ct).ConfigureAwait(false);
                        return new CountingStream(new NetworkStream(s, true), w.Wire);
                    }
                    catch
                    {
                        s.Dispose();
                        throw;
                    }
                },
            };
            if (Settings.IgnoreCertificateErrors)
                h.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            var c = new HttpClient(h, true)
            {
                Timeout = System.Threading.Timeout.InfiniteTimeSpan,
            };
            //  The headers that every request of the test has: set once (not parsed or validated per request)
            var dh = c.DefaultRequestHeaders;
            dh.TryAddWithoutValidation("User-Agent", "SysWeaver.HttpBench/1.0");
            if (AcceptEncoding != null)
                dh.TryAddWithoutValidation("Accept-Encoding", AcceptEncoding);
            return c;
        }

        async Task WorkerLoop(Worker w, CancellationToken abort)
        {
            var prep = Prep;
            var n = prep.Length;
            //  Every worker calls the end points in the same order, starting at a different one (so all end points are hit from the start)
            var i = w.Index % n;
            w.Started = Stopwatch.GetTimestamp();
            w.TimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(abort);
            try
            {
                for (; ; )
                {
                    if (abort.IsCancellationRequested)
                        break;
                    if (Stopwatch.GetTimestamp() >= EndTs)
                        break;
                    if (!await Call(w, prep[i], abort).ConfigureAwait(false))
                        break;
                    if (++i >= n)
                        i = 0;
                }
            }
            finally
            {
                //  The requests of the last second
                Merge(w);
                w.TimeoutCts.Dispose();
            }
        }

        /// <summary>
        /// Call an end point once and record the result.
        /// </summary>
        /// <returns>False if the test was stopped while it ran (nothing recorded)</returns>
        async Task<bool> Call(Worker w, Prepared p, CancellationToken abort)
        {
            using var req = new HttpRequestMessage(p.Method, p.Uri)
            {
                Version = HttpVersion.Version11,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            };
            req.Headers.TryAddWithoutValidation("Accept", p.Accept);
            //  The api value as sent (POST body or GET query string)
            long bodySent = p.ValueSize;
            var body = p.Body;
            if (body != null)
            {
                var content = new ByteArrayContent(body);
                var ch = content.Headers;
                ch.TryAddWithoutValidation("Content-Type", "application/json");
                var ce = ContentEncoding;
                if (ce != null)
                    ch.TryAddWithoutValidation("Content-Encoding", ce);
                req.Content = content;
            }
            var wire = w.Wire;
            var sent0 = Volatile.Read(ref wire.Sent);
            var recv0 = Volatile.Read(ref wire.Recv);
            var conn0 = Volatile.Read(ref wire.Connections);
            //  The time out: one token source per worker (linked to the abort), reset after every request instead of a new one per request
            var tcts = w.TimeoutCts;
            tcts.CancelAfter(Timeout);
            var token = tcts.Token;
            var t0 = Stopwatch.GetTimestamp();
            long t1 = 0;
            int status = 0;
            long bodyRecv = 0;
            String error = null;
            try
            {
                using var res = await w.Client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                t1 = Stopwatch.GetTimestamp();
                status = (int)res.StatusCode;
                MemoryStream errBody = status >= 400 ? new MemoryStream() : null;
                using (var s = await res.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                {
                    var buf = w.Buffer;
                    for (; ; )
                    {
                        var r = await s.ReadAsync(buf, token).ConfigureAwait(false);
                        if (r <= 0)
                            break;
                        bodyRecv += r;
                        if ((errBody != null) && (errBody.Length < 4096))
                            errBody.Write(buf, 0, Math.Min(r, 4096));
                    }
                }
                if (errBody != null)
                    error = ErrorText(status, res.ReasonPhrase, errBody, res.Content.Headers.ContentEncoding);
            }
            catch (OperationCanceledException) when (abort.IsCancellationRequested)
            {
                //  Stopped (or the drain time is up): not counted
                return false;
            }
            catch (OperationCanceledException)
            {
                error = String.Concat("Timed out (no complete response within ", Timeout.TotalSeconds.ToString("0"), " s)");
            }
            catch (HttpRequestException ex)
            {
                var ie = ex.InnerException;
                error = ie == null ? ex.Message : String.Concat(ex.Message, " (", ie.Message, ")");
                //  The pool drops the connection, the next request opens a new one
            }
            catch (Exception ex)
            {
                error = String.Concat(ex.GetType().Name, ": ", ex.Message);
            }
            var t2 = Stopwatch.GetTimestamp();
            if (t1 == 0)
                t1 = t2;
            //  Stops the timer; a token source that timed out can't be reset, the next request gets a new one
            if (!tcts.TryReset())
            {
                tcts.Dispose();
                w.TimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(abort);
            }
            var wireSent = Volatile.Read(ref wire.Sent) - sent0;
            var wireRecv = Volatile.Read(ref wire.Recv) - recv0;
            var newConn = Volatile.Read(ref wire.Connections) != conn0;
            Record(w, p.Index, t0, t1, t2, status, error, wireSent, wireRecv, bodySent, bodyRecv, newConn);
            return true;
        }

        /// <summary>
        /// The text of an error response: the status and the start of the body (SysWeaver servers send the reason as text).
        /// </summary>
        static String ErrorText(int status, String reason, MemoryStream body, ICollection<String> contentEncoding)
        {
            var sb = new StringBuilder();
            sb.Append("HTTP ").Append(status);
            if (!String.IsNullOrEmpty(reason))
                sb.Append(' ').Append(reason);
            try
            {
                var data = body.ToArray();
                var ce = contentEncoding?.FirstOrDefault();
                if (!String.IsNullOrEmpty(ce))
                {
                    var comp = CompManager.GetFromHttp(ce);
                    data = comp == null ? null : comp.GetDecompressedArray(data);
                }
                if ((data != null) && (data.Length > 0))
                {
                    var text = Encoding.UTF8.GetString(data).Trim();
                    if (text.Length > 0)
                    {
                        //  Html pages (sign in redirects, error pages) are not useful as text
                        if (!text.StartsWith('<') && !text.StartsWith('﻿'))
                        {
                            if (text.Length > 200)
                                text = String.Concat(text.AsSpan(0, 200), "…");
                            sb.Append(": ").Append(text.Replace('\r', ' ').Replace('\n', ' '));
                        }
                    }
                }
            }
            catch
            {
            }
            return sb.ToString();
        }

        void Record(Worker w, int ep, long t0, long t1, long t2, int status, String error, long wireSent, long wireRecv, long bodySent, long bodyRecv, bool newConn)
        {
            var us = Math.Max(1, (long)TicksToUs(t2 - t0));
            var ttfbUs = Math.Max(0, (long)TicksToUs(t1 - t0));
            var isError = (status == 0) || (status >= 400);
            //  Everything goes to counters of the worker (no locks, no interlocked operations, no allocations), the counters of a second are
            //  merged into the shared ones when the worker completes its first request of the next second
            w.Totals.AddLocal(us, ttfbUs, isError, wireSent, wireRecv, bodySent, bodyRecv, newConn);
            var sec = Math.Clamp((int)((t2 - StartTs) / Stopwatch.Frequency), 0, Seconds.Length - 1);
            if (sec != w.LocalSecond)
            {
                Merge(w);
                w.LocalSecond = sec;
            }
            w.Local[ep].AddLocal(us, ttfbUs, isError, wireSent, wireRecv, bodySent, bodyRecv, newConn);
            ++CollectionsMarshal.GetValueRefOrAddDefault(w.LocalStatuses, (ep, status), out _);
            var startUs = (long)TicksToUs(t0 - StartTs);
            if (isError)
            {
                var e = Errors.GetOrAdd((ep, status, error ?? ""), static k => new BenchError
                {
                    EndPoint = k.EndPoint,
                    Status = k.Status,
                    Message = k.Message,
                });
                lock (e)
                {
                    ++e.Count;
                    if (startUs < e.FirstUs)
                        e.FirstUs = startUs;
                    if (startUs > e.LastUs)
                        e.LastUs = startUs;
                }
            }
            //  Request samples: all requests until the reservoir is full, then a uniform random sample of all requests (algorithm R, per worker)
            var samples = w.Samples;
            var seen = w.SamplesSeen;
            var j = seen < samples.Length ? seen : w.Rng.NextInt64(seen + 1);
            if (j < samples.Length)
            {
                samples[j] = new BenchSample
                {
                    StartUs = startUs,
                    EndPoint = ep,
                    Worker = w.Index,
                    Status = status,
                    DurationUs = (int)Math.Min(int.MaxValue, us),
                    TtfbUs = (int)Math.Min(int.MaxValue, ttfbUs),
                    WireSent = (int)Math.Min(int.MaxValue, wireSent),
                    WireRecv = (int)Math.Min(int.MaxValue, wireRecv),
                    BodySent = (int)Math.Min(int.MaxValue, bodySent),
                    BodyRecv = (int)Math.Min(int.MaxValue, bodyRecv),
                    NewConnection = newConn,
                    Error = error,
                };
            }
            Volatile.Write(ref w.SamplesSeen, seen + 1);
        }

        /// <summary>
        /// Merge the counters of the current second of a worker into the shared counters (the end point totals, the second and its
        /// histogram and the status codes), then clear them. Called by the worker only (when its second changes and when it ends).
        /// </summary>
        void Merge(Worker w)
        {
            var sec = w.LocalSecond;
            if (sec < 0)
                return;
            var local = w.Local;
            BenchCounters[] s = null;
            long[] h = null;
            for (int ep = 0; ep < local.Length; ++ep)
            {
                var c = local[ep];
                if (c.Count <= 0)
                    continue;
                if (s == null)
                {
                    s = Volatile.Read(ref Seconds[sec]);
                    if (s == null)
                    {
                        var ns = new BenchCounters[local.Length];
                        for (int i = 0; i < ns.Length; ++i)
                            ns[i] = new BenchCounters(false);
                        s = Interlocked.CompareExchange(ref Seconds[sec], ns, null) ?? ns;
                    }
                    h = Volatile.Read(ref SecondHists[sec]);
                    if (h == null)
                    {
                        var nh = new long[LatencyHistogram.Count];
                        h = Interlocked.CompareExchange(ref SecondHists[sec], nh, null) ?? nh;
                    }
                }
                EpTotals[ep].AddAtomic(c, true);
                s[ep].AddAtomic(c, false);
                LatencyHistogram.AddAtomic(h, c.Hist);
                c.Reset();
            }
            var st = w.LocalStatuses;
            if (st.Count > 0)
            {
                foreach (var x in st)
                    Statuses.AddOrUpdate(x.Key, static (_, v) => v, static (_, o, v) => o + v, x.Value);
                st.Clear();
            }
            w.LocalSecond = -1;
        }

        /// <summary>
        /// The kept request samples, in the order they completed (<see cref="BenchSample.Seq"/> is set to that order).
        /// </summary>
        public List<BenchSample> GetSamples()
        {
            var l = new List<BenchSample>();
            foreach (var w in Workers)
            {
                var n = (int)Math.Min(Volatile.Read(ref w.SamplesSeen), w.Samples.Length);
                for (int i = 0; i < n; ++i)
                    l.Add(w.Samples[i]);
            }
            l.Sort(static (a, b) => (a.StartUs + a.DurationUs).CompareTo(b.StartUs + b.DurationUs));
            var span = CollectionsMarshal.AsSpan(l);
            for (int i = 0; i < span.Length; ++i)
                span[i].Seq = i + 1;
            return l;
        }

        /// <summary>
        /// The total number of requests that the samples were taken from.
        /// </summary>
        public long SamplesSeen => Workers.Sum(x => Volatile.Read(ref x.SamplesSeen));

        #endregion//Running
    }
}
