using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.HttpBench
{
    /// <summary>
    /// Performance tests a SysWeaver web server: many workers call a selection of public end points (in order, over and over) for a while,
    /// and the results are kept as metrics per end point and second.
    /// The page (httpbench/bench.html) starts and stops tests and shows the report: tables and charts from the apis of this service.
    /// </summary>
    [IsMicroService]
    [WebApiUrl("httpbench")]
    [WebMenuEmbedded(null, "Debug/HttpBench", "HTTP bench", "httpbench/bench.html", "Performance test a SysWeaver server: many workers call its public end points, then a report with tables and charts", "httpbench/bench.svg", 40, null, false, nameof(CanUse))]
    public sealed class HttpBenchService : IRunTimeWebApiAuth, IDisposable
    {
        public HttpBenchService(ServiceManager manager, HttpBenchParams p = null)
        {
            p = p ?? new HttpBenchParams();
            P = p;
            Manager = manager;
            MethodAuths = new Dictionary<String, String>(StringComparer.Ordinal)
            {
                ["*"] = p.Auth,
            };
        }

        public override string ToString() => "Auth: " + (P.Auth ?? "anyone") + ", running: " + (Current?.Id.ToString(CultureInfo.InvariantCulture) ?? "none");

        readonly HttpBenchParams P;
        readonly ServiceManager Manager;

        /// <summary>
        /// Every api of the service uses the configured auth (<see cref="HttpBenchParams.Auth"/>).
        /// </summary>
        public IReadOnlyDictionary<String, String> MethodAuths { get; }

        /// <summary>
        /// Show the menu item only to users that may use the apis.
        /// </summary>
        Task<bool> CanUse(SysWeaver.Auth.Authorization auth) => auth.IsValid(P.Auth) ? TaskExt.TrueTask : TaskExt.FalseTask;

        readonly Object Lock = new Object();
        readonly List<BenchRun> Runs = new List<BenchRun>();
        BenchRun Current;
        int NextId;

        public void Dispose()
        {
            BenchRun c;
            lock (Lock)
                c = Current;
            c?.Dispose();
        }

        const String LogPrefix = "[HttpBench] ";

        #region Options

        static String CompTitle(String code) => code switch
        {
            "br" => "Brotli",
            "gzip" => "Gzip",
            "deflate" => "Deflate",
            "zstd" => "Zstandard",
            _ => code,
        };

        /// <summary>
        /// Everything the page needs to build the test form: the modules with their end points, the compressions and the limits.
        /// </summary>
        /// <returns>The options</returns>
        [WebApi]
        public BenchOptions GetOptions()
        {
            var codes = BenchSetup.CompressionCodes();
            var limits = P.ToLimits();
            BenchRun cur, latest;
            lock (Lock)
            {
                cur = Current;
                latest = Runs.LastOrDefault();
            }
            return new BenchOptions
            {
                Modules = BenchCatalog.Modules,
                RequestCompressions = codes.Select(x => new BenchChoice
                {
                    Value = x,
                    Text = CompTitle(x),
                    Title = "Api values are sent compressed: POST bodies with Content-Encoding: " + x + ", GET query strings as _" + x switch { "br" => "b", "gzip" => "g", "deflate" => "d", "zstd" => "z", _ => "?" } + "<base64url>",
                }).Prepend(new BenchChoice
                {
                    Value = "",
                    Text = "None",
                    Title = "Api values are sent as they are: POST bodies as json, GET query strings as url encoded json",
                }).ToArray(),
                ResponseCompressions = codes.Select(x => new BenchChoice
                {
                    Value = x,
                    Text = CompTitle(x),
                    Title = "Accept-Encoding: " + x,
                }).Prepend(new BenchChoice
                {
                    Value = "",
                    Text = "None",
                    Title = "No Accept-Encoding header: the server sends uncompressed responses",
                }).Append(new BenchChoice
                {
                    Value = "*",
                    Text = "All",
                    Title = "Accept-Encoding: " + String.Join(", ", codes) + " (like a browser: the server picks one)",
                }).ToArray(),
                MinDurationSeconds = limits.MinDurationSeconds,
                MaxDurationSeconds = limits.MaxDurationSeconds,
                MaxConcurrency = limits.MaxConcurrency,
                Running = cur?.Id ?? 0,
                Latest = latest?.Id ?? 0,
                LatestSettings = latest?.Settings,
                SettingsDataType = BenchSettingsFile.DataType,
            };
        }

        #endregion//Options

        #region Run control

        /// <summary>
        /// Start a test (only one test runs at a time).
        /// </summary>
        /// <param name="r">The settings</param>
        /// <returns>The id of the test</returns>
        [WebApi]
        [WebApiAudit("HttpBench")]
        public int Start(BenchStartRequest r)
        {
            //  The page always sends the end points: no list (an old or hand made request) is no end points here, not the default selection
            if ((r != null) && (r.EndPoints == null))
            {
                r = r.Clone();
                r.EndPoints = [];
            }
            var limits = P.ToLimits();
            BenchSetup setup;
            try
            {
                setup = BenchSetup.Create(r, limits);
            }
            catch (ArgumentException ex)
            {
                throw HttpResponseException.Refused(ex.Message, 400);
            }
            BenchRun run;
            lock (Lock)
            {
                if (Current != null)
                    throw HttpResponseException.Refused("Test " + Current.Id + " is running, stop it or wait for it to end");
                run = new BenchRun(++NextId, setup, limits);
                run.OnDone = RunDone;
                Current = run;
                Runs.Add(run);
                var keep = Math.Max(1, P.KeepRuns);
                while (Runs.Count > keep)
                    Runs.RemoveAt(0);
            }
            Manager.AddMessage(String.Concat(LogPrefix, "Test ", run.Id, " started: ", run.Concurrency, " workers call ", run.EndPoints.Length, " end points of ", run.Target, " for ", run.DurationSeconds, " s"), MessageLevels.Info);
            run.Start();
            return run.Id;
        }

        void RunDone(BenchRun run)
        {
            lock (Lock)
            {
                if (Current == run)
                    Current = null;
            }
            var s = run.GetStatus();
            Manager.AddMessage(
                String.Concat(LogPrefix, "Test ", run.Id, " ", run.State.ToString().ToLowerInvariant(), ": ", s.Requests, " requests, ",
                    s.Rps.ToString("0.0", CultureInfo.InvariantCulture), " requests/s, average ", s.AvgMs.ToString("0.000", CultureInfo.InvariantCulture), " ms, ",
                    s.Errors, " errors", String.IsNullOrEmpty(s.Message) ? "" : " (" + s.Message + ")"),
                run.State == BenchStates.Failed ? MessageLevels.Warning : MessageLevels.Info);
        }

        /// <summary>
        /// Stop the running test (requests in flight are cancelled and not counted).
        /// </summary>
        /// <returns>True if a test was stopped, false if none was running</returns>
        [WebApi]
        [WebApiAudit("HttpBench")]
        public bool Stop()
        {
            BenchRun c;
            lock (Lock)
                c = Current;
            return c?.Stop() ?? false;
        }

        BenchRun GetRun(int id)
        {
            lock (Lock)
            {
                if (id <= 0)
                    return Runs.LastOrDefault() ?? throw HttpResponseException.Gone("No test has run yet", 404);
                foreach (var r in Runs)
                    if (r.Id == id)
                        return r;
            }
            throw HttpResponseException.Gone("Test " + id + " is not kept (only the " + Math.Max(1, P.KeepRuns) + " latest tests are)", 404);
        }

        BenchRun GetRun(String param) => GetRun(int.TryParse(param, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0);

        /// <summary>
        /// The live state and the totals of a test.
        /// </summary>
        /// <param name="id">The id of the test, 0 = the latest</param>
        /// <returns>The status</returns>
        [WebApi]
        public BenchStatus GetStatus(int id) => GetRun(id).GetStatus();

        /// <summary>
        /// The settings of a test.
        /// </summary>
        /// <param name="id">The id of the test, 0 = the latest</param>
        /// <returns>The settings</returns>
        [WebApi]
        public BenchStartRequest GetSettings(int id) => GetRun(id).Settings;

        #endregion//Run control

        #region Tables

        const int TableRefreshRunning = 2000;
        const int TableRefreshDone = 3600_000;

        static int Refresh(BenchRun r) => r.IsDone ? TableRefreshDone : TableRefreshRunning;

        /// <summary>
        /// The kept tests.
        /// </summary>
        /// <param name="r">The table request</param>
        /// <returns>One row per test</returns>
        [WebApi]
        public TableData RunsTable(TableDataRequest r)
        {
            BenchRun[] runs;
            lock (Lock)
                runs = Runs.ToArray();
            var rows = runs.Select(x =>
            {
                var s = x.GetStatus();
                return new BenchRunRow
                {
                    Id = x.Id,
                    Started = x.Started,
                    State = x.State.ToString(),
                    Target = x.Target.ToString(),
                    Seconds = Math.Round(s.ElapsedSeconds),
                    Workers = x.Concurrency,
                    EndPoints = x.EndPoints.Length,
                    ValueMethod = x.Settings.SendAsGet ? "GET" : "POST",
                    RequestCompression = String.IsNullOrEmpty(x.ContentEncoding) ? "None" : x.ContentEncoding,
                    ResponseCompression = String.IsNullOrEmpty(x.AcceptEncoding) ? "None" : x.AcceptEncoding,
                    Requests = s.Requests,
                    Rps = s.Rps,
                    AvgMs = s.AvgMs,
                    P95Ms = s.P95Ms,
                    Errors = s.Errors,
                };
            }).ToList();
            return TableDataTools.Get(r, runs.Any(x => !x.IsDone) ? TableRefreshRunning : 10000, rows, "Tests");
        }

        /// <summary>
        /// The results per end point.
        /// </summary>
        /// <param name="r">The table request, Param = the id of the test (empty = the latest)</param>
        /// <returns>One row per end point</returns>
        [WebApi]
        public TableData EndPointTable(TableDataRequest r)
        {
            var run = GetRun(r?.Param);
            return TableDataTools.Get(r, Refresh(run), BenchReport.EndPointRows(run), "End points of test " + run.Id);
        }

        /// <summary>
        /// The results per module (group of end points).
        /// </summary>
        /// <param name="r">The table request, Param = the id of the test (empty = the latest)</param>
        /// <returns>One row per module</returns>
        [WebApi]
        public TableData ModuleTable(TableDataRequest r)
        {
            var run = GetRun(r?.Param);
            return TableDataTools.Get(r, Refresh(run), BenchReport.ModuleRows(run), "Modules of test " + run.Id);
        }

        /// <summary>
        /// The results per second (all end points).
        /// </summary>
        /// <param name="r">The table request, Param = the id of the test (empty = the latest)</param>
        /// <returns>One row per second</returns>
        [WebApi]
        public TableData SecondTable(TableDataRequest r)
        {
            var run = GetRun(r?.Param);
            return TableDataTools.Get(r, Refresh(run), BenchReport.SecondRows(run), "Seconds of test " + run.Id);
        }

        /// <summary>
        /// The results per second and end point.
        /// </summary>
        /// <param name="r">The table request, Param = the id of the test (empty = the latest)</param>
        /// <returns>One row per second and end point (that completed requests in that second)</returns>
        [WebApi]
        public TableData SecondEndPointTable(TableDataRequest r)
        {
            var run = GetRun(r?.Param);
            return TableDataTools.Get(r, Refresh(run), BenchReport.SecondEndPointRows(run), "Seconds and end points of test " + run.Id);
        }

        /// <summary>
        /// The results per worker.
        /// </summary>
        /// <param name="r">The table request, Param = the id of the test (empty = the latest)</param>
        /// <returns>One row per worker</returns>
        [WebApi]
        public TableData WorkerTable(TableDataRequest r)
        {
            var run = GetRun(r?.Param);
            return TableDataTools.Get(r, Refresh(run), BenchReport.WorkerRows(run), "Workers of test " + run.Id);
        }

        /// <summary>
        /// The responses per end point and status code.
        /// </summary>
        /// <param name="r">The table request, Param = the id of the test (empty = the latest)</param>
        /// <returns>One row per end point and status code</returns>
        [WebApi]
        public TableData StatusTable(TableDataRequest r)
        {
            var run = GetRun(r?.Param);
            return TableDataTools.Get(r, Refresh(run), BenchReport.StatusRows(run), "Status codes of test " + run.Id);
        }

        /// <summary>
        /// The errors per end point, status and message.
        /// </summary>
        /// <param name="r">The table request, Param = the id of the test (empty = the latest)</param>
        /// <returns>One row per kind of error</returns>
        [WebApi]
        public TableData ErrorTable(TableDataRequest r)
        {
            var run = GetRun(r?.Param);
            return TableDataTools.Get(r, Refresh(run), BenchReport.ErrorRows(run), "Errors of test " + run.Id);
        }

        /// <summary>
        /// Single requests: all of them, or a uniform random sample of all requests when there were more than the service keeps.
        /// </summary>
        /// <param name="r">The table request, Param = the id of the test (empty = the latest)</param>
        /// <returns>One row per request</returns>
        [WebApi]
        public TableData RequestTable(TableDataRequest r)
        {
            var run = GetRun(r?.Param);
            var rows = BenchReport.RequestRows(run, out var title);
            return TableDataTools.Get(r, Refresh(run), rows, title);
        }

        /// <summary>
        /// The latency histogram (all end points).
        /// </summary>
        /// <param name="r">The table request, Param = the id of the test (empty = the latest)</param>
        /// <returns>One row per histogram bucket (from the fastest to the slowest request)</returns>
        [WebApi]
        public TableData HistogramTable(TableDataRequest r)
        {
            var run = GetRun(r?.Param);
            return TableDataTools.Get(r, Refresh(run), BenchReport.HistogramRows(run), "Response times of test " + run.Id);
        }

        #endregion//Tables

        #region Charts

        /// <summary>
        /// Requests and errors per second.
        /// </summary>
        /// <param name="id">The id of the test, 0 = the latest</param>
        /// <returns>A chart (ChartJsConfig json)</returns>
        [WebApi]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> ThroughputChart(int id) => BenchCharts.Throughput(GetRun(id));

        /// <summary>
        /// Response times per second: average, median, P95 and P99.
        /// </summary>
        /// <param name="id">The id of the test, 0 = the latest</param>
        /// <returns>A chart (ChartJsConfig json)</returns>
        [WebApi]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> LatencyChart(int id) => BenchCharts.Latency(GetRun(id));

        /// <summary>
        /// Bytes sent and received on the wire per second.
        /// </summary>
        /// <param name="id">The id of the test, 0 = the latest</param>
        /// <returns>A chart (ChartJsConfig json)</returns>
        [WebApi]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> BandwidthChart(int id) => BenchCharts.Bandwidth(GetRun(id));

        /// <summary>
        /// Average and P95 response time per end point.
        /// </summary>
        /// <param name="id">The id of the test, 0 = the latest</param>
        /// <returns>A chart (ChartJsConfig json)</returns>
        [WebApi]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> EndPointLatencyChart(int id) => BenchCharts.EndPointLatency(GetRun(id));

        /// <summary>
        /// Requests per second per end point.
        /// </summary>
        /// <param name="id">The id of the test, 0 = the latest</param>
        /// <returns>A chart (ChartJsConfig json)</returns>
        [WebApi]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> EndPointRpsChart(int id) => BenchCharts.EndPointRps(GetRun(id));

        /// <summary>
        /// Average response body size per end point.
        /// </summary>
        /// <param name="id">The id of the test, 0 = the latest</param>
        /// <returns>A chart (ChartJsConfig json)</returns>
        [WebApi]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> EndPointSizeChart(int id) => BenchCharts.EndPointSize(GetRun(id));

        /// <summary>
        /// The distribution of the response times.
        /// </summary>
        /// <param name="id">The id of the test, 0 = the latest</param>
        /// <returns>A chart (ChartJsConfig json)</returns>
        [WebApi]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> HistogramChart(int id) => BenchCharts.Histogram(GetRun(id));

        /// <summary>
        /// The share of the requests per module.
        /// </summary>
        /// <param name="id">The id of the test, 0 = the latest</param>
        /// <returns>A chart (ChartJsConfig json)</returns>
        [WebApi]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> ModuleChart(int id) => BenchCharts.Modules(GetRun(id));

        /// <summary>
        /// The responses per status code.
        /// </summary>
        /// <param name="id">The id of the test, 0 = the latest</param>
        /// <returns>A chart (ChartJsConfig json)</returns>
        [WebApi]
        [WebApiRaw(HttpServerTools.JsonMime)]
        public ReadOnlyMemory<Byte> StatusChart(int id) => BenchCharts.Statuses(GetRun(id), BenchReport.StatusMeaning);

        #endregion//Charts
    }
}
