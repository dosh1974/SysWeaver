using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SysWeaver.Converter.Js.Ast;

namespace SysWeaver.Converter.Js
{
    /// <summary>
    /// The kind of a file in a site
    /// </summary>
    public enum JsSiteFileKind
    {
        /// <summary>An html page (inline scripts and event handlers are converted)</summary>
        Html,
        /// <summary>A classic script (converted, parsed as a script or a module if that fails)</summary>
        Script,
        /// <summary>An ES module (converted as a module)</summary>
        Module,
        /// <summary>A style sheet (copied, its imports and urls are followed)</summary>
        Css,
        /// <summary>Any other file (images, fonts, json ...), copied</summary>
        Asset,
    }

    /// <summary>
    /// A file of a site
    /// </summary>
    public sealed class JsSiteFile
    {
        /// <summary>The full path of the file</summary>
        public String Path;
        /// <summary>The path relative to the site root (also the path in the output folder)</summary>
        public String RelativePath;
        /// <summary>The kind of file</summary>
        public JsSiteFileKind Kind;
        /// <summary>The files that reference this file (full paths), empty for the html files that were supplied</summary>
        public readonly List<String> ReferencedBy = new List<String>();

        public override String ToString() => Kind + ": " + RelativePath;
    }

    /// <summary>
    /// Options for collecting / cloning a site
    /// </summary>
    public sealed class JsSiteParams
    {
        /// <summary>
        /// The root folder of the site: root relative urls ("/js/a.js") are resolved from here and the output keeps the paths relative to it.
        /// If null, root relative urls are searched in the folder of the page and its parents, and the root is the common folder of all files.
        /// </summary>
        public String SiteRoot;

        /// <summary>
        /// Also collect / copy other files used by the pages and style sheets (images, fonts, icons, manifests ...), not only scripts and style sheets
        /// </summary>
        public bool Assets = true;

        /// <summary>
        /// Also follow string literals in scripts that are relative paths of existing .js / .mjs / .css files (ex: includeJs("lib.js")),
        /// tried relative to the script, then relative to the pages
        /// </summary>
        public bool ScanStrings = true;

        /// <summary>
        /// When cloning with external helpers (JsConverterParams.Helpers = External): the helpers (and polyfills) are written to this file
        /// (relative to the output folder) and a script element that loads it is added to every page before its first script
        /// </summary>
        public String HelpersFile;

        /// <summary>
        /// Allow the output folder to be the site root (the files are converted in place)
        /// </summary>
        public bool Overwrite;

        /// <summary>
        /// Always write the output files (by default a file is only written if its content changed)
        /// </summary>
        public bool Force;
    }

    /// <summary>
    /// The result of collecting / cloning a site
    /// </summary>
    public sealed class JsSiteResult
    {
        /// <summary>The site root (full path)</summary>
        public String Root;
        /// <summary>All files of the site (pages, scripts, style sheets and assets)</summary>
        public List<JsSiteFile> Files = new List<JsSiteFile>();
        /// <summary>Problems found while collecting (missing files, unparsable scripts, ...), File is set</summary>
        public List<JsDiagnostic> Diagnostics = new List<JsDiagnostic>();
        /// <summary>The number of files that couldn't be converted (only set by CloneAsync, the original is copied)</summary>
        public int Failed;
        /// <summary>The number of files that were written (only set by CloneAsync)</summary>
        public int Written;
    }

