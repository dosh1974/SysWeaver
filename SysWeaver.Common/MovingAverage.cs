using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// Computes the moving average of some value within a time window.
    /// A thread safe, lock free (mostly) and allocation free (mostly).
    /// Locks and allocated when internal buffers have to be resized.
    /// </summary>
    public sealed class MovingAverage
    {
        /// <summary>
        /// Create a new moving average tracker
        /// </summary>
        /// <param name="averageOverDuration">The duration of the window to computer the moving average over</param>
        public MovingAverage(TimeSpan averageOverDuration)
        {
            InternalDur = averageOverDuration.Ticks;
        }
        
        /// <summary>
        /// Number of times the underlaying data structures have to be resized (this is slow)
        /// </summary>
        public int ResizeCount => InternalResizeCount;

        /// <summary>
        /// Add a value to the moving average
        /// </summary>
        /// <param name="value"></param>
        public void Add(decimal value)
             => InternalAdd(out var now, value);

        /// <summary>
        /// Add a value to the moving average and get the average
        /// </summary>
        /// <param name="count">Number of values within the time window</param>
        /// <param name="addPerSecond">Number of adds per second</param>
        /// <param name="value">The value to add to the moving average</param>
        /// <param name="dt">Number of ticks between the last and first values within the time window</param>
        /// <returns>The moving average of the added values</returns>
        public Decimal Add(out int count, out decimal addPerSecond, decimal value, out long dt)
        {
            var n = InternalAdd(out var now, value);
            return Compute(n, now, out count, out addPerSecond, out dt);
        }

        /// <summary>
        /// Get the moving average
        /// </summary>
        /// <param name="count">Number of values within the time window</param>
        /// <param name="addPerSecond">Number of adds per second</param>
        /// <param name="dt">Number of ticks between the last and first values within the time window</param>
        /// <returns>The moving average of the added values</returns>
        public Decimal Get(out int count, out decimal addPerSecond, out long dt)
        {
            var dur = InternalDur;
            var sw = new SpinWait();
            State n;
            long now;
            for (; ; )
            {
                var current = P;
                now = DateTime.UtcNow.Ticks;
                n = current.MoveHeader(now - dur);
                if (n == current)
                    break;
                if (Interlocked.CompareExchange(ref P, n, current) == current)
                    break;
                FreeState(n);
                sw.SpinOnce();
            }
            return Compute(n, now, out count, out addPerSecond, out dt);
        }

        /// <summary>
        /// Get stats
        /// </summary>
        /// <param name="system">Name of the system</param>
        /// <param name="prefix">Optional prefix for this moving average</param>
        /// <param name="countName">Name of the values per second stats, null = use default, set to "" to exclude count stats</param>
        /// <param name="valueName">Name of the average value stats, null = use default, set to "" to exclude average value stats</param>
        /// <param name="countDesc">Description of the values per second stats, null = use default, set to "" to exclude count stats</param>
        /// <param name="valueDesc">Description of the average value stats, null = use default, set to "" to exclude average value stats</param>
        /// <returns>The stats</returns>
        public IEnumerable<Stats> GetStats(String system, String prefix = null, String countName = null, String valueName = null, String countDesc = null, String valueDesc = null)
        {
            prefix = prefix ?? "";
            var val = Get(out var count, out var addsPerSecond, out var dt);
            var dur = TimeSpan.FromTicks(InternalDur).ElapsedTime();
            if ((countName != "") || (countDesc != ""))
                yield return new Stats(system, prefix + (countName ?? "Values per second"), addsPerSecond, ((countDesc ?? "") + " over the last " + dur).TrimStart().MakeFirstUppercase());
            if ((valueName != "") || (valueDesc != ""))
                yield return new Stats(system, prefix + (valueName ?? "Average value"), val, ((valueDesc ?? "") + " over the last " + dur).TrimStart().MakeFirstUppercase());
        }

        State InternalAdd(out long now, decimal value)
        {
            var dur = InternalDur;
            State n;
            var sw = new SpinWait();
            for (; ; )
            {
                now = DateTime.UtcNow.Ticks;
                var current = P;
                n = current.Add(out var tail, value, now - dur);
                if (n == null)
                {
                    //  Resize
                    lock (Lock)
                    {
                        now = DateTime.UtcNow.Ticks;
                        current = P;
                        n = current.Add(out tail, value, now - dur);
                        if (n == null)
                        {
                            Interlocked.Increment(ref InternalResizeCount);
                            n = Resize(current, value, now);
                            n.MoveHeader(now - dur);
                            if (Interlocked.CompareExchange(ref P, n, current) == current)
                            {
                                FreeState(current);
                                return n;
                            }
                            // Should never happen
                            FreeState(n);
                            sw.SpinOnce();
                            continue;
                        }
                    }
                }
                if (Interlocked.CompareExchange(ref P, n, current) == current)
                {
                    n.Values[tail] = value;
                    n.Times[tail] = now;
                    FreeState(current);
                    return n;
                }
                FreeState(n);
                sw.SpinOnce();
            }
        }

        Decimal Compute(State n, long now, out int count, out decimal addPerSecond, out long dt)
        {
            var times = n.Times;
            var h = n.Head;
            var t = n.Tail;
            if (t < h)
                t += n.Size;
            count = t - h;
            if (count <= 0)
            {
                dt = 0;
                addPerSecond = 0;
                return 0;
            }
            dt = now - times[h];
            addPerSecond = (Decimal)(count - 1) * TimeSpan.TicksPerSecond / dt;
            return n.Sum / count;
        }

        static State Resize(State s, decimal value, long now)
        {
            Interlocked.Increment(ref InternalResizeTotalCount);
            var size = s.Size;
            var mask = size - 1;
            size += size;
            var srcTimes = s.Times;
            var newTimes = GC.AllocateUninitializedArray<long>(size);
            var srcValues = s.Values;
            var newValues = GC.AllocateUninitializedArray<Decimal>(size);
            var head = s.Head;
            var tail = s.Tail;
            int dest = 0;
            while (head != tail)
            {
                newTimes[dest] = srcTimes[head];
                newValues[dest] = srcValues[head];
                ++head;
                ++dest;
                head &= mask;
            }
            newTimes[dest] = now;
            newValues[dest] = value;
            ++dest;
            return AllocState(0, dest, s.Sum + value, size, newTimes, newValues);
        }

        readonly Object Lock = new object();
        readonly long InternalDur;
        volatile int InternalResizeCount;
        volatile State P = AllocState(0, 0, 0, InitSize, GC.AllocateUninitializedArray<long>(InitSize), GC.AllocateUninitializedArray<Decimal>(InitSize));

        ~MovingAverage()
        {
            var t = Interlocked.Exchange(ref P, null);
            if (t != null)
                FreeState(t);
        }

        sealed class State
        {
            public State NextFree;

            public int Head;
            public int Tail;
            public Decimal Sum;
            public int Size;
            public long[] Times;
            public decimal[] Values;

            public State Add(out int tail, decimal value, long expire)
            {
                tail = Tail;
                var head = Head;
                var sum = Sum;
                var times = Times;
                var values = Values;
                var size = Size;
                var mask = size - 1;
                //  Insert new value 
                var next = (tail + 1) & mask;
                if (next == head)
                    return null; // Need to resize
                sum += value;
                //  Move head (remove old values)
                while (head != tail)
                {
                    if (times[head] >= expire)
                        break;
                    sum -= values[head];
                    ++head;
                    head &= mask;
                }
                return AllocState(head, next, sum, size, times, values);
            }

            public State MoveHeader(long expire)
            {
                var tail = Tail;
                var head = Head;
                var orgHead = head;
                var sum = Sum;
                var times = Times;
                var values = Values;
                var size = Size;
                var mask = size - 1;
                //  Move head (remove old values)
                while (head != tail)
                {
                    if (times[head] >= expire)
                        break;
                    sum -= values[head];
                    ++head;
                    head &= mask;
                }
                if (head == orgHead)
                    return this;
                return AllocState(head, tail, sum, size, times, values);
            }

            public State(int head, int tail, decimal sum, int size, long[] times, decimal[] values)
            {
                Head = head;
                Tail = tail;
                Sum = sum;
                Size = size;
                Times = times;
                Values = values;
            }
        }

        public const int InitSize = 4096;



        #region Allocation pool


        /// <summary>
        /// Get global stats
        /// </summary>
        /// <returns>The stats</returns>
        public static IEnumerable<Stats> GetGlobalStats()
        {
            var system = nameof(MovingAverage);
            yield return new Stats(system, "Free count", FreeCount, "Number of state nodes ready to be used (pre-allocated)");
            yield return new Stats(system, "Alloc count", AllocCount, "Total number of state node allocations");
            yield return new Stats(system, "In use count", InUseCount, "Number of states nodes in use");
            yield return new Stats(system, "Resize count", ResizeTotalCount, "Number of times the underlaying data structures have to be resized (this is slow)");
        }

        static volatile State Free;

        /// <summary>
        /// Number of state nodes ready to be used (pre-allocated)
        /// </summary>
        public static long FreeCount => Interlocked.Read(ref InternalFreeCount);
        
        /// <summary>
        /// Total number of state node allocations
        /// </summary>
        public static long AllocCount => Interlocked.Read(ref InternalAllocCount);

        /// <summary>
        /// Number of states nodes in use
        /// </summary>
        public static long InUseCount => Interlocked.Read(ref InternalInUseCount);

        /// <summary>
        /// Number of times the underlaying data structures have to be resized (this is slow)
        /// </summary>
        public static long ResizeTotalCount => Interlocked.Read(ref InternalResizeTotalCount);

        static long InternalFreeCount;
        static long InternalAllocCount;
        static long InternalInUseCount;
        static long InternalResizeTotalCount;

        static State AllocState(int head, int tail, decimal sum, int size, long[] times, decimal[] values)
        {
            State free;
            for (; ; )
            {
                free = Free;
                if (free == null)
                {
                    Interlocked.Increment(ref InternalAllocCount);
                    Interlocked.Increment(ref InternalInUseCount);
                    return new State(head, tail, sum, size, times, values);
                }
                var next = free.NextFree;
                if (Interlocked.CompareExchange(ref Free, next, free) == free)
                    break;
            }
            Interlocked.Decrement(ref InternalFreeCount);
            free.Head = head;
            free.Tail = tail;
            free.Sum = sum;
            free.Size = size;
            free.Times = times;
            free.Values = values;
            free.NextFree = null;
            Interlocked.Increment(ref InternalInUseCount);
            return free;
        }

        static void FreeState(State state)
        {
            Interlocked.Decrement(ref InternalInUseCount);
            var fc = Interlocked.Increment(ref InternalFreeCount);
            if (fc > 64)
            {
            //  Drop state if we have enough nodes
                Interlocked.Decrement(ref InternalFreeCount);
                return;
            }
            for (; ; )
            {
                var free = Free;
                state.NextFree = free;
                if (Interlocked.CompareExchange(ref Free, state, free) == free)
                    break;
            }
        }

        #endregion//Allocation pool



    }


}