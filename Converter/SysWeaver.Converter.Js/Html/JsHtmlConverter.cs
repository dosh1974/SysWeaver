using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using SysWeaver.Converter.Js.Ast;

namespace SysWeaver.Converter.Js
{
    /// <summary>
    /// Converts the JavaScript in html documents: inline script elements and (optionally) event handler attributes (onclick="...").
    /// Scripts and attributes that don't need any transforms are left untouched.
    /// Helpers used by the converted code are added once, in a new script element before the first converted script (unless the helper mode is External).
    /// </summary>
    public static class JsHtmlConverter
    {
        static readonly HashSet<String> JsTypes = new HashSet<String>(StringComparer.OrdinalIgnoreCase)
        {
            "", "text/javascript", "application/javascript", "text/ecmascript", "application/ecmascript", "module",
            "text/jsx", "application/x-javascript", "text/x-javascript", "javascript",
        };

        sealed class Edit
        {
            public int Start;
            public int End;
            public String Text;
        }

        internal sealed class Tag
        {
            public int Start;
            /// <summary>The position after the closing '&gt;'</summary>
            public int End;
            public String Name;
            public List<(String Name, String Value, int ValueStart, int ValueEnd, char Quote)> Attrs = new List<(String, String, int, int, char)>();
        }

        /// <summary>
        /// Convert the scripts in an html document
        /// </summary>
        /// <param name="html">The html source</param>
        /// <param name="p">The options</param>
        /// <returns>The result, Code is the converted html (null if the conversion failed)</returns>
        public static JsConvertResult Convert(String html, JsConverterParams p = null)
        {
            p ??= new JsConverterParams();
            var result = new JsConvertResult();
            var diag = new Diagnostics(html);
            var edits = new List<Edit>();
            var helpers = new List<String>();
            var polyfills = new List<String>();
            var transformed = new HashSet<JsFeature>();
            int firstScript = -1;
            bool anyModule = false;
            //  Names used in all scripts (helpers and temporaries must not conflict)
            var reserved = new HashSet<String>(StringComparer.Ordinal);
            var scripts = new List<(int Start, int End, bool Module, int Tag)>();
            var attrs = new List<(int Start, int End, char Quote, String Code)>();
            int pos = 0;
            var len = html.Length;
            while (pos < len)
            {
                var lt = html.IndexOf('<', pos);
                if (lt < 0)
                    break;
                //  Comments
                if (String.CompareOrdinal(html, lt, "<!--", 0, 4) == 0)
                {
                    var ce = html.IndexOf("-->", lt + 4, StringComparison.Ordinal);
                    pos = ce < 0 ? len : ce + 3;
                    continue;
                }
                var tag = ParseTag(html, lt);
                if (tag == null)
                {
                    pos = lt + 1;
                    continue;
                }
                pos = tag.End;
                if (p.HtmlAttributes)
                {
                    foreach (var a in tag.Attrs)
                    {
                        if (a.Value != null && a.Name.Length > 2 && a.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                            attrs.Add((a.ValueStart, a.ValueEnd, a.Quote, WebUtility.HtmlDecode(a.Value)));
                    }
                }
                var name = tag.Name.ToLowerInvariant();
                if (name == "script")
                {
                    var close = IndexOfIgnoreCase(html, "</script", tag.End);
                    if (close < 0)
                        close = len;
                    String type = "", src = null;
                    foreach (var a in tag.Attrs)
                    {
                        if (a.Name.Equals("type", StringComparison.OrdinalIgnoreCase))
                            type = (a.Value ?? "").Trim();
                        else if (a.Name.Equals("src", StringComparison.OrdinalIgnoreCase))
                            src = a.Value;
                    }
                    var semi = type.IndexOf(';');
                    if (semi >= 0)
                        type = type.Substring(0, semi).Trim();
                    if (src == null && JsTypes.Contains(type))
                    {
                        var isModule = type.Equals("module", StringComparison.OrdinalIgnoreCase);
                        scripts.Add((tag.End, close, isModule, tag.Start));
                    }
                    else if (firstScript < 0 && JsTypes.Contains(type))
                        firstScript = tag.Start;
                    pos = close;
                    continue;
                }
                //  Raw text elements
                if (name == "style" || name == "textarea" || name == "title" || name == "xmp" || name == "noscript")
                {
                    var close = IndexOfIgnoreCase(html, "</" + name, tag.End);
                    pos = close < 0 ? len : close;
                }
            }
            foreach (var s in scripts)
            {
                foreach (var id in IdentifierScan(html.Substring(s.Start, s.End - s.Start)))
                    reserved.Add(id);
            }
            foreach (var a in attrs)
            {
                foreach (var id in IdentifierScan(a.Code))
                    reserved.Add(id);
            }
            var inner = p.Clone();
            if (p.Helpers == JsHelperMode.Inline)
                inner.Helpers = JsHelperMode.External;
            bool failed = false;
            //  Script elements
            foreach (var s in scripts)
            {
                var code = html.Substring(s.Start, s.End - s.Start);
                if (code.Trim().Length == 0)
                    continue;
                var lineOffset = CountLines(html, s.Start);
                var colDiag = new Diagnostics(code, lineOffset);
                var ip = inner.Clone();
                ip.SourceType = s.Module ? JsSourceType.Module : JsSourceType.Script;
                var r = JsConverter.Convert(code, ip, colDiag, reserved, true, null);
                diag.Items.AddRange(r.Diagnostics);
                if (r.Code == null)
                {
                    failed = true;
                    continue;
                }
                if (r.Transformed.Count == 0 && r.Helpers.Count == 0 && r.Polyfills.Count == 0)
                    continue;
                if (firstScript < 0 || s.Tag < firstScript)
                    firstScript = firstScript < 0 ? s.Tag : Math.Min(firstScript, s.Tag);
                anyModule |= s.Module;
                transformed.UnionWith(r.Transformed);
                Merge(helpers, r.Helpers);
                Merge(polyfills, r.Polyfills);
                var nl = code.Contains('\n') ? "\n" : "";
                edits.Add(new Edit { Start = s.Start, End = s.End, Text = nl + r.Code.TrimEnd() + nl });
            }
            //  Event handler attributes
            foreach (var a in attrs)
            {
                var lineOffset = CountLines(html, a.Start);
                var colDiag = new Diagnostics(a.Code, lineOffset);
                var ip = inner.Clone();
                ip.Minify = true;
                ip.Comments = JsComments.None;
                ip.SourceType = JsSourceType.Script;
                var r = JsConverter.Convert(a.Code, ip, colDiag, reserved, true, WrapHandler);
                diag.Items.AddRange(r.Diagnostics);
                if (r.Code == null)
                {
                    failed = true;
                    continue;
                }
                if (r.Transformed.Count == 0 && r.Helpers.Count == 0 && r.Polyfills.Count == 0)
                    continue;
                transformed.UnionWith(r.Transformed);
                Merge(helpers, r.Helpers);
                Merge(polyfills, r.Polyfills);
                var enc = a.Quote == '\'' ? r.Code.Trim().Replace("&", "&amp;").Replace("'", "&#39;") : r.Code.Trim().Replace("&", "&amp;").Replace("\"", "&quot;");
                if (a.Quote == '\0')
                    enc = "\"" + r.Code.Trim().Replace("&", "&amp;").Replace("\"", "&quot;") + "\"";
                edits.Add(new Edit { Start = a.Start, End = a.End, Text = enc });
            }
            result.Helpers = Helpers.JsHelpers.Ordered(helpers);
            result.Polyfills = polyfills;
            result.Transformed = transformed.OrderBy(x => x).ToList();
            result.Diagnostics = diag.Items.OrderBy(x => x.Line).ThenBy(x => x.Column).ToList();
            try
            {
                result.Target = JsTarget.Parse(p.Target);
            }
            catch (ArgumentException)
            {
            }
            if (failed || result.HasErrors || (p.WarningsAsErrors && result.HasWarnings))
                return result;
            //  The helpers (and polyfills) script
            if (p.Helpers == JsHelperMode.Inline && (helpers.Count > 0 || polyfills.Count > 0))
            {
                var hs = JsConverter.GetHelpersSource(helpers, p.Minify, polyfills);
                hs = hs.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);
                var at = firstScript;
                if (at < 0)
                {
                    var head = IndexOfIgnoreCase(html, "<head", 0);
                    if (head >= 0)
                    {
                        var ht = ParseTag(html, head);
                        at = ht?.End ?? head;
                    }
                    else
                        at = 0;
                }
                var nl = p.Minify ? "" : "\n";
                edits.Add(new Edit { Start = at, End = at, Text = "<script>" + nl + hs.TrimEnd() + nl + "</script>" + nl });
            }
            //  Apply the edits
            var sb = new StringBuilder(html.Length + 1024);
            int last = 0;
            foreach (var e in edits.OrderBy(x => x.Start).ThenBy(x => x.End))
            {
                sb.Append(html, last, e.Start - last);
                sb.Append(e.Text);
                last = e.End;
            }
            sb.Append(html, last, len - last);
            result.Code = sb.ToString();
            return result;
        }

