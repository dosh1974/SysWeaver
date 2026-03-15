using System;
using SysWeaver.Remote;

namespace SysWeaver.ReverseProxy
{
    public sealed class ReverseProxyClientParams : RemoteConnection
    {
        public ReverseProxyClientParams()
        {
            AuthMethod = RemoteAuthMethod.SysWeaverLogin;
        }

        public String ClientId = "$(MachineName)";

        public int MaxConcurrentRequests = 8;

    }

}
