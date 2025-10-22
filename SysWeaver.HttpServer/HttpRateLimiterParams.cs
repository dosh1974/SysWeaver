namespace SysWeaver.Net
{
    public class HttpRateLimiterParams : RateLimiterParams
    {

        /// <summary>
        /// The maximum number of request to keep queued
        /// </summary>
        public int MaxQueue = 10;

        /// <summary>
        /// The maximum time to delay a request
        /// </summary>
        public int MaxDelay = 5;
    }

}



