using System;
using System.Collections.Generic;

namespace SysWeaver
{

    /// <summary>
    /// A simple rate limiter
    /// </summary>
    public class RateLimiter
    {

#if DEBUG

        public override string ToString() => String.Concat(MaxCount, " / ", TimeSpan.FromTicks(LimitDuration).ElapsedTime());

#endif//DEBUG


        /// <summary>
        /// 
        /// </summary>
        /// <param name="maxCount">The maximum number of requests within the specified time frame</param>
        /// <param name="limitDuration">The duration time time span ticks that the max count refers to</param>
        /// <exception cref="ArgumentException"></exception>
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
        /// 
        /// </summary>
        /// <param name="maxCount">The maximum number of requests within the specified time frame</param>
        /// <param name="limitDuration">The duration that the max count refers to</param>
        /// <exception cref="ArgumentException"></exception>
        public RateLimiter(int maxCount, TimeSpan limitDuration)
        {
            if (maxCount <= 0)
                throw new ArgumentException("Must allow at least one call at the time", nameof(maxCount));
            if (limitDuration <= TimeSpan.Zero)
                throw new ArgumentException("Must be have a positive time span", nameof(limitDuration));
            MaxCount = maxCount;
            LimitDuration = limitDuration.Ticks;
        }

        public RateLimiter(RateLimiterParams p) : this(p.Count, TimeSpan.TicksPerSecond * p.Duration)
        {
        }

        /// <summary>
        /// Update a rate limiter and test if the call rate is too high
        /// </summary>
        /// <param name="ticksToNextFree">Number of ticks to the next free</param>
        /// <returns>True if the rate exceeds the limit</returns>
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
        /// Maximum number of request
        /// </summary>
        public readonly int MaxCount;

        /// <summary>
        /// Over this time frame in time span ticks
        /// </summary>
        public readonly long LimitDuration;


        readonly Queue<long> History = new Queue<long>();
    }



}
