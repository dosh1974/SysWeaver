using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SysWeaver.MicroService;

namespace SysWeaver.HttpBench
{
    /// <summary>
    /// The charts of a test (ChartJsConfig json for chart/chart.html).
    /// Time series use utc "HH:mm:ss" labels (the page shows them in local time with timeoffset=local) and are merged into buckets of
    /// several seconds when a test has more than <see cref="MaxPoints"/> seconds.
    /// </summary>
    static class BenchCharts
    {
        /// <summary>
        /// The most points of a time series.
        /// </summary>
        const int MaxPoints = 600;

        //  Chromatic colours, so the chart viewer maps them to the theme palette (in this order)
        const String Blue = "#2A78D6";
        const String Magenta = "#C4507E";
        const String Green = "#138A60";
        const String Violet = "#6250D6";
        const String Ochre = "#A87400";
        const String Copper = "#A85724";

        static int Refresh(BenchRun r) => r.IsDone ? 600_000 : 1000;

        static String Fmt(double v, int decimals) => v.ToString("F" + decimals, CultureInfo.InvariantCulture);

        /// <summary>
        /// The seconds of a test merged into at most <see cref="MaxPoints"/> buckets.
        /// </summary>
        static (String[] Labels, BenchCounters[] Buckets, int[] Widths) Series(BenchRun r)
        {
            //  Only whole seconds in which requests were sent: the last partial second and the drain time (requests that completed after the end)
            //  would show as a drop at the end. They are still in the totals and the tables.
            var n = Math.Min(r.SecondCount, Math.Max(1, (int)Math.Floor(r.MeasuredSeconds)));
            if (r.SecondCount <= 0)
                n = 0;
            if (n <= 0)
                return ([], [], []);
            var step = Math.Max(1, (n + MaxPoints - 1) / MaxPoints);
            var count = (n + step - 1) / step;
            var labels = new String[count];
            var buckets = new BenchCounters[count];
            var widths = new int[count];
            for (int b = 0; b < count; ++b)
            {
                var c = new BenchCounters(true);
                var from = b * step;
                var to = Math.Min(n, from + step);
                for (int s = from; s < to; ++s)
                {
                    var sec = r.GetSecond(s);
                    if (sec != null)
                        foreach (var x in sec)
                            c.AddFrom(x);
                    LatencyHistogram.AddTo(c.Hist, r.GetSecondHist(s));
                }
                labels[b] = r.SecondTime(from).ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                buckets[b] = c;
                widths[b] = to - from;
            }
            return (labels, buckets, widths);
        }

        static ChartJsDataSet LineSet(String label, double[] data, String color) => new ChartJsDataSet
        {
            label = label,
            data = data,
            borderColor = [color],
            backgroundColor = [color],
            borderWidth = 2,
            radius = 0,
            hitRadius = 6,
            hoverRadius = 4,
            tension = 0.25,
        };

        static ChartJsDataSet BarSet(String label, double[] data, String color) => new ChartJsDataSet
        {
            label = label,
            data = data,
            backgroundColor = [color],
            borderWidth = 0,
            categoryPercentage = 0.85,
            barPercentage = 0.9,
        };

        static ChartJsOptions Options(String title, bool legend, bool horizontal = false, double? min = 0) => new ChartJsOptions
        {
            indexAxis = horizontal ? "y" : null,
            scales = new ChartJsScalesOptions
            {
                x = horizontal ? new ChartJsScaleOptions { min = min } : null,
                y = horizontal ? null : new ChartJsScaleOptions { min = min },
            },
            plugins = new ChartJsPlugins
            {
                datalabels = new ChartJsDataLabels
                {
                    display = false,
                },
                //  At the bottom: the chart page's menu button sits in the top right corner (over a legend at the top)
                legend = new ChartJsLegend
                {
                    display = legend,
                    position = "bottom",
                },
                title = new ChartJsTitle
                {
                    display = true,
                    text = [title],
                },
            },
        };

        static ReadOnlyMemory<Byte> TimeChart(BenchRun r, String title, String suffix, int precision, String[] labels, params ChartJsDataSet[] sets)
            => ChartJsService.ChartSerialize(new ChartJsConfig
            {
                RefreshRate = Refresh(r),
                Title = title,
                type = "line",
                ValidTypes = ["bar"],
                Precision = -precision,
                ValueSuffix = suffix,
                ValueLabel = 4,
                data = new ChartJsData
                {
                    labels = labels,
                    datasets = sets,
                },
                options = Options(title, sets.Length > 1),
            });

        public static ReadOnlyMemory<Byte> Throughput(BenchRun r)
        {
            var (labels, b, w) = Series(r);
            return TimeChart(r, "Requests per second", "/s", 1, labels,
                LineSet("Requests/s", b.Select((x, i) => x.Count / (double)w[i]).ToArray(), Blue),
                LineSet("Errors/s", b.Select((x, i) => x.Errors / (double)w[i]).ToArray(), Magenta));
        }

        public static ReadOnlyMemory<Byte> Latency(BenchRun r)
        {
            var (labels, b, _) = Series(r);
            return TimeChart(r, "Response time", " ms", 2, labels,
                LineSet("Average", b.Select(x => x.AvgMs).ToArray(), Blue),
                LineSet("Median", b.Select(x => x.PercentileMs(0.5)).ToArray(), Green),
                LineSet("P95", b.Select(x => x.PercentileMs(0.95)).ToArray(), Ochre),
                LineSet("P99", b.Select(x => x.PercentileMs(0.99)).ToArray(), Magenta));
        }

