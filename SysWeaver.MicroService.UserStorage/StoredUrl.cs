using System;
using SysWeaver.Data;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    public class StoredUrl : StoredLinkInfo
    {
        /// <summary>
        /// If true, the url is compressed on disc
        /// </summary>
        [TableDataOrder(3)]
        public bool Comp;

        /// <summary>
        /// The shard that contains this url.
        /// In advanced scenarios the url may be split onto diffrent discs.
        /// </summary>
        [TableDataOrder(3)]
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
