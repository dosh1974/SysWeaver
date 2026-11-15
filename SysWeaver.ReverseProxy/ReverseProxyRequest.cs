using System;
using SysWeaver.Net;

namespace SysWeaver.ReverseProxy
{
    /// <summary>
    /// A request that the server forwards to a client
    /// </summary>
    public class ReverseProxyRequest : ReverseProxyBase
    {
        /// <summary>
        /// The http method of the request
        /// </summary>
        public HttpServerMethods Method;
        /// <summary>
        /// The name of the end point of the client that the request is for
        /// </summary>
        public String EndPoint;
        /// <summary>
        /// The url of the request, relative to the end point
        /// </summary>
        public String Url;
    }

}
