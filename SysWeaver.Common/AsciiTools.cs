using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Web;

namespace SysWeaver
{


    public static class AsciiTools
    {
        public static void Decode(out String[] text, out String[] color, Byte[] data)
        {
            using (var inMs = new MemoryStream(data))
            using (var outMs = new MemoryStream())
            using (var cs = new DeflateStream(inMs, CompressionMode.Decompress))
            {
                cs.CopyTo(outMs);
                cs.Flush();
                data = outMs.ToArray();
            }
            List<String> texts = new List<string>();
            List<String> colors = new List<string>();
            var charCount = data[0] + 1;
            var colorCount = data[1] + 1;
            int charAdd = 2;
            int colorAdd = charAdd + charCount;
            var l = data.Length;
            var sbText = new StringBuilder();
            var sbColor = new StringBuilder();
            for (int p = colorAdd + colorCount; p < l; ++p)
            {
                var dd = data[p];
                var ti = dd % charCount;
                var ci = dd / charCount;
                var t = (Char)(1 + data[charAdd + ti]);
                if (t == '\n')
                {
                    texts.Add(sbText.ToString());
                    colors.Add(sbColor.ToString());
                    sbText = new StringBuilder();
                    sbColor = new StringBuilder();
                    continue;
                }
                sbText.Append(t);
                sbColor.Append((Char)(1 + data[colorAdd + ci]));
            }
            text = texts.ToArray();
            color = colors.ToArray();
        }


        public static void RenderColorGradient(Byte[] data, String tab = "") => RenderColor(data, ConsolePalette, tab);

        public static void RenderColor(Byte[] data, ConsoleColor bright = ConsoleColor.Green, ConsoleColor dark = ConsoleColor.DarkGreen, String tab = "")
        {
            var d = new Dictionary<Char, ConsoleColor>()
            {
                { 'a', bright },
                { 'b', dark },
            };
            RenderColor(data, d, tab);
        }

        public static void RenderColor(Byte[] data, IReadOnlyDictionary<Char, ConsoleColor> palette, String tab = "")
        {
            var ca = Cache;
            if (!ca.TryGetValue(data, out var d))
            {
                Decode(out var t, out var c, data);
                d = new Tuple<string[], string[]>(t, c);
                ca[data] = d;
            }
            RenderColor(d.Item1, d.Item2, palette, tab);
        }

        static readonly ConcurrentDictionary<Byte[], Tuple<String[], String[]>> Cache = new ConcurrentDictionary<byte[], Tuple<string[], string[]>>();


        public static void RenderColor(String[] text, String[] color, IReadOnlyDictionary<Char, ConsoleColor> palette, String tab = "")
        {
            var h = text.Length;
            var orgc = Console.ForegroundColor;
            for (int y = 0; y < h; ++y)
            {
                Console.Write(tab);
                var t = text[y];
                var c = color[y];

                var l = c.Length;
                int s = 0;
                int e;
                ConsoleColor current = ConsoleColor.Black;
                for (e = 0; e < l; ++e)
                {
                    palette.TryGetValue(c[e], out var col);
                    if ((col == current) || (t[e] == ' '))
                        continue;
                    if (e == s)
                    {
                        current = col;
                        continue;
                    }
                    Console.ForegroundColor = current;
                    Console.Write(t.Substring(s, e - s));
                    s = e;
                    current = col;
                }
                if (e != s)
                {
                    Console.ForegroundColor = current;
                    Console.WriteLine(t.Substring(s, e - s));
                }
                else
                {
                    Console.WriteLine();
                }
                Console.ForegroundColor = orgc;
            }
        }


        public static readonly IReadOnlyDictionary<Char, ConsoleColor> ConsolePalette = new Dictionary<Char, ConsoleColor>()
        {
            { 'a', ConsoleColor.Red },
            { 'b', ConsoleColor.Yellow },
            { 'c', ConsoleColor.Green },
            { 'd', ConsoleColor.Cyan },
            { 'e', ConsoleColor.Blue },
            { 'f', ConsoleColor.Magenta },
            { 'A', ConsoleColor.DarkRed },
            { 'B', ConsoleColor.DarkYellow },
            { 'C', ConsoleColor.DarkGreen },
            { 'D', ConsoleColor.DarkCyan },
            { 'E', ConsoleColor.DarkBlue },
            { 'F', ConsoleColor.DarkMagenta },
            { '0', ConsoleColor.Black },
            { '1', ConsoleColor.Gray },
            { '2', ConsoleColor.DarkGray},
            { '3', ConsoleColor.White },
        }.Freeze();

