using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SysWeaver.Compression;

namespace SysWeaver.HttpBench
{
    /// <summary>
    /// The settings of a test: what the page sends when the user presses GO, what an exported parameter file holds (see <see cref="BenchSettingsFile"/>)
    /// and what the HttpBench console tool runs.
    /// </summary>
    public sealed class BenchStartRequest
    {
        /// <summary>
        /// The root url of the server to test, ex: "https://myserver/" (end points are relative to it).
        /// </summary>
        public String Target;

        /// <summary>
        /// The duration of the test in seconds.
        /// </summary>
        public int DurationSeconds = 60;

        /// <summary>
        /// The number of workers: each worker is a client with its own connection and session that sends one request at a time.
        /// </summary>
        public int Concurrency = 16;

        /// <summary>
        /// The ids of the end points to call (see <see cref="BenchCatalog"/>), every worker calls them in the catalog order, over and over.
        /// Null = the end points of the modules that are selected by default.
        /// </summary>
        public String[] EndPoints;

        /// <summary>
        /// Send the values of the apis that take one (the POST end points of the catalog) as GET requests with the value in the query string,
        /// instead of POST requests with the value as the body.
        /// </summary>
        public bool SendAsGet;

        /// <summary>
        /// The compression of the api values (an http compression such as "br", "gzip", "deflate"), null or empty for none.
        /// POST: the body is compressed (Content-Encoding). GET: the query string is "_" + the compression letter + the compressed json as
        /// base64url (without compression: the url encoded json).
        /// </summary>
        public String RequestCompression;

        /// <summary>
        /// The response compressions to accept (the Accept-Encoding header), null or empty for none, "*" for all that the bench supports.
        /// </summary>
        public String ResponseCompression = "*";

        /// <summary>
        /// Accept any https certificate (self signed, LAN or expired certificates of test servers).
        /// </summary>
        public bool IgnoreCertificateErrors = true;

        /// <summary>
        /// A copy.
        /// </summary>
        public BenchStartRequest Clone()
        {
            var c = (BenchStartRequest)MemberwiseClone();
            c.EndPoints = EndPoints?.ToArray();
            return c;
        }
    }

    /// <summary>
    /// The limits and timing of the tests of a host (the bench service or the console tool).
    /// </summary>
    public sealed class BenchLimits
    {
        /// <summary>
        /// The most workers (concurrent requests) of a test.
        /// </summary>
        public int MaxConcurrency = 4096;

        /// <summary>
        /// The shortest test, in seconds.
        /// </summary>
        public int MinDurationSeconds = 1;

        /// <summary>
        /// The longest test, in seconds.
        /// </summary>
        public int MaxDurationSeconds = 7 * 24 * 3600;

        /// <summary>
        /// The time out of one request, in seconds (a request that takes longer is counted as an error).
        /// </summary>
        public int RequestTimeoutSeconds = 30;

        /// <summary>
        /// The time that requests that were started before the end of a test may take to complete, in seconds (they are cancelled after that and are not counted).
        /// </summary>
        public int DrainSeconds = 5;

        /// <summary>
        /// The most single requests that are kept for the request details of a test (a uniform random sample of all requests when there are more).
        /// </summary>
        public int MaxRequestSamples = 100_000;
    }

    /// <summary>
    /// A validated test: the settings (normalized) and what a <see cref="BenchRun"/> needs to start.
    /// </summary>
    public sealed class BenchSetup
    {
        /// <summary>
        /// The settings, normalized: the target ends with a '/', the end points are in the catalog order, compressions are trimmed ("" for none).
        /// </summary>
        public BenchStartRequest Settings { get; private init; }

        /// <summary>
        /// The root url of the server (end points are relative to it).
        /// </summary>
        public Uri Target { get; private init; }

        /// <summary>
        /// The end points to call, in the catalog order.
        /// </summary>
        public BenchEndPoint[] EndPoints { get; private init; }

        /// <summary>
        /// The value of the Accept-Encoding header, null for none.
        /// </summary>
        public String AcceptEncoding { get; private init; }

        /// <summary>
        /// The compression of the api values, null for none.
        /// </summary>
        public ICompType BodyCompression { get; private init; }

