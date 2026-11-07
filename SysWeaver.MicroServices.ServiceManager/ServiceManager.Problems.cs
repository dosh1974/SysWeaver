using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    public partial class ServiceManager
    {
        /// <summary>
        /// A row in the problems table: the failures of one part of a system, as tracked by an <see cref="ExceptionTracker"/>
        /// </summary>
        [SysWeaver.Data.TableDataCard("problem@services/cards")]
        [TableDataPrimaryKey(nameof(System), nameof(Source))]
        [TableDataDefaultOrder("-" + nameof(LastTime))]
        public sealed class ProblemRow
        {
            /// <summary>
            /// The system that had the problems
            /// </summary>
            public String System;

            /// <summary>
            /// The part of the system that had the problems (the name of the exception tracker)
            /// </summary>
            [TableDataText(40)]
            public String Source;

            /// <summary>
            /// The number of failures since the service started
            /// </summary>
            public long Count;

            /// <summary>
            /// The time of the last failure (UTC)
            /// </summary>
            [TableDataSortDesc]
            public DateTime LastTime;

            /// <summary>
            /// The last exception (message and call stack)
            /// </summary>
            [TableDataText(64)]
            public String LastException;
        }

        /// <summary>
        /// Get the problems (failures) of all systems, from the statistics of the exception trackers.
        /// Not every failure is logged one by one (to avoid flooding the log, ex: a denial of service attempt, see LogNewProblems: rate limited warnings per part), but every tracked failure is counted here.
        /// </summary>
        /// <returns>One row per exception tracker with at least one failure</returns>
        public List<ProblemRow> GetProblems()
        {
            Dictionary<String, ProblemRow> rows = new(StringComparer.Ordinal);
            List<ProblemRow> list = new();
            ProblemRow Get(Stats s, String suffix)
            {
                var n = s.Name ?? "";
                var source = n.EndsWith(suffix, StringComparison.Ordinal) ? n.Substring(0, n.Length - suffix.Length) : n;
                source = source.TrimEnd('.');
                var key = String.Concat(s.System, "\n", source);
                if (!rows.TryGetValue(key, out var row))
                {
                    row = new ProblemRow
                    {
                        System = s.System,
                        Source = source.Length > 0 ? source : "-",
                    };
                    rows.Add(key, row);
                    list.Add(row);
                }
                return row;
            }
            foreach (var s in GetStats())
            {
                switch (s.Description)
                {
                    case ExceptionTracker.StatsCountDesc:
                        Get(s, "Count").Count = s.Value is long l ? l : 0;
                        break;
                    case ExceptionTracker.StatsTimeDesc:
                        Get(s, "Time").LastTime = s.Value is DateTime t ? t : default;
                        break;
                    case ExceptionTracker.StatsExceptionDesc:
                        Get(s, "Exception").LastException = s.Value as String;
                        break;
                }
            }
            return list;
        }

        /// <summary>
        /// Problems: how often each part of the system failed, when it last failed and why (from the exception trackers).
        /// Use this to find problems, not every failure is written to the log.
        /// </summary>
        /// <param name="r">Parameters</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>Table data</returns>
        [WebApi("debug/{0}")]
        [WebApiAuth(Roles.OpsDev)]
        [WebApiClientCache(1)]
        [WebApiRequestCache(1, WebApiCaches.Globally)]
        [WebApiCompression("br:Balanced, deflate:Balanced, gzip:Balanced")]
        [WebMenuTable(null, "Monitoring/{0}", "Problems", "How often each part of the system failed, when and why (not every failure is logged)", "IconTableStats", 3)]
        public Task<TableData> ProblemsTable(TableDataRequest r, HttpServerRequest context) => TableDataTools.Get(context, r, 1000, GetProblems(), "Problems");


        #region Problem log

        /// <summary>
        /// The number of prune cycles (2 s each) between two scans for new problems (10 s)
        /// </summary>
        const int ProblemLogPruneCycles = 5;

        /// <summary>
        /// A part that keeps failing is logged at most this often (the Problems table and the exception trackers have the details)
        /// </summary>
        static readonly long ProblemLogRepeatTicks = TimeSpan.FromHours(1).Ticks;

        /// <summary>
        /// The longest message text logged (the first line of the last exception)
        /// </summary>
        const int ProblemLogMaxMessage = 300;

        sealed class ProblemLogState
        {
            public long LoggedCount;
            public long LastLogTicks;
            public String LastMessage;
        }

        readonly Dictionary<String, ProblemLogState> ProblemLog = new(StringComparer.Ordinal);

        /// <summary>
        /// Log the failures that the exception trackers counted since the last scan (called every 10 s).
        /// Not every failure is logged (a denial of service attempt or a dead end point could flood the log): each part (exception tracker) is logged
        /// as a warning when it first fails, then at most once per hour while it keeps failing (with the number of failures since the last report).
        /// A report with a new message includes the full exception.
        /// All failures are counted in the problems table (<see cref="ProblemsTable"/>).
        /// </summary>
        void LogNewProblems()
        {
            try
            {
                var now = DateTime.UtcNow.Ticks;
                var log = ProblemLog;
                lock (log)
                {
                    foreach (var p in GetProblems())
                    {
                        var key = String.Concat(p.System, "\n", p.Source);
                        if (!log.TryGetValue(key, out var st))
                        {
                            st = new ProblemLogState();
                            log.Add(key, st);
                        }
                        //  A tracker that was replaced (ex: a service registered again) starts over
                        if (p.Count < st.LoggedCount)
                            st.LoggedCount = 0;
                        var n = p.Count - st.LoggedCount;
                        if (n <= 0)
                            continue;
                        var ex = p.LastException ?? "";
                        var nl = ex.IndexOfAny(LineBreaks);
                        var msg = (nl < 0 ? ex : ex.Substring(0, nl)).Trim();
                        if (msg.Length > ProblemLogMaxMessage)
                            msg = msg.Substring(0, ProblemLogMaxMessage - 1) + "…";
                        var first = st.LastLogTicks == 0;
                        var newMessage = !String.Equals(msg, st.LastMessage, StringComparison.Ordinal);
                        var since = now - st.LastLogTicks;
                        if (!first && (since < ProblemLogRepeatTicks))
                            continue;
                        String text;
                        if (first)
                            text = n == 1
                                ? $"{Tag} Problem in \"{p.System}\" {p.Source}: {msg}"
                                : $"{Tag} Problem in \"{p.System}\" {p.Source}, {n} failures since the start: {msg}";
                        else
                        {
                            var minutes = Math.Max(1, (int)Math.Round(TimeSpan.FromTicks(since).TotalMinutes));
                            text = $"{Tag} Problem in \"{p.System}\" {p.Source}, {n} {(n == 1 ? "failure" : "failures")} in the last {minutes} {(minutes == 1 ? "minute" : "minutes")} ({p.Count} in total), the latest: {msg}";
                        }
                        //  The full exception (call stack) once per message, repeats only name it
                        if (newMessage && (nl >= 0))
                            text = String.Concat(text, "\n", ex);
                        AddMessage(text, MessageLevels.Warning);
                        st.LoggedCount = p.Count;
                        st.LastLogTicks = now;
                        st.LastMessage = msg;
                    }
                }
            }
            catch
            {
                //  Never let the problem log break the periodic prune
            }
        }

        static readonly Char[] LineBreaks = ['\r', '\n'];

        #endregion//Problem log
    }
}
