using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Parameters for the AI host service (an OpenAI compatible API in front of one or more AI services)
    /// </summary>
    public sealed class HostParams
    {
        /// <summary>
        /// The instance names of the AI services (IAiService) to host, the services are used in creation order.
        /// If null or empty, the first AI service found is used.
        /// </summary>
        public String[] Instances;

        /// <summary>
        /// The auth token(s) required to access this service (comma separated).
        /// "" = Any authenticated user (a Bearer token can be used), null = public (no auth), "-" = no access.
        /// </summary>
        public String Auth = "";

        /// <summary>
        /// The local url prefix of the API (the base url for clients is the server url + this prefix), ex: "v1/" gives "v1/chat/completions"
        /// </summary>
        public String Prefix = "v1/";

        /// <summary>
        /// The maximum size of a request body in bytes
        /// </summary>
        public int MaxRequestSize = 32 << 20;
    }
}
