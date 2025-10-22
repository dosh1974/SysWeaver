using SysWeaver.Compression;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    public sealed class FileHttpRequestHandler : IHttpRequestHandler
    {
        public override string ToString() => Fi.FullName;

        public bool AllowTemplates { get; init;  }


        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }

        /// <summary>
        /// If true, the file's access time is updated whenever the file is read
        /// </summary>
        public readonly bool UpdateAccessTime;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mime">Mime type of the file (null to use the file extension)</param>
        /// <param name="fi">File information</param>
        /// <param name="options">Options for caching etc</param>
        /// <param name="isAccepted"></param>
        /// <param name="decoder">If the file is precompressed, this should be the decoder to use</param>
        /// <param name="updateAccessTime">If true, the file's access time is updated whenever the file is read</param>
        public FileHttpRequestHandler(Tuple<String, bool> mime, FileInfo fi, RequestOptions options, bool isAccepted, ICompDecoder decoder = null, bool updateAccessTime = false)
        {
            if (mime == null)
                mime = MimeTypeMap.GetMimeType(fi.Extension);
            UpdateAccessTime = updateAccessTime;
            Fi = fi;
            Decoder = decoder;
            if (mime.Item2 || options.ForceCache)
            {
                E = Encoding.UTF8;
                Compression = options.Compression;
                bool cache = (fi.Length < options.MaxCacheSize) && (isAccepted || (decoder == null));
                if (cache)
                    RequestCacheDuration = options.RequestCacheDuration;
            }
            AllowTemplates = mime.Item1.FastEndsWith("UTF-8");
            ClientCacheDuration = options.ClientCacheDuration;
            Mime = mime.Item1;
            Auth = options.Auth;
            IsLocalized = options.IsLocalized;
        }

        readonly Encoding E;
        readonly String Mime;
        readonly FileInfo Fi;

        public int ClientCacheDuration { get; init; }
        public int RequestCacheDuration { get; init; }

        public bool IsLocalized { get; init; }

        public bool UseStream => true;


        public HttpCompressionPriority Compression { get; init; }

        public ICompDecoder Decoder { get; init; }

        public IReadOnlyList<String> Auth { get; init; }

        public ValueTask<String> GetCacheKey(HttpServerRequest request) => HttpServerTools.NullStringValueTask;

        public ReadOnlyMemory<byte> GetData(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            useAsync = false;
            return HttpServerTools.ToEtag(Fi.LastWriteTimeUtc);
        }

        public Stream GetStream(HttpServerRequest request)
        {
            request.SetResMime(Mime);
            var fi = Fi;
            if (UpdateAccessTime)
            {
                try
                {
                    fi.LastAccessTimeUtc = DateTime.UtcNow;
                }
                catch
                {
                }
            }
            return fi.OpenRead();
        }

        public Task<Stream> GetStreamAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }
        public Task<ReadOnlyMemory<byte>> GetDataAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

    }



    public sealed class DynamicDataHttpRequestHandler : IHttpRequestHandler
    {

        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }


        public DynamicDataHttpRequestHandler(Tuple<String, bool> mime, Func<HttpServerRequest, Task<ReadOnlyMemory<Byte>>> getBody, RequestOptions options)
        {
            RequestCacheDuration = options.RequestCacheDuration;
            ClientCacheDuration = options.ClientCacheDuration;
            Compression = options.Compression;
            Mime = mime.Item1;
            Auth = options.Auth;
            GetBody = getBody;
            IsLocalized = options.IsLocalized;
        }

        readonly Func<HttpServerRequest, Task<ReadOnlyMemory<Byte>>> GetBody;


        readonly String Mime;

        public int ClientCacheDuration { get; init; }
        public int RequestCacheDuration { get; init; }

        public bool IsLocalized { get; init; }

        public bool UseStream => false;

        public HttpCompressionPriority Compression { get; init; }

        public ICompDecoder Decoder { get; init; }

        public IReadOnlyList<String> Auth { get; init; }

        public ValueTask<String> GetCacheKey(HttpServerRequest request) => HttpServerTools.NullStringValueTask;

        public ReadOnlyMemory<byte> GetData(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public string GetEtag(out bool useAsync, HttpServerRequest request)
        {
            useAsync = true;
            return null;
        }
        public Stream GetStream(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> GetStreamAsync(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }
        public Task<ReadOnlyMemory<byte>> GetDataAsync(HttpServerRequest request)
        {
            request.SetResMime(Mime);
            return GetBody(request);

        }

    }

}
