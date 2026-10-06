using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.Net
{


    /// <summary>
    /// Common members of all http server modules (<see cref="IHttpServerModule"/> and <see cref="IHttpServerRawModule"/>).
    /// </summary>
    public interface IHttpServerBaseModule
    {
        /// <summary>
        /// The name of this module (defaults to the type name).
        /// </summary>
        String Name { get => GetType().Name; }

        /// <summary>
        /// Optionally supply a list of local url prefixes (required for raw modules).
        /// The module's handler is only invoked if the local url (no leading '/', no query string) starts with any of these prefixes.
        /// Null or an empty array means that the module is tried for every request (after all prefix matched modules).
        /// </summary>
        /// <remarks>
        /// Read when the module is added to the server (the server may sort the returned array in place), so the value should not change afterwards.
        /// </remarks>
        String[] OnlyForPrefixes { get => null; }
    }


    /// <summary>
    /// A low level module that handles the complete request/response itself, bypassing the server's handler pipeline
    /// (no auth checks, caching, compression or templates are applied by the server).
    /// </summary>
    public interface IHttpServerRawModule : IHttpServerBaseModule
    {
        /// <summary>
        /// Handle the request, writing the complete response.
        /// </summary>
        /// <param name="r">The incoming request</param>
        /// <returns>True if the request was handled (and the response written), false to let other modules handle it</returns>
        Task<bool> Handle(HttpServerRequest r);
    }

    /// <summary>
    /// A module that maps requests to <see cref="IHttpRequestHandler"/> instances.
    /// The server asks each module (in registration order, prefix matched modules first) for a handler, the first non-null handler is used.
    /// The server then performs auth, rate limiting, caching, compression, templating and transformation using the handler's properties.
    /// </summary>
    /// <remarks>
    /// Any registered service implementing this interface is attached to the http server automatically.
    /// Implementations are called concurrently and must be thread safe.
    /// </remarks>
    public interface IHttpServerModule : IHttpServerBaseModule
    {



        /// <summary>
        /// An optional async handler. If an async handler is present the <see cref="Handler(HttpServerRequest)"/> method is never called.
        /// The delegate returns a handler for the request or null if the request can't be handled by this module.
        /// </summary>
        /// <remarks>
        /// The property is read for every request, but implementations should treat it as constant (don't toggle it on and off).
        /// </remarks>
        Func<HttpServerRequest, Task<IHttpRequestHandler>> AsyncHandler { get => null; }


        /// <summary>
        /// Determine if the request can be handled by this module (only called if <see cref="AsyncHandler"/> is null).
        /// </summary>
        /// <param name="context">The incoming request</param>
        /// <returns>A handler for the request, <see cref="HttpServerTools.AlreadyHandled"/> if the module wrote the response itself, or null if it can't be handled by this module</returns>
        /// <remarks>
        /// Returning <see cref="HttpServerTools.AlreadyHandled"/> skips all server side processing, including auth checks, so the module must do any required checks itself.
        /// </remarks>
        IHttpRequestHandler Handler(HttpServerRequest context) => null;

        /// <summary>
        /// Enumerate the end points exposed by this module (used for diagnostics and explorers).
        /// </summary>
        /// <param name="root">If null all endpoints are returned (recursively, may be slow). Else the local url of a folder (ex: "" or "Api/"), only the direct children (end points and implicit sub folders) are returned</param>
        /// <returns>End point information</returns>
        IEnumerable<IHttpServerEndPoint> EnumEndPoints(String root = null) => HttpServerTools.NoEndPoints;

    }
}
