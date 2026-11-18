using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace EffectWeaver.Shaders;

/// <summary>
/// The Ew snippet library: reusable GLSL functions, one <c>&lt;folder&gt;/&lt;Id&gt;.glsl</c> + <c>&lt;Id&gt;.md</c> pair per snippet.
/// The .glsl header comment names the dependencies ("//  Requires: EwHash2To1, ..."), the .md has the summary (first paragraph) and the signature,
/// the README.md the library docs and the folder list.
/// The source is srcData/ of this project, embedded compressed (<see cref="Builtin"/>, <see cref="BuiltinFiles"/>, <see cref="ReadBuiltinFile"/>);
/// a snippet folder (the Effect Weaver editor's, where admins save) goes on top of it (<see cref="WithFolder"/>).
/// </summary>
public sealed class SnippetLibrary
{
    /// <summary>A snippet (a reusable GLSL function)</summary>
    public sealed class Snippet
    {
        /// <summary>The id (the file name without extension)</summary>
        public required string Id;
        /// <summary>The folder of the snippet in the library</summary>
        public required string Folder;
        /// <summary>The summary (the first paragraph of the .md)</summary>
        public string Summary = "";
        /// <summary>The signature (from the .md)</summary>
        public string Signature = "";
        /// <summary>Snippets this one calls (inserted before it)</summary>
        public string[] Requires = [];
        /// <summary>The .glsl file (a built in snippet: its path in the library, "&lt;folder&gt;/&lt;Id&gt;.glsl", see <see cref="ReadBuiltinFile"/>)</summary>
        public required string GlslFile;
        /// <summary>The .md file, null if none (a built in snippet: its path in the library)</summary>
        public string? MdFile;
        /// <summary>An embedded snippet of the built in library (<see cref="Builtin"/>), false for a file (<see cref="Load"/>) or a user's snippet</summary>
        public bool IsBuiltin;
        /// <summary>A snippet of a folder that replaces the built in one with the same id (<see cref="WithFolder"/>)</summary>
        public bool OverridesBuiltin;
        /// <summary>Lower case text for the search: id, summary, md, code</summary>
        public string SearchText = "";
        /// <summary>The code (.glsl, "\n" line ends)</summary>
        public string Code = "";
        /// <summary>The functions / EW_ macros it defines</summary>
        public HashSet<string> Names = [];
        /// <summary>The description (.md, "" if none)</summary>
        public string Md = "";
        /// <summary>A user's snippet: its path in the editor ("My snippets/&lt;folder&gt;/&lt;Id&gt;.glsl"), null for the library's</summary>
        public string? UserPath;
    }

    /// <summary>The root folder of the library</summary>
    public string Root { get; }
    /// <summary>All snippets, sorted by folder and id</summary>
    public IReadOnlyList<Snippet> All { get; }
    /// <summary>The distinct folders that contain snippets</summary>
    public IReadOnlyList<string> Folders { get; }

    readonly Dictionary<string, Snippet> Map;

    /// <summary>Snippets by the names they define (functions, EW_ macros)</summary>
    readonly Dictionary<string, Snippet> ByName = new(StringComparer.Ordinal);

