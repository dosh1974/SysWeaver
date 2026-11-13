using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SysWeaver.Data;

namespace SysWeaver.HttpBench
{
    /// <summary>
    /// The report of a test as a markdown (GitHub flavored) document: the key results first (requests per second and response times), then the
    /// parameters and the tables (end points, modules, status codes, errors, the timeline and the response time distribution).
    /// </summary>
    public static class BenchMarkDownReport
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// The most rows of the timeline (seconds are merged into groups when a test is longer).
        /// </summary>
        public const int MaxTimelineRows = 60;

        /// <summary>
        /// A number with thousand separators (a space, readable in any locale).
        /// </summary>
        public static String Num(double v, int decimals = 0) => v.ToString("N" + decimals, Inv).Replace(',', ' ');

        /// <summary>
        /// A time in ms with a precision that fits its size: "0.152 ms", "3.41 ms", "27.5 ms", "312 ms".
        /// </summary>
        public static String Ms(double ms) => Num(ms, ms >= 100 ? 0 : ms >= 10 ? 1 : ms >= 1 ? 2 : 3) + " ms";

        /// <summary>
        /// A byte size: "512 B", "1.5 KB", "12.3 MB".
        /// </summary>
        public static String Bytes(double b)
        {
            String[] units = ["B", "KB", "MB", "GB", "TB"];
            int u = 0;
            while ((Math.Abs(b) >= 1024) && (u < units.Length - 1))
            {
                b /= 1024;
                ++u;
            }
            return String.Concat(Num(b, u == 0 ? 0 : b >= 100 ? 0 : b >= 10 ? 1 : 2), " ", units[u]);
        }

        /// <summary>
        /// A duration: "45 s", "2 min 30 s", "1 h 5 min".
        /// </summary>
        public static String Duration(double seconds)
        {
            var s = (long)Math.Round(seconds);
            if (s < 60)
                return s + " s";
            if (s < 3600)
                return (s % 60) != 0 ? String.Concat(s / 60, " min ", s % 60, " s") : String.Concat(s / 60, " min");
            var m = (long)Math.Round((s % 3600) / 60.0);
            return m != 0 ? String.Concat(s / 3600, " h ", m, " min") : String.Concat(s / 3600, " h");
        }

        /// <summary>
        /// The end points of a selection, by module: "Pages and scripts (all 11), Application and session (GetMenu, GetUser)".
        /// </summary>
        public static String DescribeEndPoints(IEnumerable<String> ids)
        {
            var set = new HashSet<String>(ids ?? [], StringComparer.Ordinal);
            var parts = new List<String>();
            foreach (var m in BenchCatalog.Modules)
            {
                var sel = m.EndPoints.Where(x => set.Contains(x.Id)).ToArray();
                if (sel.Length <= 0)
                    continue;
                parts.Add(sel.Length == m.EndPoints.Length
                    ? String.Concat(m.Name, " (all ", sel.Length, ")")
                    : String.Concat(m.Name, " (", String.Join(", ", sel.Select(x => x.Name)), ")"));
            }
            return String.Join(", ", parts);
        }

        /// <summary>
        /// The parameters of a test as (name, value) pairs (shown in the report and by the console tool).
        /// </summary>
        public static IEnumerable<(String Name, String Value)> Parameters(BenchStartRequest s)
        {
            var eps = s.EndPoints ?? BenchSetup.DefaultEndPoints();
            yield return ("Server", s.Target);
            yield return ("Duration", Duration(s.DurationSeconds));
            yield return ("Workers", Num(s.Concurrency));
            yield return ("End points", String.Concat(eps.Length, ": ", DescribeEndPoints(eps)));
            yield return ("Api values", String.Concat(s.SendAsGet ? "GET (query string)" : "POST (body)", ", ", String.IsNullOrEmpty(s.RequestCompression) ? "no compression" : s.RequestCompression));
            var ae = s.ResponseCompression;
            yield return ("Accept-Encoding", String.IsNullOrEmpty(ae) ? "none" : ae == "*" ? "all (" + String.Join(", ", BenchSetup.CompressionCodes()) + ")" : ae);
            yield return ("Certificates", s.IgnoreCertificateErrors ? "any accepted" : "validated");
        }

        static void Heading(StringBuilder sb, String text) => sb.AppendLine().Append("## ").AppendLine(text).AppendLine();

        static void KeyValues(StringBuilder sb, String keyTitle, IEnumerable<(String Key, String Value)> rows)
        {
            sb.Append("| ").Append(keyTitle).AppendLine(" | Value |");
            sb.AppendLine("|:---|:---|");
            foreach (var (k, v) in rows)
                sb.Append("| ").Append(k).Append(" | ").Append(StringTools.EscapeMD(v ?? "")).AppendLine(" |");
        }

        /// <summary>
        /// The byte size columns of the latency rows: the markdown export writes them as plain numbers, the totals are in the results.
        /// </summary>
        static readonly String[] ByteColumns = [nameof(BenchLatencyRow.WireSent), nameof(BenchLatencyRow.WireRecv), nameof(BenchLatencyRow.BodyRecv), nameof(BenchLatencyRow.RecvPerSecond)];

