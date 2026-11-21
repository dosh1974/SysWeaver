using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Parameters for the MCP (Model Context Protocol) service
    /// </summary>
    public sealed class McpParams
    {
        /// <summary>
        /// The local url of the MCP end point, this is the url that MCP clients connects to (server url + this).
        /// Ex: "mcp" gives "https://myserver/mcp".
        /// </summary>
        public String Url = "mcp";

        /// <summary>
        /// The auth token(s) required to access this service (comma separated).
        /// "" = Any authenticated user (a Bearer token or API key can be used), null = public (no auth), "-" = no access.
        /// </summary>
        public String Auth = "";

        /// <summary>
        /// The name of the server reported to MCP clients, null or empty to use the application name
        /// </summary>
        public String ServerName;

        /// <summary>
        /// Optional instructions reported to MCP clients (a hint to the LLM on how to use this server)
        /// </summary>
        public String Instructions;

        /// <summary>
        /// Browser origins (ex: "https://myapp.com") that are allowed to access the service, "*" allows any origin.
        /// Requests without an Origin header (non browser clients) and requests from the same host are always allowed.
        /// Required by the MCP specification to prevent DNS rebinding attacks.
        /// </summary>
        public String[] AllowedOrigins = ["*"];

        /// <summary>
        /// If true (and a service manager is supplied), the AI tools (methods with an AiToolAttribute or AiUseAttribute) of all service instances are exposed.
        /// Access to each tool is still restricted by its auth (WebApiAuthAttribute).
        /// </summary>
        public bool ServiceTools = true;

        /// <summary>
        /// Files attached by tools (images, maps etc) are stored in the user storage (IUserStorageService) if available, as private files of the user.
        /// Else (or if there is no logged in user) they are available at "{Url}/files/{id}/{name}" (without auth, the id is unguessable) for this many minutes
        /// </summary>
        public int FileLifetimeMinutes = 5 * 60;

        /// <summary>
        /// Data returned by WebApiRaw methods of a WebApi instance with McpWebApiInstance.ProxyData set is kept in memory at "{Url}/data/{id}/{name}" (without auth, the id is unguessable) for this many minutes
        /// </summary>
        public int DataLifetimeMinutes = 60;

        /// <summary>
        /// If true, a "tools/call" request from a client that accepts server sent events ("Accept: text/event-stream") is answered with an event stream:
        /// the response headers are sent at once, a keep alive (a progress notification if the request has a progress token, else an SSE comment) is sent every KeepAliveSeconds
        /// while the tool runs, and the result is sent as the final event.
        /// This prevents clients (and proxies) from timing out while waiting for a slow tool (ex: video generation).
        /// If false, all responses are plain json.
        /// </summary>
        public bool StreamToolCalls = true;

        /// <summary>
        /// The interval in seconds between keep alive messages of a streamed tool call (see StreamToolCalls)
        /// </summary>
        public int KeepAliveSeconds = 15;

        /// <summary>
        /// The maximum size of a request body in bytes
        /// </summary>
        public int MaxRequestSize = 4 << 20;

        /// <summary>
        /// Optional services whose WebApi's are exposed as tools (requires a service manager).
        /// All [WebApi] methods are added (except methods returning pre-compressed data), each tool keeps the auth of the method (WebApiAuthAttribute or IRunTimeWebApiAuth, else Auth).
        /// Every MCP request also requires Auth.
        /// Services that are registered later are added when they are registered (and removed when unregistered).
        /// </summary>
        public McpWebApiInstance[] WebApis;
    }

    /// <summary>
    /// A service instance whose WebApi's should be exposed as MCP tools
    /// </summary>
    public sealed class McpWebApiInstance
    {
        /// <summary>
        /// The type that the service is registered as (a type name that TypeFinder can find, ex: "SysWeaver.MyService" or an interface type)
        /// </summary>
        public String InstanceType;

        /// <summary>
        /// Optional instance name (to select what instance)
        /// </summary>
        public String InstanceName;

        /// <summary>
        /// If true, the data returned by all WebApiRaw methods (ex: a generated image, video or audio file) is not attached to the tool result,
        /// it's kept in memory (for McpParams.DataLifetimeMinutes) and the tool returns the url of the data ("{Url}/data/{id}/{name}") instead.
        /// Use this for large data (MCP clients limits the size of a tool result) or when the client should download the data itself.
        /// If false, binary data is attached to the tool result (image content for images) and raw text is returned as text.
        /// </summary>
        public bool ProxyData = true;

    }

}
