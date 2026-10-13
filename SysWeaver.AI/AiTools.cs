using System;
using System.Buffers;
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
        /// <summary>
        /// The url root of the web resources of this service
        /// </summary>
        internal const String WebRoot = "../ai/";

        /// <summary>
        /// The url root of the icons of this service
        /// </summary>
        internal const String IconRoot = WebRoot + "icons/";



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



        /// <summary>
        /// The chars escaped by <see cref="MdEscape(string)"/>
        /// </summary>
        const String MdEscapeCharList = @"\`*_{}[]<>()#+-!|";

        public static readonly IReadOnlySet<Char> MdEscapeChars = ReadOnlyData.Set(MdEscapeCharList.ToCharArray());

        /// <summary>
        /// The same chars as <see cref="MdEscapeChars"/>, for vectorized searches
        /// </summary>
        static readonly SearchValues<Char> MdEscapeSearch = SearchValues.Create(MdEscapeCharList);

        public static String MdEscape(String s)
        {
            //  Vectorized search for the first char to escape, most texts are returned as is (no allocation)
            var v = s.AsSpan(0, s.Length);
            var first = v.IndexOfAny(MdEscapeSearch);
            if (first < 0)
                return s;
            //  Count the escapes, then escape into the new string (the only allocation)
            var es = MdEscapeSearch;
            var l = v.Length;
            int count = 1;
            for (int i = first + 1; i < l; ++i)
            {
                if (es.Contains(v[i]))
                    ++count;
            }
            return String.Create(l + count, (s, first), static (dst, state) =>
            {
                var src = state.s.AsSpan();
                var es = MdEscapeSearch;
                var f = state.first;
                src.Slice(0, f).CopyTo(dst);
                var l = src.Length;
                int o = f;
                for (int i = f; i < l; ++i)
                {
                    var c = src[i];
                    if (es.Contains(c))
                    {
                        dst[o] = '\\';
                        ++o;
                    }
                    dst[o] = c;
                    ++o;
                }
            });
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
