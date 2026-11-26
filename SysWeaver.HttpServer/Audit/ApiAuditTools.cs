using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.Serialization;

namespace SysWeaver.Net
{

    /// <summary>
    /// Tools for implementing <see cref="IApiAuditReportService"/>
    /// </summary>
    public static class ApiAuditTools
    {
        /// <summary>
        /// The auth that should be required for reading audit data
        /// </summary>
        public const String ReadAuth = Roles.AdminOps;

        /// <summary>
        /// The url of the audit browser page (relative to the site root)
        /// </summary>
        public const String BrowserUrl = "audit/audit.html";

        /// <summary>
        /// The most entries returned by a single search
        /// </summary>
        public const int MaxEntries = 100000;

        /// <summary>
        /// The most values returned for a field
        /// </summary>
        public const int MaxFacetValues = 1000;

        /// <summary>
        /// The number of chars of a (legacy) session token that are kept when it is redacted
        /// </summary>
        public const int SessionChars = 6;

        /// <summary>
        /// The suffix of a redacted session
        /// </summary>
        public const String SessionSuffix = "****";

        /// <summary>
        /// Audits store the session id (<see cref="HttpSession.TokenHash"/>), never the session token.
        /// Entries written by older versions may hold a session token (or a redacted one): anything that isn't a session id
        /// (<see cref="HttpSession.TokenHashLength"/> chars) is redacted, the full token would allow anyone seeing it to hijack the session.
        /// </summary>
        /// <param name="session">The stored session value, can be null</param>
        /// <returns>The session id, or the redacted value</returns>
        public static String RedactSession(String session)
        {
            if (String.IsNullOrEmpty(session))
                return null;
            if (session.Length == HttpSession.TokenHashLength)
                return session;
            return session.Length > SessionChars ? (session.Substring(0, SessionChars) + SessionSuffix) : SessionSuffix;
        }

        /// <summary>
        /// Get the paging of a search
        /// </summary>
        /// <param name="s">The search, can be null</param>
        /// <param name="row">The first entry to return</param>
        /// <param name="count">The number of entries to return</param>
        /// <param name="lookAhead">The number of additional entries to count</param>
        public static void GetPaging(ApiAuditSearch s, out int row, out int count, out int lookAhead)
        {
            row = (int)Math.Clamp(s?.Row ?? 0, 0, Int32.MaxValue / 2);
            var c = s?.MaxRowCount ?? 0;
            count = (int)(c <= 0 ? 100 : Math.Min(c, MaxEntries));
            lookAhead = (int)Math.Clamp(s?.LookAheadCount ?? 0, 0, 1000);
        }

        /// <summary>
        /// Get the (valid) order of a search, always ends with the id (so that the order is well defined)
        /// </summary>
        /// <param name="order">The requested order, can be null</param>
        /// <returns>The fields to order by</returns>
        public static List<(ApiAuditField Field, bool Desc)> GetOrder(String[] order)
        {
            List<(ApiAuditField Field, bool Desc)> res = new();
            foreach (var x in order ?? [])
            {
                var desc = x?.StartsWith('-') ?? false;
                var f = ApiAuditField.Get(desc ? x.Substring(1) : x);
                if ((f == null) || res.Any(y => y.Field == f))
                    continue;
                res.Add((f, desc));
            }
            var id = ApiAuditField.Get(nameof(ApiAuditEntry.Id));
            if (!res.Any(x => x.Field == id))
                res.Add((id, true));
            return res;
        }

        /// <summary>
        /// Compare two entries using an order
        /// </summary>
        /// <param name="order">The order, see <see cref="GetOrder(string[])"/></param>
        /// <returns>A comparison function</returns>
        public static Comparison<ApiAuditEntry> GetComparison(List<(ApiAuditField Field, bool Desc)> order)
            => (a, b) =>
            {
                foreach (var (f, desc) in order)
                {
                    int c;
                    switch (f.Type)
                    {
                        case ApiAuditFieldTypes.Text:
                            c = String.CompareOrdinal(f.GetText(a), f.GetText(b));
                            break;
                        case ApiAuditFieldTypes.Time:
                            c = Nullable.Compare(f.GetTime(a), f.GetTime(b));
                            break;
                        default:
                            c = Nullable.Compare(f.GetNumber(a), f.GetNumber(b));
                            break;
                    }
                    if (c != 0)
                        return desc ? -c : c;
                }
                return 0;
            };


        static readonly long[] NiceSteps = [1, 5, 15, 30, 60, 5 * 60, 15 * 60, 30 * 60, 3600, 2 * 3600, 3 * 3600, 6 * 3600, 12 * 3600, 24 * 3600];

        /// <summary>
        /// Get the duration of a time line step
        /// </summary>
        /// <param name="from">The start of the time line</param>
        /// <param name="to">The end of the time line</param>
        /// <param name="steps">The wanted number of steps</param>
        /// <returns>The number of seconds</returns>
        public static long GetStepSeconds(DateTime from, DateTime to, int steps)
        {
            var want = (to - from).TotalSeconds / Math.Clamp(steps, 1, 500);
            foreach (var s in NiceSteps)
                if (s >= want)
                    return s;
            var days = (long)Math.Ceiling(want / (24 * 3600));
            return days * 24 * 3600;
        }

