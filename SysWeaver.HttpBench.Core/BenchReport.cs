using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace SysWeaver.HttpBench
{
    /// <summary>
    /// The table rows of a test (from the totals down to single requests), the same for the tables of the page and the markdown report.
    /// </summary>
    public static class BenchReport
    {
        /// <summary>
        /// The display name of the module of an end point.
        /// </summary>
        public static String ModuleName(BenchEndPoint e) => BenchCatalog.ModuleById[e.Module].Name;

        /// <summary>
        /// What an http status code means: "OK", "Not found", 0 = "No response".
        /// </summary>
        public static String StatusMeaning(int status)
        {
            if (status == 0)
                return "No response";
            var t = ((HttpStatusCode)status).ToString();
            if (int.TryParse(t, out _))
                return status < 400 ? "Ok" : "Error";
            //  "NotFound" -> "Not found"
            var sb = new StringBuilder(t.Length + 4);
            for (int i = 0; i < t.Length; ++i)
            {
                var ch = t[i];
                //  A new word starts at an upper case letter after a lower case one ("OK" stays "OK")
                if ((i > 0) && Char.IsUpper(ch) && Char.IsLower(t[i - 1]))
                {
                    sb.Append(' ');
                    ch = Char.ToLowerInvariant(ch);
                }
                sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>
        /// The results per end point (in the order the workers call them).
        /// </summary>
        public static List<BenchEndPointRow> EndPointRows(BenchRun run)
        {
            var secs = run.MeasuredSeconds;
            var rows = new List<BenchEndPointRow>(run.EndPoints.Length);
            for (int i = 0; i < run.EndPoints.Length; ++i)
            {
                var e = run.EndPoints[i];
                var c = new BenchCounters(true);
                c.AddFrom(run.EpTotals[i]);
                var row = new BenchEndPointRow
                {
                    Order = i,
                    Module = ModuleName(e),
                    EndPoint = e.Name,
                    Method = run.Methods[i],
                    Path = e.Path,
                    Desc = e.Desc,
                    P50Ms = c.PercentileMs(0.5),
                    P90Ms = c.PercentileMs(0.9),
                    P95Ms = c.PercentileMs(0.95),
                    P99Ms = c.PercentileMs(0.99),
                    AvgBody = c.Count > 0 ? c.BodyRecv / (double)c.Count : 0,
                    Connections = c.Connections,
                };
                row.Set(c, secs);
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>
        /// The results per module (group of end points), in the catalog order.
        /// </summary>
        public static List<BenchModuleRow> ModuleRows(BenchRun run)
        {
            var secs = run.MeasuredSeconds;
            var total = run.Total().Count;
            var rows = new List<BenchModuleRow>();
            int order = 0;
            foreach (var m in BenchCatalog.Modules)
            {
                var count = run.EndPoints.Count(x => x.Module == m.Id);
                if (count <= 0)
                    continue;
                var c = run.Total(i => run.EndPoints[i].Module == m.Id);
                var row = new BenchModuleRow
                {
                    Order = order++,
                    Module = m.Name,
                    EndPoints = count,
                    P50Ms = c.PercentileMs(0.5),
                    P95Ms = c.PercentileMs(0.95),
                    P99Ms = c.PercentileMs(0.99),
                    SharePercent = total > 0 ? c.Count * 100.0 / total : 0,
                };
                row.Set(c, secs);
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>
        /// The results per second (all end points), or per group of seconds when there are more than <paramref name="maxRows"/> seconds.
        /// </summary>
        /// <param name="run">The test</param>
        /// <param name="maxRows">The most rows: seconds are merged into groups of the same length to stay below it</param>
        /// <returns>One row per second (or group of seconds), <see cref="BenchSecondRow.Second"/> is the first second of a group</returns>
        public static List<BenchSecondRow> SecondRows(BenchRun run, int maxRows = int.MaxValue)
        {
            var n = run.SecondCount;
            maxRows = Math.Max(1, maxRows);
            var step = Math.Max(1, (int)((n + (long)maxRows - 1) / maxRows));
            var rows = new List<BenchSecondRow>((n + step - 1) / step);
            for (int from = 0; from < n; from += step)
            {
                var to = Math.Min(n, from + step);
                var c = new BenchCounters(true);
                for (int s = from; s < to; ++s)
                {
                    var sec = run.GetSecond(s);
                    if (sec != null)
                        foreach (var x in sec)
                            c.AddFrom(x);
                    LatencyHistogram.AddTo(c.Hist, run.GetSecondHist(s));
                }
                var row = new BenchSecondRow
                {
                    Second = from,
                    Time = run.SecondTime(from),
                    P50Ms = c.PercentileMs(0.5),
                    P95Ms = c.PercentileMs(0.95),
                    P99Ms = c.PercentileMs(0.99),
                    Connections = c.Connections,
                };
                row.Set(c, to - from);
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>
        /// The results per second and end point (that completed requests in that second).
        /// </summary>
        public static List<BenchSecondEndPointRow> SecondEndPointRows(BenchRun run)
        {
            var n = run.SecondCount;
            var rows = new List<BenchSecondEndPointRow>();
            for (int s = 0; s < n; ++s)
            {
                var sec = run.GetSecond(s);
                if (sec == null)
                    continue;
                for (int i = 0; i < sec.Length; ++i)
                {
                    var c = sec[i];
                    if (c.Count <= 0)
                        continue;
                    var e = run.EndPoints[i];
                    var row = new BenchSecondEndPointRow
                    {
                        Second = s,
                        Order = i,
                        Module = ModuleName(e),
                        EndPoint = e.Name,
                    };
                    var cc = new BenchCounters(false);
                    cc.AddFrom(c);
                    row.Set(cc, 1);
                    rows.Add(row);
                }
            }
            return rows;
        }

        /// <summary>
        /// The results per worker.
        /// </summary>
        public static List<BenchWorkerRow> WorkerRows(BenchRun run)
        {
            var secs = run.MeasuredSeconds;
            return run.Workers.Select(w =>
            {
                var c = new BenchCounters(false);
                c.AddFrom(w.Totals);
                var row = new BenchWorkerRow
                {
                    Worker = w.Index,
                    Connections = w.Wire.Connections,
                };
                row.Set(c, secs);
                return row;
            }).ToList();
        }

        /// <summary>
        /// The responses per end point and status code (in the end point order, then by status).
        /// </summary>
        public static List<BenchStatusRow> StatusRows(BenchRun run)
            => run.Statuses.ToArray().Select(x =>
            {
                var e = run.EndPoints[x.Key.EndPoint];
                var total = run.EpTotals[x.Key.EndPoint].Count;
                return new BenchStatusRow
                {
                    Order = x.Key.EndPoint,
                    Module = ModuleName(e),
                    EndPoint = e.Name,
                    Status = x.Key.Status,
                    Meaning = StatusMeaning(x.Key.Status),
                    Count = x.Value,
                    SharePercent = total > 0 ? x.Value * 100.0 / total : 0,
                };
            }).OrderBy(x => x.Order).ThenBy(x => x.Status).ToList();

        /// <summary>
        /// The responses per status code (all end points), by status.
        /// </summary>
        public static List<BenchStatusRow> StatusTotalRows(BenchRun run)
        {
            var total = run.Total().Count;
            return run.Statuses.ToArray().GroupBy(x => x.Key.Status).Select(x =>
            {
                var count = x.Sum(y => y.Value);
                return new BenchStatusRow
                {
                    Module = "",
                    EndPoint = "",
                    Status = x.Key,
                    Meaning = StatusMeaning(x.Key),
                    Count = count,
                    SharePercent = total > 0 ? count * 100.0 / total : 0,
                };
            }).OrderBy(x => x.Status).ToList();
        }

        /// <summary>
        /// The errors per end point, status and message (the most frequent first).
        /// </summary>
        public static List<BenchErrorRow> ErrorRows(BenchRun run)
            => run.Errors.Values.ToArray().Select(x =>
            {
                var e = run.EndPoints[x.EndPoint];
                lock (x)
                {
                    return new BenchErrorRow
                    {
                        Module = ModuleName(e),
                        EndPoint = e.Name,
                        Status = x.Status,
                        Message = x.Message,
                        Count = x.Count,
                        FirstSecond = x.FirstUs / 1e6,
                        LastSecond = x.LastUs / 1e6,
                    };
                }
            }).OrderByDescending(x => x.Count).ToList();

        /// <summary>
        /// Single requests: all of them, or a uniform random sample of all requests when there were more than the test keeps.
        /// </summary>
        /// <param name="run">The test</param>
        /// <param name="title">A title that says if these are all requests or a sample</param>
        /// <returns>One row per request</returns>
        public static List<BenchRequestRow> RequestRows(BenchRun run, out String title)
        {
            var samples = run.GetSamples();
            var seen = run.SamplesSeen;
            title = samples.Count >= seen
                ? "All " + seen + " requests of test " + run.Id
                : "A random sample of " + samples.Count + " of the " + seen + " requests of test " + run.Id;
            return samples.Select(x =>
            {
                var e = run.EndPoints[x.EndPoint];
                return new BenchRequestRow
                {
                    Seq = x.Seq,
                    StartSecond = x.StartUs / 1e6,
                    Worker = x.Worker,
                    Module = ModuleName(e),
                    EndPoint = e.Name,
                    Status = x.Status,
                    Ms = x.DurationUs / 1000.0,
                    TtfbMs = x.TtfbUs / 1000.0,
                    WireSent = x.WireSent,
                    WireRecv = x.WireRecv,
                    BodySent = x.BodySent,
                    BodyRecv = x.BodyRecv,
                    NewConnection = x.NewConnection,
                    Error = x.Error,
                };
            }).ToList();
        }

        /// <summary>
        /// The latency histogram (all end points), from the fastest to the slowest request.
        /// </summary>
        public static List<BenchHistogramRow> HistogramRows(BenchRun run)
        {
            var t = run.Total();
            var rows = new List<BenchHistogramRow>();
            if (t.Count <= 0)
                return rows;
            var first = LatencyHistogram.Index(t.MinUs);
            var last = LatencyHistogram.Index(t.MaxUs);
            long sum = 0;
            for (int i = first; i <= last; ++i)
            {
                var c = t.Hist[i];
                sum += c;
                rows.Add(new BenchHistogramRow
                {
                    FromMs = LatencyHistogram.From(i) / 1000.0,
                    ToMs = LatencyHistogram.To(i) / 1000.0,
                    Count = c,
                    SharePercent = c * 100.0 / t.Count,
                    CumulativePercent = sum * 100.0 / t.Count,
                });
            }
            return rows;
        }
    }
}
