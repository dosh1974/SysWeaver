using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {
        #region Transformers

        /// <summary>
        /// The chain of transformers registered for a mime type or file extension (replaced, never modified, under a lock).
        /// </summary>
        sealed class Transformer
        {
            public Func<HttpRequestTransformerState, Task<bool>>[] Transformers;
        }

        readonly SemiFrozenDictionary<String, Transformer> Transformers = new(StringComparer.Ordinal);

        /// <summary>
        /// Register a transformer for a mime type (the part before any ';') or a lower cased file extension (without the '.').
        /// Transformers are run (in registration order) for non-dynamic responses that have an etag, unless the query string starts with "raw".
        /// A transformer that returns true replaces the handler (see <see cref="HttpRequestTransformerState"/>), the following transformers see the transformed data.
        /// </summary>
        /// <param name="fileExtension">A mime type or a lower cased file extension, ex: "text/css" or "css"</param>
        /// <param name="transformer">The transformer, exceptions are caught and tracked (the request continues with the untransformed data)</param>
        public void AddTransformer(String fileExtension, Func<HttpRequestTransformerState, Task<bool>> transformer)
        {
            var t = Transformers;
            lock (t)
            {
                bool n = !t.TryGetValue(fileExtension, out var chain);
                if (n)
                    chain = new Transformer();
                chain.Transformers = chain.Transformers.Push(transformer);
                if (n)
                    t.TryAdd(fileExtension, chain);
            }
        }

        /// <summary>
        /// Remove a previously registered transformer.
        /// </summary>
        /// <param name="fileExtension">The mime type or file extension it was registered for</param>
        /// <param name="transformer">The transformer to remove (same delegate instance or an equal delegate)</param>
        /// <returns>True if the transformer was found and removed</returns>
        public bool RemoveTransformer(String fileExtension, Func<HttpRequestTransformerState, Task<bool>> transformer)
        {
            var t = Transformers;
            lock (t)
            {
                if (!t.TryGetValue(fileExtension, out var chain))
                    return false;
                var ct = chain.Transformers;
                var i = ct.IndexOf(transformer);
                if (i < 0)
                    return false;
                var newA = chain.Transformers.RemoveAt(i);
                if (newA.Length == 0)
                {
                    t.TryRemove(fileExtension, out chain);
                    return true;
                }
                chain.Transformers = newA;
                return true;
            }
        }

        /// <summary>
        /// Register all transformers of a service (see <see cref="AddTransformer"/>).
        /// </summary>
        /// <param name="service">The service</param>
        public void RegisterTransformerService(IHttpTransformerService service)
        {
            foreach (var x in service.GetTransformers())
                AddTransformer(x.Key, x.Value);
        }

        /// <summary>
        /// Remove all transformers of a service (the service must return the same transformers as when it was registered).
        /// </summary>
        /// <param name="service">The service</param>
        /// <returns>True if all transformers were found and removed</returns>
        public bool UnregisterTransformerService(IHttpTransformerService service)
        {
            bool ok = true;
            foreach (var x in service.GetTransformers())
                ok &= RemoveTransformer(x.Key, x.Value);
            return ok;
        }

        readonly ExceptionTracker TransformExceptions = new();



        #endregion//Transformers


    }


}
