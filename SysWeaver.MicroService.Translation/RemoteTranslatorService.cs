using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Net;
using SysWeaver.Remote;
using SysWeaver.Translation;

namespace SysWeaver.MicroService
{



    /// <summary>
    /// A service that fetches data from a remote service and caches request in memory
    /// </summary>
    [WebApiUrl("translator")]
    public sealed class RemoteTranslatorService : IHaveStats, IPerfMonitored, IInternalTranslator
    {
        public RemoteTranslatorService(ServiceManager manager, RemoteTranslatorParams p = null)
        {
            p = p ?? new RemoteTranslatorParams();
            FromToCacheDuration = TimeSpan.FromSeconds(Math.Max(30, p.FromToCacheDuration));
            MemCaches[0] = new FastMemCache<string, string>(TimeSpan.FromSeconds(Math.Max(30, p.ShortMemCacheDuration)), StringComparer.Ordinal);
            MemCaches[1] = new FastMemCache<string, string>(TimeSpan.FromSeconds(Math.Max(30, p.MediumMemCacheDuration)), StringComparer.Ordinal);
            MemCaches[2] = new FastMemCache<string, string>(TimeSpan.FromSeconds(Math.Max(30, p.LongMemCacheDuration)), StringComparer.Ordinal);
            p.GetUserPassword(out var user, out var password);
            int maxConcurrency = Math.Max(1, p.MaxConcurrency);
            var t = new RemoteConnection
            {
                User = user,
                Password = password,
                BaseUrl = p.BaseUrl,
                TimeoutInMilliSeconds = 300_000,
                MaxConcurrency = maxConcurrency,
            }.Create<ITranslator>();
            if (manager != null)
            {
                manager.Register(t, null, false);
                Man = manager;
            }
            T = t;
            var rapi = t as IRemoteApi;
            rapi.OnCallBegin += Rapi_OnCallBegin;
            TranslatorExtensionHandlers.Register();
            Lock = new AsyncLock(maxConcurrency);
            PruneTask = new PeriodicTask(Prune, 60000);
        }




        readonly AsyncLock Lock;


        readonly ServiceManager Man;

        public override string ToString() => T?.ToString() ?? "Remote translator";

        void Rapi_OnCallBegin(long id, string url, HttpEndPointTypes type, int timeout, string requestPayloadSerializer, ReadOnlyMemory<byte> requestPayload, int requestPayloadSize)
        {
            Interlocked.Increment(ref ApiCalls);
          
        }

        long ApiCalls;

        bool Prune()
        {
            using var _ = PerfMon.Track(nameof(Prune));
            foreach (var t in MemCaches)
                t.Prune();
            return true;
        }

        PeriodicTask PruneTask;

        public void Dispose()
        {
            TranslatorExtensionHandlers.Unregister();
            Interlocked.Exchange(ref PruneTask, null)?.Dispose();
            var t = Interlocked.Exchange(ref T, null);
            if (t != null)
            {
                Man?.Unregister(t);
                t.Dispose();
            }
        }

        ITranslator T;


        readonly FastMemCache<String, String>[] MemCaches = new FastMemCache<string, string>[3];
        readonly TimeSpan FromToCacheDuration;
        static String GetFrom(string from)
        {
            from = from?.Trim();
            return String.IsNullOrEmpty(from) ? "*" : from;
        }
        static String GetTo(string to)
            => to.Trim();

        public static String ComputeHash(String from, String to, String text, String context)
        {
            var data = Encoding.UTF8.GetBytes(String.Join('|', from, to, text, context));
            var hash = SHA512.HashData(data).ToHex();
            return hash;
        }

        /// <summary>
        /// Tos and from must be validated
        /// </summary>
        /// <param name="text"></param>
        /// <param name="tos"></param>
        /// <param name="from"></param>
        /// <param name="context"></param>
        /// <param name="effort"></param>
        /// <param name="retention"></param>
        /// <returns></returns>
        async Task<string[]> InternalValidated(string text, string[] tos, string from, String context, TranslationEffort effort, TranslationCacheRetention retention)
        {
            var count = tos.Length;
            if (String.IsNullOrEmpty(text))
                return ArrayExt.Create(count, text);
            var res = GC.AllocateUninitializedArray<String>(count);
            var cache = MemCaches[(int)retention];
            for (int i = 0; i < count; ++i)
            {
                var to = tos[i];
                Interlocked.Increment(ref TranslationCount);
                res[i] = await cache.GetOrUpdateAsync(ComputeHash(text, to, from, context), hash => InternalTranslate(from, to, text, context, effort, retention)).ConfigureAwait(false);
            }
            return res;
        }

        #region ITranslator

        /// <summary>
        /// Translate some text to one or more languages
        /// </summary>
        /// <param name="request">Paramaters</param>
        /// <returns>Translations in the same order as specified in the parameters</returns>
        public Task<string[]> Translate(TranslateRequest request)
            => Translate(request.Text, request.To, request.From, request.Context, request.Effort, request.Retention);