    /// <summary>
    /// Collects the files used by html pages (scripts, style sheets and other files, including the files referenced by them)
    /// and clones the site to a folder with all scripts converted.
    /// Dependencies are found in: script / link / img / source / video / audio / track / input / embed / object elements, srcset, style elements and attributes,
    /// css @import and url(), static and dynamic imports with literal urls, workers, importScripts and (optionally) string literals naming existing script / css files.
    /// Dependencies that are created dynamically with computed urls can't be found, iframes / frames and links (a href) are not followed.
    /// </summary>
    public static class JsSite
    {
        static readonly Regex SchemeRegex = new Regex(@"^[a-zA-Z][a-zA-Z0-9+.\-]*:", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex CssCommentRegex = new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        static readonly Regex CssImportRegex = new Regex(@"@import\s+(?:url\(\s*)?(['""]?)([^'""\)\s;]+)\1", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex CssUrlRegex = new Regex(@"url\(\s*(['""]?)(.*?)\1\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        static readonly HashSet<String> JsTypes = new HashSet<String>(StringComparer.OrdinalIgnoreCase)
        {
            "", "text/javascript", "application/javascript", "text/ecmascript", "application/ecmascript", "module",
            "application/x-javascript", "text/x-javascript", "javascript",
        };

        static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        static JsSiteFileKind KindOf(String path)
        {
            switch (System.IO.Path.GetExtension(path).ToLowerInvariant())
            {
                case ".js":
                case ".cjs":
                    return JsSiteFileKind.Script;
                case ".mjs":
                    return JsSiteFileKind.Module;
                case ".css":
                    return JsSiteFileKind.Css;
                case ".html":
                case ".htm":
                case ".xhtml":
                    return JsSiteFileKind.Html;
                default:
                    return JsSiteFileKind.Asset;
            }
        }

        sealed class Collector
        {
            readonly JsSiteParams P;
            public readonly Dictionary<String, JsSiteFile> Files = new Dictionary<String, JsSiteFile>(PathComparer);
            public readonly List<JsDiagnostic> Diag = new List<JsDiagnostic>();
            readonly Queue<(JsSiteFile File, String PageDir)> Work = new Queue<(JsSiteFile, String)>();
            readonly HashSet<String> Scanned = new HashSet<String>(PathComparer);
            readonly HashSet<String> Missing = new HashSet<String>(PathComparer);
            readonly List<String> PageDirs = new List<String>();
            readonly String Root;

            public Collector(JsSiteParams p)
            {
                P = p;
                Root = String.IsNullOrEmpty(p.SiteRoot) ? null : System.IO.Path.GetFullPath(p.SiteRoot);
            }

            void Warn(String file, String message, JsDiagnosticLevel level = JsDiagnosticLevel.Warning)
            {
                lock (Diag)
                    Diag.Add(new JsDiagnostic { Level = level, File = file, Message = message });
            }

            public void AddPage(String path)
            {
                path = System.IO.Path.GetFullPath(path);
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!PageDirs.Contains(dir, PathComparer))
                    PageDirs.Add(dir);
                Add(path, JsSiteFileKind.Html, null, dir);
            }

            void Add(String path, JsSiteFileKind kind, String referrer, String pageDir)
            {
                if (!File.Exists(path))
                {
                    //  Once per referrer
                    if (Missing.Add((referrer ?? "") + "|" + path))
                        Warn(referrer ?? path, "\"" + path + "\" was not found");
                    return;
                }
                if (Files.TryGetValue(path, out var f))
                {
                    //  A script that is imported is a module
                    if (f.Kind == JsSiteFileKind.Script && kind == JsSiteFileKind.Module)
                        f.Kind = JsSiteFileKind.Module;
                    if (referrer != null && !f.ReferencedBy.Contains(referrer, PathComparer))
                        f.ReferencedBy.Add(referrer);
                    return;
                }
                f = new JsSiteFile { Path = path, Kind = kind };
                if (referrer != null)
                    f.ReferencedBy.Add(referrer);
                Files[path] = f;
                Work.Enqueue((f, pageDir));
            }

            /// <summary>
            /// Resolve an url to a local path, null for external / data / empty urls
            /// </summary>
            String Resolve(String url, String baseDir, String referrer)
            {
                if (url == null)
                    return null;
                url = url.Trim();
                if (url.Length == 0 || url[0] == '#' || url.StartsWith("//", StringComparison.Ordinal) || SchemeRegex.IsMatch(url))
                    return null;
                var cut = url.IndexOfAny(['?', '#']);
                if (cut >= 0)
                    url = url.Substring(0, cut);
                if (url.Length == 0)
                    return null;
                try
                {
                    url = Uri.UnescapeDataString(url);
                }
                catch (UriFormatException)
                {
                }
                var rel = url.Replace('/', System.IO.Path.DirectorySeparatorChar).Replace('\\', System.IO.Path.DirectorySeparatorChar);
                if (url[0] == '/')
                {
                    rel = rel.TrimStart(System.IO.Path.DirectorySeparatorChar);
                    if (Root != null)
                        return System.IO.Path.GetFullPath(System.IO.Path.Combine(Root, rel));
                    //  Search the folder and its parents
                    for (var d = new DirectoryInfo(baseDir); d != null; d = d.Parent)
                    {
                        var c = System.IO.Path.Combine(d.FullName, rel);
                        if (File.Exists(c))
                            return System.IO.Path.GetFullPath(c);
                    }
                    return System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, rel));
                }
                return System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, rel));
            }

