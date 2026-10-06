using System;
using System.Collections.Concurrent;

namespace SysWeaver.Net
{
    /// <summary>
    /// A web folder (local url prefix) served by the <see cref="FileHttpServerModule"/> and the disc folders it maps to (searched in order).
    /// </summary>
    sealed class WebFolder
    {
        /// <inheritdoc/>
        public override string ToString() => String.Concat('"', Url, "\" from [", String.Join("], [", DiscFolders), ']');

        /// <summary>
        /// The local url prefix of the folder (ex: "files/").
        /// </summary>
        public readonly String Url;

        /// <summary>
        /// The disc folders that are searched (in order) for a requested file, replaced (never modified) when folders are added or removed.
        /// </summary>
        public volatile DiscFolder[] DiscFolders = [];

        /// <summary>
        /// Create a web folder without any disc folders.
        /// </summary>
        /// <param name="webFolder">The local url prefix of the folder</param>
        public WebFolder(string webFolder)
        {
            Url = webFolder;
        }
    }


}
