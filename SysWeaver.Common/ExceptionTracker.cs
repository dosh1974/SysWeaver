using System;
using System.Collections.Generic;
using System.Threading;
using SysWeaver.Data;

namespace SysWeaver
{
    /// <summary>
    /// Tracks failures: the number of exceptions, the time of the last one and the last exception
    /// </summary>
    /// <remarks>Thread safe, the count, time and exception are updated individually (not as an atomic unit)</remarks>
    public sealed class ExceptionTracker
    {
        /// <summary>
        /// Returns a summary of the failures
        /// </summary>
        /// <returns>"No fails!" or the count, time of the last fail and the last exception</returns>
        public override string ToString()
        {
            var c = Interlocked.Read(ref InternalCount);
            if (c <= 0)
                return "No fails!";
            var t = Interlocked.Read(ref InternalTime);
            var ex = InternalException;
            return String.Concat(c, c == 1 ? " fail at " : " fails at ", new DateTime(t, DateTimeKind.Utc), ", exception: ", ex);
        }

        /// <summary>
        /// Register an exception (call once per failure)
        /// </summary>
        /// <param name="ex">The exception that caused the failure, null is ignored</param>
        public void OnException(Exception ex)
        {
            if (ex == null)
                return;
            InternalException = ex;
            Interlocked.Exchange(ref InternalTime, DateTime.UtcNow.Ticks);
            Interlocked.Increment(ref InternalCount);
        }

        /// <summary>
        /// The time stamp (in ticks) when the last fail happened, use new DateTime(ticks, DateTimeKind.Utc) to get a DateTime time
        /// </summary>
        public long LastTime => Interlocked.Read(ref InternalTime);

        /// <summary>
        /// Number of times an exception have been registered
        /// </summary>
        public long Count => Interlocked.Read(ref InternalCount);

        /// <summary>
        /// The last exception registered
        /// </summary>
        public Exception LastException => InternalException;

       

        Exception InternalException;
        long InternalCount;
        long InternalTime;


        /// <summary>
        /// Get the failure statistics (count, time and last exception text), nothing is returned if no exception have been registered
        /// </summary>
        /// <param name="system">The system name to use for the statistics</param>
        /// <param name="prefix">A prefix for the statistics names (null is treated as empty)</param>
        /// <returns>The statistics</returns>
        public IEnumerable<Stats> GetStats(String system, String prefix)
        {
            var l = Interlocked.Read(ref InternalCount);
            if (l > 0)
            {
                var lastTime = Interlocked.Read(ref InternalTime);
                var lastException = InternalException;
                yield return new Stats(system, prefix + "Count", l, "Total number of exceptions registered");
                yield return new Stats(system, prefix + "Time", new DateTime(lastTime, DateTimeKind.Utc), "The last time an exception was registered");
                yield return new Stats(system, prefix + "Exception", lastException.ToString(), "The message of the last exception", ExMessageText);
            }
        }

        static readonly TableDataTextAttribute ExMessageText = new (64);


    }
}