            void Ref(String url, String baseDir, String referrer, JsSiteFileKind kind, String pageDir)
            {
                var p = Resolve(url, baseDir, referrer);
                if (p == null)
                    return;
                if (kind == JsSiteFileKind.Asset)
                {
                    var k = KindOf(p);
                    //  Pages referenced by assets (ex: a link to another page) are not followed
                    if (k == JsSiteFileKind.Html)
                        return;
                    if (k == JsSiteFileKind.Css)
                        kind = k;
                    else if (!P.Assets)
                        return;
                }
                Add(p, kind, referrer, pageDir);
            }

            public void Run()
            {
                while (Work.Count > 0)
                {
                    var (f, pageDir) = Work.Dequeue();
                    if (!Scanned.Add(f.Path))
                        continue;
                    try
                    {
                        switch (f.Kind)
                        {
                            case JsSiteFileKind.Html:
                                ScanHtml(f.Path, File.ReadAllText(f.Path));
                                break;
                            case JsSiteFileKind.Script:
                            case JsSiteFileKind.Module:
                                ScanJs(File.ReadAllText(f.Path), f.Kind == JsSiteFileKind.Module ? true : null, System.IO.Path.GetDirectoryName(f.Path), f.Path, pageDir);
                                break;
                            case JsSiteFileKind.Css:
                                ScanCss(File.ReadAllText(f.Path), System.IO.Path.GetDirectoryName(f.Path), f.Path, pageDir);
                                break;
                        }
                    }
                    catch (IOException ex)
                    {
                        Warn(f.Path, "Failed to read: " + ex.Message, JsDiagnosticLevel.Error);
                    }
                }
            }

