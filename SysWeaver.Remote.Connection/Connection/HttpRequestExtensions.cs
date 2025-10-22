using System;
using System.Net.Http;

namespace SysWeaver.Remote.Connection
{
    static class HttpRequestExtensions
    {
        static readonly HttpRequestOptionsKey<TimeSpan?> TimeoutPropertyKey = new ("RequestTimeout");

        public static void SetTimeout(this HttpRequestMessage request, TimeSpan? timeout)
        {
            request.Options.Set(TimeoutPropertyKey, timeout);
        }

        public static TimeSpan? GetTimeout(this HttpRequestMessage request)
        {
            if (request.Options.TryGetValue(TimeoutPropertyKey, out var value) && value is TimeSpan timeout)
                return timeout;
            return null;
        }
    }

}
