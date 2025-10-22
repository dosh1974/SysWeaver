using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.Translation;

namespace SysWeaver.MicroService
{


    [WebApiUrl("translator/debug")]
    [RequiredDep(typeof(ITranslator))]
    public sealed class TranslatorDebugService : IRunTimeWebApiAuth
    {
        const int ClientCache = 30;
        const int RequestCache = 30;
        const int RefreshRate = 60000;

        public override string ToString() => "Provides debug tables and API's for an ITranslator";

        public TranslatorDebugService(ServiceManager manager, TranslatorDebugParams p = null)
        {
            p = p ?? new TranslatorDebugParams();
            T = manager.Get<ITranslator>();
            MethodAuths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "*", p.Auth },
            }.Freeze();
        }

        readonly ITranslator T;
        public IReadOnlyDictionary<string, string> MethodAuths { get; init; }

        /// <summary>
        /// Return a list of supported source languages as a data table
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns>A list of supported source languages</returns>
        [WebApi]
        [WebApiClientCache(ClientCache)]
        [WebApiRequestCache(RequestCache)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/Translator/{0}", "Source languages", null, "IconTableTranslateFrom")]
        public async Task<TableData> SourceLanguagesTable(TableDataRequest r) => TableDataTools.Get(r, RefreshRate, (await T.GetSupportedSourceLanguages().ConfigureAwait(false)).Select(TranslatorDebugServiceData.FromString));


        /// <summary>
        /// Return a list of supported target languages as a data table
        /// </summary>
        /// <param name="r">Paramaters</param>
        /// <returns>A list of supported target languages</returns>
        [WebApi]
        [WebApiClientCache(ClientCache)]
        [WebApiRequestCache(RequestCache)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        [WebMenuTable(null, "Debug/Translator/{0}", "Target languages", null, "IconTableTranslateTo")]
        public async Task<TableData> TargetLanguagesTable(TableDataRequest r) => TableDataTools.Get(r, RefreshRate, (await T.GetSupportedTargetLanguages().ConfigureAwait(false)).Select(TranslatorDebugServiceData.FromString));

    }

}
