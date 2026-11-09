using System.Text;
using System.Text.RegularExpressions;

namespace EffectWeaver.Shaders;

/// <summary>
/// The optional vertex stage of an effect: a ShaderToy style shader may define <c>void vertexMain(vec2 fragCoord)</c> next to mainImage.
/// It runs once per vertex of the full screen triangle (the corners at the pixel coordinates (0, 0), (2 w, 0) and (0, 2 h)), its results reach
/// mainImage through <c>varying</c> declarations (top level, e.g. <c>varying vec3 vRayDir;</c>, ints are flat), linearly interpolated over the
/// screen: exact for values that are constant (camera, time based colors / matrices) or affine in fragCoord (uv, un-normalized ray directions).
/// <para>
/// The source is split (<see cref="Split"/>): the vertex stage keeps the functions vertexMain reaches (through calls and macros), the fragment
/// stage drops vertexMain and the functions only it reaches. Removed code is blanked (spaces, newlines kept), so the line numbers stay the
/// source's and preprocessor lines (#line, #define, #if ...) are kept. <see cref="RewriteVaryings"/> turns the declarations into in / out
/// (GLSL ES 3.00) or keeps varying (1.00).
/// </para>
/// <para>
/// A built program travels as one string: the fragment shader, then <see cref="Marker"/> and the vertex shader (<see cref="SplitProgram"/>),
/// so every api returning a shader source carries both. <see cref="ToPerPixel"/> converts a source to one without a vertex stage
/// (vertexMain called per pixel before mainImage, the varyings plain globals): ShaderToy exports, plots, reference renders.
/// </para>
/// </summary>
public static class ShaderStages
{
    /// <summary>The entry point of the vertex stage</summary>
    public const string Entry = "vertexMain";

    /// <summary>Separates the fragment shader (first) and the vertex shader in a built program source</summary>
    public const string Marker = "\n//@ew-vertex-shader\n";

    /// <summary>The attribute of the GLSL ES 1.00 vertex stage (GLSL ES 3.00 uses gl_VertexID), renderers bind it to location 0</summary>
    public const string PositionAttribute = "effectSaverPosition";

    static readonly Regex EntryRx = new(@"\bvoid\s+vertexMain\s*\(", RegexOptions.Compiled);
    static readonly Regex EntryNoArgsRx = new(@"\bvoid\s+vertexMain\s*\(\s*(void\s*)?\)", RegexOptions.Compiled);
    static readonly Regex VaryingRx = new(@"(?<q>(?:\b(?:flat|smooth|centroid|invariant)\s+)*)\bvarying\s+(?<p>(?:(?:highp|mediump|lowp)\s+)?)(?<t>[A-Za-z_]\w*)", RegexOptions.Compiled);
    static readonly Regex QualifierRx = new(@"\b(flat|smooth|centroid|invariant)\s+", RegexOptions.Compiled);
    static readonly Regex IntTypeRx = new(@"^(int|uint|[iu]vec[234])$", RegexOptions.Compiled);

    /// <summary>True if the code (comments removed) defines vertexMain</summary>
    public static bool HasEntry(string code) => EntryRx.IsMatch(code);

    /// <summary>The fragment shader and the vertex shader (null if none) of a built program source</summary>
    public static (string Fragment, string? Vertex) SplitProgram(string source)
    {
        var i = source.IndexOf(Marker, StringComparison.Ordinal);
        return i < 0 ? (source, null) : (source[..i], source[(i + Marker.Length)..]);
    }

    /// <summary>A program source: the fragment shader and, if there is one, the vertex shader</summary>
    public static string JoinProgram(string fragment, string? vertex) => vertex == null ? fragment : fragment + Marker + vertex;

    #region Scanner

    /// <summary>A top level function definition: [Start, End) from the end of the previous top level statement to its closing brace</summary>
    sealed class Function
    {
        public required string Name;
        public int Start, Body, End;
        public readonly HashSet<string> Ids = new(StringComparer.Ordinal);
    }

    sealed class Scan
    {
        public readonly List<Function> Functions = [];
        /// <summary>Macro name => the identifiers of its replacement</summary>
        public readonly Dictionary<string, HashSet<string>> Macros = new(StringComparer.Ordinal);
        /// <summary>Preprocessor lines (kept when code is blanked)</summary>
        public required bool[] Directive;
        /// <summary>The code with comments replaced by spaces (same positions)</summary>
        public required string Masked;
    }

