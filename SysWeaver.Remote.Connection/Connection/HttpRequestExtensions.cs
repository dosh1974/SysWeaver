using System;
using System.Net.Http;

namespace SysWeaver.Remote.Connection
{
    /// <summary>
    /// Stores a per request timeout in <see cref="HttpRequestMessage.Options"/>, consumed by <see cref="HttpClientTimeoutHandler"/>.
    /// </summary>
    static class HttpRequestExtensions
    {
        static readonly HttpRequestOptionsKey<TimeSpan?> TimeoutPropertyKey = new ("RequestTimeout");

        /// <summary>
        /// Sets the timeout for this request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="timeout">The timeout, null to use the handler default.</param>
        public static void SetTimeout(this HttpRequestMessage request, TimeSpan? timeout)
        {
            request.Options.Set(TimeoutPropertyKey, timeout);
        }

        /// <summary>
        /// Gets the timeout set using <see cref="SetTimeout"/>.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The timeout or null if none is set.</returns>
        public static TimeSpan? GetTimeout(this HttpRequestMessage request)
        {
            if (request.Options.TryGetValue(TimeoutPropertyKey, out var value) && value is TimeSpan timeout)
                return timeout;
            return null;
        }
    }

}
