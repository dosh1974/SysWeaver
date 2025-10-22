using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SysWeaver.Translation;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// An service that exposes an API to an internal ITranslator
    /// </summary>
    [WebApiUrl("translator")]
    public sealed class TranslatorApiService : IRunTimeWebApiAuth //, ITranslator // Must implement the ITranslator API but do not expose as an ITranslator
    {

        const int ClientCache = 4 * 60 * 60;
        const int RequestCache = 5 * 60;


        public override string ToString() => "Provides an protected API for an ITranslator";

        public TranslatorApiService(ServiceManager manager, TranslatorApiParams p = null)
        {
            p = p ?? new TranslatorApiParams();
            T = manager.Get<ITranslator>();
            MethodAuths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "*", p.Auth },
            }.Freeze();
        }

        public void Dispose()
        {
        }

        readonly ITranslator T;

        public IReadOnlyDictionary<string, string> MethodAuths { get; init; }


        /// <summary>
        /// Translate some text to one or more languages
        /// </summary>
        /// <param name="request">Paramaters</param>
        /// <returns>Translations in the same order as specified in the parameters</returns>
        [WebApi]
        [WebApiClientCache(ClientCache)]
        [WebApiRequestCache(RequestCache)]
        public Task<string[]> Translate(TranslateRequest request)
            => T.Translate(request);

        /// <summary>
        /// Translate multiple texts to one or more languages
        /// </summary>
        /// <param name="request">Paramaters</param>
        /// <returns>Translations in the same order as specified in the parameters</returns>
        [WebApi]
        [WebApiClientCache(ClientCache)]
        [WebApiRequestCache(RequestCache)]
        public Task<string[]> TranslateMultiple(TranslateMultipleRequest request)
            => T.TranslateMultiple(request);

        /// <summary>
        /// Translate some text to a new language
        /// </summary>
        /// <param name="request">Paramaters</param>
        /// <returns>Translated text</returns>
        [WebApi]
        [WebApiClientCache(ClientCache)]
        [WebApiRequestCache(RequestCache)]
        public Task<string> TranslateOne(TranslateRequest request)
            => T.TranslateOne(request);


        /// <summary>
        /// Returns a formatted from language if it's valid, else null
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(ClientCache)]
        [WebApiRequestCache(RequestCache)]
        public Task<String> CanFrom(String from)
            => T.CanFrom(from);

        /// <summary>
        /// Returns a formatted to language if it's valid, else null
        /// </summary>
        /// <param name="to"></param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(ClientCache)]
        [WebApiRequestCache(RequestCache)]
        public Task<String> CanTo(String to)
            => T.CanTo(to);

        /// <summary>
        /// Return a list of supported source languages
        /// </summary>
        /// <returns>A list of supported source languages</returns>
        [WebApi]
        [WebApiClientCache(ClientCache)]
        [WebApiRequestCache(RequestCache)]
        public Task<IReadOnlyList<String>> GetSupportedSourceLanguages()
            => T.GetSupportedSourceLanguages();

        /// <summary>
        /// Return a list of supported target languages
        /// </summary>
        /// <returns>A list of supported target languages</returns>
        [WebApi]
        [WebApiClientCache(ClientCache)]
        [WebApiRequestCache(RequestCache)]
        public Task<IReadOnlyList<String>> GetSupportedTargetLanguages()
            => T.GetSupportedTargetLanguages();


    }





}
