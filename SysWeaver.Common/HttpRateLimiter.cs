using System;
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


        int WaitCount;

        /// <summary>
        /// Check if we're exceeding the limit, wait if enabled and required
        /// </summary>
        /// <returns>True if the limit is exceeded (return 429)</returns>
        public async ValueTask<bool> IsOverTheLimit()
        {
            var maxWait = MaxWait;
            var maxQueue = MaxQueue;
            if (!IsOverLimit(out var timeToNext))
                return false;
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



