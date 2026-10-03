using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Parameters for the AI host service (OpenAI and Google Gemini / Vertex AI compatible APIs in front of one or more AI services)
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
        /// "" = Any authenticated user (a Bearer token or API key can be used), null = public (no auth), "-" = no access.
        /// </summary>
        public String Auth = "";

        /// <summary>
        /// The local url prefix of the OpenAI API, this is the base url for OpenAI clients (server url + this prefix).
        /// Ex: "openai/v1/" gives "openai/v1/chat/completions".
        /// Null to disable the OpenAI API.
        /// </summary>
        public String OpenAiPrefix = "openai/v1/";

        /// <summary>
        /// The local url prefix of the Google Gemini API and Vertex AI, this is the base url for Google GenAI clients (server url + this prefix).
        /// Ex: "google/" gives "google/v1beta/models/{model}:generateContent" (Gemini API) and "google/v1/projects/{project}/locations/{location}/publishers/google/models/{model}:generateContent" (Vertex AI).
        /// Null to disable the Google APIs.
        /// Note: The OpenAI and Google APIs must use different prefixes (they have conflicting end points).
        /// </summary>
        public String GooglePrefix = "google/";

        /// <summary>
        /// The maximum size of a request body in bytes
        /// </summary>
        public int MaxRequestSize = 32 << 20;
    }
}