        static void Table<T>(StringBuilder sb, IEnumerable<T> rows, params String[] hide)
        {
            var t = TableDataTools.GetAll(rows);
            foreach (var h in hide)
                t.HideColumn(h);
            sb.Append(MarkDownTableDataExporter.Instance.GetMarkDownText(t));
        }

        /// <summary>
        /// The report of a test (done or running).
        /// </summary>
        /// <param name="run">The test</param>
        /// <param name="tool">The name of the program that made the report (shown in the footer)</param>
        /// <returns>The markdown text</returns>
        public static String Create(BenchRun run, String tool = "SysWeaver HttpBench")
        {
            var s = run.GetStatus();
            var secs = Math.Max(0.001, Math.Min(s.ElapsedSeconds, s.DurationSeconds));
            var host = run.Target.Authority;
            var sb = new StringBuilder();
            sb.Append("# HTTP bench: ").AppendLine(StringTools.EscapeMD(host)).AppendLine();
            sb.Append("**").Append(s.State).Append("** · started ").Append(s.Started.ToString("yyyy-MM-dd HH:mm:ss", Inv)).Append(" UTC · ran ")
                .Append(Duration(s.ElapsedSeconds)).Append(" · ").Append(Num(s.Concurrency)).Append(" workers · ").Append(s.EndPointCount).AppendLine(" end points  ");
            if (!String.IsNullOrEmpty(s.Message))
                sb.Append("> ").AppendLine(StringTools.EscapeMD(s.Message));

            //  The key results
            Heading(sb, "Results");
            sb.AppendLine("| Metric | Value |");
            sb.AppendLine("|:---|---:|");
            void Row(String name, String value, bool strong = false)
            {
                if (strong)
                    sb.Append("| **").Append(name).Append("** | **").Append(value).AppendLine("** |");
                else
                    sb.Append("| ").Append(name).Append(" | ").Append(value).AppendLine(" |");
            }
            Row("Requests/s", Num(s.Rps, 1), true);
            Row("Peak requests/s (one second)", Num(s.PeakRps));
            Row("Average response time", Ms(s.AvgMs), true);
            Row("Median (P50)", Ms(s.P50Ms), true);
            Row("P90", Ms(s.P90Ms));
            Row("P95", Ms(s.P95Ms), true);
            Row("P99", Ms(s.P99Ms), true);
            Row("Min / max", Ms(s.MinMs) + " / " + Ms(s.MaxMs));
            Row("Time to first byte (average)", Ms(s.AvgTtfbMs));
            Row("Requests", Num(s.Requests));
            Row("Errors", String.Concat(Num(s.Errors), " (", Num(s.ErrorPercent, 2), " %)"), s.Errors > 0);
            Row("Received", String.Concat(Bytes(s.WireRecv), " (", Bytes(s.WireRecv / secs), "/s), bodies ", Bytes(s.BodyRecv)));
            Row("Sent", String.Concat(Bytes(s.WireSent), " (", Bytes(s.WireSent / secs), "/s), api values ", Bytes(s.BodySent)));
            Row("Connections opened", Num(s.Connections));
            sb.AppendLine().AppendLine("Response times are from sending a request to the last byte of its response; percentiles are read from a histogram (about ±6 %).");

            Heading(sb, "Parameters");
            KeyValues(sb, "Parameter", Parameters(run.Settings));

            Heading(sb, "End points");
            Table(sb, BenchReport.EndPointRows(run), ByteColumns.Append(nameof(BenchEndPointRow.AvgBody)).Append(nameof(BenchEndPointRow.Connections)).ToArray());

            var mods = BenchReport.ModuleRows(run);
            if (mods.Count > 1)
            {
                Heading(sb, "Modules");
                Table(sb, mods, ByteColumns);
            }

            Heading(sb, "Status codes");
            Table(sb, BenchReport.StatusTotalRows(run), nameof(BenchStatusRow.Module), nameof(BenchStatusRow.EndPoint));

            Heading(sb, "Errors");
            var errors = BenchReport.ErrorRows(run);
            if (errors.Count > 0)
                Table(sb, errors);
            else
                sb.AppendLine("No errors: every request got a response with a status below 400.");

            var timeline = BenchReport.SecondRows(run, MaxTimelineRows);
            if (timeline.Count > 0)
            {
                var step = timeline.Count > 1 ? timeline[1].Second - timeline[0].Second : 1;
                Heading(sb, step > 1 ? String.Concat("Timeline (", step, " s per row)") : "Timeline (per second)");
                Table(sb, timeline, ByteColumns.Append(nameof(BenchSecondRow.Time)).Append(nameof(BenchLatencyRow.AvgTtfbMs)).ToArray());
                sb.AppendLine().AppendLine("Requests are counted in the second they completed, the last rows include the requests that completed after the end of the test.");
            }

            var hist = BenchReport.HistogramRows(run);
            if (hist.Count > 0)
            {
                Heading(sb, "Response time distribution");
                Table(sb, hist);
            }

            sb.AppendLine().Append("---").AppendLine().AppendLine()
                .Append("*Made by ").Append(StringTools.EscapeMD(tool)).Append(" at ").Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", Inv)).AppendLine(" UTC.*");
            return sb.ToString();
        }
    }
}
