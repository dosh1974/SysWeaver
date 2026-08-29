using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;

namespace SysWeaver
{

    /// <summary>
    /// Schedules a task to be performed at a specified time, precision is fairly low so the task can be executed a few seconds later than scheduled.
    /// Use this for low frequency tasks that should run on a schedule, once a day, once per hour and so on.
    /// For high frequency tasks use the PeriodicTask instead.
    /// </summary>
    public static class Scheduler
    {

        /// <summary>
        /// Roughly the number of milli seconds between each check
        /// </summary>
        public const int CheckFrequencyMs = 100;

        /// <summary>
        /// Schedule a task to be performed at a specified time, precision is fairly low so the task can be executed a few seconds later than scheduled.
        /// Use this for low frequency tasks that should run on a schedule, once a day, once per hour and so on.
        /// For high frequency tasks use the PeriodicTask instead.
        /// </summary>
        /// <param name="when">The UTC time to execute the task at, precision is fairly low so the task can be executed a few seconds later than scheduled.
        /// </param>
        /// <param name="task">The task to execute</param>
        /// <param name="name">An optional name (for debugging)</param>
        /// <param name="repeatFn">An optional function that is executed after the task completed to re-schedule the task, given the previous execution time, returns a new execution time.</param>
        /// <param name="runAsync">Run this task independent on other tasks</param>
        /// <returns>An object that can be disposed to prevent execution of the task in the future</returns>
        public static IDisposable Add(DateTime when, Action task, String name = null, Func<DateTime, DateTime> repeatFn = null, bool runAsync = true)
        {
            if (when.Kind != DateTimeKind.Utc)
                when = when.ToUniversalTime();
            var e = Entries;
            var ee = new Entry(when.Ticks, Interlocked.Increment(ref Id), task, repeatFn, name, runAsync);
            using var _ = Lock.LockSync();
            e.Add(ee, 0);
            if (CheckTask == null)
                CheckTask = new PeriodicTask(Check, CheckFrequencyMs);
            return ee;
        }

        /// <summary>
        /// Schedule a task to be performed at a specified time, precision is fairly low so the task can be executed a few seconds later than scheduled.
        /// Use this for low frequency tasks that should run on a schedule, once a day, once per hour and so on.
        /// For high frequency tasks use the PeriodicTask instead.
        /// </summary>
        /// <param name="when">The UTC time to execute the task at, precision is fairly low so the task can be executed a few seconds later than scheduled.
        /// </param>
        /// <param name="task">The task to execute</param>
        /// <param name="name">An optional name (for debugging)</param>
        /// <param name="repeatFn">An optional function that is executed after the task completed to re-schedule the task, given the previous execution time, returns a new execution time.</param>
        /// <param name="runAsync">Run this task independent on other tasks</param>
        /// <returns>An object that can be disposed to prevent execution of the task in the future</returns>
        public static IDisposable AddTask(DateTime when, Func<Task> task, String name = null, Func<DateTime, DateTime> repeatFn = null, bool runAsync = true)
        {
            if (when.Kind != DateTimeKind.Utc)
                when = when.ToUniversalTime();
            var e = Entries;
            var ee = new Entry(when.Ticks, Interlocked.Increment(ref Id), task, repeatFn, name, runAsync);
            using var _ = Lock.LockSync();
            e.Add(ee, 0);
            if (CheckTask == null)
                CheckTask = new PeriodicTask(Check, CheckFrequencyMs);
            return ee;
        }

