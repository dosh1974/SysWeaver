using System;
using System.Collections.Generic;
using System.Web;

namespace SysWeaver.MicroService.ExtensionHandlers
{
    /// <summary>
    /// Helpers for the file translation handlers
    /// </summary>
    public static class TranslationTools
    {

        /// <summary>
        /// Create an empty dictionary for the variables of a template
        /// </summary>
        /// <returns>The dictionary (keyed by text and context)</returns>
        public static Dictionary<String, LanguageTemplateVar> GetVars() => new Dictionary<string, LanguageTemplateVar>(StringComparer.Ordinal);


        /// <summary>
        /// Check if a text is a single variable, ex: "${name}"
        /// </summary>
        /// <param name="text">The text</param>
        /// <returns>True if the text starts with "${" and ends with the first "}"</returns>
        public static bool IsVar(String text)
        {
            var tl = text.Length;
            if (tl < 4)
                return false;
            if (!text.FastStartsWith("${"))
                return false;
            --tl;
            return text.IndexOf('}') == tl;
        }

        /// <summary>
        /// Add a text (with a context) as a variable, a text and context that was already added reuses its variable
        /// </summary>
        /// <param name="vars">The variables</param>
        /// <param name="text">The text</param>
        /// <param name="context">The context of the text (used by translators)</param>
        /// <returns>The name of the variable, "Tr" + number, or "Vr" + number if the text contains "${..}" variables</returns>
        public static String TryAddVar(Dictionary<String, LanguageTemplateVar> vars, String text, String context)
        {
            var key = String.Join('\n', text, context);
            if (vars.TryGetValue(key, out var x))
                return x.VarName;
            var t = text.IndexOf("${");
            var hasVars = (t >= 0) && (text.IndexOf('}', t + 3) > t);
            var varName = (hasVars ? "Vr" : "Tr") + vars.Count;
            vars.Add(key, new LanguageTemplateVar(varName, text, context));
            return varName;
        }


        /// <summary>
        /// Decode the html entities of an attribute value
        /// </summary>
        /// <param name="text">The attribute value</param>
        /// <returns>The decoded text</returns>
        public static String HtmlAttributeDecode(String text)
            => HttpUtility.HtmlDecode(text);

    }
}
