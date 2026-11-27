using System;
using System.Collections.Generic;
using System.Threading;

namespace SysWeaver.Net
{
    /// <summary>
    /// Tracks requests of some kind (ex: requests that hit no end point): the number of requests, and the time, url and client address of the last one.
    /// </summary>
    /// <remarks>Thread safe, the count, time, url and address are updated individually (not as an atomic unit)</remarks>
    public sealed class HttpRequestTracker
    {
        /// <summary>
        /// Returns a summary
        /// </summary>
        /// <returns>"None" or the count and the last request</returns>
        public override string ToString()
        {
            var c = Count;
            if (c <= 0)
                return "None";
            return String.Concat(c.ToString(), " requests, last at ", new DateTime(LastTime, DateTimeKind.Utc).ToString("o"), " from ", LastIp, ": ", LastUrl);
        }

        /// <summary>
        /// Register a request
        /// </summary>
        /// <param name="url">The (local) url of the request</param>
        /// <param name="ip">The client address of the request</param>
        public void OnRequest(String url, String ip)
        {
            InternalUrl = url;
            InternalIp = ip;
            Interlocked.Exchange(ref InternalTime, DateTime.UtcNow.Ticks);
            Interlocked.Increment(ref InternalCount);
        }

        /// <summary>
        /// Number of registered requests
        /// </summary>
        public long Count => Interlocked.Read(ref InternalCount);

        /// <summary>
        /// The time stamp (in UTC ticks) of the last request
        /// </summary>
        public long LastTime => Interlocked.Read(ref InternalTime);

        /// <summary>
        /// The url of the last request
        /// </summary>
        public String LastUrl => InternalUrl;

        /// <summary>
        /// The client address of the last request
        /// </summary>
        public String LastIp => InternalIp;

        volatile String InternalUrl;
        volatile String InternalIp;
        long InternalCount;
        long InternalTime;

        /// <summary>
        /// Get the statistics ("Count", "Time", "Url" and "Ip", prefixed), nothing is returned if no request has been registered
        /// </summary>
        /// <param name="system">The system name to use for the statistics</param>
        /// <param name="prefix">A prefix for the statistics names (null is treated as empty)</param>
        /// <param name="what">What is counted, used in the descriptions, ex: "requests that hit no end point (404)"</param>
        /// <returns>The statistics</returns>
        public IEnumerable<Stats> GetStats(String system, String prefix, String what)
        {
            var c = Count;
            if (c <= 0)
                yield break;
            yield return new Stats(system, prefix + "Count", c, "Total number of " + what);
            yield return new Stats(system, prefix + "Time", new DateTime(LastTime, DateTimeKind.Utc), "The time of the last of the " + what);
            yield return new Stats(system, prefix + "Url", LastUrl ?? "", "The url of the last of the " + what);
            yield return new Stats(system, prefix + "Ip", LastIp ?? "", "The client address of the last of the " + what);
        }
    }
}