        public static ReadOnlyMemory<Byte> Bandwidth(BenchRun r)
        {
            var (labels, b, w) = Series(r);
            const double MB = 1024.0 * 1024.0;
            return TimeChart(r, "Network traffic", " MB/s", 2, labels,
                LineSet("Received", b.Select((x, i) => x.WireRecv / (MB * w[i])).ToArray(), Violet),
                LineSet("Sent", b.Select((x, i) => x.WireSent / (MB * w[i])).ToArray(), Copper));
        }

        static String[] EndPointLabels(BenchRun r) => r.EndPoints.Select(e => String.Concat(BenchCatalog.ModuleById[e.Module].Name, " / ", e.Name)).ToArray();

        static ReadOnlyMemory<Byte> EndPointChart(BenchRun r, String title, String suffix, int precision, params ChartJsDataSet[] sets)
            => ChartJsService.ChartSerialize(new ChartJsConfig
            {
                RefreshRate = Refresh(r),
                Title = title,
                type = "bar",
                ValidTypes = ["line"],
                Precision = -precision,
                ValueSuffix = suffix,
                ValueLabel = 4,
                data = new ChartJsData
                {
                    labels = EndPointLabels(r),
                    datasets = sets,
                },
                options = Options(title, sets.Length > 1, true),
            });

        public static ReadOnlyMemory<Byte> EndPointLatency(BenchRun r)
        {
            var c = r.EpTotals.Select(x =>
            {
                var t = new BenchCounters(true);
                t.AddFrom(x);
                return t;
            }).ToArray();
            return EndPointChart(r, "Response time per end point", " ms", 2,
                BarSet("Average", c.Select(x => x.AvgMs).ToArray(), Blue),
                BarSet("P95", c.Select(x => x.PercentileMs(0.95)).ToArray(), Ochre));
        }

        public static ReadOnlyMemory<Byte> EndPointRps(BenchRun r)
        {
            var secs = r.MeasuredSeconds;
            return EndPointChart(r, "Requests per second per end point", "/s", 1,
                BarSet("Requests/s", r.EpTotals.Select(x => x.Count / secs).ToArray(), Green));
        }

        public static ReadOnlyMemory<Byte> EndPointSize(BenchRun r)
            => EndPointChart(r, "Average response size per end point", " KB", 2,
                BarSet("Body (as received)", r.EpTotals.Select(x => x.Count > 0 ? x.BodyRecv / (1024.0 * x.Count) : 0).ToArray(), Violet),
                BarSet("On the wire", r.EpTotals.Select(x => x.Count > 0 ? x.WireRecv / (1024.0 * x.Count) : 0).ToArray(), Copper));

        static String MsText(double ms) => ms >= 100 ? Fmt(ms, 0) : ms >= 10 ? Fmt(ms, 1) : ms >= 1 ? Fmt(ms, 2) : Fmt(ms, 3);

        public static ReadOnlyMemory<Byte> Histogram(BenchRun r)
        {
            var t = r.Total();
            var labels = new List<String>();
            var data = new List<double>();
            if (t.Count > 0)
            {
                var first = LatencyHistogram.Index(t.MinUs);
                var last = LatencyHistogram.Index(t.MaxUs);
                for (int i = first; i <= last; ++i)
                {
                    labels.Add(MsText(LatencyHistogram.Mid(i) / 1000.0) + " ms");
                    data.Add(t.Hist[i]);
                }
            }
            const String title = "Response time distribution";
            return ChartJsService.ChartSerialize(new ChartJsConfig
            {
                RefreshRate = Refresh(r),
                Title = title,
                type = "bar",
                ValidTypes = ["line"],
                Precision = 0,
                ValueSuffix = " requests",
                ValueLabel = 4,
                DisableOrderMenu = true,
                data = new ChartJsData
                {
                    labels = labels.ToArray(),
                    datasets = [BarSet("Requests", data.ToArray(), Blue)],
                },
                options = Options(title, false),
            });
        }

        static ReadOnlyMemory<Byte> Doughnut(BenchRun r, String title, String suffix, String[] labels, double[] data)
            => ChartJsService.ChartSerialize(new ChartJsConfig
            {
                RefreshRate = Refresh(r),
                Title = title,
                type = "doughnut",
                ValidTypes = ["pie", "bar", "polarArea"],
                Precision = 0,
                ValueSuffix = suffix,
                ValueLabel = 1,
                data = new ChartJsData
                {
                    labels = labels,
                    datasets = [
                        new ChartJsDataSet
                        {
                            label = title,
                            data = data,
                            backgroundColor = [Blue, Green, Ochre, Violet, Magenta, Copper],
                            borderWidth = 0,
                        }
                    ],
                },
                options = new ChartJsOptions
                {
                    plugins = new ChartJsPlugins
                    {
                        title = new ChartJsTitle
                        {
                            display = true,
                            text = [title],
                        },
                    },
                },
            });

        public static ReadOnlyMemory<Byte> Modules(BenchRun r)
        {
            var mods = BenchCatalog.Modules.Where(m => r.EndPoints.Any(e => e.Module == m.Id)).ToArray();
            return Doughnut(r, "Requests per module", " requests",
                mods.Select(m => m.Name).ToArray(),
                mods.Select(m => (double)r.Total(i => r.EndPoints[i].Module == m.Id).Count).ToArray());
        }

        public static ReadOnlyMemory<Byte> Statuses(BenchRun r, Func<int, String> meaning)
        {
            var g = r.Statuses.ToArray().GroupBy(x => x.Key.Status).Select(x => (Status: x.Key, Count: x.Sum(y => y.Value))).OrderBy(x => x.Status).ToArray();
            return Doughnut(r, "Responses per status code", " responses",
                g.Select(x => String.Concat(x.Status.ToString(CultureInfo.InvariantCulture), " ", meaning(x.Status))).ToArray(),
                g.Select(x => (double)x.Count).ToArray());
        }
    }
}
