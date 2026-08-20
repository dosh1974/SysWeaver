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
    /// Two tasks that are scheduled at the same time will be executed in the order of being added.
    /// </summary>
    public static class Scheduler
    {
        /// <summary>
        /// Roughly the number of milli seconds between each check
        /// </summary>
        public const int CheckFrequencyMs = 1000;

        /// <summary>
        /// Schedule a task to be performed at a specified time, precision is fairly low so the task can be executed a few seconds later than scheduled.
        /// Two tasks that are scheduled at the same time will be executed in the order of being added.
        /// </summary>
        /// <param name="when">The UTC time to execute the task at, precision is fairly low so the task can be executed a few seconds later than scheduled.
        /// Two tasks that are scheduled at the same time will be executed in the order of being added.
        /// </param>
        /// <param name="task">The task to execute</param>
        /// <returns>An object that can be disposed to prevent execution of the task in the future</returns>
        public static IDisposable Add(DateTime when, Action task)
        {
            if (when.Kind != DateTimeKind.Utc)
                when = when.ToUniversalTime();
            var e = Entries;
            lock (e)
            {
                var ee = new Entry(when.Ticks, Interlocked.Increment(ref Id), task);
                e.Add(ee, 0);
                if (CheckTask == null)
                    CheckTask = new PeriodicTask(Check, CheckFrequencyMs);
                return ee;
            }
        }

        /// <summary>
        /// Schedule a task to be performed at a specified time, precision is fairly low so the task can be executed a few seconds later than scheduled.
        /// Two tasks that are scheduled at the same time will be executed in the order of being added.
        /// </summary>
        /// <param name="when">The UTC time to execute the task at, precision is fairly low so the task can be executed a few seconds later than scheduled.
        /// Two tasks that are scheduled at the same time will be executed in the order of being added.
        /// </param>
        /// <param name="task">The task to execute</param>
        /// <returns>An object that can be disposed to prevent execution of the task in the future</returns>
        public static IDisposable AddTask(DateTime when, Func<Task> task)
        {
            if (when.Kind != DateTimeKind.Utc)
                when = when.ToUniversalTime();
            var e = Entries;
            lock (e)
            {
                var ee = new Entry(when.Ticks, Interlocked.Increment(ref Id), task);
                e.Add(ee, 0);
                if (CheckTask == null)
                    CheckTask = new PeriodicTask(Check, CheckFrequencyMs);
                return ee;
            }
        }

        /// <summary>
        /// Schedule a task to be performed at a specified time, precision is fairly low so the task can be executed a few seconds later than scheduled.
        /// Two tasks that are scheduled at the same time will be executed in the order of being added.
        /// </summary>
        /// <param name="when">The UTC time to execute the task at, precision is fairly low so the task can be executed a few seconds later than scheduled.
        /// Two tasks that are scheduled at the same time will be executed in the order of being added.
        /// </param>
        /// <param name="task">The task to execute</param>
        /// <returns>An object that can be disposed to prevent execution of the task in the future</returns>
        public static IDisposable AddValueTask(DateTime when, Func<Task> task)
        {
            if (when.Kind != DateTimeKind.Utc)
                when = when.ToUniversalTime();
            var e = Entries;
            lock (e)
            {
                var ee = new Entry(when.Ticks, Interlocked.Increment(ref Id), task);
                e.Add(ee, 0);
                if (CheckTask == null)
                    CheckTask = new PeriodicTask(Check, CheckFrequencyMs);
                return ee;
            }
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
                lock (ee)
                    return [.. ee.Keys];
            }
        }

        static async Task<bool> Check()
        {
            var now = DateTime.UtcNow.Ticks;
            var e = Entries;
            for (; ; )
            {
            //  Get an entry to execute
                Entry ee;
                lock (e)
                {
                    ee = e.FirstOrDefault().Key;
                    if (ee == null)
                        break;
                    if (now < ee.Time)
                        break;
                    if (Interlocked.CompareExchange(ref ee.Guard, 1, 0) != 0)
                        continue;
                }
            //  Execute the task
                try
                {
                    for (; ; )
                    {
                        var ta = ee.TA;
                        if (ta != null)
                        {
                            await ta().ConfigureAwait(false);
                            break;
                        }
                        var vta = ee.VTA;
                        if (vta != null)
                        {
                            await vta().ConfigureAwait(false);
                            break;
                        }
                        var a = ee.A;
                        if (a != null)
                            a();
                        break;
                    }
                }
                catch (Exception ex)
                {
                    TaskExceptions.OnException(new Exception(ee.ToString() + " failed with an exception", ex));
                }
            //  Remove the task
                lock (e)
                {
                    e.Remove(ee);
                    if (e.Count < 0)
                    {
                        Interlocked.Exchange(ref CheckTask, null);
                        return false;
                    }
                }
            }
            return true;
        }

        static PeriodicTask CheckTask;
            
        static bool DoRemove(Entry ee)
        {
            if (Interlocked.CompareExchange(ref ee.Guard, 1, 0) != 0)
                return false;
            var e = Entries;
            lock (e)
            {
                e.Remove(ee);
            }
            return true;
        }


        public sealed class Entry : IDisposable, IComparable<Entry>, IEquatable<Entry>
        {
#if DEBUG
            public override string ToString() 
                => String.Concat(Type, " #", TaskId, " @ ", Scheduled, ": ", Scheduler);
#else//DEBUG
            public override string ToString() 
                => String.Concat(Type, " #", TaskId);
#endif//DEBUG

            internal Entry(long time, long id, Action task)
            {
                Time = time;
                Id = id;
                HashCode = (int)id;
                A = task;
#if DEBUG
                var s = new StackTrace(2, true);
                var frame = s.GetFrame(0);
                Scheduled = DateTime.UtcNow;
                Scheduler = frame.ToString().Trim();
#endif//DEBUG
            }

            internal Entry(long time, long id, Func<Task> task)
            {
                Time = time;
                Id = id;
                HashCode = (int)id;
                TA = task;
#if DEBUG
                var s = new StackTrace(2, true);
                var frame = s.GetFrame(0);
                Scheduled = DateTime.UtcNow;
                Scheduler = frame.ToString().Trim();
#endif//DEBUG
            }

            internal Entry(long time, long id, Func<ValueTask> task)
            {
                Time = time;
                Id = id;
                HashCode = (int)id;
                VTA = task;
#if DEBUG
                var s = new StackTrace(2, true);
                var frame = s.GetFrame(0);
                Scheduled = DateTime.UtcNow;
                Scheduler = frame.ToString().Trim();
#endif//DEBUG
            }

            [TableDataIgnore]
            internal int Guard;

            [TableDataIgnore]
            internal readonly long Time;

            [TableDataIgnore]
            public readonly long Id;

            [TableDataIgnore]
            internal readonly Action A;

            [TableDataIgnore]
            internal readonly Func<Task> TA;

            [TableDataIgnore]
            internal readonly Func<ValueTask> VTA;

            readonly int HashCode;

            /// <summary>
            /// When the task will be executed
            /// </summary>
            public DateTime RunAt => new DateTime(Time, DateTimeKind.Utc);

            /// <summary>
            /// The internal unique Id of this task
            /// </summary>
            public long TaskId => Id;

            /// <summary>
            /// True if it's an async job
            /// </summary>
            public String Type
            {
                get
                {
                    if (A != null)
                        return "Sync";
                    if (TA != null)
                        return "Task";
                    if (VTA != null)
                        return "ValueTask";
                    return "-";
                }
            }
#if DEBUG
            /// <summary>
            /// When this task was scheduled
            /// </summary>
            public DateTime Scheduled { get; init; }

            /// <summary>
            /// The scheduler
            /// </summary>
            public String Scheduler { get; init; }

#endif//DEBUG





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

            public override int GetHashCode() => HashCode;

            public void Dispose() => DoRemove(this);

        }



    }

}