    static bool IdStart(char c) => char.IsAsciiLetter(c) || c == '_';
    static bool IdChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    static Scan Analyze(string code)
    {
        var n = code.Length;
        var directive = new bool[n];
        var masked = code.ToCharArray();
        var functions = new List<Function>();
        var macros = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        int depth = 0, paren = 0, stmtStart = 0;
        string? lastId = null, candidate = null;
        bool afterParams = false, lineStart = true;
        Function? cur = null;
        int i = 0;
        while (i < n)
        {
            var c = code[i];
            if (c == '/' && i + 1 < n && code[i + 1] == '/')
            {
                while (i < n && code[i] != '\n')
                    masked[i++] = ' ';
                continue;
            }
            if (c == '/' && i + 1 < n && code[i + 1] == '*')
            {
                var e = code.IndexOf("*/", i + 2, StringComparison.Ordinal);
                e = e < 0 ? n : e + 2;
                for (; i < e; ++i)
                    if (code[i] != '\n')
                        masked[i] = ' ';
                continue;
            }
            if (c == '\n')
            {
                lineStart = true;
                ++i;
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                ++i;
                continue;
            }
            if (c == '#' && lineStart)
            {
                // A preprocessor line (with \ continuations)
                var s = i;
                while (i < n && !(code[i] == '\n' && code[i - 1] != '\\'))
                    ++i;
                for (var k = s; k < i; ++k)
                    directive[k] = true;
                var text = code[s..i];
                var m = Regex.Match(text, @"^#\s*define\s+([A-Za-z_]\w*)(.*)$", RegexOptions.Singleline);
                if (m.Success)
                {
                    var ids = macros.TryGetValue(m.Groups[1].Value, out var set) ? set : macros[m.Groups[1].Value] = new(StringComparer.Ordinal);
                    foreach (Match x in Regex.Matches(CommentStrip(m.Groups[2].Value), @"[A-Za-z_]\w*"))
                        ids.Add(x.Value);
                }
                if (depth == 0)
                    stmtStart = i;
                continue;
            }
            lineStart = false;
            if (IdStart(c))
            {
                var s = i;
                while (i < n && IdChar(code[i]))
                    ++i;
                var id = code[s..i];
                if (cur != null)
                    cur.Ids.Add(id);
                else if (depth == 0)
                {
                    if (paren == 0)
                    {
                        lastId = id;
                        afterParams = false;
                    }
                }
                continue;
            }
            if (char.IsAsciiDigit(c))
            {
                while (i < n && (IdChar(code[i]) || code[i] == '.'))
                    ++i;
                continue;
            }
            if (depth == 0)
            {
                switch (c)
                {
                    case '(':
                        if (paren++ == 0)
                            candidate = lastId;
                        break;
                    case ')':
                        if (paren > 0 && --paren == 0)
                            afterParams = true;
                        break;
                    case '{':
                        if (paren == 0 && afterParams && candidate != null)
                            cur = new Function { Name = candidate, Start = stmtStart, Body = i };
                        ++depth;
                        afterParams = false;
                        candidate = null;
                        break;
                    case ';':
                        if (paren == 0)
                        {
                            stmtStart = i + 1;
                            afterParams = false;
                            candidate = lastId = null;
                        }
                        break;
                    default:
                        if (paren == 0)
                            afterParams = false;
                        break;
                }
            }
            else if (c == '{')
                ++depth;
            else if (c == '}' && --depth == 0)
            {
                if (cur != null)
                {
                    cur.End = i + 1;
                    functions.Add(cur);
                    cur = null;
                }
                stmtStart = i + 1;
                afterParams = false;
                candidate = lastId = null;
            }
            ++i;
        }
        var scan = new Scan { Directive = directive, Masked = new string(masked) };
        scan.Functions.AddRange(functions);
        foreach (var (k, v) in macros)
            scan.Macros[k] = v;
        return scan;
    }

    /// <summary>A top level function: [Start, End) from the end of the previous top level statement to its closing brace, Body = its opening brace</summary>
    public sealed record FunctionRange(string Name, int Start, int Body, int End);

    /// <summary>The top level functions of a source and the source with comments replaced by spaces (same positions)</summary>
    public static (string Masked, List<FunctionRange> Functions) Outline(string code)
    {
        var s = Analyze(code);
        return (s.Masked, s.Functions.Select(f => new FunctionRange(f.Name, f.Start, f.Body, f.End)).ToList());
    }

    static string CommentStrip(string s) => Regex.Replace(s, @"/\*.*?\*/|//[^\n]*", " ", RegexOptions.Singleline);