        public async Task<string[]> Translate(string text, string to, string from, String context, TranslationEffort effort, TranslationCacheRetention retention)
        {
            if (!text.AnyLetter())
                return ArrayExt.Create(to.Split(',').Length, text);
            var fromO = from;
            from = GetFrom(from);
            if (from != "*")
            {
                from = await CanFrom(from).ConfigureAwait(false);
                if (from == null)
                    throw new Exception("Can't translate from \"" + fromO + "\"");
            }
            var tos = to.Split(',');
            var count = tos.Length;
            var res = GC.AllocateUninitializedArray<string>(count);
            var cache = MemCaches[(int)retention];
            for (int i = 0; i < count; ++i)
            {
                to = GetTo(tos[i]);
                Interlocked.Increment(ref TranslationCount);
                res[i] = await cache.GetOrUpdateAsync(ComputeHash(text, to, from, context), hash => InternalTranslate(from, to, text, context, effort, retention)).ConfigureAwait(false);
            }
            return res;
        }

        /// <summary>
        /// Translate multiple texts to one or more languages
        /// </summary>
        /// <param name="request">Paramaters</param>
        /// <returns>Translations in the same order as specified in the parameters</returns>
        public Task<string[]> TranslateMultiple(TranslateMultipleRequest request)
            => TranslateMultiple(request.Texts, request.To, request.From, request.Context, request.Effort, request.Retention);

