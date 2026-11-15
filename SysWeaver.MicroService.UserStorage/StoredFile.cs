using System;
using SysWeaver.Data;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Information about a file in the user storage
    /// </summary>
    public class StoredFile : StoredFileInfo
    {

        /// <summary>
        /// The compression method used on disc (or null if not compressed)
        /// </summary>
        [TableDataOrder(3)]
        [TableDataTags]
        public String Comp;

        /// <summary>
        /// The shard that contains this file.
        /// In advanced scenarios the files may be split onto different discs.
        /// </summary>
        [TableDataOrder(3)]
        public int Shard;
        
        /// <summary>
        /// Create an empty instance (for serialization)
        /// </summary>
        public StoredFile()
        {
        }

        /// <summary>
        /// Create an instance
        /// </summary>
        /// <param name="url">The url of the file</param>
        /// <param name="size">The size of the file on disc in bytes</param>
        /// <param name="compressed">The compression method used on disc, null if not compressed</param>
        /// <param name="isPrivate">True if the file is private</param>
        /// <param name="auth">The auth tokens required to read the file</param>
        /// <param name="saved">When the file was saved</param>
        /// <param name="lastViewed">When the file was last viewed</param>
        /// <param name="expires">When the file expires</param>
        /// <param name="shard">The shard that contains the file</param>
        public StoredFile(string url, long size, String compressed, bool isPrivate, string auth, DateTime saved, DateTime lastViewed, DateTime expires, int shard)
        {
            Url = url;
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