    /// <summary>The names reachable from the entry points (function calls and macros)</summary>
    static HashSet<string> Reachable(Scan s, params string[] entries)
    {
        var byName = s.Functions.GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(entries);
        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            if (!seen.Add(name))
                continue;
            if (byName.TryGetValue(name, out var fs))
                foreach (var f in fs)
                    foreach (var id in f.Ids)
                        if (!seen.Contains(id))
                            queue.Enqueue(id);
            if (s.Macros.TryGetValue(name, out var ids))
                foreach (var id in ids)
                    if (!seen.Contains(id))
                        queue.Enqueue(id);
        }
        return seen;
    }

    /// <summary>Blank the functions not kept (spaces, newlines and preprocessor lines stay)</summary>
    static string Blank(string code, Scan s, Func<Function, bool> keep)
    {
        var a = code.ToCharArray();
        foreach (var f in s.Functions)
        {
            if (keep(f))
                continue;
            for (var i = f.Start; i < f.End; ++i)
                if (a[i] != '\n' && !s.Directive[i])
                    a[i] = ' ';
        }
        return new string(a);
    }

    #endregion

    /// <summary>
    /// Split a shader body (with mainImage and vertexMain) into the code of the two stages (same length and lines as the input):
    /// the vertex stage keeps the functions vertexMain reaches, the fragment stage drops the ones only vertexMain reaches (and vertexMain).
    /// Everything that isn't a function (constants, globals, structs, uniforms, varyings, prototypes, preprocessor lines) is in both.
    /// </summary>
    public static (string Fragment, string Vertex) Split(string body)
    {
        var s = Analyze(body);
        var vertex = Reachable(s, Entry);
        var fragment = Reachable(s, "mainImage");
        return (Blank(body, s, f => !vertex.Contains(f.Name) || fragment.Contains(f.Name)), Blank(body, s, f => vertex.Contains(f.Name)));
    }

    public enum Stage { Fragment, Vertex, PerPixel }

    /// <summary>
    /// The top level varying declarations for a stage: GLSL ES 3.00 out (vertex) / in (fragment), integer types flat; GLSL ES 1.00 varying
    /// (interpolation qualifiers removed); PerPixel: plain globals. Only declarations outside of functions are changed.
    /// </summary>
    public static string RewriteVaryings(string body, Stage stage, bool es3)
    {
        var s = Analyze(body);
        // A top level declaration ends with ';', so a match inside a function's range is in its body
        bool InFunction(int pos) => s.Functions.Any(f => pos >= f.Start && pos < f.End);
        var sb = new StringBuilder(body.Length + 64);
        var last = 0;
        foreach (Match m in VaryingRx.Matches(s.Masked))
        {
            if (InFunction(m.Index))
                continue;
            var q = m.Groups["q"].Value;
            var type = m.Groups["t"].Value;
            string repl;
            if (stage == Stage.PerPixel)
                repl = m.Groups["p"].Value + type;
            else if (!es3)
                repl = QualifierRx.Replace(q, x => x.Groups[1].Value == "invariant" ? x.Value : "") + "varying " + m.Groups["p"].Value + type;
            else
            {
                if (IntTypeRx.IsMatch(type) && !q.Contains("flat"))
                    q = "flat " + q;
                repl = q + (stage == Stage.Vertex ? "out " : "in ") + m.Groups["p"].Value + type;
            }
            sb.Append(body, last, m.Index - last).Append(repl);
            last = m.Index + m.Length;
        }
        return sb.Append(body, last, body.Length - last).ToString();
    }

    /// <summary>The vertex stage's main() (after the shader): the triangle corner, gl_Position, then vertexMain with its pixel coordinates</summary>
    public static string VertexMain(string body, bool es3, bool compact)
    {
        var args = EntryNoArgsRx.IsMatch(CommentStrip(body)) ? "" : "(p*0.5+0.5)*iResolution.xy";
        var pos = es3 ? "vec2 p=vec2(gl_VertexID==1?3.0:-1.0,gl_VertexID==2?3.0:-1.0);" : "vec2 p=" + PositionAttribute + ";";
        return compact ? "\nvoid main(){" + pos + "gl_Position=vec4(p,0.0,1.0);" + Entry + "(" + args + ");}"
            : "\nvoid main()\n{\n    " + pos + "\n    gl_Position = vec4(p, 0.0, 1.0);\n    " + Entry + "(" + args + ");\n}\n";
    }

    /// <summary>
    /// A source (with mainImage and vertexMain) as a shader without a vertex stage: the varyings become globals, mainImage is renamed and a new
    /// mainImage calls vertexMain(fragCoord) first (per pixel, no interpolation). Sources without vertexMain are returned as is.
    /// </summary>
    public static string ToPerPixel(string source)
    {
        var code = CommentStrip(source);
        if (!HasEntry(code) || !Regex.IsMatch(code, @"\bvoid\s+mainImage\s*\("))
            return source;
        var body = RewriteVaryings(source, Stage.PerPixel, true);
        var s = Analyze(body);
        var sb = new StringBuilder(body);
        // Rename mainImage (its definition / prototypes) and vertexMain (no vertex stage any more), the masked code finds no comments
        var renames = Regex.Matches(s.Masked, @"\bvoid\s+mainImage\s*\(").Select(m => (At: s.Masked.IndexOf("mainImage", m.Index, StringComparison.Ordinal), From: "mainImage", To: "ewPixelMain"))
            .Concat(Regex.Matches(s.Masked, @"\b" + Entry + @"\b").Select(m => (At: m.Index, From: Entry, To: "ewVertexMain")))
            .OrderByDescending(x => x.At);
        foreach (var (at, from, to) in renames)
            sb.Remove(at, from.Length).Insert(at, to);
        var args = EntryNoArgsRx.IsMatch(code) ? "" : "fragCoord";
        sb.Append("\n\n// Effect Weaver: vertexMain runs per pixel here (the shader was written for a vertex stage)\n")
          .Append("void mainImage(out vec4 fragColor, in vec2 fragCoord)\n{\n    ewVertexMain(").Append(args).Append(");\n")
          .Append("    ewPixelMain(fragColor, fragCoord);\n}\n");
        return sb.ToString();
    }
}
