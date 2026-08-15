using System;
using System.Collections.Generic;
using System.Threading;
using SysWeaver.Data;
using SysWeaver.IsoData;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {
        [TableDataPrimaryKey(nameof(LocalUrl))]
        sealed class CacheData
        {
            public CacheData(KeyValuePair<String, HttpCacheEntry> cs, DateTime utcNow)
            {
                var s = cs.Value;
                var p = cs.Key.Split('\n');
                var pl = p.Length;
                if (pl >= 4)
                {
                    var l = p[3];
                    Flag = l;
                    Language = IsoLanguage.TryGetName(l)?.Name ?? l;
                }
                LocalUrl = s.LocalUrl;
                LastUsed = new DateTime(Interlocked.Read(ref s.LastUsed), DateTimeKind.Utc);
                Expires = new DateTime(s.Expires, DateTimeKind.Utc);
                Size = s.Length;
            }   

            /// <summary>
            /// The url of the cached asset
            /// </summary>
            [TableDataUrl("{0}", "../{2}")]
            public readonly String LocalUrl;


            /// <summary>
            /// The flag of the language (auto translated to)
            /// </summary>
            [TableDataIsoLanguageImage]
            public readonly String Flag;

            /// <summary>
            /// The language (auto translated to)
            /// </summary>
            public readonly String Language;

            /// <summary>
            /// Size of the cached asset
            /// </summary>
            [TableDataByteSize]
            public readonly long Size;

            /// <summary>
            /// The time when this expires
            /// </summary>
            public readonly DateTime Expires;


            /// <summary>
            /// The time when this was last accessed
            /// </summary>
            public readonly DateTime LastUsed;



        }

        


    }

}
