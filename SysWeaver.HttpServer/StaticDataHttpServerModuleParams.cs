using System;

namespace SysWeaver.Net
{
    public class StaticDataHttpServerModuleParams
    {
        public override string ToString() =>
            String.Concat(
                nameof(UrlRoot), ": ", UrlRoot.ToQuoted(), ", ",
                nameof(ClientCacheDuration), ": ", ClientCacheDuration, ", ",
                nameof(Compression), ": ", Compression.ToQuoted());

        /// <summary>
        /// Root url for the assets
        /// </summary>
        public String UrlRoot;

        /// <summary>
        /// The number of seconds that a client should re-use this resource without additional requests
        /// </summary>
        public int ClientCacheDuration = 15;

        /// <summary>
        /// The compression methods in the preferred order to server data
        /// </summary>
        public String Compression = "br: Balanced, deflate: Balanced, gzip: Balanced";



    }

}
