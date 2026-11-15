using System;
using SysWeaver.Data;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Information about a stored link (url) in the user storage
    /// </summary>
    public class StoredUrl : StoredLinkInfo
    {
        /// <summary>
        /// If true, the url is compressed on disc
        /// </summary>
        [TableDataOrder(3)]
        public bool Comp;

        /// <summary>
        /// The shard that contains this url.
        /// In advanced scenarios the url may be split onto different discs.
        /// </summary>
        [TableDataOrder(3)]
        public int Shard;

        /// <summary>
        /// Create an empty instance (for serialization)
        /// </summary>
        public StoredUrl()
        {
        }

        /// <summary>
        /// Create an instance
        /// </summary>
        /// <param name="url">The url of the stored link (".html" is appended)</param>
        /// <param name="size">The size of the link on disc in bytes</param>
        /// <param name="compressed">True if the link is compressed on disc</param>
        /// <param name="isPrivate">True if the link is private</param>
        /// <param name="auth">The auth tokens required to read the link</param>
        /// <param name="saved">When the link was saved</param>
        /// <param name="lastViewed">When the link was last viewed</param>
        /// <param name="expires">When the link expires</param>
        /// <param name="shard">The shard that contains the link</param>
        public StoredUrl(string url, long size, bool compressed, bool isPrivate, string auth, DateTime saved, DateTime lastViewed, DateTime expires, int shard)
        {
            Url = url + ".html";
            var l = url.LastIndexOf('.');
            Size = size;
            Comp = compressed;
            Private = isPrivate;
            Auth = auth;
            Saved = saved;
            LastViewed = lastViewed;
            Expires = expires;
            Shard = shard;
        }




    }

}
