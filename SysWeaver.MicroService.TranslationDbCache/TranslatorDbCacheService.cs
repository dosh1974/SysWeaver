using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Data;
using SysWeaver.Db;
using SysWeaver.IsoData;
using SysWeaver.Net;
using SysWeaver.Translation;

namespace SysWeaver.MicroService
{



    /// <summary>
    /// A service that uses a memory and database to cache translation request.
    /// </summary>
    [WebApiUrl("translator")]
    public class TranslatorDbCacheService : IHaveStats, IPerfMonitored, ITranslator
    {
        public TranslatorDbCacheService(ServiceManager manager, TranslatorDbCacheParams p = null)
        {
            p = p ?? new TranslatorDbCacheParams();
            MemCaches[0] = new FastMemCache<string, string>(TimeSpan.FromSeconds(Math.Max(30, p.ShortMemCacheDuration)), StringComparer.Ordinal);
            MemCaches[1] = new FastMemCache<string, string>(TimeSpan.FromSeconds(Math.Max(30, p.MediumMemCacheDuration)), StringComparer.Ordinal);
            MemCaches[2] = new FastMemCache<string, string>(TimeSpan.FromSeconds(Math.Max(30, p.LongMemCacheDuration)), StringComparer.Ordinal);
            DbCacheDurations[0] = TimeSpan.FromHours(Math.Max(1, p.ShortDbCacheDuration)).Ticks;
            DbCacheDurations[1] = TimeSpan.FromHours(Math.Max(1, p.MediumDbCacheDuration)).Ticks;
            DbCacheDurations[2] = TimeSpan.FromHours(Math.Max(1, p.LongDbCacheDuration)).Ticks;
            RebuildInputs = p.RebuildInputs;
            MaxRandom = TimeSpan.FromHours(Math.Min(1, p.RandomizeExpiration)).Ticks;
            MaxRandomMask = MaxRandom.MaxMask();

            T = manager.Get<IInternalTranslator>(p.TranslatorInstance);
            var db = new MySqlDbSimpleStack(p);
            Db = db;
            Init(db, p).RunAsync();
            PruneTask = new PeriodicTask(Prune, 60000);
        }

        async ValueTask<bool> Prune()
        {
            using (PerfMon.Track(nameof(Prune)))
            {
                long count = 0;
                foreach (var t in MemCaches)
                {
                    t.Prune();
                    count += t.GetCount();
                }
                Interlocked.Exchange(ref MemCacheSize, count);
                var cc = PruneCount;
                ++cc;
                PruneCount = cc;
                if ((cc & 127) != 1)
                    return true;
            }
            using (PerfMon.Track(nameof(Prune) + ".Db"))
            {
                //  Keep entries longer than the cache expiration, will re-use id's more frequent and probably lead to better db performance
                var now = DateTime.UtcNow.Ticks;
                using var c = await Db.GetAsync().ConfigureAwait(false);
                await c.DeleteAllAsync<DbTranslation>(x => x.Expiration < now).ConfigureAwait(false);
                await c.DeleteAllAsync<DbInput>(x => x.Expiration < now).ConfigureAwait(false);
                return true;
            }
        }

        long MemCacheSize;

        long PruneCount;


        readonly PeriodicTask PruneTask;

        public void Dispose()
        {
            PruneTask.Dispose();
        }

        async Task Init(MySqlDbSimpleStack db, TranslatorDbCacheParams p)
        {
            await db.Init().ConfigureAwait(false);
            using (var c = await db.GetAsync().ConfigureAwait(false))
            {
                await db.InitTable<DbTranslation>(c).ConfigureAwait(false);
                await db.InitTable<DbInput>(c).ConfigureAwait(false);
                await db.ValidateTable<DbTranslation>(c).ConfigureAwait(false);
                await db.ValidateTable<DbInput>(c).ConfigureAwait(false);
            }
        }

        readonly MySqlDbSimpleStack Db;

