using SysWeaver.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

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
    /// Collects timing statistics (count, total, average, min, max, concurrency etc) for named operations of a system.
    /// Use <see cref="PerfMonitorEx.Track(PerfMonitor, string)"/> in a using statement to measure an operation, enumerate the instance to get the statistics.
    /// </summary>
    /// <remarks>
    /// Thread safe and lock free when measuring (an entry is created on first use of a name).
    /// Timing uses <see cref="Stopwatch"/> time stamps. Enumerating updates the computed values of each entry, the values of an entry are not a consistent snapshot under concurrent measurements.
    /// </remarks>
    public sealed class PerfMonitor : IEnumerable<IPerfEntry>
    {

        /// <summary>
        /// Globally enable/disable all performance monitors (process wide), when false no measurements are made and enumerating returns nothing
        /// </summary>
        public static bool EnableAny = true;


        /// <summary>
        /// Enable/disable performance tracking for this instance, changing the value resets all counters
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
        /// The name of the system being monitored
        /// </summary>
        [TableDataOrder(1)]
        public readonly String System;

        /// <summary>
        /// The system name, used as the argument of the reset / toggle actions when displayed in a table
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
        /// Reset all counters (all entries are removed and the running time restarts)
        /// </summary>
        /// <remarks>Measurements in progress complete on the removed entries (they are not counted in the new entries)</remarks>
        public void Reset()
        {
            Interlocked.Exchange(ref Start, Stopwatch.GetTimestamp());
            Entries.Clear();
        }

        /// <summary>
        /// Create a new performance tracker
        /// </summary>
        /// <param name="systemName">The name of the system being monitored</param>
        public PerfMonitor(String systemName)
        {
            System = systemName;
            Interlocked.Exchange(ref Start, Stopwatch.GetTimestamp());
        }

        long Start = long.MaxValue;

        /// <summary>
        /// Get current performance information, one entry per measured name (computed values are updated during the enumeration)
        /// </summary>
        /// <returns>An enumerator of the entries, empty if <see cref="EnableAny"/> is false</returns>
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


        readonly SemiFrozenDictionary<String, PerfTrackerEntry> Entries = new(StringComparer.Ordinal);

        bool InternalEnable = true;


        /// <summary>
        /// Get or create the entry for a name
        /// </summary>
        /// <param name="name">The name of the operation</param>
        /// <returns>The entry, or null if monitoring is disabled</returns>
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
        /// <returns>The current <see cref="Stopwatch.GetTimestamp"/> value</returns>
        public static long GetTimestamp() => Stopwatch.GetTimestamp();

        /// <summary>
        /// Elapsed time since a given stopwatch time stamp
        /// </summary>
        /// <param name="sinceTimeStamp">A time stamp obtained from <see cref="GetTimestamp"/></param>
        /// <returns>The elapsed time</returns>
        public static TimeSpan GetEllapsed(long sinceTimeStamp) => TimeSpan.FromTicks(ToTicks(Stopwatch.GetTimestamp() - sinceTimeStamp));


        static readonly bool NeedConversion = TimeSpan.TicksPerSecond != Stopwatch.Frequency;
        static readonly long Gcd = MathExt.Gcd(TimeSpan.TicksPerSecond, Stopwatch.Frequency);
        static readonly long Mul = TimeSpan.TicksPerSecond / Gcd;
        static readonly long Div = Stopwatch.Frequency / Gcd;

        /// <summary>
        /// Convert from stopwatch ticks to time span ticks (rounded to the nearest tick, an identity function if the stopwatch frequency is 10 MHz)
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

    /// <summary>
    /// Extension methods for <see cref="PerfMonitor"/>
    /// </summary>
    public static class PerfMonitorEx
    {
        /// <summary>
        /// Start measuring an operation, dispose the returned value (typically with a using statement) when the operation completes
        /// </summary>
        /// <param name="tracker">The performance monitor, may be null (nothing is measured)</param>
        /// <param name="name">The name of the operation (case sensitive)</param>
        /// <returns>A measurement, a default (no-op) measurement if <paramref name="tracker"/> is null or monitoring is disabled</returns>
        public static PerfMesurement Track(this PerfMonitor tracker, String name)
        {
            if (tracker == null)
                return default;
            var e = tracker.Begin(name);
            if (e == null)
                return default;
            return new PerfMesurement(e);
        }
    }


    /// <summary>
    /// Extension methods for <see cref="PerfMesurement"/>
    /// </summary>
    public static class PerfMesurementExt
    {
        /// <summary>
        /// Get the time elapsed since the measurement started
        /// </summary>
        /// <param name="perf">The measurement</param>
        /// <returns>The elapsed time (meaningless for a default measurement)</returns>
        public static TimeSpan GetEllapsedTime(this PerfMesurement perf) => 
            PerfMonitor.ToTimeSpan(perf.Ellapsed);
    }

    /// <summary>
    /// An in progress measurement started by <see cref="PerfMonitorEx.Track(PerfMonitor, string)"/>, dispose it exactly once when the measured operation completes.
    /// </summary>
    /// <remarks>A default instance measures nothing (disposing it does nothing). Disposing a copy (or disposing twice) counts the operation twice.</remarks>
    public readonly struct PerfMesurement : IDisposable
    {
        /// <summary>
        /// Create an empty measurement (that measures nothing)
        /// </summary>
        public PerfMesurement()
        {
        }

        internal PerfMesurement(PerfTrackerEntry e)
        {
            E = e;
            int inp = Interlocked.Increment(ref e.InProgress);
            InterlockedEx.Max(ref e.MaxConcurrency, inp);
            Tc = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Time taken so far, in stopwatch ticks (meaningless for a default measurement)
        /// </summary>
        public long Ellapsed => Stopwatch.GetTimestamp() - Tc;

        /// <summary>
        /// The stopwatch time stamp when the measurement started (zero for a default measurement)
        /// </summary>
        public long StartTimeStamp => Tc;

        /// <summary>
        /// End the measurement and update the statistics of the entry
        /// </summary>
        public void Dispose() 
        {
            var tc = Stopwatch.GetTimestamp();
            var took = tc - Tc;
            var e = E;
            if (e == null)
                return;
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

    /// <summary>
    /// Performance statistics of a named operation, as returned when enumerating a <see cref="PerfMonitor"/>
    /// </summary>
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
        /// The average time spent executing one execution of the "method".
        /// </summary>
        TimeSpan Average { get; }

        /// <summary>
        /// The percentage of the time since the monitor was created (or reset) that was spent in this "method" (can exceed 100 with concurrent executions).
        /// </summary>
        [TableDataPercentage(3)]
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

    /// <summary>
    /// The mutable statistics of one named operation, raw counters are updated atomically by <see cref="PerfMesurement"/> and the computed values are refreshed by <see cref="Update"/>
    /// </summary>
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


        /// <summary>
        /// Refresh the computed values from the raw counters
        /// </summary>
        /// <param name="runningFor">Number of stopwatch ticks since the monitor was created or reset</param>
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
            if (count == 0)
                count = 1;
            d += (count >> 1);
            d /= count;
            IntAverage = TimeSpan.FromTicks(d);
            IntMin = TimeSpan.FromTicks(min >= long.MaxValue ? 0 : getTicks(min));
            IntMax = TimeSpan.FromTicks(getTicks(max));
            IntLastDuration = TimeSpan.FromTicks(getTicks(lastDuration));
            IntLastExecution = new DateTime(last, DateTimeKind.Utc);

            //  Avoid a division by zero if enumerated in the same stopwatch tick as the creation / reset
            if (runningFor <= 0)
                runningFor = 1;
            Decimal fr = Stopwatch.Frequency;
            Decimal rate = count;
            rate *= fr;
            rate /= runningFor;
            Interlocked.Exchange(ref IntCountPerMinute, (float)rate);

            //  Both in stopwatch ticks
            Decimal timePer = 100M * total;
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
