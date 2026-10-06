using System;

namespace SysWeaver
{
    /// <summary>
    /// Parameters for a <see cref="RateLimiter"/>: allow at most <see cref="Count"/> requests per <see cref="Duration"/> seconds
    /// </summary>
    public class RateLimiterParams
    {

#if DEBUG
        /// <summary>
        /// Returns the limit as "count / duration seconds"
        /// </summary>
        /// <returns>The limit as text</returns>
        public override string ToString() => String.Concat(Count, " / ", Duration, Duration == 1 ? " second" : " seconds");

#endif//DEBUG

        /// <summary>
        /// Validate the parameters
        /// </summary>
        /// <exception cref="Exception"><see cref="Count"/> or <see cref="Duration"/> is less than one</exception>
        public virtual void Validate()
        {
            if (Count <= 0)
                throw new Exception("Count must be at least 1");
            if (Duration <= 0)
                throw new Exception("Duration must be at least one second");
        }

        /// <summary>
        /// Maximum number of requests within the time window
        /// </summary>
        public int Count = 10;

        /// <summary>
        /// The time window in seconds
        /// </summary>
        public int Duration = 1;
    }




}
