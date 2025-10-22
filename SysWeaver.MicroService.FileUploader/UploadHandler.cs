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

        public async Task<ReadOnlyMemory<Byte>> GetDataAsync(HttpServerRequest request)
        {
            var res = await Fs.Upload(request, Repo).ConfigureAwait(false);
            request.SetResMime(HttpServerTools.JsonMime);
            return JsonSer.Serialize(res);
        }

        public int ClientCacheDuration => 0;
        public int RequestCacheDuration => 0;

        public bool UseStream => false;

        public HttpCompressionPriority Compression => null;

        public ICompDecoder Decoder => null;

        public IReadOnlyList<String> Auth { get; private set; }

        public ValueTask<String> GetCacheKey(HttpServerRequest request) => HttpServerTools.NullStringValueTask;

        public String GetEtag(out bool useAsync, HttpServerRequest request)
        {
            useAsync = true;
            return null;
        }

        public Stream GetStream(HttpServerRequest request) => throw new NotImplementedException();


        public ReadOnlyMemory<Byte> GetData(HttpServerRequest request) => throw new NotImplementedException();

        public Task<Stream> GetStreamAsync(HttpServerRequest request) => throw new NotImplementedException();


    }





}