        static readonly String[] HtmlColors =
        [
            "#000",     // Black        
            "#008",     // DarkBlue     
            "#080",     // DarkGreen    
            "#088",     // DarkCyan     
            "#800",     // DarkRed      
            "#808",     // DarkMagenta  
            "#880",     // DarkYellow   
            "#888",     // Gray         
            "#444",     // DarkGray     
            "#00f",     // Blue         
            "#0f0",     // Green        
            "#0ff",     // Cyan         
            "#f00",     // Red          
            "#f0f",     // Magenta      
            "#ff0",     // Yellow       
            "#fff",     // White        
        ];


        public static String GetHtml(Byte[] data, ConsoleColor bright = ConsoleColor.Green, ConsoleColor dark = ConsoleColor.DarkGreen, String tab = "", String nlStart = "<div>", String nlEnd = "</div>", String tag = "span")
        {
            var d = new Dictionary<Char, ConsoleColor>()
            {
                { 'a', bright },
                { 'b', dark },
            };
            return GetHtml(data, d, tab, nlStart, nlEnd, tag);
        }

        public static String GetHtmlGradient(Byte[] data, String tab = "", String nlStart = "<div>", String nlEnd = "</div>", String tag = "span")
            => GetHtml(data, ConsolePalette, tab, nlStart, nlEnd, tag);

        public static String GetHtml(Byte[] data, IReadOnlyDictionary<Char, ConsoleColor> palette, String tab = "", String nlStart = "<div>", String nlEnd = "</div>", String tag = "span")
        {
            var ca = Cache;
            if (!ca.TryGetValue(data, out var d))
            {
                Decode(out var t, out var c, data);
                d = new Tuple<string[], string[]>(t, c);
                ca[data] = d;
            }
            return GetHtml(d.Item1, d.Item2, palette, tab, nlStart, nlEnd, tag);
        }

        public static String GetHtml(String[] text, String[] color, IReadOnlyDictionary<Char, ConsoleColor> palette, String tab = "", String nlStart = "<div>", String nlEnd = "</div>", String tag = "span")
        {
            StringBuilder stringBuilder = new StringBuilder();
            var h = text.Length;
            var orgc = Console.ForegroundColor;
            var beginOpen = "<" + tag + " style='color:";
            var endOpen  = "'>";
            var close = "</" + tag + ">";
            for (int y = 0; y < h; ++y)
            {
                stringBuilder.Append(nlStart);
                bool first = true;
                var t = text[y];
                var c = color[y];
                var l = c.Length;
                int s = 0;
                int e;
                ConsoleColor current = ConsoleColor.Black;
                for (e = 0; e < l; ++e)
                {
                    palette.TryGetValue(c[e], out var col);
                    if ((col == current) || (t[e] == ' '))
                        continue;
                    if (e == s)
                    {
                        current = col;
                        continue;
                    }
                    stringBuilder.Append(beginOpen);
                    stringBuilder.Append(HtmlColors[(int)current]);
                    stringBuilder.Append(endOpen);
                    if (first)
                        stringBuilder.Append(tab);
                    first = false;
                    stringBuilder.Append(HttpUtility.HtmlEncode(t.Substring(s, e - s)).Replace(" ", "&nbsp;"));
                    stringBuilder.Append(close);
                    s = e;
                    current = col;
                }
                if (e != s)
                {
                    stringBuilder.Append(beginOpen);
                    stringBuilder.Append(HtmlColors[(int)current]);
                    stringBuilder.Append(endOpen);
                    if (first)
                        stringBuilder.Append(tab);
                    first = false;
                    stringBuilder.Append(HttpUtility.HtmlEncode(t.Substring(s, e - s)).Replace(" ", "&nbsp;"));
                    stringBuilder.Append(close);
                }
                stringBuilder.Append(nlEnd);
            }
            return stringBuilder.ToString();
        }


    }
}