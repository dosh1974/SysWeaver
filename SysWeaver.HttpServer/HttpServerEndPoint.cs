using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.Net
{

    public class HttpServerEndPoint : IHttpServerEndPoint
    {

        public readonly String Uri;
        public readonly HttpServerEndpointTypes Type;
        public readonly String Method;
        public readonly String Mime;
        public readonly int ClientCacheDuration;
        public readonly int RequestCacheDuration;
        public readonly bool UseStream;
        public readonly String Compression;
        public readonly String Decoder;
        public readonly IReadOnlyList<String> Auth;
        public readonly String Location;
        public readonly long? Size;
        public readonly DateTime LastModified;
        public readonly IReadOnlySet<String> Props;

        /// <summary>
        /// Constructor for files
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="method"></param>
        /// <param name="clientCacheDuration"></param>
        /// <param name="requestCacheDuration"></param>
        /// <param name="useStream"></param>
        /// <param name="compression"></param>
        /// <param name="decoder"></param>
        /// <param name="auth"></param>
        /// <param name="type"></param>
        /// <param name="location"></param>
        /// <param name="size"></param>
        /// <param name="lastModified"></param>
        /// <param name="mime"></param>
        /// <param name="props">A custom hash set of properties (use only lowercase)</param>
        public HttpServerEndPoint(string uri, string method, int clientCacheDuration, int requestCacheDuration, bool useStream, string compression, string decoder, IReadOnlyList<String> auth, HttpServerEndpointTypes type, String location, long? size, DateTime lastModified, String mime, IReadOnlySet<String> props)
        {
            Uri = uri;
            Type = type;
            Method = method;
            ClientCacheDuration = clientCacheDuration;
            RequestCacheDuration = requestCacheDuration;
            UseStream = useStream;
            Compression = compression;
            Decoder = decoder;
            Auth = auth;
            Location = location;
            Size = size;
            LastModified = lastModified;
            Mime = mime;
            Props = props.Freeze();
        }

        /// <summary>
        /// Constructor for folders
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="location"></param>
        /// <param name="lastModified"></param>
        /// <param name="type"></param>
        /// <param name="method"></param>
        public HttpServerEndPoint(string uri, String location, DateTime lastModified, HttpServerEndpointTypes type = HttpServerEndpointTypes.Folder, String method = null)
        {
            Uri = uri;
            Location = location;
            LastModified = lastModified;
            Type = type;
            Method = method;
        }


        public override string ToString() => String.Concat('"', Uri, "\" @ ", Location);


        string IHaveUri.Uri => Uri;

        HttpServerEndpointTypes IHttpServerEndPoint.Type => Type;

        string IHttpServerEndPoint.Method => Method;

        bool IHttpServerEndPoint.UseStream => UseStream;

        int IHttpServerEndPoint.ClientCacheDuration => ClientCacheDuration;

        int IHttpServerEndPoint.RequestCacheDuration => RequestCacheDuration;

        string IHttpServerEndPoint.CompPreference => Compression;

        string IHttpServerEndPoint.PreCompressed => Decoder;
        IReadOnlyList<String> IHttpServerEndPoint.Auth => Auth;
        string IHttpServerEndPoint.Location=> Location;
        long? IHttpServerEndPoint.Size => Size;

        DateTime IHttpServerEndPoint.LastModified => LastModified;

        string IHttpServerEndPoint.Mime => Mime;

    }
}