    /// <summary>Folders of the snippet root that aren't snippets</summary>
    static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase) { "effects", "tools" };

    static readonly Regex RequiresRx = new(@"^//\s*Requires\s*:\s*(.*)$", RegexOptions.Multiline | RegexOptions.Compiled);
    static readonly Regex SnippetNameRx = new(@"\bEw[A-Z]\w*", RegexOptions.Compiled);
    static readonly Regex SignatureRx = new(@"##\s*Signature\s*\n+```\w*\n(.*?)\n```", RegexOptions.Singleline | RegexOptions.Compiled);

    SnippetLibrary(string root, List<Snippet> all)
    {
        Root = root;
        all.Sort((a, b) => string.Compare(a.Folder + "/" + a.Id, b.Folder + "/" + b.Id, StringComparison.OrdinalIgnoreCase));
        All = all;
        Map = all.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        Folders = all.Select(x => x.Folder).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var s in all)
            foreach (var n in s.Names)
                ByName.TryAdd(n, s);
        // Dependencies: the Requires header line + the snippet names the code uses
        foreach (var s in all)
            s.Requires = s.Requires.Concat(UsedNames(StripComments(s.Code)).Where(n => !s.Names.Contains(n)).Select(n => ByName.GetValueOrDefault(n)?.Id).OfType<string>())
                .Where(x => !x.Equals(s.Id, StringComparison.Ordinal)).Distinct().ToArray();
    }

    /// <summary>Get a snippet by id (case insensitive), null if unknown</summary>
    public Snippet? Get(string? id) => id != null && Map.TryGetValue(id, out var s) ? s : null;

    /// <summary>The snippet defining a name (function or EW_ macro), null if none</summary>
    public Snippet? ByDefinedName(string name) => ByName.GetValueOrDefault(name);

    static readonly Regex UsedNameRx = new(@"\b(?:Ew[A-Z]\w*|EW_[A-Z0-9_]+)\b", RegexOptions.Compiled);

    /// <summary>The Ew… / EW_… names in some code (comments removed first, see <see cref="StripComments"/>)</summary>
    public static IEnumerable<string> UsedNames(string code) => UsedNameRx.Matches(code).Select(m => m.Value).Distinct();

    /// <summary>The code with the comments replaced by spaces (same length, line ends kept)</summary>
    public static string StripComments(string s)
    {
        var c = s.ToCharArray();
        for (int i = 0; i < c.Length; ++i)
        {
            if (c[i] == '/' && i + 1 < c.Length && c[i + 1] == '/')
            {
                while (i < c.Length && c[i] != '\n')
                    c[i++] = ' ';
            }
            else if (c[i] == '/' && i + 1 < c.Length && c[i + 1] == '*')
            {
                c[i] = c[i + 1] = ' ';
                i += 2;
                while (i < c.Length && !(c[i] == '*' && i + 1 < c.Length && c[i + 1] == '/'))
                {
                    if (c[i] != '\n')
                        c[i] = ' ';
                    ++i;
                }
                if (i < c.Length)
                    c[i] = c[i + 1] = ' ';
                ++i;
            }
        }
        return new string(c);
    }

    #region The built in library (embedded)

    /// <summary>The name of the root of the built in library (<see cref="Root"/>)</summary>
    public const string BuiltinRoot = "(built in)";

    /// <summary>The prefix of the embedded files: "snippets/&lt;path&gt;.br"</summary>
    const string ResourcePrefix = "snippets/";

    /// <summary>The embedded files by path ("color/EwHsvToRgb.glsl", "color/EwHsvToRgb.md", "README.md"): their resource names</summary>
    static readonly Lazy<Dictionary<string, string>> LazyResources = new(() =>
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in typeof(SnippetLibrary).Assembly.GetManifestResourceNames())
            if (name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
                map[name[ResourcePrefix.Length..^".br".Length].Replace('\\', '/')] = name;
        return map;
    });

    /// <summary>Decoded embedded files (each is decoded once)</summary>
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Decoded = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The files of the built in library, as paths relative to its root ("/" separated): "&lt;folder&gt;/&lt;Id&gt;.glsl", "&lt;folder&gt;/&lt;Id&gt;.md"
    /// and "README.md" (the library docs and the folder list). Sorted.
    /// </summary>
    public static IReadOnlyList<string> BuiltinFiles => LazyBuiltinFiles.Value;

    static readonly Lazy<IReadOnlyList<string>> LazyBuiltinFiles = new(() => LazyResources.Value.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList());

    /// <summary>
    /// The text of a file of the built in library (see <see cref="BuiltinFiles"/>, ex: "color/EwHsvToRgb.md"), "\n" line ends; null if there is no
    /// such file. Embedded brotli compressed, decoded the first time it's read and cached.
    /// </summary>
    public static string? ReadBuiltinFile(string path)
    {
        var key = (path ?? "").Replace('\\', '/').TrimStart('/');
        if (Decoded.TryGetValue(key, out var text))
            return text;
        if (!LazyResources.Value.TryGetValue(key, out var name))
            return null;
        using var stream = typeof(SnippetLibrary).Assembly.GetManifestResourceStream(name)!;
        using var brotli = new BrotliStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(brotli, Encoding.UTF8);
        return Decoded.GetOrAdd(key, reader.ReadToEnd().Replace("\r\n", "\n"));
    }

    /// <summary>
    /// The built in library: the snippets embedded in SysWeaver.Media.Shader (srcData/, the source of the library, embedded brotli compressed: the code,
    /// and the descriptions). Made when first used, then shared.
    /// Used when an effect is built (<see cref="ShaderParser.BuildParts"/>), so Effect Saver, Effect Vj and the SysWeaver media player run effects that call snippets;
    /// the Effect Weaver editor uses it with its snippet folder on top (<see cref="WithFolder"/>: the admins' edits).
    /// </summary>
    public static SnippetLibrary Builtin => LazyBuiltin.Value;

    static readonly Lazy<SnippetLibrary> LazyBuiltin = new(() =>
    {
        var list = new List<Snippet>();
        foreach (var path in BuiltinFiles)
        {
            var parts = path.Split('/');
            if (parts.Length != 2 || Skip.Contains(parts[0]) || !parts[1].EndsWith(".glsl", StringComparison.OrdinalIgnoreCase))
                continue;
            var id = System.IO.Path.GetFileNameWithoutExtension(parts[1]);
            var mdPath = parts[0] + "/" + id + ".md";
            var md = ReadBuiltinFile(mdPath);
            var s = Create(id, parts[0], ReadBuiltinFile(path)!, md ?? "", path, md == null ? null : mdPath);
            s.IsBuiltin = true;
            list.Add(s);
        }
        return new SnippetLibrary(BuiltinRoot, list);
    });

    /// <summary>
    /// The built in library with the snippets of a folder on top (the Effect Weaver editor's snippet folder, where the admins save): a snippet of the folder
    /// replaces the built in one with the same id (<see cref="Snippet.OverridesBuiltin"/>), the others are added. Built in snippets the folder's replacement
    /// no longer defines a name of are still found by their other names. No folder (null / empty / missing): the built in library.
    /// </summary>
    public static SnippetLibrary WithFolder(string? folder, Action<string> log)
    {
        var builtin = Builtin;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return builtin;
        var files = Load(folder, log);
        if (files.All.Count == 0)
            return new SnippetLibrary(folder, builtin.All.ToList());
        var list = new List<Snippet>();
        foreach (var s in builtin.All)
            if (files.Get(s.Id) == null)
                list.Add(s);
        foreach (var f in files.All)
        {
            var b = builtin.Get(f.Id);
            f.OverridesBuiltin = b != null;
            list.Add(f);
        }
        return new SnippetLibrary(folder, list);
    }

    #endregion

    /// <summary>Load the snippets of a folder (an empty library if it doesn't exist), without the built in ones (see <see cref="WithFolder"/>)</summary>
    public static SnippetLibrary Load(string root, Action<string> log)
    {
        var list = new List<Snippet>();
        if (!Directory.Exists(root))
            return new SnippetLibrary(root, list);
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var folder = System.IO.Path.GetFileName(dir);
            if (Skip.Contains(folder) || folder.StartsWith('.'))
                continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.glsl"))
            {
                try
                {
                    var id = System.IO.Path.GetFileNameWithoutExtension(file);
                    var mdFile = System.IO.Path.ChangeExtension(file, ".md");
                    var md = File.Exists(mdFile) ? File.ReadAllText(mdFile) : "";
                    var s = Create(id, folder, File.ReadAllText(file), md, file, md.Length > 0 ? mdFile : null);
                    list.Add(s);
                }
                catch (Exception ex)
                {
                    log($"Failed to load snippet \"{file}\": {ex.Message}");
                }
            }
        }
        return new SnippetLibrary(root, list);
    }

    /// <summary>A snippet from its code and description (the library's files, or a user's snippet from the storage)</summary>
    public static Snippet Create(string id, string folder, string code, string md, string glslFile, string? mdFile)
    {
        code = code.Replace("\r\n", "\n");
        md = md.Replace("\r\n", "\n");
        var s = new Snippet
        {
            Id = id,
            Folder = folder,
            GlslFile = glslFile,
            MdFile = mdFile,
            Code = code,
            Md = md,
            Names = DefinedIn(StripComments(code)),
        };
        var rm = RequiresRx.Match(code);
        if (rm.Success)
            s.Requires = SnippetNameRx.Matches(rm.Groups[1].Value).Select(x => x.Value).Where(x => !x.Equals(id, StringComparison.Ordinal)).Distinct().ToArray();
        s.Summary = Summary(md);
        var sm = SignatureRx.Match(md);
        if (sm.Success)
            s.Signature = sm.Groups[1].Value.Trim();
        s.SearchText = (id + "\n" + folder + "\n" + md + "\n" + code).ToLowerInvariant();
        return s;
    }

    /// <summary>This library and a user's snippets (they can't replace the library's: conflicting ones are left out, see <see cref="Conflict"/>)</summary>
    public SnippetLibrary With(IEnumerable<Snippet> user)
    {
        var add = user.Where(s => Conflict(s) == null).ToList();
        return add.Count == 0 ? this : new SnippetLibrary(Root, All.Concat(add).ToList());
    }

    /// <summary>Why a user's snippet can't be added to this library (it would replace one of the library's), null if it can</summary>
    public string? Conflict(Snippet s)
    {
        if (Map.TryGetValue(s.Id, out var o) && o.UserPath == null)
            return s.Id + " is a built in snippet";
        var n = s.Names.FirstOrDefault(x => ByName.TryGetValue(x, out var d) && d.UserPath == null);
        return n == null ? null : n + " is defined by the built in snippet " + ByName[n].Id;
    }

    /// <summary>The first paragraph after the title of a description</summary>
    static string Summary(string md)
    {
        var sb = new StringBuilder();
        foreach (var raw in md.Split('\n'))
        {
            var l = raw.Trim();
            if (l.StartsWith('#'))
            {
                if (sb.Length > 0)
                    break;
                continue;
            }
            if (l.Length == 0)
            {
                if (sb.Length > 0)
                    break;
                continue;
            }
            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(l);
        }
        return PlainText(sb.ToString());
    }

    /// <summary>
    /// Search: all words of the query must be found (ignoring case). Ranked: name matches first, then the summary, then the rest; then by use.
    /// Returns the snippets with a short excerpt around the first match in the description.
    /// </summary>
    public List<(Snippet Snippet, string Excerpt)> Search(string query, string? folder)
    {
        var words = (query ?? "").ToLowerInvariant().Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries);
        var r = new List<(Snippet, int, string)>();
        foreach (var s in All)
        {
            if (!string.IsNullOrEmpty(folder) && !s.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase))
                continue;
            if (words.Any(w => !s.SearchText.Contains(w, StringComparison.Ordinal)))
                continue;
            var id = s.Id.ToLowerInvariant();
            var sum = s.Summary.ToLowerInvariant();
            var score = words.Sum(w => id.Contains(w) ? 100 : sum.Contains(w) ? 10 : 1);
            r.Add((s, score, words.Length == 0 ? "" : Excerpt(s, words)));
        }
        return r.OrderByDescending(x => x.Item2).ThenBy(x => x.Item1.Id, StringComparer.OrdinalIgnoreCase).Select(x => (x.Item1, x.Item3)).ToList();
    }

    /// <summary>Markdown inline formatting removed: links → their text, no backticks / emphasis</summary>
    static string PlainText(string md) =>
        Regex.Replace(Regex.Replace(md, @"!?\[([^\]]*)\]\([^)]*\)", "$1"), @"[`*]|(?<!\w)_|_(?!\w)", "");

    /// <summary>The line of the description (else the code) with most of the words (shortened around the first one), "" if all are in the name / summary</summary>
    static string Excerpt(Snippet s, string[] words)
    {
        var sum = s.Summary.ToLowerInvariant();
        if (words.All(w => s.Id.Contains(w, StringComparison.OrdinalIgnoreCase) || sum.Contains(w)))
            return "";
        foreach (var text in new[] { s.Md, s.Code })
        {
            string? best = null;
            int bestCount = 0;
            foreach (var raw in text.Split('\n'))
            {
                var l = PlainText(raw.Trim().TrimStart('/', '#', '|', '-', ' '));
                if (l.Length < 8 || raw.TrimStart().StartsWith("<!--") || raw.TrimStart().StartsWith('#') || s.Summary.Contains(l, StringComparison.OrdinalIgnoreCase))
                    continue;
                var count = words.Count(w => l.Contains(w, StringComparison.OrdinalIgnoreCase));
                if (count > bestCount)
                    (best, bestCount) = (l, count);
            }
            if (best == null)
                continue;
            var i = words.Select(w => best.IndexOf(w, StringComparison.OrdinalIgnoreCase)).Where(x => x >= 0).Min();
            var start = Math.Max(0, i - 50);
            var end = Math.Min(best.Length, i + 90);
            return (start > 0 ? "…" : "") + best[start..end] + (end < best.Length ? "…" : "");
        }
        return "";
    }

    /// <summary>The snippet and its dependencies in insertion order (dependencies first, each once)</summary>
    public List<Snippet> WithDependencies(Snippet s)
    {
        var order = new List<Snippet>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(Snippet x)
        {
            if (!seen.Add(x.Id))
                return;
            foreach (var d in x.Requires)
                if (Get(d) is { } dep)
                    Visit(dep);
            order.Add(x);
        }
        Visit(s);
        return order;
    }

    /// <summary>A function definition / prototype ("float EwHash2To1(", not "return EwHash2To1("), an EW_ macro or a global Ew constant
    /// ("const float EwAudioBassV = ...;")</summary>
    static readonly Regex DefinedRx = new(@"^[ \t]*(?:(?:highp|mediump|lowp)[ \t]+)?(?!return\b|else\b|const\b)\w+[ \t]+(Ew[A-Z]\w*)[ \t]*\(|^[ \t]*#[ \t]*define[ \t]+(EW_\w+)|^const[ \t]+(?:(?:highp|mediump|lowp)[ \t]+)?\w+[ \t]+(Ew[A-Z]\w*)[ \t]*=", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>The Ew functions / EW_ macros / Ew constants a source defines</summary>
    public static HashSet<string> DefinedIn(string source)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in DefinedRx.Matches(source))
            set.Add(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value);
        return set;
    }

    /// <summary>The names a snippet file defines (functions / EW_ macros)</summary>
    public static HashSet<string> DefinedBy(Snippet s) => s.Names;
}