        /// <summary>
        /// The compressions this process can encode and decode (the http codes, best first).
        /// </summary>
        public static String[] CompressionCodes()
        {
            String[] order = ["br", "zstd", "gzip", "deflate"];
            var codes = CompManager.HttpCodes.ToHashSet(StringComparer.Ordinal);
            return order.Where(codes.Contains).Concat(codes.Except(order).OrderBy(x => x, StringComparer.Ordinal)).ToArray();
        }

        /// <summary>
        /// The ids of the end points of the modules that are selected by default, in the catalog order.
        /// </summary>
        public static String[] DefaultEndPoints() => BenchCatalog.Modules.Where(x => x.Default).SelectMany(x => x.EndPoints).Select(x => x.Id).ToArray();

        /// <summary>
        /// Validate settings.
        /// </summary>
        /// <param name="r">The settings (not changed)</param>
        /// <param name="limits">The limits, null for the defaults</param>
        /// <returns>The validated test</returns>
        /// <exception cref="ArgumentException">The settings are invalid (the message says why, it can be shown to a user)</exception>
        public static BenchSetup Create(BenchStartRequest r, BenchLimits limits = null)
        {
            if (r == null)
                throw new ArgumentException("No settings given");
            limits = limits ?? new BenchLimits();
            var target = (r.Target ?? "").Trim();
            if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) || ((uri.Scheme != Uri.UriSchemeHttp) && (uri.Scheme != Uri.UriSchemeHttps)))
                throw new ArgumentException("The target must be an http or https url, ex: https://myserver/");
            //  End points are relative to the root folder of the target
            var b = new UriBuilder(uri)
            {
                Query = "",
                Fragment = "",
            };
            if (!b.Path.EndsWith('/'))
                b.Path += '/';
            uri = b.Uri;
            var ids = r.EndPoints ?? DefaultEndPoints();
            foreach (var id in ids)
                if (!BenchCatalog.ById.ContainsKey(id ?? ""))
                    throw new ArgumentException("Unknown end point " + (id ?? "null").ToQuoted());
            var idSet = new HashSet<String>(ids, StringComparer.Ordinal);
            //  The catalog order (modules, then end points): the order the workers call them in
            var eps = BenchCatalog.Modules.SelectMany(x => x.EndPoints).Where(x => idSet.Contains(x.Id)).ToArray();
            if (eps.Length <= 0)
                throw new ArgumentException("Select at least one end point");
            var minD = Math.Max(1, limits.MinDurationSeconds);
            var maxD = Math.Max(minD, limits.MaxDurationSeconds);
            if ((r.DurationSeconds < minD) || (r.DurationSeconds > maxD))
                throw new ArgumentException(String.Concat("The duration must be ", minD, " to ", maxD, " seconds"));
            var maxC = Math.Max(1, limits.MaxConcurrency);
            if ((r.Concurrency < 1) || (r.Concurrency > maxC))
                throw new ArgumentException(String.Concat("The number of workers must be 1 to ", maxC));
            var codes = CompressionCodes();
            ICompType bodyComp = null;
            var rc = r.RequestCompression?.Trim() ?? "";
            if (rc.Length > 0)
            {
                if (!codes.Contains(rc))
                    throw new ArgumentException(String.Concat("Unknown api value compression ", rc.ToQuoted(), ", valid: ", String.Join(", ", codes)));
                bodyComp = CompManager.GetFromHttp(rc);
            }
            var ac = r.ResponseCompression?.Trim() ?? "";
            String acceptEncoding = null;
            if (ac == "*")
                acceptEncoding = String.Join(", ", codes);
            else if (ac.Length > 0)
            {
                if (!codes.Contains(ac))
                    throw new ArgumentException(String.Concat("Unknown response compression ", ac.ToQuoted(), ", valid: *, ", String.Join(", ", codes)));
                acceptEncoding = ac;
            }
            return new BenchSetup
            {
                Settings = new BenchStartRequest
                {
                    Target = uri.ToString(),
                    DurationSeconds = r.DurationSeconds,
                    Concurrency = r.Concurrency,
                    EndPoints = eps.Select(x => x.Id).ToArray(),
                    RequestCompression = rc,
                    ResponseCompression = ac,
                    IgnoreCertificateErrors = r.IgnoreCertificateErrors,
                    SendAsGet = r.SendAsGet,
                },
                Target = uri,
                EndPoints = eps,
                AcceptEncoding = acceptEncoding,
                BodyCompression = bodyComp,
            };
        }
    }

    /// <summary>
    /// A parameter file: the settings of a test as json, with a "DataType": "HttpBench" member that identifies the file.
    /// The page exports and imports these files, the HttpBench console tool loads them (-File) and writes them (-Save).
    /// </summary>
    public static class BenchSettingsFile
    {
        /// <summary>
        /// The name of the member that identifies the file type.
        /// </summary>
        public const String TypeMember = "DataType";

        /// <summary>
        /// The value of the <see cref="TypeMember"/> member.
        /// </summary>
        public const String DataType = "HttpBench";

        static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>
        /// The json text of settings.
        /// </summary>
        public static String ToJson(BenchStartRequest r)
        {
            var o = new JsonObject
            {
                [TypeMember] = DataType,
            };
            foreach (var x in JsonSerializer.SerializeToNode(r, Options).AsObject().ToList())
                o[x.Key] = x.Value?.DeepClone();
            return o.ToJsonString(Options);
        }

        /// <summary>
        /// Read settings from json text.
        /// </summary>
        /// <param name="json">The json text</param>
        /// <returns>The settings (members that are missing in the json get their default value)</returns>
        /// <exception cref="InvalidDataException">Not a parameter file (invalid json or no "DataType": "HttpBench")</exception>
        public static BenchStartRequest FromJson(String json)
        {
            JsonNode n;
            try
            {
                n = JsonNode.Parse(json ?? "", null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Not a json file: " + ex.Message);
            }
            if (n is not JsonObject o)
                throw new InvalidDataException("Not an HttpBench parameter file (expected a json object)");
            var t = o.FirstOrDefault(x => String.Equals(x.Key, TypeMember, StringComparison.OrdinalIgnoreCase)).Value;
            if ((t == null) || (t.GetValueKind() != JsonValueKind.String) || (t.GetValue<String>() != DataType))
                throw new InvalidDataException(String.Concat("Not an HttpBench parameter file (expected \"", TypeMember, "\": \"", DataType, "\")"));
            try
            {
                return o.Deserialize<BenchStartRequest>(Options) ?? new BenchStartRequest();
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Invalid HttpBench parameter file: " + ex.Message);
            }
        }

        /// <summary>
        /// Load settings from a file.
        /// </summary>
        /// <exception cref="InvalidDataException">Not a parameter file</exception>
        public static BenchStartRequest Load(String filename) => FromJson(File.ReadAllText(filename));

        /// <summary>
        /// Save settings to a file (UTF-8 json).
        /// </summary>
        public static void Save(String filename, BenchStartRequest r) => File.WriteAllText(filename, ToJson(r));
    }

    /// <summary>
    /// The state of a test.
    /// </summary>
    public enum BenchStates
    {
        /// <summary>
        /// Workers are sending requests.
        /// </summary>
        Running,
        /// <summary>
        /// The time is up, the last requests are completing.
        /// </summary>
        Finishing,
        /// <summary>
        /// The test ran for its whole duration.
        /// </summary>
        Completed,
        /// <summary>
        /// The test was stopped by a user (or the host stopped).
        /// </summary>
        Stopped,
        /// <summary>
        /// The test failed to run.
        /// </summary>
        Failed,
    }

    /// <summary>
    /// The live state and the totals of a test.
    /// </summary>
    public sealed class BenchStatus
    {
        public int Id;
        public BenchStates State;
        public bool Done;
        public String Target;
        public DateTime Started;
        public DateTime? Ended;
        public double ElapsedSeconds;
        public double DurationSeconds;
        /// <summary>
        /// 0 to 100.
        /// </summary>
        public double Progress;
        public int Concurrency;
        public int EndPointCount;
        public String RequestCompression;
        public String ResponseCompression;
        /// <summary>
        /// How api values are sent: "POST" (body) or "GET" (query string).
        /// </summary>
        public String ValueMethod;
        public long Requests;
        public long Errors;
        public double ErrorPercent;
        /// <summary>
        /// Requests per second over the whole test.
        /// </summary>
        public double Rps;
        /// <summary>
        /// Requests per second of the last complete second.
        /// </summary>
        public double CurrentRps;
        public double PeakRps;
        public double AvgMs;
        public double CurrentAvgMs;
        public double MinMs;
        public double MaxMs;
        public double P50Ms;
        public double P90Ms;
        public double P95Ms;
        public double P99Ms;
        public double AvgTtfbMs;
        public long WireSent;
        public long WireRecv;
        public long BodySent;
        public long BodyRecv;
        public long Connections;
        public String Message;
    }
}
