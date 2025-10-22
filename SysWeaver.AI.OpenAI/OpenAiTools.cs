using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using SysWeaver.Docs;

namespace SysWeaver.AI
{

    public static class OpenAiTools
    {

        internal const String DebugKey = "*open_ai_debug*";
        internal const String DebugMenuId = "FnDebug";

        internal static readonly IReadOnlySet<String> EmptyStringSet = ReadOnlyData.Set<String>();
        internal static readonly IReadOnlySet<String> NoDebugSet = ReadOnlyData.Set(Enum.GetNames(typeof(OpenAiDebugInfo)).Select(x => OpenAiTools.DebugMenuId + x));

        internal static readonly Chat.ChatMenuItem[] DebugItems = Enum.GetNames(typeof(OpenAiDebugInfo)).Select(x =>
        {
            var et = typeof(OpenAiDebugInfo).XmlDocEnum(x)?.Summary;
            return new Chat.ChatMenuItem
            {
                Id = OpenAiTools.DebugMenuId + x,
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



        internal sealed class Opt
        {
            public String Model;
            /// <summary>
            /// True if model supports temperature
            /// </summary>
            public bool Temp = true;
            /// <summary>
            /// True if model supports the System role
            /// </summary>
            public bool System = true;
            /// <summary>
            /// True if model supports paralell tool calls
            /// </summary>
            public bool? PTools = true;
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

        static internal Opt GetOptions(String model)
        {
            Opt o = null;
            foreach (var x in Opts)
            {
                if (model.StartsWith(x.Model, StringComparison.OrdinalIgnoreCase))
                {
                    o = x;
                    break;
                }
            }
            return o ?? new Opt();
        }

        static readonly Opt[] Opts =
        [
            new Opt { Model = "o1-preview", Temp = false, System = false, PTools = null },
            new Opt { Model = "o1-mini", Temp = false, System = false, PTools = null },
            new Opt { Model = "o1", Temp = false, PTools = null },
            new Opt { Model = "o3-mini", Temp = false, PTools = null },
            new Opt { Model = "gpt-5", Temp = false },
        ];




    }

}
