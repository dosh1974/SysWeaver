using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Schedules a task to be performed at a specified time, precision is fairly low so the task can be executed a few seconds later than scheduled.
    /// Two tasks that are scheduled at the same time will be executed in the order of being added.
    /// </summary>
    public static class Scheduler
    {
        /// <summary>
        /// Roughly the nuumber of millie seconds between each check
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
        /// <param name="noThrow">If true, return null instead of throwing exception</param>
        /// <returns>An object that can be disposed to prevent execution of the task in the future</returns>
        public static IDisposable Add(DateTime when, Action task, bool noThrow = false)
        {
            if (when.Kind != DateTimeKind.Utc)
                when = when.ToUniversalTime();
            if (when <= DateTime.UtcNow)
            {
                if (noThrow)
                    return null;
                throw new ArgumentException("The execution time must be in the future!", nameof(when));
            }
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
        /// <param name="noThrow">If true, return null instead of throwing exception</param>
        /// <returns>An object that can be disposed to prevent execution of the task in the future</returns>
        public static IDisposable Add(DateTime when, Func<Task> task, bool noThrow = false)
        {
            if (when.Kind != DateTimeKind.Utc)
                when = when.ToUniversalTime();
            if (when <= DateTime.UtcNow)
            {
                if (noThrow)
                    return null;
                throw new ArgumentException("The execution time must be in the future!", nameof(when));
            }
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

        static async ValueTask<bool> Check()
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
                    var a = ee.TA;
                    if (a != null)
                        await a().ConfigureAwait(false);
                    else
                        ee.A();
                }
                catch
                {
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


        sealed class Entry : IDisposable, IComparable<Entry>, IEquatable<Entry>
        {
            public Entry(long time, long id, Action task)
            {
                Time = time;
                Id = id;
                HashCode = (int)id;
                A = task;
            }

            public Entry(long time, long id, Func<Task> task)
            {
                Time = time;
                Id = id;
                HashCode = (int)id;
                TA = task;
            }

            public int Guard;

            public readonly long Time;
            public readonly long Id;
            public readonly Action A;
            public readonly Func<Task> TA;


            readonly int HashCode;


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
