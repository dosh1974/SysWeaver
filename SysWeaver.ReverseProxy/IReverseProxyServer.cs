using System;
using System.Threading.Tasks;
using SysWeaver.Remote;

namespace SysWeaver.ReverseProxy
{
    [RemotePathPrefix("ReverseProxy/")]
    public interface IReverseProxyServer : IDisposable
    {
        [RemoteTimeout(5 * 60 * 1000)]
        Task<ReverseProxyRequest> GetReverseProxyRequest(String proxyId);

        Task ReverseProxyResponse(ReverseProxyResponse response);
    }

}
