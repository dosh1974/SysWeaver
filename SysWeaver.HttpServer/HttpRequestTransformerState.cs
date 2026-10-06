using System;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    /// <summary>
    /// The state passed to a file transformer (see <see cref="HttpServerBase.AddTransformer"/>).
    /// A transformer that handles the request sets <see cref="Handler"/> (and optionally <see cref="Mime"/> and <see cref="UseAsync"/>) to a handler producing the transformed data and returns true.
    /// </summary>
    public sealed class HttpRequestTransformerState
    {
        /// <summary>
        /// The request being handled.
        /// </summary>
        public readonly HttpServerRequest Request;
        /// <summary>
        /// The etag of the source data (can be used as a cache key for the transformed data).
        /// </summary>
        public readonly String ETag;
        /// <summary>
        /// The mime type of the data, update if the transformer changes it.
        /// </summary>
        public String Mime;        
        /// <summary>
        /// The handler that produces the data, replace with a handler producing the transformed data.
        /// </summary>
        public IHttpRequestHandler Handler;
        /// <summary>
        /// True if <see cref="Handler"/> should be read using <see cref="IHttpRequestHandler.GetAsync"/>, false for <see cref="IHttpRequestHandler.Get"/>.
        /// </summary>
        public bool UseAsync;
        /// <summary>
        /// The lower cased file extension of the local url (without the '.'), may be empty.
        /// </summary>
        public String Ext;

        /// <summary>
        /// Create a transformer state.
        /// </summary>
        /// <param name="request">The request being handled</param>
        /// <param name="eTag">The etag of the source data</param>
        /// <param name="mime">The mime type of the data</param>
        /// <param name="handler">The handler that produces the data</param>
        /// <param name="useAsync">True if the handler should be read asynchronously</param>
        /// <param name="ext">The lower cased file extension</param>
        public HttpRequestTransformerState(HttpServerRequest request, string eTag, string mime, IHttpRequestHandler handler, bool useAsync, String ext)
        {
            Request = request;
            ETag = eTag;
            Mime = mime;
            Handler = handler;
            UseAsync = useAsync;
            Ext = ext;
        }


        /// <summary>
        /// Read all data from the current <see cref="Handler"/> into a new managed array.
        /// </summary>
        /// <returns>A copy of the (possibly still compressed, see <see cref="IHttpRequestHandler.Decoder"/>) data</returns>
        public async Task<ReadOnlyMemory<Byte>> ReadAllData()
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