            static String Attr(JsHtmlConverter.Tag t, String name)
            {
                foreach (var a in t.Attrs)
                    if (a.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return a.Value == null ? "" : WebUtility.HtmlDecode(a.Value);
                return null;
            }

            void ScanHtml(String file, String html)
            {
                var dir = System.IO.Path.GetDirectoryName(file);
                var baseDir = dir;
                int pos = 0;
                var len = html.Length;
                while (pos < len)
                {
                    var lt = html.IndexOf('<', pos);
                    if (lt < 0)
                        break;
                    if (String.CompareOrdinal(html, lt, "<!--", 0, 4) == 0)
                    {
                        var ce = html.IndexOf("-->", lt + 4, StringComparison.Ordinal);
                        pos = ce < 0 ? len : ce + 3;
                        continue;
                    }
                    var tag = JsHtmlConverter.ParseTag(html, lt);
                    if (tag == null)
                    {
                        pos = lt + 1;
                        continue;
                    }
                    pos = tag.End;
                    var name = tag.Name.ToLowerInvariant();
                    var style = Attr(tag, "style");
                    if (!String.IsNullOrEmpty(style))
                        ScanCss(style, baseDir, file, dir);
                    switch (name)
                    {
                        case "base":
                            {
                                var href = Attr(tag, "href");
                                var b = Resolve(href, dir, file);
                                if (b != null)
                                    baseDir = href.EndsWith('/') ? b : System.IO.Path.GetDirectoryName(b);
                                break;
                            }
                        case "script":
                            {
                                var type = (Attr(tag, "type") ?? "").Trim();
                                var semi = type.IndexOf(';');
                                if (semi >= 0)
                                    type = type.Substring(0, semi).Trim();
                                bool isJs = JsTypes.Contains(type);
                                bool isModule = type.Equals("module", StringComparison.OrdinalIgnoreCase);
                                var src = Attr(tag, "src");
                                var close = JsHtmlConverter.IndexOfIgnoreCase(html, "</script", tag.End);
                                if (close < 0)
                                    close = len;
                                if (src != null)
                                    Ref(src, baseDir, file, isJs ? (isModule ? JsSiteFileKind.Module : JsSiteFileKind.Script) : JsSiteFileKind.Asset, dir);
                                else if (isJs)
                                    ScanJs(html.Substring(tag.End, close - tag.End), isModule ? true : null, baseDir, file, dir);
                                pos = close;
                                break;
                            }
                        case "style":
                            {
                                var close = JsHtmlConverter.IndexOfIgnoreCase(html, "</style", tag.End);
                                if (close < 0)
                                    close = len;
                                ScanCss(html.Substring(tag.End, close - tag.End), baseDir, file, dir);
                                pos = close;
                                break;
                            }
                        case "link":
                            {
                                var href = Attr(tag, "href");
                                if (href == null)
                                    break;
                                var rel = " " + (Attr(tag, "rel") ?? "").ToLowerInvariant() + " ";
                                var @as = (Attr(tag, "as") ?? "").ToLowerInvariant();
                                JsSiteFileKind kind;
                                if (rel.Contains(" stylesheet ", StringComparison.Ordinal))
                                    kind = JsSiteFileKind.Css;
                                else if (rel.Contains(" modulepreload ", StringComparison.Ordinal))
                                    kind = JsSiteFileKind.Module;
                                else if (rel.Contains(" preload ", StringComparison.Ordinal) && @as == "script")
                                    kind = JsSiteFileKind.Script;
                                else if (rel.Contains(" preload ", StringComparison.Ordinal) && @as == "style")
                                    kind = JsSiteFileKind.Css;
                                else if (rel.Contains(" alternate ", StringComparison.Ordinal) || rel.Contains(" canonical ", StringComparison.Ordinal) || rel.Contains(" next ", StringComparison.Ordinal) || rel.Contains(" prev ", StringComparison.Ordinal))
                                    break;
                                else
                                    kind = JsSiteFileKind.Asset;
                                Ref(href, baseDir, file, kind, dir);
                                break;
                            }
                        case "img":
                        case "source":
                        case "video":
                        case "audio":
                        case "track":
                        case "input":
                        case "embed":
                        case "object":
                            Ref(Attr(tag, "src"), baseDir, file, JsSiteFileKind.Asset, dir);
                            Ref(Attr(tag, "poster"), baseDir, file, JsSiteFileKind.Asset, dir);
                            Ref(Attr(tag, "data"), baseDir, file, JsSiteFileKind.Asset, dir);
                            var srcset = Attr(tag, "srcset");
                            if (srcset != null)
                            {
                                foreach (var part in srcset.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                    Ref(part.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), baseDir, file, JsSiteFileKind.Asset, dir);
                            }
                            break;
                        case "textarea":
                        case "title":
                        case "xmp":
                        case "iframe":
                        case "noscript":
                        case "template":
                            {
                                var close = JsHtmlConverter.IndexOfIgnoreCase(html, "</" + name, tag.End);
                                if (name != "template" && name != "noscript")
                                    pos = close < 0 ? len : close;
                                break;
                            }
                    }
                }
            }

            void ScanCss(String css, String baseDir, String file, String pageDir)
            {
                css = CssCommentRegex.Replace(css, " ");
                var imports = new HashSet<int>();
                foreach (Match m in CssImportRegex.Matches(css))
                {
                    Ref(m.Groups[2].Value, baseDir, file, JsSiteFileKind.Css, pageDir);
                    imports.Add(m.Groups[2].Index);
                }
                foreach (Match m in CssUrlRegex.Matches(css))
                {
                    if (imports.Contains(m.Groups[2].Index))
                        continue;
                    Ref(m.Groups[2].Value, baseDir, file, JsSiteFileKind.Asset, pageDir);
                }
            }

            static String Literal(Expr e) => e switch
            {
                StringLiteral s => s.Value,
                TemplateLiteral { Expressions.Count: 0 } t => t.Quasis[0].Cooked,
                _ => null,
            };

            static bool LooksLikeFile(String s)
            {
                if (s == null || s.Length < 4 || s.Length > 260)
                    return false;
                foreach (var c in s)
                    if (char.IsWhiteSpace(c) || c == '<' || c == '>' || c == '"' || c == '\'' || c == '*' || c == '|' || c == '{' || c == '}')
                        return false;
                var e = System.IO.Path.GetExtension(s.Split('?', '#')[0]).ToLowerInvariant();
                return e == ".js" || e == ".mjs" || e == ".css";
            }

            /// <summary>
            /// Resolve a literal relative to the script folder, then the page folders, returns the first existing file
            /// </summary>
            void RefExisting(String url, IEnumerable<String> dirs, String file, String pageDir, JsSiteFileKind kind)
            {
                foreach (var d in dirs)
                {
                    var p = Resolve(url, d, file);
                    if (p != null && File.Exists(p))
                    {
                        Add(p, kind == JsSiteFileKind.Asset ? KindOf(p) : kind, file, pageDir);
                        return;
                    }
                }
            }

            void ScanJs(String code, bool? module, String baseDir, String file, String pageDir)
            {
                var prog = JsConverter.ParseProgram(code, module, out var isModule, out var error);
                if (prog == null)
                {
                    Warn(file, "Can't scan the script for dependencies, syntax error: " + error);
                    return;
                }
                var dirs = new List<String> { baseDir };
                if (pageDir != null && !dirs.Contains(pageDir, PathComparer))
                    dirs.Add(pageDir);
                foreach (var d in PageDirs)
                    if (!dirs.Contains(d, PathComparer))
                        dirs.Add(d);
                //  Workers and importScripts urls are relative to the page
                var docDirs = new List<String>(dirs.Skip(1)) { baseDir };
                void Module(String spec)
                {
                    if (spec == null)
                        return;
                    if (!(spec.StartsWith("./", StringComparison.Ordinal) || spec.StartsWith("../", StringComparison.Ordinal) || spec.StartsWith('/')))
                    {
                        if (!SchemeRegex.IsMatch(spec))
                            Warn(file, "The bare module specifier \"" + spec + "\" can't be resolved (use a relative path)", JsDiagnosticLevel.Info);
                        return;
                    }
                    Ref(spec, baseDir, file, JsSiteFileKind.Module, pageDir);
                }
                Walker.Walk(prog, n =>
                {
                    switch (n)
                    {
                        case ImportDecl im:
                            Module(im.Source?.Value);
                            break;
                        case ExportNamedDecl { Source: not null } en:
                            Module(en.Source.Value);
                            break;
                        case ExportAllDecl ea:
                            Module(ea.Source?.Value);
                            break;
                        case ImportCall ic:
                            Module(Literal(ic.Source));
                            break;
                        case NewExpr { Callee: Identifier { Name: "Worker" or "SharedWorker" } } nw when nw.Args.Count > 0:
                            {
                                var url = Literal(nw.Args[0]);
                                bool mod = nw.Args.Count > 1 && nw.Args[1] is ObjectExpr o && o.Properties.Exists(p => p is Property { Key: Identifier { Name: "type" }, Value: StringLiteral { Value: "module" } });
                                if (url != null)
                                    RefExisting(url, docDirs, file, pageDir, mod ? JsSiteFileKind.Module : JsSiteFileKind.Script);
                                break;
                            }
                        case CallExpr { Callee: Identifier { Name: "importScripts" } } isc:
                            foreach (var a in isc.Args)
                                RefExisting(Literal(a), docDirs, file, pageDir, JsSiteFileKind.Script);
                            break;
                        case CallExpr { Callee: MemberExpr { Computed: false, Property: Identifier { Name: "register" } } } reg when reg.Args.Count > 0 && Literal(reg.Args[0]) is String swUrl && LooksLikeFile(swUrl):
                            RefExisting(swUrl, docDirs, file, pageDir, JsSiteFileKind.Script);
                            break;
                        case StringLiteral or TemplateLiteral when P.ScanStrings:
                            {
                                var s = Literal((Expr)n);
                                if (LooksLikeFile(s) && !SchemeRegex.IsMatch(s) && !s.StartsWith("//", StringComparison.Ordinal))
                                    RefExisting(s, dirs, file, pageDir, JsSiteFileKind.Asset);
                                break;
                            }
                    }
                    return true;
                });
            }

            /// <summary>
            /// Compute the root and the relative paths, files outside an explicit root are removed
            /// </summary>
            public String Finish()
            {
                var root = Root;
                if (root == null)
                {
                    foreach (var f in Files.Values)
                    {
                        var d = System.IO.Path.GetDirectoryName(f.Path);
                        root = root == null ? d : Common(root, d);
                    }
                }
                root ??= Environment.CurrentDirectory;
                foreach (var f in Files.Values.ToList())
                {
                    var rel = System.IO.Path.GetRelativePath(root, f.Path);
                    if (rel.StartsWith("..", StringComparison.Ordinal) || System.IO.Path.IsPathRooted(rel))
                    {
                        Warn(f.Path, "The file is outside of the site root \"" + root + "\" and is skipped");
                        Files.Remove(f.Path);
                        continue;
                    }
                    f.RelativePath = rel;
                }
                return root;
            }

            static String Common(String a, String b)
            {
                while (!(b + System.IO.Path.DirectorySeparatorChar).StartsWith(a.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar, PathComparison))
                {
                    var p = System.IO.Path.GetDirectoryName(a);
                    if (p == null)
                        return a;
                    a = p;
                }
                return a;
            }
        }

