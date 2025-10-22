using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{
    public class StoredUrl
    {
        /// <summary>
        /// The url
        /// </summary>
        [TableDataUrl("{0}", "../{0}")]
        public String Url;

        /// <summary>
        /// The size of the url on disc, excluding any stored files (what counts towards quota)
        /// </summary>
        [TableDataByteSize]
        public long Size;

        /// <summary>
        /// When the url was saved
        /// </summary>
        [TableDataOrder(2)]
        public DateTime Saved;

        /// <summary>
        /// If true, the url is hidden from other users
        /// </summary>
        [TableDataOrder(2)]
        public bool Private;

        /// <summary>
        /// If the url isn't private, and this is:
        /// null - anyone can see this url, logged in or not.
        /// "" - any logged in user can see this url.
        /// [other] - Users with at least one of these tokens can see the url.
        /// </summary>
        [TableDataTags("{^0}", null, "{0}", true)]
        [TableDataOrder(2)]
        public String Auth;

        /// <summary>
        /// When the url was last viewed
        /// </summary>
        [TableDataOrder(2)]
        public DateTime LastViewed;

        /// <summary>
        /// When the url expires
        /// </summary>
        [TableDataOrder(2)]
        public DateTime Expires;

        /// <summary>
        /// If true, the url is compressed on disc
        /// </summary>
        [TableDataOrder(2)]
        public bool Comp;

        /// <summary>
        /// The shard that contains this url.
        /// In advanced scenarios the url may be split onto diffrent discs.
        /// </summary>
        [TableDataOrder(2)]
        public int Shard;

        public StoredUrl()
        {
        }

        public StoredUrl(string url, long size, bool compressed, bool isPrivate, string auth, DateTime saved, DateTime lastViewed, DateTime expires, int shard)
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
