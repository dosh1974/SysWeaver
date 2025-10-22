using System;

namespace SysWeaver.Net
{
    public class BaseHttpServerModuleParams
    {
        /// <summary>
        /// The on-the fly compression to use (with cached results), null = use static module config, "" = disable compression
        /// </summary>
        public String Compression = "br:Best, deflate:Best, gzip:Best";

        /// <summary>
        /// The number of seconds that a client should re-use this resource without additional requests, use null to use static module config
        /// </summary>
        public int? ClientCacheDuration;

        /// <summary>
        /// Auth required to access
        /// </summary>
        public String Auth;
    }
}
