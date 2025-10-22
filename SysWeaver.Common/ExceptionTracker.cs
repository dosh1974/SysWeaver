using System;
using System.Collections.Generic;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// Class that can be used to track failures
    /// </summary>
    public sealed class ExceptionTracker
    {
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
        /// Call once on an exception
        /// </summary>
        /// <param name="ex">The exception that cause the failure</param>
        public void OnException(Exception ex)
        {
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


        public IEnumerable<Stats> GetStats(String system, String prefix)
        {
            var l = Interlocked.Read(ref InternalCount);
            if (l > 0)
            {
                var lastTime = Interlocked.Read(ref InternalTime);
                var lastException = InternalException;
                yield return new Stats(system, prefix + "Count", l, "Total number of exceptions registered");
                yield return new Stats(system, prefix + "Time", new DateTime(lastTime, DateTimeKind.Utc), "The last time an exception was registered");
                yield return new Stats(system, prefix + "Exception", lastException.Message, "The message of the last exception");
            }
        }


    }

}
