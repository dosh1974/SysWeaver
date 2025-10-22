using System;

namespace SysWeaver.MicroService
{
    public sealed class AvatarParams
    {
        /// <summary>
        /// Where avatar images are located
        /// </summary>
        public String DiscFolder;

        /// <summary>
        /// Number of seconds to cache generated images (mem vs performace)
        /// </summary>
        public int CacheSeconds = 5 * 60;

    }
}
