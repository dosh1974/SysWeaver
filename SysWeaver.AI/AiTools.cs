using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using SysWeaver.Docs;

namespace SysWeaver.AI
{

    public static class AiTools
    {

        internal const String DebugKey = "*open_ai_debug*";
        internal const String DebugMenuId = "FnDebug";

        internal static readonly IReadOnlySet<String> EmptyStringSet = ReadOnlyData.Set<String>();
        internal static readonly IReadOnlySet<String> NoDebugSet = ReadOnlyData.Set(Enum.GetNames(typeof(AiDebugInfo)).Select(x => AiTools.DebugMenuId + x));

        internal static readonly Chat.ChatMenuItem[] DebugItems = Enum.GetNames(typeof(AiDebugInfo)).Select(x =>
        {
            var et = typeof(AiDebugInfo).XmlDocEnum(x)?.Summary;
            if (x.FastEquals(AiDebugInfo.Stats.ToString()))
            {
                return new Chat.ChatMenuItem
                {
                    Id = "ShowMessageStats",
                    Name = "Stats",
                    Value = x,
                    Icon = "IconDebug" + x,
                    Desc = et,
                };
            }
            return new Chat.ChatMenuItem
            {
                Id = AiTools.DebugMenuId + x,
                Name = x.RemoveCamelCase() + " of called functions",
                Value = x,
                Icon = "IconDebug" + x,
                Desc = et,
            };
        }).ToArray();



        public static readonly IReadOnlySet<Char> MdEscapeChars = ReadOnlyData.Set(@"\`*_{}[]<>()#+-!|".ToCharArray());

        public static String MdEscape(String s)
        {
            var l = s.Length;
            var t = new StringBuilder(l + l);
            var es = MdEscapeChars;
            for (int i = 0; i < l; ++i)
            {
                char c = s[i];
                if (es.Contains(c))
                    t.Append('\\');
                t.Append(c);
            }
            return t.Length == l ? s : t.ToString();
        }




        public static String Intendent(String t, String i)
        {
            if (String.IsNullOrEmpty(t))
                return i;
            t = t.Replace("\n", "\n" + i);
            return t;
        }


        public static String BeautifyJson(String json, String indent = "")
        {
            if (String.IsNullOrEmpty(json))
                return indent;
            using var stringReader = new StringReader(json);
            using var stringWriter = new StringWriter();
            using var jsonReader = new JsonTextReader(stringReader);
            using var jsonWriter = new JsonTextWriter(stringWriter) { Formatting = Formatting.Indented };
            jsonWriter.WriteToken(jsonReader);
            var t = stringWriter.ToString();
            t = t.Replace("\n", "\n" + indent);
            return t;
        }



        internal static String FilterSpeechName(String s)
        {
            var l = s.Length;
            var sb = new StringBuilder(l);
            for (int i = 0; i < l; ++i)
            {
                var c = s[i];
                if (!char.IsLetter(c))
                {
                    if (sb.Length > 0)
                        return sb.ToString();
                    continue;
                }
                sb.Append(c);
            }
            var sl = s.Length;
            if (sl > 0)
                return sl == l ? s : sb.ToString();
            return "AI";

        }





    }

}
