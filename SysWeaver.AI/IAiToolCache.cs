using System;
using System.Reflection;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// A cache of AI tools (wrapping API end points), so the same tool is shared between sessions
    /// </summary>
    public interface IAiToolCache
    {
        /// <summary>
        /// Get a tool that has already been registered
        /// </summary>
        /// <param name="fn">The tool (function) name</param>
        /// <returns>The tool, or null if no tool with the name is registered</returns>
        AiTool GetRegisteredTool(String fn);

        /// <summary>
        /// Get (or create) a tool for an existing API
        /// </summary>
        /// <param name="apiName">The name (url) of the API</param>
        /// <param name="fn">The tool name, null to derive it from the API method</param>
        /// <returns>The tool, or null if the service has no API server</returns>
        /// <exception cref="ArgumentException">If the API name is unknown</exception>
        AiTool GetTool(String apiName, String fn = null);

        /// <summary>
        /// Get (or create) a tool for a method of an object
        /// </summary>
        /// <param name="instance">The object instance (null for static methods)</param>
        /// <param name="method">The method</param>
        /// <param name="fn">The tool name, null to derive it from the method</param>
        /// <param name="perfMonitor">Optional performance monitor for the calls</param>
        /// <param name="defaultAuth">The auth used if the method has none</param>
        /// <param name="defaultCachedCompression">The cached compression used if the method has none</param>
        /// <param name="defaultCompression">The compression used if the method has none</param>
        /// <param name="locationPrefix">The location prefix of the created end point</param>
        /// <returns>The tool</returns>
        AiTool GetTool(Object instance, MethodInfo method, String fn = null, PerfMonitor perfMonitor = null, String defaultAuth = ApiHttpEntry.DefaultAuth, String defaultCachedCompression = ApiHttpEntry.DefaultCachedCompression, String defaultCompression = ApiHttpEntry.DefaultCompression, String locationPrefix = ApiHttpEntry.DefaultLocationPrefix);
    }


}
