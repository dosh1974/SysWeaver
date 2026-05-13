using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace SysWeaver.MicroService.ExtensionHandlers
{

    public sealed class HtmlTranslation : IDisposable
    {

        public override string ToString() => "Handle .html file translations";

        public HtmlTranslation()
        {
            LanguageTemplate.AddHandler("html", CreateTemplate);
            LanguageTemplate.AddHandler("htm", CreateTemplate);
        }

        public void Dispose()
        {
            LanguageTemplate.RemoveHandler("htm");
            LanguageTemplate.RemoveHandler("html");
        }

        static String Trim(out String prefix, out String suffix, String text)
        {
            var t = text.TrimStart();
            prefix = text.Substring(0, text.Length - t.Length);
            var tt = t.TrimEnd();
            suffix = t.Substring(tt.Length);
            return tt;
        }

        static readonly IReadOnlySet<String> IncludeAttributes = ReadOnlyData.Set<String>(
            [
                "title",
                "placeholder",
                "alt",
            ]);

        static bool NoopHandler(Dictionary<String, LanguageTemplateVar> vars, HtmlNode node)
        {
            return false;
        }

        static bool JsHandler(Dictionary<String, LanguageTemplateVar> vars, HtmlNode node)
        {
            var t = node.FirstChild as HtmlTextNode;
            if (t == null)
                return false;
            var text = t.Text;
            if (String.IsNullOrEmpty(text))
                return false;
            if (JsTranslation.Process(vars, ref text))
                t.Text = text;
            return false;
        }

        static bool CssHandler(Dictionary<String, LanguageTemplateVar> vars, HtmlNode node)
        {
            //  TODO: Handle css
            return false;
        }

        static readonly IReadOnlyDictionary<String, Func<Dictionary<String, LanguageTemplateVar>, HtmlNode, bool>> SpecialTagHandlers = new Dictionary<String, Func<Dictionary<String, LanguageTemplateVar>, HtmlNode, bool>>(StringComparer.Ordinal)
        {
            {  "style", CssHandler },
            {  "script", JsHandler },
        }.Freeze();


        static String GetTextContext(String text, HtmlNode node)
        {
            var cc = GetContext(node);
            var tn = node;
            while ((tn != null) && (tn is HtmlTextNode))
                tn = tn.ParentNode;

            var maxExtra = Math.Max(text.Length * 3, 10);
            var minLen = text.Length + maxExtra * 2;
            String val = null;
            while (node != null)
            {
                val = node.InnerText.Trim();
                if (val.Length >= minLen)
                    break;
                node = node.ParentNode;
            }
            var t = val.IndexOf(text);
            String res = null;
            if (t >= 0)
            {
                var prefix = StringTools.CodeSanitize(val.Substring(0, t)).Trim() + " ";
                while (prefix.Length > maxExtra)
                {
                    var test = prefix.IndexOf(ch => !Char.IsLetterOrDigit(ch));
                    if (test < 0)
                        break;
                    var n = prefix.Substring(test + 1).TrimStart();
                    if (n.Length < maxExtra)
                        break;
                    prefix = n;
                }
                var suffix = " " + StringTools.CodeSanitize(val.Substring(t + text.Length)).Trim();
                while (suffix.Length > maxExtra)
                {
                    var test = suffix.LastIndexOf(ch => !Char.IsLetterOrDigit(ch));
                    if (test <= 0)
                        break;
                    --test;
                    var n = suffix.Substring(0, test).TrimEnd();
                    if (n.Length < maxExtra)
                        break;
                    suffix = n;
                }
                if ((prefix.Length > 0) || (suffix.Length > 0))
                {
                    res = String.Concat(
                        "The text to translate is the content of an individual HTML Text node on a \"",
                        tn.Name,
                        "\"-html element.\nIt's part of the longer text: \"",
                        prefix,
                        ">>",
                        text,
                        "<<",
                        suffix,
                        "\".");
                }
            }
            if (res == null)
                res = String.Concat(
                        "The text to translate is the content of an individual HTML Text node on a \"",
                        tn.Name,
                        "\"-html element.");
            if (cc == null)
                return res;
            return String.Join('\n', cc, res);
        }

        static String GetContext(HtmlNode node, int maxDepth = 2)
        {
            List<String> m = new List<string>(maxDepth);              
            for (int i = 0; (node != null) && (i < maxDepth); ++ i)
            {
                String val = null;
                for (; node != null; )
                {
                    val = node.GetAttributeValue("data-translation_context", null);
                    node = node.ParentNode;
                    if (val != null)
                        break;
                }
                if (val != null)
                    m.Add(TranslationTools.HtmlAttributeDecode(val));
            }
            if (m.Count <= 0)
                return null;
            return String.Join('\n', m);
        }

        static String GetAttributeContext(HtmlAttribute a)
        {
            var node = a.OwnerNode;
            var text = String.Concat(
                "The text is the value of a \"",
                a.OriginalName,
                "\"-html attribute on a \"",
                node.Name,
                "\"-html element.");

            var cc = GetContext(node);
            if (cc == null)
                return text;
            return String.Join('\n', cc, text);
        }


        static void TranslateAttribute(Dictionary<String, LanguageTemplateVar> vars, HtmlAttribute a)
        {
            if (!IncludeAttributes.Contains(a.Name.FastToLower()))
                return;
            var t = a.Value;
            if (String.IsNullOrEmpty(t))
                return;
            if (!t.AnyLetter())
                return;
            var text = Trim(out var prefix, out var suffix, t);
            if (TranslationTools.IsVar(text))
                return;
            text = TranslationTools.HtmlAttributeDecode(text);
            var context = GetAttributeContext(a);
            var varName = TranslationTools.TryAddVar(vars, text, context);
            a.Value = String.Concat(prefix, "${#", varName, '}', suffix);
        }

        static void TranslateNode(List<Tuple<HtmlTextNode, String>> replace, Dictionary<String, LanguageTemplateVar> vars, HtmlNode node)
        {
            foreach (var a in node.Attributes)
                TranslateAttribute(vars, a);
            var nn = node as HtmlTextNode;
            if (nn == null)
                return;
            var t = node.InnerText;
            if (String.IsNullOrEmpty(t))
                return;
            if (!t.AnyLetter())
                return;
            var text = Trim(out var prefix, out var suffix, t);
            if (TranslationTools.IsVar(text))
                return;
            text = HttpUtility.HtmlDecode(text);
            var context = GetTextContext(text, node);
            var varName = TranslationTools.TryAddVar(vars, text, context);
            replace.Add(Tuple.Create(nn, String.Concat(prefix, "${#", varName, '}', suffix)));
        }

        static void Visit(List<Tuple<HtmlTextNode, String>> replace, Dictionary<String, LanguageTemplateVar> vars, HtmlNode node, bool willTranslate)
        {
            if (node.GetAttributeValue("translate", null).FastEquals("no"))
            {
                if (willTranslate)
                    foreach (var x in node.GetAttributes("translate").ToList())
                        x.Remove();
                return;
            }
            if (SpecialTagHandlers.TryGetValue(node.Name.FastToLower(), out var h))
            {
                if (!h(vars, node))
                    return;
            }else 
                TranslateNode(replace, vars, node);
            foreach (var c in node.ChildNodes)
                Visit(replace, vars, c, willTranslate);
        }

        static bool RemoveAttr(HtmlNode node)
        {
            bool didRemove = false;
            var x = node.GetDataAttributes().Where(x => x.OriginalName.FastEquals("data-translation_context")).ToList();
            foreach (var t in x)
            {
                t.Remove();
                didRemove = true;
            }
            foreach (var c in node.ChildNodes)
                didRemove |= RemoveAttr(c);
            return didRemove;
        }

        public static LanguageTemplate CreateTemplate(String text, bool willTranslate, bool allowBrowserTranslation)
        {
            var vars = TranslationTools.GetVars();
            var doc = new HtmlDocument();
            doc.LoadHtml(text);
            List<Tuple<HtmlTextNode, String>> replace = new List<Tuple<HtmlTextNode, string>>();
            Visit(replace, vars, doc.DocumentNode, willTranslate);
            RemoveAttr(doc.DocumentNode);
            foreach (var x in replace)
                x.Item1.Text = x.Item2;
            if (willTranslate)
            {
                var html = doc.DocumentNode.SelectSingleNode("/html");
                if (html != null)
                    html.SetAttributeValue("lang", "${Session.Lang}");
                if (!allowBrowserTranslation)
                {
                    var body = doc.DocumentNode.SelectSingleNode("//body");
                    if (body != null)
                    {
                        body.SetAttributeValue("translate", "no");
                        body.AddClass("notranslate");
                    }
                    var head = doc.DocumentNode.SelectSingleNode("//head");
                    if (head != null)
                    {
                        head.SetAttributeValue("translate", "no");
                        var meta = head.SelectSingleNode("//meta[@name='google']");
                        if (meta == null)
                        {
                            meta = doc.CreateElement("meta");
                            head.ChildNodes.Add(meta);
                            meta.SetAttributeValue("name", "google");
                        }
                        meta.SetAttributeValue("content", "notranslate");
                    }
                }
            }
            using var ms = new MemoryStream(text.Length + 1024);
            doc.Save(ms, Utf8WithoutBom);
            var buf = ms.GetBuffer();
            text = Encoding.UTF8.GetString(buf, 0, (int)ms.Position);
            return new LanguageTemplate(text, vars.Values);
        }


        static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    }
}
