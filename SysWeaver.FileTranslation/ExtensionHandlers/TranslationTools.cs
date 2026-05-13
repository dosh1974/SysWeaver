using System;
using System.Collections.Generic;
using System.Web;

namespace SysWeaver.MicroService.ExtensionHandlers
{
    public static class TranslationTools
    {

        public static Dictionary<String, LanguageTemplateVar> GetVars() => new Dictionary<string, LanguageTemplateVar>(StringComparer.Ordinal);


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


        public static String HtmlAttributeDecode(String text)
        {
            text = text.Replace("&amp;", "&");
            text = text.Replace("&lt;", "<");
            text = text.Replace("&gt;", ">");
            text = text.Replace("&quot;", "\"");
            text = text.Replace("&apos;", "'");
            text = HttpUtility.HtmlDecode(text);
            return text;
        }

    }
}