        static void Merge(List<String> to, IEnumerable<String> from)
        {
            foreach (var x in from)
                if (!to.Contains(x))
                    to.Add(x);
        }

        /// <summary>
        /// Event handler attribute code is the body of a function: it is parsed as a script that allows return (the wrapper marks the conversion as an event handler)
        /// </summary>
        static ProgramNode WrapHandler(ProgramNode p) => p;

        static int CountLines(String s, int end)
        {
            int n = 0;
            for (int i = 0; i < end; ++i)
                if (s[i] == '\n')
                    ++n;
            return n;
        }

        internal static int IndexOfIgnoreCase(String s, String what, int start) => s.IndexOf(what, start, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// All identifier like tokens (over approximation, used to reserve names)
        /// </summary>
        static IEnumerable<String> IdentifierScan(String code)
        {
            int i = 0;
            var l = code.Length;
            while (i < l)
            {
                var c = code[i];
                if (char.IsLetter(c) || c == '_' || c == '$')
                {
                    int s = i;
                    while (i < l && (char.IsLetterOrDigit(code[i]) || code[i] == '_' || code[i] == '$'))
                        ++i;
                    yield return code.Substring(s, i - s);
                    continue;
                }
                ++i;
            }
        }

        static bool IsNameChar(char c) => !char.IsWhiteSpace(c) && c != '>' && c != '/' && c != '=' && c != '"' && c != '\'' && c != '<';

        /// <summary>
        /// Parse a start tag at the position of a '&lt;', returns null if it isn't a start tag
        /// </summary>
        internal static Tag ParseTag(String html, int lt)
        {
            var l = html.Length;
            int i = lt + 1;
            if (i >= l || !char.IsLetter(html[i]))
                return null;
            int ns = i;
            while (i < l && IsNameChar(html[i]))
                ++i;
            var t = new Tag { Start = lt, Name = html.Substring(ns, i - ns) };
            while (i < l)
            {
                while (i < l && char.IsWhiteSpace(html[i]))
                    ++i;
                if (i >= l)
                    break;
                var c = html[i];
                if (c == '>')
                {
                    t.End = i + 1;
                    return t;
                }
                if (c == '/')
                {
                    ++i;
                    continue;
                }
                int an = i;
                while (i < l && IsNameChar(html[i]))
                    ++i;
                if (i == an)
                {
                    ++i;
                    continue;
                }
                var attrName = html.Substring(an, i - an);
                while (i < l && char.IsWhiteSpace(html[i]))
                    ++i;
                if (i < l && html[i] == '=')
                {
                    ++i;
                    while (i < l && char.IsWhiteSpace(html[i]))
                        ++i;
                    if (i < l && (html[i] == '"' || html[i] == '\''))
                    {
                        var q = html[i];
                        var vs = i + 1;
                        var ve = html.IndexOf(q, vs);
                        if (ve < 0)
                            ve = l;
                        t.Attrs.Add((attrName, html.Substring(vs, ve - vs), vs, ve, q));
                        i = Math.Min(ve + 1, l);
                    }
                    else
                    {
                        var vs = i;
                        while (i < l && !char.IsWhiteSpace(html[i]) && html[i] != '>')
                            ++i;
                        t.Attrs.Add((attrName, html.Substring(vs, i - vs), vs, i, '\0'));
                    }
                }
                else
                    t.Attrs.Add((attrName, null, i, i, '\0'));
            }
            return null;
        }
    }
}
