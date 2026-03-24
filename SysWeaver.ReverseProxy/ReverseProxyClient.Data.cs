using System;
using System.Threading;
using SysWeaver.Data;

namespace SysWeaver.ReverseProxy
{


    public sealed partial class ReverseProxyClient
    {
        sealed class Data
        {
            public Data(String serverBase, String internalBase, LocalEndPoint p)
            {
                var i = p.IsInternal;
                IsInternal = i;
                var l = p.BaseUrl;
                LocalUrl = String.IsNullOrEmpty(l) ? "(internal root)" : l;
                LocalLink = i ? (internalBase + l) : l;
                var s = p.Name;
                ServerName = String.IsNullOrEmpty(s) ? "(server base url)" : s;
                ServerUrl = (serverBase + s).TrimEnd('-') + '/';

                InProgress = Interlocked.Read(ref p.InProgress);
                Completed = Interlocked.Read(ref p.Completed);

                var f = p.Fails;
                FailCount = f.Count;
                var time = f.LastTime;
                LastFailTime = time == 0 ? DateTime.MinValue : new DateTime(f.LastTime, DateTimeKind.Utc);
                LastFail = f.LastException?.ToString();
            }

            /// <summary>
            /// If true the end point is for the current service.
            /// These are handled directly without any http requests (much faster)
            /// </summary>
            public bool IsInternal;

            /// <summary>
            /// The local url, if it's internal it's the path from the internal root.
            /// If it's empty it's a local url to the root
            /// </summary>
            [TableDataUrl("{0}", "{1}")]
            public String LocalUrl;

            [TableDataHide]
            public String LocalLink;


            /// <summary>
            /// The name of the sub path on the server that is required to access this end point.
            /// If it's empty then there is no need for a sub path on the server.
            /// </summary>
            [TableDataUrl("{0}", "{1}")]
            public String ServerName;

            [TableDataHide]
            public String ServerUrl;

            /// <summary>
            /// Number of requests currently in progress
            /// </summary>
            public long InProgress;

            /// <summary>
            /// Number of requests completed (including failed requests)
            /// </summary>
            public long Completed;

            /// <summary>
            /// Number of fails
            /// </summary>
            public long FailCount;

            /// <summary>
            /// The last time it failed
            /// </summary>
            public DateTime LastFailTime;

            /// <summary>
            /// The last fail message
            /// </summary>
            [TableDataText(64)]
            public String LastFail;

        }


    }

}
