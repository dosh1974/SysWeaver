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
        /// If true (and a service manager is supplied), the AI tools (methods with an AiToolAttribute or OpenAiUseAttribute) of all service instances are exposed.
        /// Access to each tool is still restricted by its auth (WebApiAuthAttribute).
        /// </summary>
        public bool ServiceTools = true;

        /// <summary>
        /// Files attached by tools (images, maps etc) are stored in the user storage (IUserStorageService) if available, as private files of the user.
        /// Else (or if there is no logged in user) they are available at "{Url}/files/{id}/{name}" (without auth, the id is unguessable) for this many minutes
        /// </summary>
        public int FileLifetimeMinutes = 60;

        /// <summary>
        /// The maximum size of a request body in bytes
        /// </summary>
        public int MaxRequestSize = 4 << 20;
    }
}
