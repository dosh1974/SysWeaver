using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="AvatarService"/>
    /// </summary>
    public sealed class AvatarParams
    {
        /// <summary>
        /// Folder with the avatar images (*.svg), if null or empty the images embedded in the assembly are used
        /// </summary>
        public String DiscFolder;

        /// <summary>
        /// Number of seconds to cache generated images (mem vs performance), default is 300 (values below 1 are treated as 1)
        /// </summary>
        public int CacheSeconds = 5 * 60;

        /// <summary>
        /// Auth required to fetch the avatar images (a list of roles / tokens), null for no restriction
        /// </summary>
        public String Auth;

        /// <summary>
        /// If true, avatar images will be transparent, otherwise (default) they are opaque
        /// </summary>
        public bool UseTransparent;
    }
}
