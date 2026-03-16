using System;

namespace SysWeaver.ReverseProxy
{


    public sealed partial class ReverseProxyClient
    {
        sealed class LocalEndPoint
        {
            public override string ToString() => String.Concat(Name, " => ", BaseUrl);
            public readonly int  NameLen;
            public readonly String Name;
            public readonly String BaseUrl;
            public readonly bool IsInternal;
            public long InProgress;
            public long Completed;

            public LocalEndPoint(string name, string baseUrl)
            {
                Name = name;
                NameLen = name.Length <= 0 ? 0 : (name.Length + 1);
                baseUrl = baseUrl.Trim(TrimChars);
                if (baseUrl.Length > 0)
                    baseUrl += '/';
                BaseUrl = baseUrl;
                IsInternal = baseUrl.IndexOf("://") < 0;
            }
        }


    }

}