        /// <summary>
        /// Get the start of the time line step that a time is within
        /// </summary>
        /// <param name="time">The time</param>
        /// <param name="stepSeconds">Number of seconds of a step</param>
        /// <returns>The number of steps since 1970</returns>
        public static long GetStepIndex(DateTime time, long stepSeconds)
        {
            var s = (time.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerSecond;
            return s / stepSeconds;
        }

        /// <summary>
        /// Set the time line of a summary (the first and last time must be set)
        /// </summary>
        /// <param name="summary">The summary to update</param>
        /// <param name="q">The query</param>
        /// <param name="stepSeconds">Number of seconds of a step</param>
        /// <param name="steps">The steps that have any calls, the key is the <see cref="GetStepIndex(DateTime, long)"/></param>
        public static void SetSteps(ApiAuditSummary summary, ApiAuditMatcher q, long stepSeconds, IReadOnlyDictionary<long, ApiAuditStep> steps)
        {
            summary.StepSeconds = stepSeconds;
            GetTimeLine(q, summary, out var from, out var to);
            var first = GetStepIndex(from, stepSeconds);
            var last = GetStepIndex(to, stepSeconds);
            var res = new List<ApiAuditStep>();
            for (var i = first; i <= last; ++i)
            {
                if (!steps.TryGetValue(i, out var s))
                    s = new ApiAuditStep();
                s.Time = DateTime.UnixEpoch.AddSeconds(i * stepSeconds);
                res.Add(s);
            }
            summary.Steps = res.ToArray();
        }

        /// <summary>
        /// Get the start and end of the time line: the requested time range if there is any (but not into the future), else the time of the first and last call.
        /// </summary>
        /// <param name="q">The query</param>
        /// <param name="summary">The summary (the first and last time must be set)</param>
        /// <param name="from">The start of the time line</param>
        /// <param name="to">The end of the time line (inclusive)</param>
        public static void GetTimeLine(ApiAuditMatcher q, ApiAuditSummary summary, out DateTime from, out DateTime to)
        {
            var first = summary.First.Value;
            var last = summary.Last.Value;
            from = q.From ?? first;
            to = last;
            if (q.To != null)
                to = q.To.Value.AddTicks(-1);
            else if (q.From != null)
                to = DateTime.UtcNow;
            var now = DateTime.UtcNow;
            if (to > now)
                to = now;
            if (to < last)
                to = last;
            //  A very early start (ex: the year 1) would be a very long time line of nothing
            var minFrom = first.AddDays(-400);
            if (from < minFrom)
                from = minFrom;
            if (from > first)
                from = first;
        }

        /// <summary>
        /// Combine a table request with the query in it's param
        /// </summary>
        /// <param name="r">The table request</param>
        /// <returns>The search</returns>
        /// <exception cref="HttpResponseException">400: The param isn't a valid query</exception>
        public static ApiAuditSearch GetSearch(TableDataRequest r)
        {
            r = r ?? new TableDataRequest();
            var s = new ApiAuditSearch();
            if (!String.IsNullOrWhiteSpace(r.Param))
            {
                try
                {
                    s.CopyFrom(SerManager.Get("json").Create<ApiAuditQuery>(Encoding.UTF8.GetBytes(r.Param).AsSpan()));
                }
                catch (Exception ex)
                {
                    throw new HttpResponseException(400, "Invalid audit query: " + ex.Message);
                }
            }
            if (r.Filters != null)
                s.All = s.All == null ? r.Filters : s.All.Concat(r.Filters).ToArray();
            if (!String.IsNullOrWhiteSpace(r.SearchText))
                s.Text = String.IsNullOrWhiteSpace(s.Text) ? r.SearchText : String.Join(' ', s.Text, r.SearchText);
            s.Order = r.Order;
            s.Row = r.Row;
            s.MaxRowCount = r.MaxRowCount <= 0 ? MaxEntries : r.MaxRowCount;
            s.LookAheadCount = r.LookAheadCount;
            return s;
        }

        /// <summary>
        /// Implementation of <see cref="IApiAuditReportService.AuditTable(TableDataRequest)"/> using <see cref="IApiAuditReportService.Search(ApiAuditSearch)"/>.
        /// </summary>
        /// <param name="service">The service</param>
        /// <param name="r">The table request</param>
        /// <param name="refreshRate">Number of ms the client should wait before refreshing the data</param>
        /// <returns>The table</returns>
        public static async Task<TableData> GetTable(IApiAuditReportService service, TableDataRequest r, long refreshRate = 5000)
        {
            var page = await service.Search(GetSearch(r)).ConfigureAwait(false);
            var t = new TableData
            {
                Rows = TableDataTools.ExtractGet(out _, page.Entries),
                RowCount = page.RowCount,
                RefreshRate = refreshRate,
            };
            //  Show the name of the state (a table has the number of an enum value), the conditions accept both
            foreach (var row in t.Rows)
                row.Values[StateColumn] = ((ApiAuditStates)Convert.ToByte(row.Values[StateColumn])).ToString();
            t.HandleCc(r?.Cc ?? 0, TableDataTools.GetCols<ApiAuditEntry>(), "Audit");
            if (t.ModifyColumns(out var cols))
                cols[StateColumn].Type = typeof(String).FullName;
            return t;
        }

        static readonly int StateColumn = TableDataTools.GetColumnIndex<ApiAuditEntry>(nameof(ApiAuditEntry.State));

        /// <summary>
        /// Look up the location of an ip address (for the details of a call in the audit browser)
        /// </summary>
        /// <param name="source">The location source, null if there is none</param>
        /// <param name="ip">The ip address</param>
        /// <returns>The location, null if there is no source, the ip address is invalid, local, unknown or the look up failed</returns>
        public static async Task<Location> GetIpLocation(IIpLocation source, String ip)
        {
            if ((source == null) || !IPAddress.TryParse(ip?.Trim() ?? "", out var a))
                return null;
            if (a.IsIPv4MappedToIPv6)
                a = a.MapToIPv4();
            //  (a local address has no location, don't ask a remote service)
            if (IPAddress.IsLoopback(a) || a.Equals(IPAddress.Any) || a.Equals(IPAddress.IPv6Any))
                return null;
            try
            {
                return await source.GetLocation(a.ToString()).ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }

    }

}
