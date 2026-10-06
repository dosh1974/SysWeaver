using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// Parameters for <see cref="StaticDataHttpServerModule"/>.
    /// </summary>
    public class StaticDataHttpServerModuleParams
    {
        public override string ToString() =>
            String.Concat(
                nameof(UrlRoot), ": ", UrlRoot.ToQuoted(), ", ",
                nameof(ClientCacheDuration), ": ", ClientCacheDuration, ", ",
                nameof(Compression), ": ", Compression.ToQuoted());

        /// <summary>
        /// Root url for the assets (prefixed to all registered urls, leading and trailing '/' are ignored), null or empty for the server root.
        /// </summary>
        public String UrlRoot;

        /// <summary>
        /// The number of seconds that a client should re-use this resource without additional requests
        /// </summary>
        public int ClientCacheDuration = 15;

        /// <summary>
        /// The default runtime compression methods in the preferred order, ex: "br: Balanced, deflate: Balanced".
        /// </summary>
        public String Compression = "br: Balanced, deflate: Balanced, gzip: Balanced";



    }

}
