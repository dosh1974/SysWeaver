using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {
        #region Transformers

        sealed class Transformer
        {
            public Func<HttpRequestTransformerState, ValueTask<bool>>[] Transformers;
        }

        readonly SemiFrozenDictionary<String, Transformer> Transformers = new(StringComparer.Ordinal);

        public void AddTransformer(String fileExtension, Func<HttpRequestTransformerState, ValueTask<bool>> transformer)
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

        public bool RemoveTransformer(String fileExtension, Func<HttpRequestTransformerState, ValueTask<bool>> transformer)
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

        public void RegisterTransformerService(IHttpTransformerService service)
        {
            foreach (var x in service.GetTransformers())
                AddTransformer(x.Key, x.Value);
        }

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
