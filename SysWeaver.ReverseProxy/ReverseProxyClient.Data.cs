using System;
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


        }


    }

}
