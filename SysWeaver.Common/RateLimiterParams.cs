using System;

namespace SysWeaver
{
    /// <summary>
    /// A parameters for a rate limiter
    /// </summary>
    public class RateLimiterParams
    {

#if DEBUG
        public override string ToString() => String.Concat(Count, " / ", Duration, Duration == 1 ? " second" : " seconds");

#endif//DEBUG

        public virtual void Validate()
        {
            if (Count <= 0)
                throw new Exception("Count must be at least 1");
            if (Duration <= 0)
                throw new Exception("Duration must be at least one second");
        }

        /// <summary>
        /// Number of request
        /// </summary>
        public int Count = 10;

        /// <summary>
        /// Over this time frame in seconds
        /// </summary>
        public int Duration = 1;
    }




}
