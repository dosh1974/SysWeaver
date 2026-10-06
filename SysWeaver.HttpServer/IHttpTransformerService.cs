using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    /// <summary>
    /// A service that provides file transformers to the http server (registered with <see cref="HttpServerBase.RegisterTransformerService"/>).
    /// </summary>
    public interface IHttpTransformerService
    {
        /// <summary>
        /// Get the transformers to register.
        /// </summary>
        /// <returns>Pairs of key (a file extension or mime type) and transformer function.
        /// A transformer returns true if it handled the request (by updating the <see cref="HttpRequestTransformerState"/>), false to pass.
        /// The sequence is enumerated on registration and again on unregistration, so it should return the same (equal) delegates each time.</returns>
        IEnumerable<KeyValuePair<String, Func<HttpRequestTransformerState, Task<bool>>>> GetTransformers();
    }


}
