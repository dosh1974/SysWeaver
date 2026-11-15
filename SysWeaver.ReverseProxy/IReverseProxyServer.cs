using System;
using System.Threading.Tasks;
using SysWeaver.Remote;

namespace SysWeaver.ReverseProxy
{
    /// <summary>
    /// The remote api of a reverse proxy server (used by the clients)
    /// </summary>
    [RemotePathPrefix("ReverseProxy/")]
    public interface IReverseProxyServer : IDisposable
    {
        /// <summary>
        /// Deliver the response to the previous request and wait for the next request (long poll)
        /// </summary>
        /// <param name="response">The response to a request, also identifies the client</param>
        /// <returns>The next request that the client should handle</returns>
        [RemoteTimeout(5 * 60 * 1000)]
        Task<ReverseProxyRequest> GetReverseProxyRequest(ReverseProxyResponse response);

    }

}
