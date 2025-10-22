using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{
    public class StoredFile
    {
        /// <summary>
        /// The url to the file
        /// </summary>
        [TableDataUrl("{0}", "../{0}")]
        public String Url;

        /// <summary>
        /// The size of the file on disc (what counts towards quota)
        /// </summary>
        [TableDataByteSize]
        public long Size;

        /// <summary>
        /// When the file was saved
        /// </summary>
        [TableDataOrder(2)]
        public DateTime Saved;

        /// <summary>
        /// If true, the file is hidden from other users
        /// </summary>
        [TableDataOrder(2)]
        public bool Private;

        /// <summary>
        /// If the file isn't private, and this is:
        /// null - anyone can see this file, logged in or not.
        /// "" - any logged in user can see this file.
        /// [other] - Users with at least one of these tokens can see the file.
        /// </summary>
        [TableDataTags("{^0}", null, "{0}", true)]
        [TableDataOrder(2)]
        public String Auth;

        /// <summary>
        /// When the file was last viewed
        /// </summary>
        [TableDataOrder(2)]
        public DateTime LastViewed;

        /// <summary>
        /// When the file expires
        /// </summary>
        [TableDataOrder(2)]
        public DateTime Expires;

        /// <summary>
        /// The compression method used on disc (or null if not compressed)
        /// </summary>
        [TableDataOrder(2)]
        [TableDataTags]
        public String Comp;

        /// <summary>
        /// The shard that contains this file.
        /// In advanced scenarios the files may be split onto diffrent discs.
        /// </summary>
        [TableDataOrder(2)]
        public int Shard;

        public StoredFile()
        {
        }

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
