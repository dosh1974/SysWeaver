using System;
using SysWeaver.Net;

namespace SysWeaver.ReverseProxy
{
    public class ReverseProxyRequest : ReverseProxyBase
    {
        public HttpServerMethods Method;
        public String EndPoint;
        public String Url;
    }

}
