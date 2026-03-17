using System;
using System.Collections.Generic;
using System.Linq;

namespace SysWeaver.ReverseProxy
{
    static class ReverseProxyTools
    {

        public static readonly IReadOnlySet<String> ContentHeaders = ReadOnlyData.Set(StringComparer.Ordinal,
            "content-length",
            "content-type",
            "content-encoding"
        );


        public static readonly IReadOnlySet<String> IgnoreHeaders = ReadOnlyData.Set(StringComparer.Ordinal,
            "host",
/*            "sec-ch-ua",
            "sec-ch-ua-mobile",
            "sec-ch-ua-platform",
            "sec-fetch-site",
            "sec-fetch-dest",
            "sec-fetch-mode",
            "sec-fetch-user",
*/            "upgrade-insecure-requests",
            "transfer-encoding"
        );


        public static readonly IReadOnlySet<String> QuotedHeaders = ReadOnlyData.Set(StringComparer.Ordinal,
            "if-none-match",
            "etag"
        );

        public static readonly IReadOnlySet<String> AllowMultipleHeaders = ReadOnlyData.Set(StringComparer.Ordinal,
            "set-cookie"
        );


        public static readonly IEnumerable<KeyValuePair<string, IEnumerable<string>>> EmptyHeaders = Array.Empty<KeyValuePair<string, IEnumerable<string>>>();



        public static String[] EncodeHeaders(params IEnumerable<KeyValuePair<string, IEnumerable<string>>>[] headers)
        {
            List<String> h = new List<string>(16);
            var l = headers.Length;
            var ih = IgnoreHeaders;
            var am = AllowMultipleHeaders;
            for (int i = 0; i < l; ++ i)
            {
                var hlist = headers[i];
                if (hlist == null)
                    continue;
                foreach (var kv in hlist)
                {
                    var key = kv.Key;
                    var kl = key.FastToLower();
                    if (ih.Contains(kl))
                        continue;
                    if (am.Contains(kl))
                    {
                        foreach (var v in kv.Value)
                            h.Add(String.Concat(key, ':', v));
                        continue;
                    }
                    h.Add(String.Concat(key, ':', String.Join(',', kv.Value)));
                }
            }
            return h.ToArray();
        }

    }

}
