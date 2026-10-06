namespace SysWeaver
{
    /// <summary>
    /// Parameters for a <see cref="HttpRateLimiter"/>, a rate limit plus how many requests that may be delayed (queued) and for how long, before being rejected.
    /// </summary>
    public class HttpRateLimiterParams : RateLimiterParams
    {

        /// <summary>
        /// The maximum number of requests that may be delayed (waiting for a free slot) at the same time, additional requests over the limit are rejected immediately
        /// </summary>
        public int MaxQueue = 10;

        /// <summary>
        /// The maximum time in seconds to delay a request that is over the limit, requests that can't be served within this time are rejected immediately
        /// </summary>
        public int MaxDelay = 5;
    }

}



