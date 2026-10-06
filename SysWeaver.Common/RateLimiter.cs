using System;
using System.Collections.Generic;

namespace SysWeaver
{

    /// <summary>
    /// A simple sliding window rate limiter, allows at most <see cref="MaxCount"/> calls within any <see cref="LimitDuration"/> time window.
    /// </summary>
    /// <remarks>
    /// Thread safe (a lock is taken per call). The time stamp of every allowed call within the window is kept, so memory usage is proportional to <see cref="MaxCount"/>.
    /// Uses the wall clock (<see cref="DateTime.UtcNow"/>), so a clock adjustment may temporarily affect the limiting.
    /// </remarks>
    public class RateLimiter
    {

#if DEBUG

        /// <summary>
        /// Returns the limit as "count / duration"
        /// </summary>
        /// <returns>The limit as text</returns>
        public override string ToString() => String.Concat(MaxCount, " / ", TimeSpan.FromTicks(LimitDuration).ElapsedTime());

#endif//DEBUG


        /// <summary>
        /// Create a new rate limiter
        /// </summary>
        /// <param name="maxCount">The maximum number of requests within the specified time frame</param>
        /// <param name="limitDuration">The duration (in <see cref="TimeSpan"/> ticks) that the max count refers to</param>
        /// <exception cref="ArgumentException"><paramref name="maxCount"/> or <paramref name="limitDuration"/> is zero or negative</exception>
        public RateLimiter(int maxCount, long limitDuration)
        {
            if (maxCount <= 0)
                throw new ArgumentException("Must allow at least one call at the time", nameof(maxCount));
            if (limitDuration <= 0)
                throw new ArgumentException("Must be have a positive time span", nameof(limitDuration));
            MaxCount = maxCount;
            LimitDuration = limitDuration;
        }

        /// <summary>
        /// Create a new rate limiter
        /// </summary>
        /// <param name="maxCount">The maximum number of requests within the specified time frame</param>
        /// <param name="limitDuration">The duration that the max count refers to</param>
        /// <exception cref="ArgumentException"><paramref name="maxCount"/> or <paramref name="limitDuration"/> is zero or negative</exception>
        public RateLimiter(int maxCount, TimeSpan limitDuration)
        {
            if (maxCount <= 0)
                throw new ArgumentException("Must allow at least one call at the time", nameof(maxCount));
            if (limitDuration <= TimeSpan.Zero)
                throw new ArgumentException("Must be have a positive time span", nameof(limitDuration));
            MaxCount = maxCount;
            LimitDuration = limitDuration.Ticks;
        }

        /// <summary>
        /// Create a new rate limiter from parameters
        /// </summary>
        /// <param name="p">The parameters, <see cref="RateLimiterParams.Count"/> requests per <see cref="RateLimiterParams.Duration"/> seconds</param>
        /// <exception cref="ArgumentException">The count or duration is zero or negative</exception>
        /// <exception cref="NullReferenceException"><paramref name="p"/> is null</exception>
        public RateLimiter(RateLimiterParams p) : this(p.Count, TimeSpan.TicksPerSecond * p.Duration)
        {
        }

        /// <summary>
        /// Test if a call is allowed, and if so register it (a call that is over the limit is not registered)
        /// </summary>
        /// <param name="ticksToNextFree">If over the limit, the number of <see cref="TimeSpan"/> ticks until the oldest registered call leaves the window (a slot is free), else zero</param>
        /// <returns>True if the rate exceeds the limit (the call should be rejected or delayed)</returns>
        public bool IsOverLimit(out long ticksToNextFree)
        {
            ticksToNextFree = 0;
            var h = History;
            lock (h)
            {
                var tickUtcNow = DateTime.UtcNow.Ticks;
                var maxAge = tickUtcNow - LimitDuration;
                long t;
                while (h.TryPeek(out t))
                {
                    if (t > maxAge)
                        break;
                    h.Dequeue();
                }
                if (h.Count >= MaxCount)
                {
                    ticksToNextFree = t - maxAge;
                    return true;
                }
                h.Enqueue(tickUtcNow);
                return false;
            }
        }

        /// <summary>
        /// Maximum number of requests within the time window
        /// </summary>
        public readonly int MaxCount;

        /// <summary>
        /// The time window in <see cref="TimeSpan"/> ticks
        /// </summary>
        public readonly long LimitDuration;


        readonly Queue<long> History = new Queue<long>();
    }





}