        /// <summary>
        /// Collect all files used by some html pages
        /// </summary>
        /// <param name="htmlFiles">The pages</param>
        /// <param name="sp">Options, null for the defaults</param>
        /// <returns>The files (pages first, then in discovery order) and the problems found</returns>
        public static JsSiteResult Collect(IEnumerable<String> htmlFiles, JsSiteParams sp = null)
        {
            sp ??= new JsSiteParams();
            var c = new Collector(sp);
            foreach (var h in htmlFiles)
                c.AddPage(h);
            c.Run();
            var r = new JsSiteResult();
            r.Root = c.Finish();
            r.Files.AddRange(c.Files.Values);
            r.Diagnostics.AddRange(c.Diag);
            return r;
        }

        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        static async Task<bool> WriteText(String file, String text, bool force)
        {
            if (!force && File.Exists(file) && await File.ReadAllTextAsync(file).ConfigureAwait(false) == text)
                return false;
            await File.WriteAllTextAsync(file, text, Utf8).ConfigureAwait(false);
            return true;
        }

        static bool Copy(String source, String dest, bool force)
        {
            var fs = new FileInfo(source);
            var fd = new FileInfo(dest);
            if (!force && fd.Exists && fd.Length == fs.Length && fd.LastWriteTimeUtc == fs.LastWriteTimeUtc)
                return false;
            File.Copy(source, dest, true);
            File.SetLastWriteTimeUtc(dest, fs.LastWriteTimeUtc);
            return true;
        }