        /// <summary>
        /// Schedule a task to be performed at a specified time, precision is fairly low so the task can be executed a few seconds later than scheduled.
        /// Use this for low frequency tasks that should run on a schedule, once a day, once per hour and so on.
        /// For high frequency tasks use the PeriodicTask instead.
        /// </summary>
        /// <param name="when">The UTC time to execute the task at, precision is fairly low so the task can be executed a few seconds later than scheduled.
        /// </param>
        /// <param name="task">The task to execute</param>
        /// <param name="name">An optional name (for debugging)</param>
        /// <param name="repeatFn">An optional function that is executed after the task completed to re-schedule the task, given the previous execution time, returns a new execution time.</param>
        /// <param name="runAsync">Run this task independent on other tasks</param>
        /// <returns>An object that can be disposed to prevent execution of the task in the future</returns>
        public static IDisposable AddValueTask(DateTime when, Func<ValueTask> task, String name = null, Func<DateTime, DateTime> repeatFn = null, bool runAsync = true)
        {
            if (when.Kind != DateTimeKind.Utc)
                when = when.ToUniversalTime();
            var e = Entries;
            var ee = new Entry(when.Ticks, Interlocked.Increment(ref Id), task, repeatFn, name, runAsync);
            using var _ = Lock.LockSync();
            e.Add(ee, 0);
            if (CheckTask == null)
                CheckTask = new PeriodicTask(Check, CheckFrequencyMs);
            return ee;
        }

        static long Id;
        static readonly SortedDictionary<Entry, int> Entries = new SortedDictionary<Entry, int>();

        /// <summary>
        /// Exception information
        /// </summary>
        public static readonly ExceptionTracker TaskExceptions = new();
        
        /// <summary>
        /// Scheduled entries
        /// </summary>
        public static List<Entry> AllScheduled
        {
            get
            {
                var ee = Entries;
                using var _ = Lock.LockSync();
                return [.. ee.Keys];
            }
        }

        public enum TaskTypes
        {
            /// <summary>
            /// A non-async action
            /// </summary>
            TypeAction,
            /// <summary>
            /// An async Task
            /// </summary>
            TypeTask,
            /// <summary>
            /// An async ValueTask
            /// </summary>
            TypeValueTask,
        };



        [TableDataPrimaryKey(nameof(TaskId), nameof(Name))]
        public sealed class Entry : IDisposable, IComparable<Entry>, IEquatable<Entry>
        {
#if DEBUG
            public override string ToString()
                => Name == null ? String.Concat(Type, " #", TaskId, " @ ", Scheduled, ": ", Scheduler) : String.Concat(Type, " #", TaskId, ' ', Name, " @ ", Scheduled, ": ", Scheduler);
#else//DEBUG
            public override string ToString() 
                => Name == null ? String.Concat(Type, " #", TaskId) : String.Concat(Type, " #", TaskId, ' ', Name);
#endif//DEBUG


            public bool RunAsync { get; init; }

            /// <summary>
            /// When the task will be executed
            /// </summary>
            public DateTime RunAt => new DateTime(Time, DateTimeKind.Utc);

            /// <summary>
            /// The internal unique Id of this task
            /// </summary>
            public long TaskId => Id;

            /// <summary>
            /// Optional name
            /// </summary>
            public String Name { get; init; }


            /// <summary>
            /// The type of task to perform
            /// </summary>
            public TaskTypes Type { get; init; }

            /// <summary>
            /// True if the function should be repeated
            /// </summary>
            public bool Repeat => RepeatFn != null;

            /// <summary>
            /// The repeat interval, zero if not repeating
            /// </summary>
            public TimeSpan RepeatFrequency
            {
                get
                {
                    var fn = RepeatFn;
                    if (fn == null)
                        return TimeSpan.Zero;
                    var n = RunAt;
                    return fn(n) - n;
                }
            }


            /// <summary>
            /// Number of times the task have completed (exception or not)
            /// </summary>
            public long Count => Interlocked.Read(ref InternalCount);

            /// <summary>
            /// True if the task is currently running
            /// </summary>
            public bool IsRunning => Interlocked.Read(ref Guard) != 0;

            /// <summary>
            /// The time when the task was last started
            /// </summary>
            public DateTime LastStart
            {
                get
                {
                    var t = Interlocked.Read(ref InternalLastStart);
                    if (t == 0)
                        return DateTime.MinValue;
                    return new DateTime(t, DateTimeKind.Utc);
                }
            }

            /// <summary>
            /// The time when the task was last started
            /// </summary>
            public DateTime LastEnd
            {
                get
                {
                    var t = Interlocked.Read(ref InternaLastEnd);
                    if (t == 0)
                        return DateTime.MinValue;
                    return new DateTime(t, DateTimeKind.Utc);
                }
            }

