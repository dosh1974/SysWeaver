using System;

namespace SysWeaver.ReverseProxy
{

    /// <summary>
    /// Base class for requests and responses that are passed between a reverse proxy server and its clients
    /// </summary>
    public class ReverseProxyBase
    {
        /// <summary>
        /// The id of the client
        /// </summary>
        public String ClientId;
        /// <summary>
        /// The id that connects a response to its request (null if there is no request)
        /// </summary>
        public String RequestId;
        /// <summary>
        /// The http headers, as alternating names and values
        /// </summary>
        public String[] Headers;
        /// <summary>
        /// The body data, can be null
        /// </summary>
        public Byte[] Data;
    }


    /// <summary>
    /// A response from a client to a request
    /// </summary>
    public class ReverseProxyResponse : ReverseProxyBase
    {
        /// <summary>
        /// The http status code
        /// </summary>
        public int StatusCode;
    }

}
