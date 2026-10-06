using System;
using System.Collections.Generic;
using System.Buffers;
using System.Linq;
using System.Text;
using System.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace SysWeaver
{

/// <summary>
/// A translatable text extracted from a file (ex: html or js), that has been replaced by a variable in <see cref="LanguageTemplate.Text"/>.
/// </summary>
    public sealed class LanguageTemplateVar
    {
#if DEBUG
        public override string ToString() =>
            Context == null
            ?
                String.Concat(VarName, " = \"", Text, '"')
            :
                String.Concat(VarName, " = \"", Text, "\" (", Context, ')');
#endif//DEBUG


        /// <summary>
        /// The name of the variable (without the "${" and "}" tokens) that replaced the text.
        /// </summary>
        public readonly String VarName;
        /// <summary>
        /// The original (untranslated) text.
        /// </summary>
        public readonly String Text;
        /// <summary>
        /// Optional context that helps a translator (or translation service) interpret the text, may be null.
        /// </summary>
        public readonly String Context;

        /// <summary>
        /// Create a translatable text variable.
        /// </summary>
        /// <param name="varName">The name of the variable (without the "${" and "}" tokens).</param>
        /// <param name="text">The original (untranslated) text.</param>
        /// <param name="context">Optional context, may be null.</param>
        public LanguageTemplateVar(string varName, string text, string context)
        {
            VarName = varName;
            Text = text;
            Context = context;
        }
    }

/// <summary>
/// A file (ex: html or js) where translatable texts have been replaced by "${Var}" variables, used by the HTTP server to serve translated (localized) versions of files.
/// Handlers that create templates for a given file extension are registered using <see cref="AddHandler(string, LangHandler)"/> (ex: by the SysWeaver.FileTranslation assembly).
/// </summary>
    public sealed class LanguageTemplate
    {

/// <summary>
/// Create a language template from the content of a file.
/// </summary>
/// <param name="text">The file content.</param>
/// <param name="willTranslate">True if the file will be translated (server side).</param>
/// <param name="allowBrowserTranslation">True if the browser is allowed to translate the content (client side).</param>
/// <returns>A language template.</returns>
        public delegate LanguageTemplate LangHandler(String text, bool willTranslate, bool allowBrowserTranslation);

        /// <summary>
        /// Map for handling different extensions, the key is the lower cased extension without a leading '.' (ex: "html").
        /// A new immutable snapshot is published whenever a handler is added or removed, so reads are lock free.
        /// </summary>
        public static IReadOnlyDictionary<String, LangHandler> ExtBuilders => RoData;
        

        static readonly Object Lock = new object();
        static readonly Dictionary<String, LangHandler> Data = new (StringComparer.Ordinal);
        static IReadOnlyDictionary<String, LangHandler> RoData = new Dictionary<String, LangHandler>(StringComparer.Ordinal).Freeze();


        /// <summary>
        /// Add an extension handler
        /// </summary>
        /// <param name="ext">The file extension to add a handler for (case insensitive, a leading '.' is ignored)</param>
        /// <param name="fn">The function that create a language template for that extension</param>
        /// <returns>True if the handler was added, else false (a handler for the extension already exists)</returns>
        public static bool AddHandler(String ext, LangHandler fn)
        {
            lock (Lock)
            {
                var d = Data;
                if (!d.TryAdd(ext.FastTrimStartToLower('.'), fn))
                    return false;
                RoData = d.Freeze();
            }
            return true;
        }

        /// <summary>
        /// Remove an extension handler
        /// </summary>
        /// <param name="ext">The file extension to remove the handler for (case insensitive, a leading '.' is ignored)</param>
        /// <returns>True if the handler was removed, else false</returns>
        public static bool RemoveHandler(String ext)
        {
            lock (Lock)
            {
                var d = Data;
                if (!d.TryRemove(ext.FastTrimStartToLower('.'), out var fn))
                    return false;
                RoData = d.Freeze();
            }
            return true;
        }

        /// <summary>
        /// The modified text (with variables using the ${Var} syntax).
        /// </summary>
        public readonly String Text;

        /// <summary>
        /// The variables used in the text.
        /// </summary>
        public readonly IReadOnlyList<LanguageTemplateVar> Vars;


        /// <summary>
        /// Cache of translated variable values, the key is the language code and the value maps variable names to translated texts.
        /// Entries expire after 24 hours.
        /// </summary>
        public readonly FastMemCache<String, IReadOnlyDictionary<String, String>> LangVars = new (TimeSpan.FromHours(24), StringComparer.Ordinal);

        /// <summary>
        /// Create a language template.
        /// </summary>
        /// <param name="text">The modified text, with translatable texts replaced by "${Var}" variables.</param>
        /// <param name="vars">The variables used in the text (copied to an array).</param>
        public LanguageTemplate(String text, IEnumerable<LanguageTemplateVar> vars)
        {
            Text = text;
            Vars = vars.ToArray();
        }





    }



}