            /// <summary>
            /// The average execution duration of the task
            /// </summary>
            public TimeSpan AvgDuration
            {
                get
                {
                    var count = Interlocked.Read(ref InternalCount);
                    var dur = Interlocked.Read(ref InternaTotalDuration);
                    if (count == 0)
                        return TimeSpan.Zero;
                    return TimeSpan.FromTicks(dur / count);

                }
            }

            #region Error tracking


            /// <summary>
            /// Total number of exceptions
            /// </summary>
            public long ExceptionCount => Interlocked.Read(ref InternalExceptionCount);


            /// <summary>
            /// The time of the last failure
            /// </summary>
            public DateTime LastException
            {
                get
                {
                    var t = Interlocked.Read(ref InternaLastException);
                    if (t == 0)
                        return DateTime.MinValue;
                    return new DateTime(t, DateTimeKind.Utc);
                }
            }


            /// <summary>
            /// The last exception text
            /// </summary>
            [TableDataText(60)]
            public String LastExceptionEx { get; internal set; }


            #endregion//Error tracking


            #region Create

            /// <summary>
            /// When this task was scheduled
            /// </summary>
            public DateTime Scheduled { get; init; }


#if DEBUG

            /// <summary>
            /// Call stack to where this task was scheduled fromn
            /// </summary>
            [TableDataText(60)]
            public String Scheduler { get; init; }

#endif//DEBUG

            #endregion//Create




            public int CompareTo(Entry other)
            {
                var i = Time.CompareTo(other.Time);
                if (i != 0)
                    return i;
                return Id.CompareTo(other.Id);
            }

            public bool Equals(Entry other) => Id == other.Id;

            public override bool Equals(object obj)
            {
                var o = obj as Entry;
                if (o == null)
                    return false;
                return Id == o.Id;
            }

            public override int GetHashCode() => (int)Id;

            public void Dispose() => DoRemove(this);


            internal Entry(long time, long id, Action task, Func<DateTime, DateTime> repeatFn, String name, bool runAsync)
            {
                Time = time;
                Id = id;
                A = task;
                Type = TaskTypes.TypeAction;
                RepeatFn = repeatFn;
                Name = name;
                Scheduled = DateTime.UtcNow;
                RunAsync = runAsync;
#if DEBUG
                Scheduler = String.Concat(new StackTrace(2, true).GetFrames().Take(5));
#endif//DEBUG
            }

            internal Entry(long time, long id, Func<Task> task, Func<DateTime, DateTime> repeatFn, String name, bool runAsync)
            {
                Time = time;
                Id = id;
                TA = task;
                Type = TaskTypes.TypeTask;
                RepeatFn = repeatFn;
                Name = name;
                Scheduled = DateTime.UtcNow;
                RunAsync = runAsync;
#if DEBUG
                Scheduler = String.Concat(new StackTrace(2, true).GetFrames().Take(5));
#endif//DEBUG
            }

            internal Entry(long time, long id, Func<ValueTask> task, Func<DateTime, DateTime> repeatFn, String name, bool runAsync)
            {
                Time = time;
                Id = id;
                VTA = task;
                Type = TaskTypes.TypeValueTask;
                RepeatFn = repeatFn;
                Name = name;
                Scheduled = DateTime.UtcNow;
                RunAsync = runAsync;
#if DEBUG
                Scheduler = String.Concat(new StackTrace(2, true).GetFrames().Take(5));
#endif//DEBUG
            }


            internal readonly long Id;

            internal readonly Action A;

            internal readonly Func<Task> TA;

            internal readonly Func<ValueTask> VTA;

            internal readonly Func<DateTime, DateTime> RepeatFn;

            internal long Removed;

            internal long Guard;

            internal long Time;

            internal long InternalCount;
            internal long InternalLastStart;
            internal long InternaLastEnd;
            internal long InternalExceptionCount;
            internal long InternaLastException;
            internal long InternaTotalDuration;


        }