        public async Task<string[]> TranslateMultiple(string[] texts, string to, string from, String context, TranslationEffort effort, TranslationCacheRetention retention)
        {
            var c = texts.Length;
            if (c <= 0)
                return null;
            if (c == 1)
                return await Translate(texts[0], to, from, context, effort, retention).ConfigureAwait(false);
            var fromO = from;
            from = GetFrom(from);
            if (from != "*")
            {
                from = await CanFrom(from).ConfigureAwait(false);
                if (from == null)
                    throw new Exception("Can't translate from \"" + fromO + "\"");
            }
            var tos = to.Split(',');
            var count = tos.Length;
            for (int i = 0; i < count; ++i)
            {
                to = GetTo(tos[i]);
                to = await CanTo(to).ConfigureAwait(false);
                if (to == null)
                    throw new Exception("Can't translate to \"" + tos[i] + "\"");
                tos[i] = to;
            }
            Task<String[]>[] tasks = new Task<string[]>[c];
            for (int i = 0; i < c; ++i)
            {
                var text = texts[i];
                if (text.AnyLetter())
                    tasks[i] = InternalValidated(texts[i], tos, from, context, effort, retention);
                else
                    tasks[i] = Task.FromResult(ArrayExt.Create(count, text));
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            String[] res = GC.AllocateUninitializedArray<string>(count * c);
            for (int i = 0, o = 0; i < c; ++i, o += count)
                Array.Copy(tasks[i].Result, 0, res, o, count);
            return res;
        }

        /// <summary>
        /// Translate some text to a new language
        /// </summary>
        /// <param name="request">Paramaters</param>
        /// <returns>Translated text</returns>
        public Task<string> TranslateOne(TranslateRequest request)
            => TranslateOne(request.Text, request.To, request.From, request.Context, request.Effort, request.Retention);

        public async Task<string> TranslateOne(string text, string to, string from, String context, TranslationEffort effort, TranslationCacheRetention retention)
        {
            if (!text.AnyLetter())
                return text;
            var fo = from;
            from = GetFrom(from);
            if (from != "*")
            {
                from = await CanFrom(from).ConfigureAwait(false);
                if (from == null)
                    throw new Exception("Can't translate from \"" + fo + "\"");
            }
            var too = to;
            to = GetTo(to);
            Interlocked.Increment(ref TranslationCount);
            return await MemCaches[(int)retention].GetOrUpdateAsync(ComputeHash(text, to, from, context), hash => InternalTranslate(from, to, text, context, effort, retention)).ConfigureAwait(false);
        }


        class Cache
        {
            public Cache(IReadOnlyList<String> valid)
            {
                When = DateTime.UtcNow;
                ValidList = valid;
                var s = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
                foreach (var x in valid)
                {
                    s.TryAdd(x, x);
                    var ss = x.Split('-');
                    if (ss.Length > 1)
                        s.TryAdd(ss[0], x);
                }
                ValidSet = s.Freeze();
            }

            public readonly DateTime When;
            public readonly IReadOnlyList<String> ValidList;
            public readonly IReadOnlyDictionary<String, String> ValidSet;

        }

        volatile Cache SourceCache;
        volatile Cache TargetCache;

        async Task<Cache> GetSource()
        {
            var c = SourceCache;
            if ((c != null) && ((DateTime.UtcNow - c.When) < FromToCacheDuration))
                return c;
            IReadOnlyList<String> d;
            using (await Lock.Lock().ConfigureAwait(false))
                d = await T.GetSupportedSourceLanguages().ConfigureAwait(false);
            c = new Cache(d);
            Interlocked.Exchange(ref SourceCache, c);
            return c;
        }


        async Task<Cache> GetTarget()
        {
            var c = TargetCache;
            if ((c != null) && ((DateTime.UtcNow - c.When) < FromToCacheDuration))
                return c;
            IReadOnlyList<String> d;
            using (await Lock.Lock().ConfigureAwait(false))
                d = await T.GetSupportedTargetLanguages().ConfigureAwait(false);
            c = new Cache(d);
            Interlocked.Exchange(ref TargetCache, c);
            return c;
        }

        /// <summary>
        /// Return a list of supported source languages
        /// </summary>
        /// <returns>A list of supported source languages</returns>
        public async Task<IReadOnlyList<String>> GetSupportedSourceLanguages()
        {
            var c = await GetSource().ConfigureAwait(false);
            return c.ValidList;
        }

        /// <summary>
        /// Return a list of supported target languages
        /// </summary>
        /// <returns>A list of supported target languages</returns>
        public async Task<IReadOnlyList<String>> GetSupportedTargetLanguages()
        {
            var c = await GetTarget().ConfigureAwait(false);
            return c.ValidList;
        }

        /// <summary>
        /// Returns a formatted from language if it's valid, else null
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public async Task<String> CanFrom(String from)
        {
            var c = await GetSource().ConfigureAwait(false);
            return c.ValidSet.TryGetValue(from, out var result) ? result : null;
        }

        /// <summary>
        /// Returns a formatted to language if it's valid, else null
        /// </summary>
        /// <param name="to"></param>
        /// <returns></returns>
        public async Task<String> CanTo(String to)
        {
            var c = await GetTarget().ConfigureAwait(false);
            return c.ValidSet.TryGetValue(to, out var result) ? result : null;
        }

        #endregion//ITranslator


        long TranslationCount;
        long CacheMissCount;

        readonly ExceptionTracker RemoteFails = new ExceptionTracker();

        async Task<String> InternalTranslate(String from, String to, String text, String context, TranslationEffort effort, TranslationCacheRetention retention)
        {
            try
            {
                if (text.FastStartsWith(TranslationTools.NoTranslatePrefix))
                    return text.Substring(TranslationTools.NoTranslatePrefixLength);
                String translated;
                using (await Lock.Lock().ConfigureAwait(false))
                    translated = await T.TranslateOne(new TranslateRequest
                    {
                        From = from,
                        To = to,
                        Context = context,
                        Text = text,
                        Effort = effort,
                        Retention = retention,
                    }).ConfigureAwait(false);
                Interlocked.Increment(ref CacheMissCount);
                return translated;
            }
            catch (Exception ex)
            {
                RemoteFails.OnException(ex);
                return null;
            }
        }



        static readonly String StatName = nameof(RemoteTranslatorService);

        public PerfMonitor PerfMon { get; private set; } = new PerfMonitor(StatName);

        static readonly String[] CacheNames = [
                "Cache.Short.",
                "Cache.Medium.",
                "Cache.Long.",
            ];

        public IEnumerable<Stats> GetStats()
        {
            var count = Interlocked.Read(ref TranslationCount);
            yield return new Stats(StatName, "Remote", Interlocked.Read(ref ApiCalls), "Total number of remote requests (including language support)");
            yield return new Stats(StatName, "Count", count, "Total number of translations requested");
            for (int i = 0; i < 3; ++ i)
                foreach (var x in MemCaches[i].GetStats(StatName, CacheNames[i]))
                    yield return x;
            foreach (var x in RemoteFails.GetStats(StatName, "Remote."))
                yield return x;
        }

        #region IInternalTranslator    

        public Task<string> RequestOne(string from, string to, string text, string context, TranslationEffort effort, TranslationCacheRetention retention)
            => InternalTranslate(from, to, text, context, effort, retention);

        public IReadOnlyList<string> SupportedSourceLanguages()
        {
            var c = SourceCache;
            if (c == null)
            {
                GetSupportedSourceLanguages().RunAsync();
                c = SourceCache;
            }
            if (c == null)
                return null;
            return c.ValidList;
        }

        public IReadOnlyList<string> SupportedTargetLanguages()
        {
            var c = TargetCache;
            if (c == null)
            {
                GetSupportedTargetLanguages().RunAsync();
                c = TargetCache;
            }
            if (c == null)
                return null;
            return c.ValidList;
        }

        public string CanTranslateFrom(string from)
        {
            var c = SourceCache;
            if (c == null)
            {
                GetSupportedSourceLanguages().RunAsync();
                c = SourceCache;
            }
            if (c == null)
                return null;
            return c.ValidSet.TryGetValue(from, out var result) ? result : null;
        }

        public string CanTranslateTo(string to)
        {
            var c = TargetCache;
            if (c == null)
            {
                GetSupportedTargetLanguages().RunAsync();
                c = TargetCache;
            }
            if (c == null)
                return null;
            return c.ValidSet.TryGetValue(to, out var result) ? result : null;
        }

        #endregion//IInternalTranslator    

        /// <summary>
        /// Clears the internal memory cache so all translations have to be reloaded from the remote server
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiAudit("Translation")]
        public bool RefreshFromServer()
        {
            foreach (var x in MemCaches)
                x.Clear();
            return true;
        }

    }


}
