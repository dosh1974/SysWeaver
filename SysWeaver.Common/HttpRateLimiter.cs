using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// A rate limiter for http requests, a request that is over the limit can be delayed (async) for a while instead of being rejected immediately.
    /// </summary>
    /// <remarks>
    /// Used by the http server to limit requests per server, per api and per session (a rejected request typically returns http status 429).
    /// Waiting requests poll the limit (they are not queued in order), so they are not served in a fair (FIFO) order.
    /// </remarks>
    public class HttpRateLimiter : RateLimiter
    {
        /// <summary>
        /// Create a new http rate limiter
        /// </summary>
        /// <param name="p">The parameters</param>
        /// <exception cref="ArgumentException">The count or duration is zero or negative</exception>
        /// <exception cref="NullReferenceException"><paramref name="p"/> is null</exception>
        public HttpRateLimiter(HttpRateLimiterParams p) : base(p)
        {
            MaxQueue = p.MaxQueue;
            MaxWait = TimeSpan.TicksPerSecond * p.MaxDelay;
        }

        /// <summary>
        /// The maximum number of requests that may be delayed at the same time
        /// </summary>
        public readonly int MaxQueue;

        /// <summary>
        /// The maximum time to delay a request, in <see cref="TimeSpan"/> ticks
        /// </summary>
        public readonly long MaxWait;

        /// <summary>
        /// Number of requests that are currently delayed (waiting for a free slot)
        /// </summary>
        public long Waiting => Interlocked.Read(ref WaitCount);


        long WaitCount;

        /// <summary>
        /// Check if a request is over the limit, if it is, the request may be delayed (async) until a slot is free.
        /// </summary>
        /// <returns>False if the request is allowed (possibly after a delay), true if the limit is exceeded and the request should be rejected (return 429).
        /// The returned value task is completed synchronously (no allocation) if the request is within the limit</returns>
        /// <remarks>A request is rejected immediately if <see cref="MaxQueue"/> requests are already delayed, or if the remaining allowed delay (<see cref="MaxWait"/>) isn't enough to reach a free slot</remarks>
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