        static async Task ExecuteOne(Entry ee)
        {
            //  Execute the task
            var start = DateTime.UtcNow.Ticks;
            try
            {
                switch (ee.Type)
                {
                    case TaskTypes.TypeAction:
                        ee.A();
                        break;
                    case TaskTypes.TypeTask:
                        await ee.TA().ConfigureAwait(false);
                        break;
                    case TaskTypes.TypeValueTask:
                        await ee.VTA().ConfigureAwait(false);
                        break;
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref ee.InternalExceptionCount);
                Interlocked.Exchange(ref ee.InternaLastException, DateTime.UtcNow.Ticks);
                ee.LastExceptionEx = ex.ToString();
                TaskExceptions.OnException(new Exception(ee.ToString() + " failed with an exception", ex));
            }
            var end = DateTime.UtcNow.Ticks;
            var duration = end - start;
            Interlocked.Exchange(ref ee.InternalLastStart, start);
            Interlocked.Exchange(ref ee.InternaLastEnd, end);
            Interlocked.Add(ref ee.InternaTotalDuration, duration);
            Interlocked.Increment(ref ee.InternalCount);
            //  Remove the task (or re-schedule it)
            var e = Entries;
            using var _ = await Lock.Lock().ConfigureAwait(false);
            e.Remove(ee);
            if (Interlocked.Read(ref ee.Removed) == 0)
            {
            //  If it's not removed, check if we need to re-schedule
                var fn = ee.RepeatFn;
                if (fn != null)
                {
                    try
                    {
                        var n = GetNext(ee.RunAt, fn, DateTime.UtcNow.AddMilliseconds(100));
                        ee.Time = n.Ticks;
                        e.Add(ee, 0);
                        if (CheckTask == null)
                            CheckTask = new PeriodicTask(Check, CheckFrequencyMs);
                    }
                    catch (Exception ex)
                    {
                        TaskExceptions.OnException(new Exception(ee.ToString() + " failed to re-schedule", ex));
                    }
                }
            }
            Interlocked.Exchange(ref ee.Guard, 0);
        }

        static async Task Execute(List<Entry> e)
        {
            foreach (var ee in e)
                await ExecuteOne(ee).ConfigureAwait(false);
        }

        static readonly AsyncLock Lock = new AsyncLock();

        static async Task<bool> Check()
        {
            var now = DateTime.UtcNow.Ticks;
            var e = Entries;
            List<Entry> nonAsync = null;
            IDisposable d = null;
            using (var _ = await Lock.Lock().ConfigureAwait(false))
            {
                foreach (var ee in e.Keys)
                {
                    if (now < ee.Time)
                        break;
                    if (Interlocked.CompareExchange(ref ee.Guard, 1, 0) != 0)
                        continue;
                    if (ee.RunAsync)
                    {
                        TaskExt.StartNewAsyncChain(() => ExecuteOne(ee));
                        continue;
                    }
                    nonAsync = nonAsync ?? new List<Entry>();
                    nonAsync.Add(ee);
                }
                if (e.Count <= 0)
                    d = Interlocked.Exchange(ref CheckTask, null);
                if (nonAsync != null)
                    TaskExt.StartNewAsyncChain(() => Execute(nonAsync));
            }
            if (d != null)
                return false;
            return true;
        }

        static PeriodicTask CheckTask;
            
        static bool DoRemove(Entry ee)
        {
            var e = Entries;
            using (var _ = Lock.LockSync())
            {
                if (Interlocked.CompareExchange(ref ee.Removed, 1, 0) != 0)
                    return false;
                e.Remove(ee);
            }
            for (; ;)
            {
                if (Interlocked.Read(ref ee.Guard) == 0)
                    return true;
                Thread.Sleep(1);
            }
        }

        
        static DateTime GetNext(DateTime c, Func<DateTime, DateTime> fn, DateTime now)
        {
            for (; ; )
            {
                var n = fn(c);
                if (n <= c)
                    throw new Exception("Invalid repeat function!");
                if (n > now)
                    return n;
                c = n;
            }
        }




    }

}