        /// <summary>
        /// Insert a script element that loads a file before the first script of a page (or before the end of the head, or at the start)
        /// </summary>
        static String InsertScript(String html, String src)
        {
            var tag = "<script src=\"" + WebUtility.HtmlEncode(src) + "\"></script>";
            int at = -1, pos = 0;
            while (pos < html.Length)
            {
                var lt = html.IndexOf('<', pos);
                if (lt < 0)
                    break;
                if (String.CompareOrdinal(html, lt, "<!--", 0, 4) == 0)
                {
                    var ce = html.IndexOf("-->", lt + 4, StringComparison.Ordinal);
                    pos = ce < 0 ? html.Length : ce + 3;
                    continue;
                }
                var t = JsHtmlConverter.ParseTag(html, lt);
                if (t != null && t.Name.Equals("script", StringComparison.OrdinalIgnoreCase))
                {
                    at = lt;
                    break;
                }
                pos = lt + 1;
            }
            if (at < 0)
                at = JsHtmlConverter.IndexOfIgnoreCase(html, "</head", 0);
            if (at < 0)
                at = 0;
            return html.Substring(0, at) + tag + "\n" + html.Substring(at);
        }

        /// <summary>
        /// Clone the site of some html pages to a folder: pages and scripts are converted, style sheets and other files are copied.
        /// Files that can't be converted are copied unchanged (and counted in Failed).
        /// </summary>
        /// <param name="htmlFiles">The pages</param>
        /// <param name="outputFolder">The output folder (the files keep their paths relative to the site root)</param>
        /// <param name="p">The conversion options</param>
        /// <param name="sp">The site options, null for the defaults</param>
        /// <param name="onFile">Optionally called for every file: the file, the conversion result (null for copied files), true if the output was written</param>
        /// <returns>The files and all diagnostics (with File set)</returns>
        public static async Task<JsSiteResult> CloneAsync(IEnumerable<String> htmlFiles, String outputFolder, JsConverterParams p, JsSiteParams sp = null, Action<JsSiteFile, JsConvertResult, bool> onFile = null)
        {
            sp ??= new JsSiteParams();
            p ??= new JsConverterParams();
            var r = Collect(htmlFiles, sp);
            var output = System.IO.Path.GetFullPath(outputFolder);
            if (!sp.Overwrite && String.Equals(output.TrimEnd(System.IO.Path.DirectorySeparatorChar), r.Root.TrimEnd(System.IO.Path.DirectorySeparatorChar), PathComparison))
            {
                r.Diagnostics.Add(new JsDiagnostic { Level = JsDiagnosticLevel.Error, File = r.Root, Message = "The output folder is the site root, the files would be overwritten (use a different output folder or allow overwriting)" });
                ++r.Failed;
                return r;
            }
            bool external = p.Helpers == JsHelperMode.External && !String.IsNullOrEmpty(sp.HelpersFile);
            var helpers = new ConcurrentDictionary<String, bool>(StringComparer.Ordinal);
            var polyfills = new ConcurrentDictionary<String, bool>(StringComparer.Ordinal);
            var helpersPath = external ? System.IO.Path.GetFullPath(System.IO.Path.Combine(output, sp.HelpersFile)) : null;
            var diags = new ConcurrentBag<JsDiagnostic>();
            int failed = 0, written = 0;
            await Parallel.ForEachAsync(r.Files, async (f, ct) =>
            {
                var dest = System.IO.Path.Combine(output, f.RelativePath);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest));
                JsConvertResult cr = null;
                bool w;
                if (f.Kind == JsSiteFileKind.Css || f.Kind == JsSiteFileKind.Asset)
                    w = Copy(f.Path, dest, sp.Force);
                else
                {
                    var src = await File.ReadAllTextAsync(f.Path, ct).ConfigureAwait(false);
                    var fp = p.Clone();
                    if (f.Kind == JsSiteFileKind.Module)
                        fp.SourceType = JsSourceType.Module;
                    cr = f.Kind == JsSiteFileKind.Html ? JsHtmlConverter.Convert(src, fp) : JsConverter.Convert(src, fp);
                    foreach (var d in cr.Diagnostics)
                    {
                        d.File = f.Path;
                        diags.Add(d);
                    }
                    String text;
                    if (cr.Code == null)
                    {
                        System.Threading.Interlocked.Increment(ref failed);
                        text = src;
                    }
                    else
                    {
                        text = cr.Code;
                        foreach (var h in cr.Helpers)
                            helpers[h] = true;
                        foreach (var h in cr.Polyfills)
                            polyfills[h] = true;
                        if (external && f.Kind == JsSiteFileKind.Html)
                        {
                            var rel = System.IO.Path.GetRelativePath(System.IO.Path.GetDirectoryName(dest), helpersPath).Replace(System.IO.Path.DirectorySeparatorChar, '/');
                            text = InsertScript(text, rel);
                        }
                    }
                    w = await WriteText(dest, text, sp.Force).ConfigureAwait(false);
                }
                if (w)
                    System.Threading.Interlocked.Increment(ref written);
                onFile?.Invoke(f, cr, w);
            }).ConfigureAwait(false);
            if (external)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(helpersPath));
                var hs = JsConverter.GetHelpersSource(helpers.Keys.OrderBy(x => x, StringComparer.Ordinal), p.Minify, polyfills.Keys.OrderBy(x => x, StringComparer.Ordinal));
                if (await WriteText(helpersPath, hs, sp.Force).ConfigureAwait(false))
                    ++written;
            }
            r.Diagnostics.AddRange(diags.OrderBy(x => x.File, StringComparer.Ordinal).ThenBy(x => x.Line).ThenBy(x => x.Column));
            r.Failed = failed;
            r.Written = written;
            return r;
        }
    }
}
