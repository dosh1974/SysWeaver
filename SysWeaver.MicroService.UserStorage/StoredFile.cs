using System;
using SysWeaver.Data;
using SysWeaver.Net;

namespace SysWeaver.MicroService
{
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
        /// In advanced scenarios the files may be split onto diffrent discs.
        /// </summary>
        [TableDataOrder(3)]
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
