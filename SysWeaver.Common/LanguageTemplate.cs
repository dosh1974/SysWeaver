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


        public readonly String VarName;
        public readonly String Text;
        public readonly String Context;

        public LanguageTemplateVar(string varName, string text, string context)
        {
            VarName = varName;
            Text = text;
            Context = context;
        }
    }

    public sealed class LanguageTemplate
    {

        public delegate LanguageTemplate LangHandler(String text, bool willTranslate, bool allowBrowserTranslation);

        /// <summary>
        /// Map for handling different extensions
        /// </summary>
        public static IReadOnlyDictionary<String, LangHandler> ExtBuilders => RoData;
        

        static readonly Object Lock = new object();
        static readonly Dictionary<String, LangHandler> Data = new (StringComparer.Ordinal);
        static IReadOnlyDictionary<String, LangHandler> RoData = new Dictionary<String, LangHandler>(StringComparer.Ordinal).Freeze();


        /// <summary>
        /// Add an extension handler
        /// </summary>
        /// <param name="ext">The file extension to add a handler for</param>
        /// <param name="fn">The function that create a language template for that extension</param>
        /// <returns>True if the handler was added, else false</returns>
        public static bool AddHandler(String ext, LangHandler fn)
        {
            lock (Lock)
            {
                var d = Data;
                if (!d.TryAdd(ext.TrimStart('.').FastToLower(), fn))
                    return false;
                RoData = d.Freeze();
            }
            return true;
        }

        /// <summary>
        /// Remove an extension handler
        /// </summary>
        /// <param name="ext">The file extension to remove the handler for</param>
        /// <returns>True if the handler was removed, else false</returns>
        public static bool RemoveHandler(String ext)
        {
            lock (Lock)
            {
                var d = Data;
                if (!d.TryRemove(ext.TrimStart('.').FastToLower(), out var fn))
                    return false;
                RoData = d.Freeze();
            }
            return true;
        }

        /// <summary>
        /// The modified text (with variables using the ${Var} syntax.
        /// </summary>
        public readonly String Text;

        /// <summary>
        /// The variables used in the text.
        /// </summary>
        public readonly IReadOnlyList<LanguageTemplateVar> Vars;


        public readonly FastMemCache<String, IReadOnlyDictionary<String, String>> LangVars = new FastMemCache<string, IReadOnlyDictionary<string, string>>(TimeSpan.FromHours(24), StringComparer.Ordinal);

        public LanguageTemplate(String text, IEnumerable<LanguageTemplateVar> vars)
        {
            Text = text;
            Vars = vars.ToArray();
        }





    }



}