        readonly long[] DbCacheDurations = new long[3];

        readonly IInternalTranslator T;


        readonly FastMemCache<String, String>[] MemCaches = new FastMemCache<string, string>[3];


        static String GetFrom(string from)
        {
            from = from?.Trim();
            return String.IsNullOrEmpty(from) ? "*" : from;
        }
        static String GetTo(string to)
            => to.Trim();


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
            var cache = MemCaches[(int)effort];
            for (int i = 0; i < count; ++i)
            {
                var to = tos[i];
                Interlocked.Increment(ref TranslationCount);
                res[i] = await cache.GetOrUpdateAsync(DbTranslation.ComputeHash(text, to, from, context), hash => InternalTranslate(hash, from, to, text, context, effort, retention)).ConfigureAwait(false);
            }
            return res;
        }

        #region ITranslator

        /// <summary>
        /// Return a list of supported source languages
        /// </summary>
        /// <returns>A list of supported source languages</returns>
        public IReadOnlyList<string> SupportedSourceLanguages() => T.SupportedSourceLanguages();


        /// <summary>
        /// Return a list of supported target languages 
        /// </summary>
        /// <returns>A list of supported target languages</returns>
        public IReadOnlyList<String> SupportedTargetLanguages() => T.SupportedTargetLanguages();

        /// <summary>
        /// Translate some text to one or more languages
        /// </summary>
        /// <param name="request">Paramaters</param>
        /// <returns>Translations in the same order as specified in the parameters</returns>
        public Task<string[]> Translate(TranslateRequest request)
            => Translate(request.Text, request.To, request.From, request.Context, request.Effort, request.Retention);

