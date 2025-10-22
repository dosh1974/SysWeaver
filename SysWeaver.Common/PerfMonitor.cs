using SysWeaver.Data;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Used to mark a type as having a performance monitor (may be collected by a table etc)
    /// </summary>
    public interface IPerfMonitored
    {
        /// <summary>
        /// The performance monitor instance
        /// </summary>
        PerfMonitor PerfMon { get; }
    }

    /// <summary>
    /// A performance monitor collector instance
    /// </summary>
    public sealed class PerfMonitor : IEnumerable<IPerfEntry>
    {

        /// <summary>
        /// Globally enable/disable all
        /// </summary>
        public static bool EnableAny = true;


        /// <summary>
        /// Enable/disable performance tracking
        /// </summary>
        [TableDataOrder(0)]
        [TableDataBooleanToggle("../Api/debug/TogglePerformanceMonitor?\"{1}\"", "Enabled", "Enabled", "Click to disabled performance monitoring of \"{1}\"", "Click to enable performance monitoring of \"{1}\"")]
        public bool Enabled
        {
            get => InternalEnable;
            set
            {
                if (value == InternalEnable)
                    return;
                InternalEnable = value;
                Reset();
            }
        }

        /// <summary>
        /// System name
        /// </summary>
        [TableDataOrder(1)]
        public readonly String System;

        /// <summary>
        /// Reset the performance counters for this system
        /// </summary>
        [TableDataOrder(2)]
        [TableDataActions(
            "Reset", 
            "Click to reset the performance counter for \"{0}\"",
            "../Api/debug/ResetPerformanceMonitor?\"{0}\"",
            "IconReset",


            "Toggle",
            "Click to toggle the performance counter for \"{0}\"",
            "../Api/debug/TogglePerformanceMonitor?\"{0}\"",
            "IconReload"
            )]
        public String Actions => System;


        /// <summary>
        /// Reset all counters
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref Start, Stopwatch.GetTimestamp());
            Entries.Clear();
        }

        /// <summary>
        /// Create a new peformance tracker
        /// </summary>
        /// <param name="systemName"></param>
        public PerfMonitor(String systemName)
        {
            System = systemName;
            Interlocked.Exchange(ref Start, Stopwatch.GetTimestamp());
        }

        long Start = long.MaxValue;

        /// <summary>
        /// Get current performance information
        /// </summary>
        /// <returns></returns>
        public IEnumerator<IPerfEntry> GetEnumerator()
        {
            if (EnableAny)
            {
                var now = Stopwatch.GetTimestamp();
                var runningFor = now - Interlocked.Read(ref Start);
                foreach (var x in Entries)
                {
                    var k = x.Value;
                    k.Update(runningFor);
                    yield return k;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


        readonly ConcurrentDictionary<String, PerfTrackerEntry> Entries = new(StringComparer.Ordinal);

        bool InternalEnable = true;


        internal PerfTrackerEntry Begin(String name)
        {
            if (!EnableAny)
                return null;
            if (!InternalEnable)
                return null;
            var e = Entries;
            if (e.TryGetValue(name, out var entry))
                return entry;
            entry = new PerfTrackerEntry(System, name);
            if (!e.TryAdd(name, entry))
                entry = e[name];
            return entry;
        }

        #region Tools

        /// <summary>
        /// Get a stopwatch time stamp
        /// </summary>
        /// <returns></returns>
        public static long GetTimestamp() => Stopwatch.GetTimestamp();

        /// <summary>
        /// Ellapsed tíme since a given stopwatch time stamp
        /// </summary>
        /// <param name="sinceTimeStamp"></param>
        /// <returns></returns>
        public static TimeSpan GetEllapsed(long sinceTimeStamp) => TimeSpan.FromTicks(ToTicks(Stopwatch.GetTimestamp() - sinceTimeStamp));


        static readonly bool NeedConversion = TimeSpan.TicksPerSecond != Stopwatch.Frequency;
        static readonly long Gcd = MathExt.Gcd(TimeSpan.TicksPerSecond, Stopwatch.Frequency);
        static readonly long Mul = TimeSpan.TicksPerSecond / Gcd;
        static readonly long Div = Stopwatch.Frequency / Gcd;

        /// <summary>
        /// Convert from stop watchticks to time span ticks
        /// </summary>
        public static readonly Func<long, long> ToTicks = NeedConversion ? new Func<long, long>(SlowGetTicks) : x => x;

        /// <summary>
        /// Convert from stopwatch ticks to time span
        /// </summary>
        /// <param name="stopWatchTicks">Stopwatch ticks</param>
        /// <returns>TimeSpan</returns>
        public static TimeSpan ToTimeSpan(long stopWatchTicks) => TimeSpan.FromTicks(ToTicks(stopWatchTicks));


        static long SlowGetTicks(long counter)
        {
            var tps = Mul;
            var fr = Div;
            Decimal d = counter;
            var frh = fr >> 1;
            d *= tps;
            d += frh;
            d /= fr;
            return (long)d;
        }

        #endregion//Tools


    }

    public static class PerfMonitorEx
    {
        public static IPerfMesurement Track(this PerfMonitor tracker, String name)
        {
            if (tracker == null)
                return null;
            var e = tracker.Begin(name);
            if (e == null)
                return null;
            return new PerfMesurement(e);
        }
    }


    public interface IPerfMesurement : IDisposable
    {
        /// <summary>
        /// Time taken so far, in stop watch ticks
        /// </summary>
        long Ellapsed { get; }

        /// <summary>
        /// Get the start time stamp
        /// </summary>
        long StartTimeStamp { get; }

    }

    public static class PerfMesurementExt
    {
        public static TimeSpan GetEllapsedTime(this IPerfMesurement perf) => 
            PerfMonitor.ToTimeSpan(perf.Ellapsed);
    }

    struct PerfMesurement : IPerfMesurement
    {
        public PerfMesurement(PerfTrackerEntry e)
        {
            E = e;
            int inp = Interlocked.Increment(ref e.InProgress);
            InterlockedEx.Max(ref e.MaxConcurrency, inp);
            Tc = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Time taken so far, in stop watch ticks
        /// </summary>
        public long Ellapsed => Stopwatch.GetTimestamp() - Tc;

        /// <summary>
        /// Get the start time stamp
        /// </summary>
        public long StartTimeStamp => Tc;

        public readonly void Dispose() 
        {
            var tc = Stopwatch.GetTimestamp();
            var took = tc - Tc;
            var e = E;
            Interlocked.Decrement(ref e.InProgress);
            Interlocked.Add(ref e.Total, took);
            Interlocked.Increment(ref e.Count);
            Interlocked.Exchange(ref e.Last, DateTime.UtcNow.Ticks);
            Interlocked.Exchange(ref e.LastDuration, took);
            InterlockedEx.Max(ref e.Max, took);
            InterlockedEx.Min(ref e.Min, took);
        }
        readonly long Tc;
        readonly PerfTrackerEntry E;
    }

    [TableDataPrimaryKey(nameof(System), nameof(Name))]
    public interface IPerfEntry
    {
        /// <summary>
        /// The system that captured this performance entry
        /// </summary>
        String System { get; }
        /// <summary>
        /// The name of the measured "method"
        /// </summary>
        [TableDataKey]
        String Name { get; }
        /// <summary>
        /// The total number of times that the "method" have been executed / measured.
        /// </summary>
        long Count { get; }
        /// <summary>
        /// Number of current concurrent executions of the "method".
        /// </summary>
        int InProgress { get; }
        /// <summary>
        /// The maximum number of concurrrent executions of the "method".
        /// </summary>
        int MaxConcurrency { get; }
        /// <summary>
        /// The total time spent executing the "method".
        /// </summary>
        TimeSpan Total { get; }
        /// <summary>
        /// The avergae time spent executing one execution of the "method".
        /// </summary>
        TimeSpan Average { get; }

        /// <summary>
        /// The pecentage of time spent in this "method".
        /// </summary>
        [TableDataNumber(3, "{0}%")]
        float Percentage { get; }

        /// <summary>
        /// The number of times per second that this "method" have been called.
        /// </summary>
        [TableDataNumber(3, "{0} e/s")]
        float Rate { get; }

        /// <summary>
        /// The duration of the last execution of this "method".
        /// </summary>
        TimeSpan LastDuration { get; }

        /// <summary>
        /// When the "method" was last executed (completed).
        /// </summary>
        DateTime LastExecution { get; }

        /// <summary>
        /// The minimum duration of a method "execution".
        /// </summary>
        TimeSpan Min { get; }
        
        /// <summary>
        /// The maximum duration of a method "execution".
        /// </summary>
        TimeSpan Max { get; }




    }

    sealed class PerfTrackerEntry : IPerfEntry
    { 
        public PerfTrackerEntry(String system, String name)
        {
            IntSystem = system;
            IntName = name;
        }

        internal long Count;
        internal long Total;
        internal long LastDuration;
        internal long Last;
        internal int MaxConcurrency;
        internal int InProgress;
        internal long Min = long.MaxValue;
        internal long Max;


        public string System => IntSystem;

        public string Name => IntName;

        long IPerfEntry.Count => Interlocked.Read(ref IntCount);

        int IPerfEntry.InProgress => IntInProgress;

        int IPerfEntry.MaxConcurrency => IntMaxConcurrency;

        TimeSpan IPerfEntry.Total => IntTotal ?? TimeSpan.Zero;

        public TimeSpan Average => IntAverage ?? TimeSpan.Zero;

        TimeSpan IPerfEntry.Min => IntMin ?? TimeSpan.Zero;

        TimeSpan IPerfEntry.Max => IntMax ?? TimeSpan.Zero;

        TimeSpan IPerfEntry.LastDuration => IntLastDuration ?? TimeSpan.Zero;

        public DateTime LastExecution => IntLastExecution ?? DateTime.MinValue;

        public float Percentage => IntPercentage;

        public float Rate => IntCountPerMinute;


        internal void Update(long runningFor)
        {
            var count = Interlocked.Read(ref Count);
            var total = Interlocked.Read(ref Total);
            var last = Interlocked.Read(ref Last);
            var min = Interlocked.Read(ref Min);
            var max = Interlocked.Read(ref Max);
            var lastDuration = Interlocked.Read(ref LastDuration);
            IntInProgress = InProgress;
            IntMaxConcurrency = MaxConcurrency;
            IntCount = count;
            var getTicks = PerfMonitor.ToTicks;
            var d = getTicks(total);
            IntTotal = TimeSpan.FromTicks(d);
            Decimal tot = d;
            if (count == 0)
                count = 1;
            d += (count >> 1);
            d /= count;
            IntAverage = TimeSpan.FromTicks(d);
            IntMin = TimeSpan.FromTicks(min >= long.MaxValue ? 0 : getTicks(min));
            IntMax = TimeSpan.FromTicks(getTicks(max));
            IntLastDuration = TimeSpan.FromTicks(getTicks(lastDuration));
            IntLastExecution = new DateTime(last, DateTimeKind.Utc);

            Decimal fr = Stopwatch.Frequency;
            Decimal rate = count;
            rate *= fr;
            rate /= runningFor;
            Interlocked.Exchange(ref IntCountPerMinute, (float)rate);

            Decimal timePer = 100M * tot;
            timePer /= runningFor;
            Interlocked.Exchange(ref IntPercentage, (float)timePer);
        }


        readonly String IntSystem;
        readonly String IntName;
        long IntCount;
        int IntInProgress;
        int IntMaxConcurrency;
        TimeSpan? IntTotal;
        TimeSpan? IntAverage;
        TimeSpan? IntMin;
        TimeSpan? IntMax;
        TimeSpan? IntLastDuration;
        DateTime? IntLastExecution;

        float IntPercentage;
        float IntCountPerMinute;

    }





}
