using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// The rate limits of an internal end point of the <see cref="HttpServerBase"/> (the forced and optional end points, ex: "auth/redirect"),
    /// declared with the end point: a limit shared by all callers and / or a limit per client address (see <see cref="HttpIpRateLimiters"/>).
    /// API end points declare their limits with attributes instead (<see cref="SysWeaver.MicroService.WebApiServiceRateLimitAttribute"/>, <see cref="SysWeaver.MicroService.WebApiIpRateLimitAttribute"/>).
    /// </summary>
    /// <remarks>Thread safe.</remarks>
    public sealed class HttpEndPointRateLimits
    {
        /// <summary>
        /// Create the rate limits of an end point, null if no limit is given
        /// </summary>
        /// <param name="service">The limit shared by all callers, null for none</param>
        /// <param name="ip">The limit per client address, null for none</param>
        /// <returns>The limits, or null if both limits are null</returns>
        /// <exception cref="Exception">A limit is invalid (see <see cref="RateLimiterParams.Validate"/>)</exception>
        public static HttpEndPointRateLimits Create(HttpRateLimiterParams service, HttpRateLimiterParams ip = null)
        {
            if ((service == null) && (ip == null))
                return null;
            return new HttpEndPointRateLimits(service, ip);
        }

        HttpEndPointRateLimits(HttpRateLimiterParams service, HttpRateLimiterParams ip)
        {
            if (service != null)
            {
                service.Validate();
                ServiceRateLimiter = new HttpRateLimiter(service);
            }
            if (ip != null)
                IpRateLimiters = new HttpIpRateLimiters(ip);
        }

        /// <summary>
        /// The limiter shared by all callers, null if not limited
        /// </summary>
        public readonly HttpRateLimiter ServiceRateLimiter;

        readonly HttpIpRateLimiters IpRateLimiters;

        /// <summary>
        /// Get the limiter for the client address of a request
        /// </summary>
        /// <param name="request">The request</param>
        /// <returns>The limiter, null if not limited per address</returns>
        public HttpRateLimiter IpRateLimiter(HttpServerRequest request) => IpRateLimiters?.Get(request);
    }

    /// <summary>
    /// An internal end point of the <see cref="HttpServerBase"/>: the handler and its optional rate limits.
    /// </summary>
    /// <typeparam name="T">The handler type</typeparam>
    /// <param name="Handler">The handler</param>
    /// <param name="Limits">The rate limits (applied before the handler is called), null if not limited</param>
    sealed record HttpInternalEndPoint<T>(T Handler, HttpEndPointRateLimits Limits = null) where T : Delegate;

}
