using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{

    public class HttpRateLimiter : RateLimiter
    {
        public HttpRateLimiter(HttpRateLimiterParams p) : base(p)
        {
            MaxQueue = p.MaxQueue;
            MaxWait = TimeSpan.TicksPerSecond * p.MaxDelay;
        }

        /// <summary>
        /// The maximum number of request to keep queued
        /// </summary>
        public readonly int MaxQueue;

        /// <summary>
        /// The maximum time to delay a request
        /// </summary>
        public readonly long MaxWait;

        /// <summary>
        /// Number of waiting threads now
        /// </summary>
        public long Waiting => Interlocked.Read(ref WaitCount);


        long WaitCount;

        /// <summary>
        /// Check if we're exceeding the limit, wait if enabled and required
        /// </summary>
        /// <returns>True if the limit is exceeded (return 429)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<bool> IsOverTheLimit()
            => IsOverLimit(out var timeToNext) ? InternalIsOverTheLimit(timeToNext) : TaskExt.FalseValueTask;

        async ValueTask<bool> InternalIsOverTheLimit(long timeToNext)
        {
            var maxWait = MaxWait;
            var maxQueue = MaxQueue;
            var count = Interlocked.Increment(ref WaitCount);
            try
            {
                do
                {
                    //  Don't allow to many waiters
                    if (count > maxQueue)
                        return true;
                    //  Don't wait if we won't make it
                    if (timeToNext > maxWait)
                        return true;
                    //  Wait less next round
                    maxWait -= timeToNext;
                    //  Computer number of ms to wait
                    timeToNext += (TimeSpan.TicksPerMillisecond - 1);
                    timeToNext /= TimeSpan.TicksPerMillisecond;
                    //  Wait
                    await Task.Delay((int)timeToNext).ConfigureAwait(false);
                    //  Re-test
                } while (IsOverLimit(out timeToNext));
            }
            finally
            {
                Interlocked.Decrement(ref WaitCount);
            }
            return false;
        }


    }

}



