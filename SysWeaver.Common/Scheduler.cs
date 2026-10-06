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
    /// <remarks>
    /// A single process wide <see cref="PeriodicTask"/> checks the schedule every <see cref="CheckFrequencyMs"/> ms while there are scheduled entries (it's stopped when the schedule is empty).
    /// An entry never runs concurrently with itself. Exceptions thrown by tasks are recorded on the entry and in <see cref="TaskExceptions"/>.
    /// Disposing an entry waits for a running execution to complete, except when called from within the entry's own task (then it returns immediately and the entry is not re-scheduled).
    /// </remarks>
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
        /// <param name="when">The time to execute the task at (a non UTC time, including <see cref="DateTimeKind.Unspecified"/>, is treated as local time and converted to UTC), precision is fairly low so the task can be executed a few seconds later than scheduled.
        /// A time in the past executes the task at the next check.
        /// </param>
        /// <param name="task">The task to execute (on the thread pool)</param>
        /// <param name="name">An optional name (for debugging)</param>
        /// <param name="repeatFn">An optional function that is executed after the task completed to re-schedule the task, given the previous scheduled execution time (UTC), returns the next execution time (that must be later).
        /// It's called repeatedly until a time in the future is returned (so missed executions are skipped). If it returns a time that isn't later than it's input, the entry is not re-scheduled and the error is recorded in <see cref="TaskExceptions"/>.</param>
        /// <param name="runAsync">If true the task runs in it's own async chain (independent of other tasks), else all non async tasks that are due at the same check are executed one after another in a shared async chain</param>
        /// <returns>The scheduled <see cref="Entry"/>, dispose it to prevent execution of the task in the future (disposing blocks until a currently running execution has completed, unless called from within the task itself)</returns>
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
        /// <param name="when">The time to execute the task at (a non UTC time, including <see cref="DateTimeKind.Unspecified"/>, is treated as local time and converted to UTC), precision is fairly low so the task can be executed a few seconds later than scheduled.
        /// A time in the past executes the task at the next check.
        /// </param>
        /// <param name="task">The task to execute (on the thread pool)</param>
        /// <param name="name">An optional name (for debugging)</param>
        /// <param name="repeatFn">An optional function that is executed after the task completed to re-schedule the task, given the previous scheduled execution time (UTC), returns the next execution time (that must be later).
        /// It's called repeatedly until a time in the future is returned (so missed executions are skipped). If it returns a time that isn't later than it's input, the entry is not re-scheduled and the error is recorded in <see cref="TaskExceptions"/>.</param>
        /// <param name="runAsync">If true the task runs in it's own async chain (independent of other tasks), else all non async tasks that are due at the same check are executed one after another in a shared async chain</param>
        /// <returns>The scheduled <see cref="Entry"/>, dispose it to prevent execution of the task in the future (disposing blocks until a currently running execution has completed, unless called from within the task itself)</returns>
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
        /// <param name="when">The time to execute the task at (a non UTC time, including <see cref="DateTimeKind.Unspecified"/>, is treated as local time and converted to UTC), precision is fairly low so the task can be executed a few seconds later than scheduled.
        /// A time in the past executes the task at the next check.
        /// </param>
        /// <param name="task">The task to execute (on the thread pool)</param>
        /// <param name="name">An optional name (for debugging)</param>
        /// <param name="repeatFn">An optional function that is executed after the task completed to re-schedule the task, given the previous scheduled execution time (UTC), returns the next execution time (that must be later).
        /// It's called repeatedly until a time in the future is returned (so missed executions are skipped). If it returns a time that isn't later than it's input, the entry is not re-scheduled and the error is recorded in <see cref="TaskExceptions"/>.</param>
        /// <param name="runAsync">If true the task runs in it's own async chain (independent of other tasks), else all non async tasks that are due at the same check are executed one after another in a shared async chain</param>
        /// <returns>The scheduled <see cref="Entry"/>, dispose it to prevent execution of the task in the future (disposing blocks until a currently running execution has completed, unless called from within the task itself)</returns>
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
        /// Tracks exceptions thrown by scheduled tasks and by failed re-scheduling (process wide)
        /// </summary>
        public static readonly ExceptionTracker TaskExceptions = new();

        /// <summary>
        /// A snapshot of all scheduled entries (including entries that are currently executing), ordered by execution time
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

        /// <summary>
        /// The kind of function that a scheduled <see cref="Entry"/> executes
        /// </summary>
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



        /// <summary>
        /// A scheduled task, returned by the Add methods of <see cref="Scheduler"/>.
        /// Exposes execution statistics (suitable for display in a table), dispose it to remove the task from the schedule.
        /// </summary>
        /// <remarks>
        /// Entries are ordered by execution time and then by id, equality and hash code are based on the unique id only.
        /// </remarks>
        [TableDataPrimaryKey(nameof(TaskId), nameof(Name))]
        public sealed class Entry : IDisposable, IComparable<Entry>, IEquatable<Entry>
        {
#if DEBUG
            /// <summary>
            /// Returns the type, id, name, schedule time and the call stack where the entry was scheduled from
            /// </summary>
            /// <returns>A description of the entry</returns>
            public override string ToString()
                => Name == null ? String.Concat(Type, " #", TaskId, " @ ", Scheduled, ": ", Scheduler) : String.Concat(Type, " #", TaskId, ' ', Name, " @ ", Scheduled, ": ", Scheduler);
#else//DEBUG
            /// <summary>
            /// Returns the type, id and name of the entry
            /// </summary>
            /// <returns>A description of the entry</returns>
            public override string ToString()
                => Name == null ? String.Concat(Type, " #", TaskId) : String.Concat(Type, " #", TaskId, ' ', Name);
#endif//DEBUG


            /// <summary>
            /// True if the task runs in it's own async chain, false if it's executed (one after another) together with other non async tasks that are due at the same time
            /// </summary>
            public bool RunAsync { get; init; }

            /// <summary>
            /// When the task will be executed next (UTC), for a task that is currently executing it's the time it was scheduled at
            /// </summary>
            public DateTime RunAt => new DateTime(Time, DateTimeKind.Utc);

            /// <summary>
            /// The internal unique Id of this task
            /// </summary>
            public long TaskId => Id;

            /// <summary>
            /// Optional name (for debugging / display)
            /// </summary>
            public String Name { get; init; }


            /// <summary>
            /// The kind of function this entry executes
            /// </summary>
            public TaskTypes Type { get; init; }

            /// <summary>
            /// True if the task is re-scheduled after each execution (a repeat function was supplied)
            /// </summary>
            public bool Repeat => RepeatFn != null;

            /// <summary>
            /// The repeat interval, zero if not repeating
            /// </summary>
            /// <remarks>Computed by calling the repeat function with <see cref="RunAt"/>, so it's only accurate for fixed intervals</remarks>
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
            /// Number of times the task have completed (successfully or with an exception)
            /// </summary>
            public long Count => Interlocked.Read(ref InternalCount);

            /// <summary>
            /// True if the task is currently running (or have been picked for execution and is about to run)
            /// </summary>
            public bool IsRunning => Interlocked.Read(ref Guard) != 0;

            /// <summary>
            /// The time when the task was last started (UTC, it's updated when the execution completes), <see cref="DateTime.MinValue"/> if it never completed
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
            /// The time when the task was last completed (UTC), <see cref="DateTime.MinValue"/> if it never completed
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
            /// The time of the last failure (UTC), <see cref="DateTime.MinValue"/> if it never failed
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
            /// The last exception text (the result of ToString on the exception), null if it never failed
            /// </summary>
            [TableDataText(60)]
            public String LastExceptionEx { get; internal set; }


            #endregion//Error tracking


            #region Create

            /// <summary>
            /// When this task was created (UTC)
            /// </summary>
            public DateTime Scheduled { get; init; }


#if DEBUG

            /// <summary>
            /// Call stack to where this task was scheduled from (only available in debug builds)
            /// </summary>
            [TableDataText(60)]
            public String Scheduler { get; init; }

#endif//DEBUG

            #endregion//Create




            /// <summary>
            /// Compare by execution time and then by id
            /// </summary>
            /// <param name="other">The entry to compare with (must not be null)</param>
            /// <returns>A negative value if this entry should execute before <paramref name="other"/>, zero if it's the same entry, else a positive value</returns>
            public int CompareTo(Entry other)
            {
                var i = Time.CompareTo(other.Time);
                if (i != 0)
                    return i;
                return Id.CompareTo(other.Id);
            }

            /// <summary>
            /// Check if this is the same entry (same unique id)
            /// </summary>
            /// <param name="other">The entry to compare with (must not be null)</param>
            /// <returns>True if the ids are equal</returns>
            public bool Equals(Entry other) => Id == other.Id;

            /// <summary>
            /// Check if an object is the same entry (same unique id)
            /// </summary>
            /// <param name="obj">The object to compare with</param>
            /// <returns>True if the object is an entry with the same id</returns>
            public override bool Equals(object obj)
            {
                var o = obj as Entry;
                if (o == null)
                    return false;
                return Id == o.Id;
            }

            /// <summary>
            /// Hash code based on the unique id
            /// </summary>
            /// <returns>The hash code</returns>
            public override int GetHashCode() => (int)Id;

            /// <summary>
            /// Remove the task from the schedule, if it's currently executing this blocks (sleeping) until the execution has completed.
            /// Calling it more than once does nothing.
            /// </summary>
            /// <remarks>When called from within the task itself (including it's async continuations) it doesn't wait, the current execution completes and the task is not re-scheduled.
            /// An execution that was already picked (but not started) is skipped.</remarks>
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

            /// <summary>
            /// 0 = idle, 1 = picked for execution (not started), 2 = executing
            /// </summary>
            internal long Guard;

            internal long Time;

            internal long InternalCount;
            internal long InternalLastStart;
            internal long InternaLastEnd;
            internal long InternalExceptionCount;
            internal long InternaLastException;
            internal long InternaTotalDuration;


        }


        /// <summary>
        /// The entry being executed by the current async flow (used to detect an entry disposing itself)
        /// </summary>
        static readonly AsyncLocal<Entry> Current = new AsyncLocal<Entry>();

        static async Task ExecuteOne(Entry ee)
        {
            //  Mark as executing before checking Removed (DoRemove does the opposite), so either we skip it or DoRemove waits for it
            Interlocked.Exchange(ref ee.Guard, 2);
            if (Interlocked.Read(ref ee.Removed) != 0)
            {
                Interlocked.Exchange(ref ee.Guard, 0);
                return;
            }
            //  Only flows to the task (and it's continuations), restored when this async method returns to it's caller
            Current.Value = ee;
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
            Current.Value = null;
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
            //  Disposed from within it's own task, waiting would block forever (Removed prevents re-scheduling)
            if (Current.Value == ee)
                return true;
            for (; ;)
            {
                //  0 = idle, 1 = picked but not started (ExecuteOne will see Removed and skip it), only wait for a running execution
                if (Interlocked.Read(ref ee.Guard) != 2)
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