        public async Task<string[]> Translate(string text, string to, string from, String context, TranslationEffort effort, TranslationCacheRetention retention)
        {
            if (text.FastStartsWith(TranslationTools.NoTranslatePrefix))
                return ArrayExt.Create(to.Split(',').Length, text.Substring(TranslationTools.NoTranslatePrefixLength));
            if (!text.AnyLetter())
                return ArrayExt.Create(to.Split(',').Length, text);
            var fromO = from;
            var translator = T;
            from = GetFrom(from);
            if (from != "*")
            {
                from = translator.CanTranslateFrom(from);
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
                res[i] = await cache.GetOrUpdateAsync(DbTranslation.ComputeHash(text, to, from, context), hash => InternalTranslate(hash, from, to, text, context, effort, retention)).ConfigureAwait(false);
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
            var translator = T;
            from = GetFrom(from);
            if (from != "*")
            {
                from = translator.CanTranslateFrom(from);
                if (from == null)
                    throw new Exception("Can't translate from \"" + fromO + "\"");
            }
            var tos = to.Split(',');
            var count = tos.Length;
            for (int i = 0; i < count; ++ i)
            {
                to = GetTo(tos[i]);
                to = translator.CanTranslateTo(to);
                if (to == null)
                    throw new Exception("Can't translate to \"" + tos[i] + "\"");
                tos[i] = to;
            }
            Task<String[]>[] tasks = new Task<string[]>[c];
            for (int i = 0; i < c; ++i)
            {
                var text = texts[i];
                if (text.FastStartsWith(TranslationTools.NoTranslatePrefix))
                {
                    tasks[i] = InternalValidated(text.Substring(TranslationTools.NoTranslatePrefixLength), tos, from, context, effort, retention);
                }
                else
                {
                    if (text.AnyLetter())
                        tasks[i] = InternalValidated(text, tos, from, context, effort, retention);
                    else
                        tasks[i] = Task.FromResult(ArrayExt.Create(count, text));
                }
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

        public Task<string> TranslateOne(string text, string to, string from, String context, TranslationEffort effort, TranslationCacheRetention retention)
        {
            if (text.FastStartsWith(TranslationTools.NoTranslatePrefix))
                return Task.FromResult(text.Substring(TranslationTools.NoTranslatePrefixLength));
            if (!text.AnyLetter())
                return Task.FromResult(text);
            var fo = from;
            var translator = T;
            from = GetFrom(from);
            if (from != "*")
            {
                from = translator.CanTranslateFrom(from);
                if (from == null)
                    throw new Exception("Can't translate from \"" + fo + "\"");
            }
            var too = to;
            to = GetTo(to);
            Interlocked.Increment(ref TranslationCount);
            return MemCaches[(int)retention].GetOrUpdateAsync(DbTranslation.ComputeHash(text, to, from, context), hash => InternalTranslate(hash, from, to, text, context, effort, retention));
        }

        public string CanTranslateFrom(string from) => T.CanTranslateFrom(from);

        public string CanTranslateTo(string to) => T.CanTranslateTo(to);

        /// <summary>
        /// Return a list of supported source languages
        /// </summary>
        /// <returns>A list of supported source languages</returns>
        public Task<IReadOnlyList<String>> GetSupportedSourceLanguages()
            => T.GetSupportedSourceLanguages();

        /// <summary>
        /// Return a list of supported target languages
        /// </summary>
        /// <returns>A list of supported target languages</returns>
        public Task<IReadOnlyList<String>> GetSupportedTargetLanguages()
            => T.GetSupportedTargetLanguages();

        /// <summary>
        /// Returns a formatted from language if it's valid, else null
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public Task<String> CanFrom(String from)
            => Task.FromResult(T.CanTranslateFrom(from));

        /// <summary>
        /// Returns a formatted to language if it's valid, else null
        /// </summary>
        /// <param name="to"></param>
        /// <returns></returns>
        public Task<String> CanTo(String to)
            => Task.FromResult(T.CanTranslateTo(to));

        #endregion//ITranslator


        long TranslationCount;
        long DbCacheCount;
        long CacheMissCount;

        readonly ExceptionTracker TranslateFails = new ExceptionTracker();
        readonly ExceptionTracker UpdateFails = new ExceptionTracker();

        readonly bool RebuildInputs = true;


        static readonly IReadOnlySet<String> RetryOn = ReadOnlyData.Set(
            [
                "Error: Incomplete model output due to MaxTokens parameter or token limit exceeded.",
            ]);



        readonly long MaxRandom;
        readonly long MaxRandomMask;

        long GetRandomTicks()
        {
            using var s = SecureRng.Get();
            return s.GetInt64Max(MaxRandom, MaxRandomMask);
        }

        async Task<String> InternalTranslate(String hash, String from, String to, String text, String context, TranslationEffort effort, TranslationCacheRetention retention)
        {
            if (text.FastStartsWith(TranslationTools.NoTranslatePrefix))
                return text.Substring(TranslationTools.NoTranslatePrefixLength);
            using var c = await Db.GetAsync().ConfigureAwait(false);
            var res = await c.FirstOrDefaultAsync<DbTranslation>(x => x.Hash == hash).ConfigureAwait(false);
            if (res != null)
            {
                if (DateTime.UtcNow.Ticks < res.Expiration)
                {
                    Interlocked.Increment(ref DbCacheCount);
                    var data = DbTranslation.FromBlob(res.Translated);
                    if (RebuildInputs)
                        await c.Upsert(new DbInput
                        {
                            Hash = hash,
                            From = from,
                            To = to,
                            Text = text.LimitLength(DbInput.MaxTextLen, ""),
                            Context = context.LimitLength(DbInput.MaxContextLen, ""),
                            Translated = data.LimitLength(DbInput.MaxTranslatedLen, ""),
                            Time = res.Time,
                            Expiration = res.Expiration,
                        }).ConfigureAwait(false);
                    return data;
                }
            }
            else
            {
                res = new DbTranslation
                {
                    Hash = hash,
                };
            }
            String translated;
            try
            {
                var r = RetryOn;
                for (int i = 0; ; ++i)
                {
                    translated = await T.RequestOne(from, to, text, context, effort, retention).ConfigureAwait(false);
                    if (!r.Contains(translated))
                        break;
                    if (i >= 10)
                        throw new Exception("Got error " + translated.ToQuoted() + " more than " + i + " times!");
                    await Task.Delay(10 + (i * i * 20)).ConfigureAwait(false);
                }
            }
            catch (Exception tex)
            {
                TranslateFails.OnException(tex);
                throw;
            }
            Interlocked.Increment(ref CacheMissCount);
            var now = DateTime.UtcNow;
            var time = now.Ticks;
            res.Translated = DbTranslation.ToBlob(translated);
            res.Time = time;
            var exp = now.AddTicks(DbCacheDurations[(int)retention] + GetRandomTicks()).Ticks;
            res.Expiration = exp;
            try
            {
                await c.Upsert(res).ConfigureAwait(false);
                await c.Upsert(new DbInput
                {
                    Hash = hash,
                    From = from,
                    To = to,
                    Text = text.LimitLength(DbInput.MaxTextLen, ""),
                    Context = context.LimitLength(DbInput.MaxContextLen, ""),
                    Translated = translated.LimitLength(DbInput.MaxTranslatedLen, ""),
                    Time = time,
                    Expiration = exp,
                }).ConfigureAwait(false);
            }
            catch (Exception uex)
            {
                UpdateFails.OnException(uex);
            }
            return translated;
        }

        static readonly String StatName = nameof(TranslatorDbCacheService);

        public PerfMonitor PerfMon { get; private set; } = new PerfMonitor(StatName);

        public IEnumerable<Stats> GetStats()
        {
            var count = Interlocked.Read(ref TranslationCount);
            var db = Interlocked.Read(ref DbCacheCount);
            var miss = Interlocked.Read(ref CacheMissCount);
            var mem = count - db - miss;
            yield return new Stats(StatName, "MemSize", Interlocked.Read(ref MemCacheSize), "Approximate number of entries in the memory cache");
            yield return new Stats(StatName, "Count", count, "Total number of translations requested");
            if (count <= 0)
                count = 1;
            yield return new Stats(StatName, "MemCache", (double)(((Decimal)mem) * 100M / (Decimal)count), "The ratio of memory cache hits as a percentage", Data.TableDataNumberAttribute.Percentage);
            yield return new Stats(StatName, "DbCache", (double)(((Decimal)db) * 100M / (Decimal)count), "The ratio of database cache hits as a percentage", Data.TableDataNumberAttribute.Percentage);
            yield return new Stats(StatName, "Miss", (double)(((Decimal)miss) * 100M / (Decimal)count), "The ratio of cache misses as a percentage", Data.TableDataNumberAttribute.Percentage);
            foreach (var x in UpdateFails.GetStats(StatName, "Update."))
                yield return x;
            foreach (var x in TranslateFails.GetStats(StatName, "Translate."))
                yield return x;
        }

        /// <summary>
        /// Display all translations in the database
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.Debug)]
        [WebMenuTable(null, "Debug/Translator/{0}", "Translations DB", null, null)]
        public async Task<TableData> TranslationsTable(TableDataRequest r)
        {
            return await DbData.GetAsTableData<DbInput>(Db, r).ConfigureAwait(false);
        }


        /// <summary>
        /// Get the details of a translation
        /// </summary>
        /// <param name="key">The hash key of the specific translation</param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.Debug)]
        public async Task<Translation> GetTranslation(String key)
        {
            DbInput inp;
            DbTranslation tr;
            using (var db = await Db.GetAsync().ConfigureAwait(false))
            {
                inp = await db.FirstOrDefaultAsync<DbInput>(x => x.Hash == key).ConfigureAwait(false);
                if (inp == null)
                    return null;
                tr = await db.FirstOrDefaultAsync<DbTranslation>(x => x.Hash == key).ConfigureAwait(false);
                if (tr == null)
                    return null;
            }
            return new Translation
            {
                Context = inp.Context,
                From = inp.From,
                FromName = IsoLanguage.TryGetName(inp.From)?.Name,
                Text = inp.Text,
                Time = new DateTime(inp.Time, DateTimeKind.Utc),
                To = inp.To,
                ToName = IsoLanguage.TryGetName(inp.To)?.Name,
                Translated = DbTranslation.FromBlob(tr.Translated),
            };
        }

        /// <summary>
        /// Set a specific translation
        /// </summary>
        /// <param name="r">Parameters</param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.Debug)]
        [WebApiAudit("Translation")]
        public async Task<bool> SetTranslation(SetTranslationRequest r, HttpServerRequest context)
        {
            var key = r.Key;
            var text = r.NewTranslation?.Trim();
            if (String.IsNullOrEmpty(text))
                throw new Exception("Text may not be empty!");
            using var db = await Db.GetAsync().ConfigureAwait(false);
            using var dbt = await db.BeginTransactionAsync().ConfigureAwait(false);
            var inp = await db.FirstOrDefaultAsync<DbInput>(x => x.Hash == key).ConfigureAwait(false);
            if (inp == null)
                return false;
            var tr = await db.FirstOrDefaultAsync<DbTranslation>(x => x.Hash == key).ConfigureAwait(false);
            if (tr == null)
                return false;
            inp.Translated = text;
            tr.Translated = DbTranslation.ToBlob(text);
            var now = DateTime.UtcNow.Ticks;
            inp.Time = now;
            tr.Time = now;
            inp.Expiration = DbInput.Max;
            tr.Expiration = DbInput.Max;
            await db.Upsert(inp).ConfigureAwait(false);
            await db.Upsert(tr).ConfigureAwait(false);
            await dbt.CommitAsync().ConfigureAwait(false);
            foreach (var x in MemCaches)
                x.Remove(key);
            context.Server.InvalidateCache(x =>
                x.FastStartsWith("Api/translator/"));
            return true;
        }


        /// <summary>
        /// Remove a specific translation
        /// </summary>
        /// <param name="key">The hash key of the translation to remove</param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.Debug)]
        [WebApiAudit("Translation")]
        public async Task<bool> DeleteTranslation(String key, HttpServerRequest context)
        {
            using var db = await Db.GetAsync().ConfigureAwait(false);
            using var dbt = await db.BeginTransactionAsync().ConfigureAwait(false);
            await db.DeleteAllAsync<DbInput>(x => x.Hash == key).ConfigureAwait(false);
            await db.DeleteAllAsync<DbTranslation>(x => x.Hash == key).ConfigureAwait(false);
            await dbt.CommitAsync().ConfigureAwait(false);
            foreach (var x in MemCaches)
                x.Remove(key);
            context.Server.InvalidateCache(x =>
                x.FastStartsWith("Api/translator/"));
            return true;
        }


    }


    public sealed class SetTranslationRequest
    {
        /// <summary>
        /// The hash key of the specific translation
        /// </summary>
        public String Key;

        /// <summary>
        /// The new translation
        /// </summary>
        public String NewTranslation;
    }

    public sealed class Translation
    {

        /// <summary>
        /// True if this translation was done manually
        /// </summary>
        public bool IsManual;

        /// <summary>
        /// Time stamp when translation was performed
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// The language of the input text
        /// </summary>
        public String From;

        /// <summary>
        /// The language name of the input text
        /// </summary>
        public String FromName;

        /// <summary>
        /// The text to translate.
        /// Text is truncated to at most 768 chars.
        /// </summary>
        public String Text;

        /// <summary>
        /// The language to translate to
        /// </summary>
        public String To;

        /// <summary>
        /// The language name to translate to
        /// </summary>
        public String ToName;

        /// <summary>
        /// The context used for the translation.
        /// Text is truncated to at most 768 chars.
        /// </summary>
        public String Context;

        /// <summary>
        /// The translated text.
        /// </summary>
        public String Translated;

    }




}
