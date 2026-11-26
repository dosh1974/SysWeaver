using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// Interface for the McpService.
    /// Can be used to register all [WebApi] methods of an object (make it available to the Mcp).
    /// </summary>
    public interface IMcpService
    {
        /// <summary>
        /// Register all [WebApi] methods of an instance as tools, each tool keeps the auth of the method (WebApiAuthAttribute or IRunTimeWebApiAuth, else McpParams.Auth).
        /// Methods with an AiHideMcpAttribute, disabled methods (WebApiOptionalAttribute) and methods returning pre-compressed data (WebApiRawAttribute) are ignored.
        /// </summary>
        /// <param name="instance">The instance</param>
        /// <param name="proxyData">True if the data returned by all WebApiRaw methods is kept in memory and the url is returned (McpWebApiInstance.ProxyData)</param>
        /// <returns>The number of tools added</returns>
        int AddWebApiTools(Object instance, bool proxyData);

        /// <summary>
        /// Unregister all tools that was added for the [WebApi] methods of an instance
        /// </summary>
        /// <param name="instance">The instance</param>
        /// <returns>The number of tools removed</returns>
        int RemoveWebApiTools(Object instance);
    }

}
