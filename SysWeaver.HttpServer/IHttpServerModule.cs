using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    public interface IHttpServerModule
    {
        /// <summary>
        /// The name of this module
        /// </summary>
        String Name { get => GetType().Name; }

        /// <summary>
        /// Optionally supply a list of prefixes.
        /// The AsyncHandler or Handler methods are only called if the local url starts with any of these prefixes
        /// </summary>
        String[] OnlyForPrefixes { get => null;  }


        /// <summary>
        /// An optional async handler. If an async handler is present the Handler method is never called.
        /// This value is only read once so it can't be toggled on and off.
        /// </summary>
        Func<HttpServerRequest, ValueTask<IHttpRequestHandler>> AsyncHandler { get => null; }


        /// <summary>
        /// Determine if the request can be handled by this module
        /// </summary>
        /// <param name="context">The incoming request</param>
        /// <returns>A handler for the request or null if it can't be handled by this module</returns>
        IHttpRequestHandler Handler(HttpServerRequest context) => null;

        /// <summary>
        /// Enumerate all enpoints
        /// </summary>
        /// <param name="root">If null all endpoints are returned (recursively)</param>
        /// <returns>End point information</returns>
        IEnumerable<IHttpServerEndPoint> EnumEndPoints(String root = null) => HttpServerTools.NoEndPoints;

    }
}
