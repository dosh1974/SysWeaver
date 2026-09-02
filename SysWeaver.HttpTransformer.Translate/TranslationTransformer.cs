using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.Compression;
using SysWeaver.Net;
using SysWeaver.Translation;

namespace SysWeaver.HttpTransformer
{

    public sealed class TranslationTransformerParams : CachedTransformerParams
    {

    }


    sealed class FileTranslationTransformer : ICachedTransformer
    {
        public FileTranslationTransformer(String ext, LanguageTemplate.LangHandler langHandler)
        {
            Ext = ext;
            Info = ext;
            Handler = langHandler;
            
        }

        readonly String Ext;
        readonly LanguageTemplate.LangHandler Handler;

        public string Info { get; init; }

        public CachedTransformerBuildStrategies BuildStrategy { get; } = CachedTransformerBuildStrategies.AlwaysDirect;

        public CachedTransformerEntry Validate(CachedTransformer service, CachedTransformerFile info)
        {
            var language = info.State.Request.Language;
            if (String.IsNullOrEmpty(language))
                return null;
            var baseName = info.BaseName;
            var name = String.Concat(baseName, '[', language, "].", Ext);
            var fi = new FileInfo(name);
            if (!fi.Exists)
                return null;
            var orgSize = CachedTransformer.ReadOrg(baseName);
            if (orgSize < 0)
                return null;
            return new CachedTransformerEntry
            {
                Completed = true,
                OrgSize = orgSize,
                Files = CachedTransformer.GetValidSorted([new FileHttpRequestHandler(Tuple.Create(info.Mime, true), fi, CachedTransformer.Options, true, null, true)], orgSize),
            };
        }

        public async Task<FileHttpRequestHandler[]> Build(CachedTransformer service, CachedTransformerFile info, ReadOnlyMemory<byte> inputData, CachedTransformerEntry entry)
        {
            var language = info.State.Request.Language;
            if (String.IsNullOrEmpty(language))
                return null;
            var baseName = info.BaseName;
            var name = String.Concat(baseName, '[', language, "].", Ext);
            var fi = new FileInfo(name);
            if (fi.Exists)
            {
                if (fi.Length > 0)
                    return [new FileHttpRequestHandler(Tuple.Create(info.Mime, true), fi, CachedTransformer.Options, true, null, true)];
            }
            var decoder = info.Decoder;
            var orgLen = inputData.Length;
            if (decoder != null)
                inputData = decoder.GetDecompressed(inputData.Span);
            var text = Encoding.UTF8.GetString(inputData.Span);
            var tr = info.State.Request.Translator;
            var langTemp = Handler(text, tr != null, tr == null);
            var tt = new TextTemplate(langTemp.Text, "${", "}", false, false);
            FileHttpRequestHandler file = null;
            if (tt.HaveVars)
            {
                var vars = langTemp.Vars;
                var vcount = vars.Count;
                var d = new Dictionary<String, String>(vcount, StringComparer.Ordinal);
                if (tr != null)
                {
                    var vals = await vars.ConvertAsyncValue(x => tr.TranslateOne(new TranslateRequest
                    {
                        From = "en",
                        To = language,
                        Effort = TranslationEffort.High,
                        Retention = TranslationCacheRetention.Long,
                        ContentType = TranslationContentTypes.Text,
                        Text = x.Text,
                        Context = x.Context,
                    })).ConfigureAwait(false);
                    for (int i = 0; i < vcount; ++i)
                        d[vars[i].VarName] = vals[i];
                }
                else
                {
                    for (int i = 0; i < vcount; ++i)
                    {
                        var x = vars[i];
                        d[x.VarName] = x.Text;
                    }
                }
                text = tt.Get(d);

                var tempName = name + CachedTransformer.TempExt;
                try
                {
                    await File.WriteAllTextAsync(tempName, text).ConfigureAwait(false);
                    await PathExt.TryMoveFileAsync(tempName, name).ConfigureAwait(false);
                    fi = new FileInfo(name);
                    if (fi.Exists && (fi.Length > 0))
                        file = new FileHttpRequestHandler(Tuple.Create(info.Mime, true), fi, CachedTransformer.Options, true, null, true);
                }
                finally
                {
                    await PathExt.TryDeleteFileAsync(tempName).ConfigureAwait(false);
                }
            }
            await CachedTransformer.SaveOrg(baseName, orgLen).ConfigureAwait(false);
            return CachedTransformer.GetValidSorted([file], orgLen);
        }


    }


    public sealed class TranslationTransformer : CachedTransformer
    {



        public TranslationTransformer(TranslationTransformerParams p = null)
            : base(p ?? new TranslationTransformerParams())
        {
            p = p ?? new TranslationTransformerParams();
            TranslatorExtensionHandlers.Register();
            foreach (var x in LanguageTemplate.ExtBuilders)
                Add(x.Key, new FileTranslationTransformer(x.Key, x.Value));
        }

        /*
        readonly ICompType[] Methods;
        readonly String[] MethodsExtensions;

        */

    }

}
