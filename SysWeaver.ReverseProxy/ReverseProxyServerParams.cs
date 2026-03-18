using System;

namespace SysWeaver.ReverseProxy
{
    public sealed class ReverseProxyServerParams
    {
        /// <summary>
        /// The base url to access a client.
        /// EVERYTHING under this base url will be redirected so it must be unique.
        /// The first "folder" under the base url is the "ClientId-EndPoint", in other words the first folder after the base will be split on '-' (if present).
        /// The ClientId (and EndPoint) will be extracted and the request will go to that client.
        /// Ex:
        ///     Request: https://server.example.com/ReverseProxyFiles/computer-service/debug/index.html
        ///         ClientId = "computer"
        ///         EndPoint = "service"
        ///         Client url: "debug/index.html" (appended to the enpoint defined in the client)
        ///         
        /// </summary>
        public String BaseUrl = "ReverseProxyFiles";

        /// <summary>
        /// If populated, all domains listed will be ignored.
        /// Other domains will use the reverse proxy, following this pattern:
        /// ClientId-EndPoint.*.*, in other words the first part of the domain will be split on '-' (if present).
        /// The ClientId (and EndPoint) will be extracted and the request will go to that client.
        /// Ex:
        ///     AllButDomains:  = ["server.example.com"].
        ///     Request: https://computer-service.example.com/debug/index.html 
        ///         ClientId = "computer"
        ///         EndPoint = "service"
        ///         Client url: "debug/index.html"  (appended to the enpoint defined in the client)
        ///     
        /// </summary>
        public String[] AllButDomains;

    }

}
