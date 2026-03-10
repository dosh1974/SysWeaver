using System;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    public sealed class HttpRequestTransformerState
    {
        public readonly HttpServerRequest Request;
        public readonly String ETag;
        public String Mime;        
        public IHttpRequestHandler Handler;
        public bool UseAsync;

        public HttpRequestTransformerState(HttpServerRequest request, string eTag, string mime, IHttpRequestHandler handler, bool useAsync)
        {
            Request = request;
            ETag = eTag;
            Mime = mime;
            Handler = handler;
            UseAsync = useAsync;
        }


        public async ValueTask<ReadOnlyMemory<Byte>> ReadAllData()
        {
            var t = Handler;
            var data = Request;
            using var i = UseAsync ? await t.GetAsync(data).ConfigureAwait(false) : t.Get(data);
            var s = i.Stream;
            if (s != null)
            {
                using var m = new ArrayPoolStream();
                await s.CopyToAsync(m).ConfigureAwait(false);
                return m.ToArray();
            }else
            {
                var mem = i.GetMemory();
                var l = mem.Length;
                var dest = GC.AllocateUninitializedArray<Byte>(l);
                mem.Span.CopyTo(dest.AsSpan());
                return dest;
            }
        }

    }


}
