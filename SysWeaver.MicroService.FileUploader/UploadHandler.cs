using SysWeaver.Auth;
using SysWeaver.Compression;
using SysWeaver.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SysWeaver.Serialization;

namespace SysWeaver.MicroService
{

    sealed class UploadHandler : IHttpRequestHandler
    {
        /// <summary>
        /// Ignore, used internally
        /// </summary>
        public HttpServerRequest Redirected { get; set; }

        public UploadHandler(FileUploaderService fs, IFileRepo repo)
        {
            Fs = fs;
            Repo = repo;
            Auth = Authorization.GetRequiredTokens(repo.UploadAuth);
        }

        readonly IFileRepo Repo;
        readonly FileUploaderService Fs;

        static readonly ISerializer JsonSer = SerManager.Get("json");

        public HttpRequestData Get(HttpServerRequest request)
        {
            throw new NotImplementedException();
        }

        public async Task<HttpRequestData> GetAsync(HttpServerRequest request)
        {
            var res = await Fs.Upload(request, Repo).ConfigureAwait(false);
            return new HttpRequestData(JsonSer.Serialize(res));
        }

        public int ClientCacheDuration => 0;
        public int RequestCacheDuration => 0;


        public HttpCompressionPriority Compression => null;

        public ICompDecoder Decoder => null;

        public IReadOnlyList<String> Auth { get; private set; }

        public ValueTask<String> GetCacheKey(HttpServerRequest request) => TaskExt.NullStringValueTask;

        public String GetEtag(out bool useAsync, HttpServerRequest request)
        {
            request.SetResMime(HttpServerTools.JsonMime);
            useAsync = true;
            return null;
        }

    }





}
