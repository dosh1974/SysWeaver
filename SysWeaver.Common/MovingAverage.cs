using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// Computes the moving average (and rates) of some value within a sliding time window.
    /// - Thread safe.
    /// - Accurate: the rates are computed over the full window (or the time since this instance was created if that is less than the window),
    ///   so the result is correct even if there are only one or two samples in the window, or if the samples stopped coming in a while ago.
    /// - Bounded memory: samples that are closer in time than 1/1024 of the window are merged (so the window edge has a precision of 1/1024 of the window).
    /// - Allocation free (except when the internal buffer has to grow, at most log2(2048 / InitSize) times).
    /// </summary>
    [SkipLocalsInit]
    public sealed class MovingAverage
    {
        /// <summary>
        /// Create a new moving average tracker
        /// </summary>
        /// <param name="averageOverDuration">The duration of the window to compute the moving average over</param>
        public MovingAverage(TimeSpan averageOverDuration) : this(averageOverDuration, null)
        {
        }

        /// <summary>
        /// Create a new moving average tracker
        /// </summary>
        /// <param name="averageOverDuration">The duration of the window to compute the moving average over</param>
        /// <param name="timeProvider">The time provider to use, null = the system time provider</param>
        public MovingAverage(TimeSpan averageOverDuration, TimeProvider timeProvider)
        {
            if (averageOverDuration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(averageOverDuration), averageOverDuration, "The duration must be positive");
            Duration = averageOverDuration;
            long freq;
            long start;
            if ((timeProvider == null) || ReferenceEquals(timeProvider, TimeProvider.System))
            {
                //  Use the stop watch directly (no virtual call)
                freq = Stopwatch.Frequency;
                start = Stopwatch.GetTimestamp();
            }
            else
            {
                Time = timeProvider;
                freq = timeProvider.TimestampFrequency;
                start = timeProvider.GetTimestamp();
            }
            Start = start;
            var ticks = averageOverDuration.Ticks;
            var window = freq == TimeSpan.TicksPerSecond ? ticks : (long)((decimal)ticks * freq / TimeSpan.TicksPerSecond);
            if (window < 1)
                window = 1;
            Window = window;
            var res = window / Resolution;
            Merge = res < 1 ? 1 : res;
            //  Converting time stamps to ticks
            if ((TimeSpan.TicksPerSecond % freq) == 0)
                TicksScale = TimeSpan.TicksPerSecond / freq;
            else if ((freq % TimeSpan.TicksPerSecond) == 0)
                TicksScale = -(freq / TimeSpan.TicksPerSecond);
            Entries = new Entry[InitSize];
        }

        /// <summary>
        /// The duration of the window that the moving average is computed over
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Number of times the underlaying data structures have been resized
        /// </summary>
        public int ResizeCount => Volatile.Read(ref InternalResizeCount);

        /// <summary>
        /// Add a value to the moving average
        /// </summary>
        /// <param name="value">The value to add</param>
        public void Add(decimal value)
        {
            var now = Now();
            Enter();
            try
            {
                Expire(now);
                var e = Entries;
                var mask = e.Length - 1;
                var count = Count;
                var head = Head;
                if (count > 0)
                {
                    ref var last = ref e[(head + count - 1) & mask];
                    if ((now - last.Time) < Merge)
                    {
                        //  Merge with the last entry (the last entry isn't part of the closed sum)
                        last.Sum += value;
                        ++last.Count;
                        ++Samples;
                        return;
                    }
                    //  Close the last entry
                    var closed = ClosedSum + last.Sum;
                    if (count == e.Length)
                    {
                        e = Grow(e, head, count);
                        mask = e.Length - 1;
                        head = 0;
                    }
                    ClosedSum = closed;
                }
                ref var n = ref e[(head + count) & mask];
                n.Time = now;
                n.Sum = value;
                n.Count = 1;
                Count = count + 1;
                ++Samples;
            }
            finally
            {
                Exit();
            }
        }

        /// <summary>
        /// Get the moving average
        /// </summary>
        /// <param name="sum">Total sum of the values in the time window</param>
        /// <param name="count">Number of values within the time window</param>
        /// <param name="dt">The duration (in ticks) that the window covers (the window duration, or the time since this instance was created if that is less)</param>
        /// <returns>The average value (sum / count)</returns>
        public decimal GetAverage(out decimal sum, out int count, out long dt)
        {
            GetData(out sum, out count, out dt);
            return count <= 0 ? 0 : (sum / count);
        }

        /// <summary>
        /// Get the sum per second
        /// </summary>
        /// <param name="sum">Total sum of the values in the time window</param>
        /// <param name="count">Number of values within the time window</param>
        /// <param name="dt">The duration (in ticks) that the window covers (the window duration, or the time since this instance was created if that is less)</param>
        /// <returns>The sum of the values per second (sum / dt)</returns>
        public decimal GetVolume(out decimal sum, out int count, out long dt)
        {
            GetData(out sum, out count, out dt);
            return dt <= 0 ? 0 : (sum * TimeSpan.TicksPerSecond / dt);
        }

        /// <summary>
        /// Get the number of values per second
        /// </summary>
        /// <param name="sum">Total sum of the values in the time window</param>
        /// <param name="count">Number of values within the time window</param>
        /// <param name="dt">The duration (in ticks) that the window covers (the window duration, or the time since this instance was created if that is less)</param>
        /// <returns>The number of values per second (count / dt)</returns>
        public decimal GetRate(out decimal sum, out int count, out long dt)
        {
            GetData(out sum, out count, out dt);
            return dt <= 0 ? 0 : ((decimal)count * TimeSpan.TicksPerSecond / dt);
        }

        /// <summary>
        /// Get data
        /// </summary>
        /// <param name="sum">Total sum of the values in the time window</param>
        /// <param name="count">Number of values within the time window</param>
        /// <param name="dt">The duration (in ticks) that the window covers (the window duration, or the time since this instance was created if that is less)</param>
        public void GetData(out decimal sum, out int count, out long dt)
        {
            var now = Now();
            decimal closed, open;
            Enter();
            try
            {
                Expire(now);
                var c = Count;
                if (c > 0)
                {
                    var e = Entries;
                    closed = ClosedSum;
                    open = e[(Head + c - 1) & (e.Length - 1)].Sum;
                    count = Samples;
                }
                else
                {
                    closed = 0;
                    open = 0;
                    count = 0;
                }
            }
            finally
            {
                Exit();
            }
            sum = closed + open;
            var dts = now - Start;
            if (dts > Window)
                dts = Window;
            if (dts < 0)
                dts = 0;
            dt = ToTicks(dts);
        }

        /// <summary>
        /// Get stats
        /// </summary>
        /// <param name="system">Name of the system</param>
        /// <param name="prefix">Optional prefix for this moving average</param>
        /// <param name="countName">Name of the values per second stats, null = use default, set to "" to exclude count stats</param>
        /// <param name="avgName">Name of the average value stats, null = use default, set to "" to exclude average value stats</param>
        /// <param name="volumeName">Name of the volume stats, null = use default, set to "" to exclude volume stats</param>
        /// <param name="countDesc">Description of the values per second stats, null = use default, set to "" to exclude count stats</param>
        /// <param name="valueDesc">Description of the average value stats, null = use default, set to "" to exclude average value stats</param>
        /// <param name="volumeDesc">Description of the volume stats, null = use default, set to "" to exclude volume stats</param>
        /// <returns>The stats</returns>
        public IEnumerable<Stats> GetStats(String system, String prefix = null, String countName = null, String avgName = null, String volumeName = null, String countDesc = null, String valueDesc = null, String volumeDesc = null)
        {
            //  The texts are usually the same for every call (from the same call site), so reuse them
            var t = Texts;
            if ((t == null) || !t.IsSame(prefix, countName, avgName, volumeName, countDesc, valueDesc, volumeDesc))
                Texts = t = new StatsTexts(Duration, prefix, countName, avgName, volumeName, countDesc, valueDesc, volumeDesc);
            GetData(out var sum, out var count, out var dt);
            var c = t.CountName == null ? null : new Stats(system, t.CountName, Box(dt <= 0 ? 0 : ((decimal)count * TimeSpan.TicksPerSecond / dt)), t.CountDesc, DecimalTypeName);
            var a = t.AvgName == null ? null : new Stats(system, t.AvgName, Box(count <= 0 ? 0 : (sum / count)), t.AvgDesc, DecimalTypeName);
            var v = t.VolumeName == null ? null : new Stats(system, t.VolumeName, Box(dt <= 0 ? 0 : (sum * TimeSpan.TicksPerSecond / dt)), t.VolumeDesc, DecimalTypeName);
            return new StatsList(c, a, v);
        }

        /// <summary>
        /// Enumerates up to three stats (null's are skipped).
        /// Smaller than a compiler generated iterator, it's its own enumerator the first time it's enumerated
        /// </summary>
        sealed class StatsList : IEnumerable<Stats>, IEnumerator<Stats>
        {
            public StatsList(Stats s0, Stats s1, Stats s2)
            {
                S0 = s0;
                S1 = s1;
                S2 = s2;
            }

            readonly Stats S0;
            readonly Stats S1;
            readonly Stats S2;
            Stats C;
            /// <summary>
            /// -1 = not enumerated yet, 0-3 = next stats to check
            /// </summary>
            int State = -1;

            public IEnumerator<Stats> GetEnumerator()
            {
                if (Interlocked.CompareExchange(ref State, 0, -1) == -1)
                    return this;
                var n = new StatsList(S0, S1, S2);
                n.State = 0;
                return n;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

            public Stats Current => C;

            Object System.Collections.IEnumerator.Current => C;

            public bool MoveNext()
            {
                for (; ; )
                {
                    Stats s;
                    switch (State)
                    {
                        case 0:
                            s = S0;
                            break;
                        case 1:
                            s = S1;
                            break;
                        case 2:
                            s = S2;
                            break;
                        default:
                            C = null;
                            return false;
                    }
                    ++State;
                    if (s != null)
                    {
                        C = s;
                        return true;
                    }
                }
            }

            public void Reset()
            {
                State = 0;
                C = null;
            }

            public void Dispose()
            {
            }
        }

        /// <summary>
        /// Box a decimal (zero is common, so use a shared instance for that)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Object Box(decimal value) => value == 0 ? BoxedZero : value;

        static readonly Object BoxedZero = 0m;

        static readonly String DecimalTypeName = typeof(decimal).CleanTypename();

        /// <summary>
        /// The names and descriptions of the stats (immutable)
        /// </summary>
        sealed class StatsTexts
        {
            public StatsTexts(TimeSpan duration, String prefix, String countName, String avgName, String volumeName, String countDesc, String valueDesc, String volumeDesc)
            {
                InPrefix = prefix;
                InCountName = countName;
                InAvgName = avgName;
                InVolumeName = volumeName;
                InCountDesc = countDesc;
                InValueDesc = valueDesc;
                InVolumeDesc = volumeDesc;
                prefix = prefix ?? "";
                var over = " over the last " + duration.ElapsedTime();
                if ((countName != "") && (countDesc != ""))
                {
                    CountName = prefix + (countName ?? "Values");
                    CountDesc = ((countDesc ?? "per second") + over).TrimStart().MakeFirstUppercase();
                }
                if ((avgName != "") && (valueDesc != ""))
                {
                    AvgName = prefix + (avgName ?? "Average");
                    AvgDesc = ((valueDesc ?? "") + over).TrimStart().MakeFirstUppercase();
                }
                if ((volumeName != "") && (volumeDesc != ""))
                {
                    VolumeName = prefix + (volumeName ?? "Volume");
                    VolumeDesc = ((volumeDesc ?? "per second") + over).TrimStart().MakeFirstUppercase();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsSame(String prefix, String countName, String avgName, String volumeName, String countDesc, String valueDesc, String volumeDesc)
                => ReferenceEquals(prefix, InPrefix)
                && ReferenceEquals(countName, InCountName)
                && ReferenceEquals(avgName, InAvgName)
                && ReferenceEquals(volumeName, InVolumeName)
                && ReferenceEquals(countDesc, InCountDesc)
                && ReferenceEquals(valueDesc, InValueDesc)
                && ReferenceEquals(volumeDesc, InVolumeDesc);

            readonly String InPrefix;
            readonly String InCountName;
            readonly String InAvgName;
            readonly String InVolumeName;
            readonly String InCountDesc;
            readonly String InValueDesc;
            readonly String InVolumeDesc;

            /// <summary>
            /// Null if the stats should be excluded
            /// </summary>
            public readonly String CountName;
            public readonly String CountDesc;
            /// <summary>
            /// Null if the stats should be excluded
            /// </summary>
            public readonly String AvgName;
            public readonly String AvgDesc;
            /// <summary>
            /// Null if the stats should be excluded
            /// </summary>
            public readonly String VolumeName;
            public readonly String VolumeDesc;
        }

        /// <summary>
        /// Get the current time stamp
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        long Now()
        {
            var t = Time;
            return t == null ? Stopwatch.GetTimestamp() : t.GetTimestamp();
        }

        /// <summary>
        /// Convert a duration in time stamp units to ticks
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        long ToTicks(long stamps)
        {
            var s = TicksScale;
            if (s > 0)
                return stamps * s;
            if (s < 0)
                return stamps / -s;
            return ToTicksSlow(stamps);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        long ToTicksSlow(long stamps)
            => (long)((decimal)stamps * TimeSpan.TicksPerSecond / (Time?.TimestampFrequency ?? Stopwatch.Frequency));

        /// <summary>
        /// Acquire the (spin) lock
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Enter()
        {
            if (Interlocked.CompareExchange(ref LockFlag, 1, 0) != 0)
                EnterSlow();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void EnterSlow()
        {
            var sw = new SpinWait();
            do
            {
                //  Never Sleep(1), the lock is only held for a few ns
                sw.SpinOnce(-1);
            }
            while ((Volatile.Read(ref LockFlag) != 0) || (Interlocked.CompareExchange(ref LockFlag, 1, 0) != 0));
        }

        /// <summary>
        /// Release the (spin) lock
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Exit() => Volatile.Write(ref LockFlag, 0);

        /// <summary>
        /// Remove all entries that are older than the window (must be called with the lock held)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Expire(long now)
        {
            if ((Count > 0) && (Entries[Head].Time < (now - Window)))
                ExpireSlow(now - Window);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void ExpireSlow(long expire)
        {
            var e = Entries;
            var mask = e.Length - 1;
            var head = Head;
            var count = Count;
            //  Entry times are strictly increasing, the last entry isn't part of the closed sum
            var closed = ClosedSum;
            var samples = Samples;
            while (count > 1)
            {
                ref var h = ref e[head];
                if (h.Time >= expire)
                    break;
                closed -= h.Sum;
                samples -= h.Count;
                head = (head + 1) & mask;
                --count;
            }
            if ((count == 1) && (e[head].Time < expire))
            {
                //  Everything expired
                Head = 0;
                Count = 0;
                ClosedSum = 0;
                Samples = 0;
                return;
            }
            Head = head;
            Count = count;
            ClosedSum = closed;
            Samples = samples;
        }

        /// <summary>
        /// Double the size of the buffer (must be called with the lock held), the head is moved to index 0
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        Entry[] Grow(Entry[] e, int head, int count)
        {
            Interlocked.Increment(ref InternalResizeCount);
            Interlocked.Increment(ref InternalResizeTotalCount);
            var size = e.Length;
            var n = GC.AllocateUninitializedArray<Entry>(size + size);
            var src = e.AsSpan();
            var first = size - head;
            if (first > count)
                first = count;
            src.Slice(head, first).CopyTo(n);
            src.Slice(0, count - first).CopyTo(n.AsSpan(first));
            Entries = n;
            Head = 0;
            return n;
        }

        /// <summary>
        /// Samples closer in time than the window duration divided by this value are merged into a single entry
        /// </summary>
        public const int Resolution = 1024;

        /// <summary>
        /// The initial number of entries
        /// </summary>
        public const int InitSize = 4;

        struct Entry
        {
            /// <summary>
            /// Time stamp of the first sample in this entry
            /// </summary>
            public long Time;
            /// <summary>
            /// Sum of the samples in this entry
            /// </summary>
            public decimal Sum;
            /// <summary>
            /// Number of samples in this entry
            /// </summary>
            public int Count;
        }

        /// <summary>
        /// Null = use the stop watch
        /// </summary>
        readonly TimeProvider Time;
        readonly long Window;
        readonly long Merge;
        readonly long Start;
        /// <summary>
        /// Positive: multiply time stamps with this to get ticks, negative: divide time stamps with -this to get ticks, zero: use decimal math
        /// </summary>
        readonly long TicksScale;

        /// <summary>
        /// Ring buffer of entries (power of 2 size)
        /// </summary>
        Entry[] Entries;
        /// <summary>
        /// Sum of all entries except the last one
        /// </summary>
        decimal ClosedSum;
        /// <summary>
        /// Index of the oldest entry
        /// </summary>
        int Head;
        /// <summary>
        /// Number of entries
        /// </summary>
        int Count;
        /// <summary>
        /// Number of samples in all entries
        /// </summary>
        int Samples;
        int LockFlag;
        int InternalResizeCount;
        StatsTexts Texts;

        /// <summary>
        /// Get global stats
        /// </summary>
        /// <returns>The stats</returns>
        public static IEnumerable<Stats> GetGlobalStats()
        {
            var system = nameof(MovingAverage);
            yield return new Stats(system, "Resize count", ResizeTotalCount, "Number of times the underlaying data structures have been resized");
        }

        /// <summary>
        /// Number of times the underlaying data structures have been resized (all instances)
        /// </summary>
        public static long ResizeTotalCount => Interlocked.Read(ref InternalResizeTotalCount);

        static long InternalResizeTotalCount;
    }
}
