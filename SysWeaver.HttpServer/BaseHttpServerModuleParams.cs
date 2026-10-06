using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// Common base for module parameter classes (e.g. the explore and icon modules), holding compression, client caching and auth settings.
    /// </summary>
    /// <remarks>Interpretation of the values is up to the deriving module.</remarks>
    public class BaseHttpServerModuleParams
    {
        /// <summary>
        /// The on-the-fly compression to use (results are cached), in order of preference, e.g. "br:Best, deflate:Best, gzip:Best".
        /// Null = use static module config, "" = disable compression.
        /// </summary>
        public String Compression = "br:Best, deflate:Best, gzip:Best";

        /// <summary>
        /// The number of seconds that a client should re-use this resource without additional requests, null to use the static module config.
        /// </summary>
        public int? ClientCacheDuration;

        /// <summary>
        /// Auth required to access: null = open, "" = any logged in user, else comma separated tokens where at least one is required.
        /// </summary>
        public String Auth;
    }
}
