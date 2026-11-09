using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace EffectWeaver.Shaders;

/// <summary>
/// Describes a single editable setting (used both for the fixed settings and the per effect shader variables).
/// The settings page renders a widget per definition.
/// </summary>
public class SettingDef
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    /// <summary>float, int, bool, enum, vec2, vec3, vec4, color, color4</summary>
    public string Type { get; set; } = "float";
    public JsonNode? Default { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Step { get; set; }
    /// <summary>Use a logarithmic slider (for wide ranges like speed)</summary>
    public bool? Log { get; set; }
    public string? Unit { get; set; }
    /// <summary>The display of a value (a template: "{0} BPM", "{0:1} %" = 1 decimal, "{0:0}" = whole numbers), wins over Unit; from "format" of a "var:" annotation</summary>
    public string? Format { get; set; }
    /// <summary>The display scale ("scale" of a "var:" annotation, default 1): the value is shown and typed × Scale (0.3 with Scale 100 and Unit "%" shows as 30 %)</summary>
    public double? Scale { get; set; }
    public string? Desc { get; set; }
    public List<SettingOption>? Options { get; set; }
    /// <summary>Vectors: names of the components (default x, y, z, w)</summary>
    public List<string>? Names { get; set; }
    /// <summary>Vectors: descriptions of the components (shown with the names, e.g. the iMouse channels of an effect)</summary>
    public List<string>? Descs { get; set; }
    /// <summary>Vectors: one slider per component (one per row) instead of number boxes</summary>
    public bool? Sliders { get; set; }
}

public sealed record SettingOption(JsonNode Value, string Label);

/// <summary>A fragment shader source in parts: head (version, precision, uniforms ...), body (the shader), tail (main() if added)</summary>
public sealed record SourceParts(string Head, string Body, string Tail);

/// <summary>
/// A tunable variable found in a shader (a top level #define or const with a literal value)
/// </summary>
public sealed class EffectVar : SettingDef
{
    /// <summary>The line index (0 based) in the source</summary>
    [JsonIgnore] public int Line;
    /// <summary>Start of the literal value in the line</summary>
    [JsonIgnore] public int ValueStart;
    /// <summary>Length of the literal value in the line</summary>
    [JsonIgnore] public int ValueLength;
    [JsonIgnore] public bool IsDefine;
    /// <summary>The glsl type (float, int, bool, vec2, vec3, vec4)</summary>
    [JsonIgnore] public string GlslType = "float";
    /// <summary>A bool that is represented as 0 / 1 (used by #if)</summary>
    [JsonIgnore] public bool BoolAsInt;
}

public sealed class EffectInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string[] Lines { get; init; }
    public required List<EffectVar> Vars { get; init; }
    /// <summary>Folder on disk, null if embedded</summary>
    public string? SourceDir { get; init; }
    /// <summary>Sub folder of an effect folder ("Simple/Slow", "/" separated), "" for the effect folder itself (and logos)</summary>
    public string Folder { get; set; } = "";
    /// <summary>Keywords from the "Keywords" section of the description (.md), filled by the effect library</summary>
    public string[] Keywords { get; set; } = [];
    public bool HasMainImage { get; init; }
    public bool HasMain { get; init; }
    /// <summary>A ShaderToy style shader with a vertex stage (void vertexMain(vec2 fragCoord), see <see cref="ShaderStages"/>)</summary>
    public bool HasVertexMain { get; init; }
    /// <summary>The shader is built with a vertex stage: vertexMain + mainImage, no own main()</summary>
    public bool UsesVertexStage => HasVertexMain && HasMainImage && !HasMain;
    public bool UsesDerivatives { get; init; }
    public bool UsesLod { get; init; }
    /// <summary>The shader uses texture2D (not available in GLSL ES 3.00)</summary>
    public bool UsesTexture2D { get; init; }
    /// <summary>Uniforms declared by the shader itself (not declared again by the ShaderToy prefix)</summary>
    public HashSet<string> Uniforms { get; init; } = [];
    public string? HeaderComment { get; init; }
    /// <summary>The iMouse channels x, y, z, w (always 4), from "// iMouse.x = Name | Description | Default" comments</summary>
    public MouseChannel[] Mouse { get; init; } = ShaderParser.ParseMouse(new Dictionary<char, string>());
    /// <summary>The default iMouse value of the effect</summary>
    public double[] MouseDefault => Mouse.Select(x => x.Default).ToArray();
}

/// <summary>An iMouse channel (x, y, z or w), a controller value in [0, 1]</summary>
public sealed class MouseChannel
{
    public string Name { get; set; } = "";
    public string Desc { get; set; } = "";
    public double Default { get; set; } = ShaderParser.MouseDefault;
    /// <summary>Described by the effect (an "iMouse.x = ..." comment)</summary>
    public bool Known { get; set; }
    /// <summary>Read by the effect's code (see <see cref="ShaderParser.MarkMouseUsage"/>)</summary>
    /// <summary>The values the 0..1 control drives (optional 4th part "Min .. Max"), shown next to it: "= 3000"</summary>
    public double? Min { get; set; }
    public double? Max { get; set; }
    /// <summary>The unit of the driven value (optional 5th part, ex: "BPM"), or a template with {0} (see <see cref="SettingDef.Format"/>) in Format</summary>
    public string? Unit { get; set; }
    public string? Format { get; set; }
    public bool Used { get; set; }
}

/// <summary>
/// Parses effects (glsl pixel shaders, ShaderToy / GlslSandbox style) and builds the final shader source (WebGL2 / WebGL1).
/// Shared by Effect Saver, Effect Vj, the editor and SysWeaver's media player (MediaService.BuildEffect).
/// </summary>
public static class ShaderParser
{
    #region Naming

    /// <summary>
    /// Create a nice display name from a file name, "veil2_aurora_fjord" => "Veil 2 Aurora Fjord", "goldenLion" => "Golden Lion"
    /// </summary>
    public static string BeautifyName(string id)
    {
        var sb = new StringBuilder();
        char prev = ' ';
        foreach (var c in id)
        {
            if (c == '_' || c == '-' || c == '.' || c == ' ')
            {
                if (prev != ' ')
                    sb.Append(' ');
                prev = ' ';
                continue;
            }
            bool boundary =
                (char.IsUpper(c) && char.IsLower(prev)) ||
                (char.IsDigit(c) && char.IsLetter(prev)) ||
                (char.IsLetter(c) && char.IsDigit(prev));
            if (boundary)
                sb.Append(' ');
            sb.Append(prev == ' ' ? char.ToUpperInvariant(c) : c);
            prev = c;
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Create a label from a variable name, "MOUNTAIN_BRIGHTNESS" => "Mountain brightness"
    /// </summary>
    static string LabelFromVar(string name)
    {
        var s = BeautifyName(name.All(c => !char.IsLetter(c) || char.IsUpper(c)) ? name.ToLowerInvariant() : name);
        return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant() : name;
    }

    #endregion

    #region Parsing

    static readonly Regex DefineRx = new(@"^#\s*define\s+([A-Za-z_]\w*)\b(?!\()(.*)$", RegexOptions.Compiled);
    static readonly Regex ConstRx = new(@"^const\s+(?:(?:highp|mediump|lowp)\s+)?(float|int|bool|vec2|vec3|vec4)\s+([A-Za-z_]\w*)\s*=\s*([^;]+);", RegexOptions.Compiled);
    static readonly Regex DirectiveRx = new(@"^#\s*(\w+)\s*(.*)$", RegexOptions.Compiled);
    static readonly Regex NumRx = new(@"^[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?[fF]?$", RegexOptions.Compiled);
    static readonly Regex VecRx = new(@"^(vec[234])\s*\((.*)\)$", RegexOptions.Compiled);
    static readonly Regex RangeRx = new(@"(-?\d+(?:\.\d+)?)\s*(?:\.\.|\bto\b)\s*(-?\d+(?:\.\d+)?)|\((-?\d+(?:\.\d+)?)\s*[-\p{Pd}]\s*(-?\d+(?:\.\d+)?)", RegexOptions.Compiled);
    static readonly Regex OptionRx = new(@"(?:^|:\s*)\(?(\d+)\s*[=:]?\s+([A-Za-z][^().]*)", RegexOptions.Compiled);
    static readonly Regex BoolCommentRx = new(@"\b[01]\s*[=:]|\bon\s*/\s*off\b|\b(enable|disable|toggle)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex BoolNameRx = new(@"^(USE|ENABLE|SHOW|ANIM|HAS|NO|DO|WITH)_", RegexOptions.Compiled);
    static readonly Regex ColorNameRx = new(@"(^|_)(COL|COLOR|COLOUR|TINT|WHITE|BLACK|GOLD|LAPIS|RED|GREEN|BLUE|AMBER|BG|SKY|FOG|HAZE|GLOW|ALBEDO|DIFFUSE|EMISSIVE|HUE|PAINT|INK)(_|$|\d)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex ColorCommentRx = new(@"\bcolou?r|\btint\b|#[0-9a-fA-F]{6}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex UnitRangeCommentRx = new(@"probab|chance|fraction|amount|opacity|\bmix\b|strength|(?<![\d.])0\s*=", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex ProbabilityCommentRx = new(@"probab|chance|fraction|opacity|0\s*\.\.\s*1\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex TimeNameRx = new(@"(^|_)(TIME|START|OFFSET|DELAY|PERIOD)(_|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    /// <summary>Comments indicating that the value is derived from other values (changing it would break the effect)</summary>
    static readonly Regex DerivedCommentRx = new(@"\b[A-Z_]{2,}\s*[/*]\s*[A-Z_0-9(]|\b(floor|ceil|sin|cos|tan|sqrt|exp|log|pow|normalize|atan|length)\s*\(|normaliz|\bnormals?\b|\bmeasured\b|low-discrepancy", RegexOptions.Compiled);
    static readonly Regex HighPrecisionRx = new(@"\d*\.?\d{7,}", RegexOptions.Compiled);
    static readonly Regex DerivRx = new(@"\b(dFdx|dFdy|fwidth)\b", RegexOptions.Compiled);
    static readonly Regex LodRx = new(@"\btexture2DLod\b", RegexOptions.Compiled);
    static readonly Regex MainImageRx = new(@"\bvoid\s+mainImage\s*\(", RegexOptions.Compiled);
    static readonly Regex MainRx = new(@"\bvoid\s+main\s*\(", RegexOptions.Compiled);
    static readonly Regex Texture2DRx = new(@"\btexture2D\b", RegexOptions.Compiled);
    static readonly Regex UniformRx = new(@"\buniform\s+(?:(?:lowp|mediump|highp)\s+)?\w+\s+(\w+)", RegexOptions.Compiled);
    static readonly Regex CommentRx = new(@"/\*.*?\*/|//[^\n]*", RegexOptions.Compiled | RegexOptions.Singleline);
    static readonly Regex PrecisionRx = new(@"\bprecision\s+\w+\s+float\b", RegexOptions.Compiled);
    static readonly Regex FragColorRx = new(@"\bgl_FragColor\b", RegexOptions.Compiled);
    /// <summary>An iMouse channel comment line (comment markers included): "// iMouse.x = Name | Description | Default"</summary>
    static readonly Regex MouseRx = new(@"^(?://+|/\*+|\*+)?\s*iMouse\s*\.\s*([xyzw])\s*[=:]\s*(.*?)\s*(?:\*/)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Names that are math constants, never tunables</summary>
    static readonly HashSet<string> ExcludedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PI", "TAU", "TWOPI", "TWO_PI", "HALF_PI", "HALFPI", "PI2", "PI_2", "INV_PI", "PHI", "E", "SQRT2", "SQRT3", "SQRT_2",
        "ZERO", "ONE", "INF", "INFINITY", "EPS", "EPSILON", "DEG", "RAD", "DEG2RAD", "RAD2DEG", "LN2", "LOG2",
    };

    /// <summary>Loop start helpers like LOOP_ZERO / ZERO_CONST (used to prevent loop unrolling)</summary>
    static readonly Regex ZeroNameRx = new(@"(^|_)ZERO(_|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly double[] MathConstants = [Math.PI, Math.PI * 2, Math.PI * 0.5, Math.PI * 0.25, 1 / Math.PI, 1 / (Math.PI * 2), 1.6180339887, 0.6180339887, Math.Sqrt(2), Math.Sqrt(3), Math.E];

    sealed class Candidate
    {
        public required EffectVar Var;
        public required double[] Values;
        public required string LiteralType;
        public string Comment = "";
        public int CommentCol = -1;
        public JsonObject? Annotation;
    }

    public static (string Value, string Comment, int CommentCol) SplitComment(string text)
    {
        var ci = text.IndexOf("//", StringComparison.Ordinal);
        var bi = text.IndexOf("/*", StringComparison.Ordinal);
        if (bi >= 0 && (ci < 0 || bi < ci))
        {
            var be = text.IndexOf("*/", bi + 2, StringComparison.Ordinal);
            var comment = be > 0 ? text[(bi + 2)..be] : text[(bi + 2)..];
            return (text[..bi], comment.Trim(), bi);
        }
        if (ci < 0)
            return (text, "", -1);
        return (text[..ci], text[(ci + 2)..].Trim(), ci);
    }

    static bool TryParseNumber(string s, out double v) =>
        double.TryParse(s.TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    /// <summary>
    /// Parse a literal value: number, bool or vecN(numbers)
    /// </summary>
    static bool TryParseLiteral(string text, out string type, out double[] values)
    {
        type = "";
        values = [];
        var t = text.Trim();
        while (t.Length > 1 && t[0] == '(' && t[^1] == ')')
            t = t[1..^1].Trim();
        if (t == "true" || t == "false")
        {
            type = "bool";
            values = [t == "true" ? 1 : 0];
            return true;
        }
        if (NumRx.IsMatch(t))
        {
            if (!TryParseNumber(t, out var v))
                return false;
            type = (t.IndexOf('.') >= 0 || t.IndexOfAny(['e', 'E']) >= 0) ? "float" : "int";
            values = [v];
            return true;
        }
        var m = VecRx.Match(t);
        if (!m.Success)
            return false;
        var vt = m.Groups[1].Value;
        var count = vt[3] - '0';
        var parts = m.Groups[2].Value.Split(',');
        if (parts.Length != 1 && parts.Length != count)
            return false;
        var list = new double[count];
        for (int i = 0; i < parts.Length; ++i)
        {
            var p = parts[i].Trim();
            if (!NumRx.IsMatch(p) || !TryParseNumber(p, out list[i]))
                return false;
        }
        if (parts.Length == 1)
            Array.Fill(list, list[0]);
        type = vt;
        values = list;
        return true;
    }

    static JsonObject? ParseAnnotation(string comment)
    {
        if (!comment.StartsWith("var:", StringComparison.OrdinalIgnoreCase))
            return null;
        var json = comment[4..].Trim();
        if (json.Length == 0)
            return [];
        // Relaxed json, allow unquoted keys
        json = Regex.Replace(json, @"([{,])\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*:", "$1\"$2\":");
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>The default value of an iMouse channel that the effect doesn't set</summary>
    public const double MouseDefault = 0.5;

    const string MouseChannels = "xyzw";
    /// <summary>
    /// The iMouse channels from the comment texts (the part after "iMouse.x =") per channel, missing channels are unknown.
    /// "Name | Description | Default", the default is optional, without a "|" the text is the description.
    /// Optional: a 4th part "Min .. Max" (the values the 0..1 control drives, mix(Min, Max, iMouse.c)) and a 5th part, the unit ("BPM") or a
    /// template with {0} ("{0} BPM", "{0:1} %").
    /// </summary>
    public static MouseChannel[] ParseMouse(IReadOnlyDictionary<char, string> texts)
    {
        var r = new MouseChannel[4];
        for (int i = 0; i < 4; ++i)
        {
            var ch = MouseChannels[i];
            var c = r[i] = new MouseChannel { Name = "iMouse." + ch, Desc = "Unknown function (not described by the effect)." };
            if (!texts.TryGetValue(ch, out var text))
                continue;
            var parts = text.Split('|').Select(x => x.Trim()).ToArray();
            if (parts.Length == 1)
            {
                if (parts[0].Length > 0)
                    c.Desc = parts[0];
            }
            else
            {
                if (parts[0].Length > 0)
                    c.Name = parts[0];
                c.Desc = parts[1];
            }
            if (parts.Length > 2 && TryParseNumber(parts[2].TrimEnd('.', ' '), out var d) && double.IsFinite(d))
                c.Default = Math.Clamp(d, 0, 1);
            if (parts.Length > 3 && RangeRx.Match(parts[3]) is { Success: true } rm
                && TryParseNumber(rm.Groups[rm.Groups[1].Success ? 1 : 3].Value, out var lo) && TryParseNumber(rm.Groups[rm.Groups[1].Success ? 2 : 4].Value, out var hi)
                && double.IsFinite(lo) && double.IsFinite(hi) && lo != hi)
            {
                c.Min = lo;
                c.Max = hi;
            }
            if (parts.Length > 4 && parts[4].Length > 0)
            {
                if (parts[4].Contains("{0"))
                    c.Format = parts[4];
                else
                    c.Unit = parts[4];
            }
            c.Known = true;
        }
        return r;
    }

    public static EffectInfo Parse(string id, string src, string? dir, string? displayName = null)
    {
        src = src.TrimStart((char)0xFEFF);
        var lines = src.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var candidates = new List<Candidate>();
        var defineCount = new Dictionary<string, int>(StringComparer.Ordinal);
        var usedInBareIf = new HashSet<string>(StringComparer.Ordinal);
        var guards = new Stack<string?>();
        var mouse = new Dictionary<char, string>();
        bool inBlockComment = false;
        Candidate? last = null;

        for (int i = 0; i < lines.Length; ++i)
        {
            var raw = lines[i];
            var t = raw.Trim();
            // iMouse channel descriptions (in comments, the first one of a channel is used)
            if ((inBlockComment || t.StartsWith("//") || t.StartsWith("/*")) && t.Contains("iMouse", StringComparison.OrdinalIgnoreCase))
            {
                var mm = MouseRx.Match(t);
                if (mm.Success)
                    mouse.TryAdd(char.ToLowerInvariant(mm.Groups[1].Value[0]), mm.Groups[2].Value);
            }
            if (inBlockComment)
            {
                if (t.Contains("*/"))
                    inBlockComment = false;
                last = null;
                continue;
            }
            if (t.StartsWith("/*") && !t.Contains("*/"))
            {
                inBlockComment = true;
                last = null;
                continue;
            }
            // Comment continuation lines (aligned with the comment of the previous variable)
            if (t.StartsWith("//"))
            {
                if (last != null && last.CommentCol >= 0)
                {
                    var col = raw.IndexOf("//", StringComparison.Ordinal);
                    if (col >= last.CommentCol - 2)
                    {
                        last.Comment += " " + t[2..].Trim();
                        continue;
                    }
                }
                last = null;
                continue;
            }
            last = null;
            if (t.StartsWith('#'))
            {
                var dm = DirectiveRx.Match(t);
                if (!dm.Success)
                    continue;
                var directive = dm.Groups[1].Value;
                var arg = SplitComment(dm.Groups[2].Value).Value.Trim();
                switch (directive)
                {
                    case "if":
                    case "elif":
                        {
                            var bare = arg.TrimStart('!').Trim();
                            if (Regex.IsMatch(bare, @"^[A-Za-z_]\w*$"))
                                usedInBareIf.Add(bare);
                            if (directive == "if")
                                guards.Push(null);
                            continue;
                        }
                    case "ifdef":
                        guards.Push(null);
                        continue;
                    case "ifndef":
                        guards.Push(arg);
                        continue;
                    case "endif":
                        if (guards.Count > 0)
                            guards.Pop();
                        continue;
                    case "define":
                        break;
                    default:
                        continue;
                }
                var m = DefineRx.Match(t);
                if (!m.Success)
                    continue;
                var name = m.Groups[1].Value;
                defineCount[name] = defineCount.GetValueOrDefault(name) + 1;
                // Only top level defines (or the "#ifndef X / #define X" pattern)
                var depth = guards.Count;
                if (depth > 1 || (depth == 1 && guards.Peek() != name))
                    continue;
                var rest = m.Groups[2].Value;
                var (valueText, comment, _) = SplitComment(rest);
                if (!TryParseLiteral(valueText, out var litType, out var values))
                    continue;
                // Locate the value in the raw line
                var nameEnd = raw.IndexOf(name, raw.IndexOf("define", StringComparison.Ordinal), StringComparison.Ordinal) + name.Length;
                var trimmedValue = valueText.Trim();
                var valueStart = raw.IndexOf(trimmedValue, nameEnd, StringComparison.Ordinal);
                if (valueStart < 0)
                    continue;
                var c = new Candidate
                {
                    Var = new EffectVar
                    {
                        Key = name,
                        Line = i,
                        ValueStart = valueStart,
                        ValueLength = trimmedValue.Length,
                        IsDefine = true,
                    },
                    Values = values,
                    LiteralType = litType,
                    Comment = comment,
                    CommentCol = comment.Length > 0 ? raw.IndexOf("//", nameEnd, StringComparison.Ordinal) : -1,
                    Annotation = ParseAnnotation(comment),
                };
                candidates.Add(c);
                last = c;
                continue;
            }
            if (guards.Count == 0 && t.StartsWith("const"))
            {
                var m = ConstRx.Match(t);
                if (!m.Success)
                    continue;
                var glslType = m.Groups[1].Value;
                var name = m.Groups[2].Value;
                var valueText = m.Groups[3].Value.Trim();
                defineCount[name] = defineCount.GetValueOrDefault(name) + 1;
                if (valueText.Contains(','))
                {
                    // Must be a single vector constructor, not multiple declarations
                    if (!valueText.StartsWith("vec"))
                        continue;
                }
                if (!TryParseLiteral(valueText, out var litType, out var values))
                    continue;
                // GLSL ES 1.0 has no implicit conversions
                bool ok = glslType switch
                {
                    "float" => litType == "float",
                    "int" => litType == "int",
                    "bool" => litType == "bool",
                    _ => litType == glslType,
                };
                if (!ok)
                    continue;
                var after = t[(m.Index + m.Length)..];
                var (_, comment, _) = SplitComment(after);
                var eq = raw.IndexOf('=', raw.IndexOf(name, StringComparison.Ordinal));
                var valueStart = raw.IndexOf(valueText, eq + 1, StringComparison.Ordinal);
                if (valueStart < 0)
                    continue;
                var c = new Candidate
                {
                    Var = new EffectVar
                    {
                        Key = name,
                        Line = i,
                        ValueStart = valueStart,
                        ValueLength = valueText.Length,
                        IsDefine = false,
                        GlslType = glslType,
                    },
                    Values = values,
                    LiteralType = litType,
                    Comment = comment,
                    CommentCol = comment.Length > 0 ? raw.IndexOf("//", eq, StringComparison.Ordinal) : -1,
                    Annotation = ParseAnnotation(comment),
                };
                candidates.Add(c);
                last = c;
            }
        }

        // Filter and classify
        var vars = new List<EffectVar>();
        foreach (var c in candidates)
        {
            var v = c.Var;
            if (defineCount.GetValueOrDefault(v.Key) > 1)
                continue;
            var ann = c.Annotation;
            if (ann == null)
            {
                if (ExcludedNames.Contains(v.Key) || ZeroNameRx.IsMatch(v.Key))
                    continue;
                if (c.Values.Length == 1 && c.LiteralType == "float" && MathConstants.Any(k => Math.Abs(Math.Abs(c.Values[0]) - k) < 1e-4))
                    continue;
                // Derived values (formulas, normalized vectors, high precision math values)
                if (DerivedCommentRx.IsMatch(c.Comment))
                    continue;
                var line = lines[v.Line];
                if (HighPrecisionRx.IsMatch(line.Substring(v.ValueStart, v.ValueLength)))
                    continue;
                // Constants without any comment are usually internal
                if (!v.IsDefine && c.Comment.Length == 0)
                    continue;
            }
            Classify(c, usedInBareIf.Contains(v.Key));
            vars.Add(v);
        }

        var header = ExtractHeaderComment(lines);
        // Detection on the code only (a "void main(" in a comment doesn't count)
        var code = CommentRx.Replace(src, " ");
        return new EffectInfo
        {
            Id = id,
            Name = displayName ?? BeautifyName(id),
            Lines = lines,
            Vars = vars,
            SourceDir = dir,
            HasMainImage = MainImageRx.IsMatch(code),
            HasMain = MainRx.IsMatch(code),
            HasVertexMain = ShaderStages.HasEntry(code),
            UsesDerivatives = DerivRx.IsMatch(code),
            UsesLod = LodRx.IsMatch(code),
            UsesTexture2D = Texture2DRx.IsMatch(code),
            Uniforms = UniformRx.Matches(code).Select(m => m.Groups[1].Value).ToHashSet(),
            HeaderComment = header,
            Mouse = MarkMouseUsage(ParseMouse(mouse), code),
        };
    }

    //  iMouse with an optional swizzle or index (the declaration "vec4 iMouse" isn't a use)
    static readonly Regex MouseUseRx = new(@"(?<!\bvec4\s+)\biMouse\b(?:\s*\.\s*([xyzwrgbastpq]+)|\s*\[\s*([0-3])\s*\])?", RegexOptions.Compiled);

    /// <summary>
    /// Set <see cref="MouseChannel.Used"/> from the code (comments removed): "iMouse.x", swizzles ("iMouse.zw", rgba / stpq), "iMouse[2]";
    /// iMouse on its own (passed on, assigned, "#define M iMouse") uses all channels
    /// </summary>
    public static MouseChannel[] MarkMouseUsage(MouseChannel[] channels, string code)
    {
        foreach (Match m in MouseUseRx.Matches(code))
        {
            if (m.Groups[1].Success)
            {
                foreach (var c in m.Groups[1].Value)
                {
                    var i = "xyzw".IndexOf(c);
                    if (i < 0)
                        i = "rgba".IndexOf(c);
                    if (i < 0)
                        i = "stpq".IndexOf(c);
                    if (i >= 0 && i < channels.Length)
                        channels[i].Used = true;
                }
            }
            else if (m.Groups[2].Success)
                channels[m.Groups[2].Value[0] - '0'].Used = true;
            else
                foreach (var c in channels)
                    c.Used = true;
        }
        return channels;
    }

    static double NiceStep(double range)
    {
        if (range <= 0)
            return 0.01;
        var raw = range / 200.0;
        var mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var n = raw / mag;
        var step = n < 1.5 ? 1 : n < 3.5 ? 2 : n < 7.5 ? 5 : 10;
        return Math.Round(step * mag, 10);
    }

    static double NiceMax(double v)
    {
        if (v <= 0)
            return 1;
        var mag = Math.Pow(10, Math.Floor(Math.Log10(v)));
        var n = v / mag;
        var r = n <= 1 ? 1 : n <= 2 ? 2 : n <= 2.5 ? 2.5 : n <= 5 ? 5 : 10;
        return Math.Round(r * mag, 10);
    }

    static void Classify(Candidate c, bool usedInBareIf)
    {
        var v = c.Var;
        var comment = c.Comment;
        var ann = c.Annotation;
        var values = c.Values;
        v.Label = LabelFromVar(v.Key);
        v.Desc = comment.Length > 0 && ann == null ? comment : null;
        if (v.IsDefine)
            v.GlslType = c.LiteralType;

        string? annType = null;
        if (ann != null)
        {
            if (ann["name"] is JsonValue n)
                v.Label = n.ToString();
            if (ann["desc"] is JsonValue d)
                v.Desc = d.ToString();
            if (ann["type"] is JsonValue at)
                annType = at.ToString();
            if (ann["min"] is JsonValue mn && mn.TryGetValue<double>(out var mnv))
                v.Min = mnv;
            if (ann["max"] is JsonValue mx && mx.TryGetValue<double>(out var mxv))
                v.Max = mxv;
            if (ann["step"] is JsonValue st && st.TryGetValue<double>(out var stv) && stv > 0)
                v.Step = stv;
            // The display: a unit ("BPM") or a template ("{0} BPM", "{0:1} %")
            if (ann["unit"] is JsonValue un && un.ToString() is { Length: > 0 } unv)
                v.Unit = unv;
            if (ann["format"] is JsonValue fm && fm.ToString() is { Length: > 0 } fmv && fmv.Contains("{0"))
                v.Format = fmv;
            if (ann["scale"] is JsonValue sc && sc.TryGetValue<double>(out var scv) && scv > 0 && scv != 1)
                v.Scale = scv;
        }

        switch (v.GlslType)
        {
            case "bool":
                v.Type = "bool";
                v.Default = values[0] != 0;
                return;
            case "int":
                {
                    var def = (int)values[0];
                    // Enumerations: "0 fast, 1 default, 2 high"
                    var options = new List<(int Value, string Label)>();
                    foreach (var part in comment.Split([',', ';']))
                    {
                        var om = OptionRx.Match(part.Trim());
                        if (!om.Success)
                            continue;
                        var ov = int.Parse(om.Groups[1].Value, CultureInfo.InvariantCulture);
                        var ol = om.Groups[2].Value.Trim().TrimEnd('+', '-', ':', '=').Trim();
                        if (ol.Length == 0 || options.Any(x => x.Value == ov))
                            continue;
                        options.Add((ov, char.ToUpperInvariant(ol[0]) + ol[1..]));
                    }
                    if (ann == null && options.Count >= 2 && options.Any(x => x.Value == def))
                    {
                        v.Type = "enum";
                        v.Default = def;
                        v.Options = options.OrderBy(x => x.Value).Select(x => new SettingOption(JsonValue.Create(x.Value), $"{x.Label} ({x.Value})")).ToList();
                        return;
                    }
                    if ((def == 0 || def == 1) && (annType == "bool" || (ann == null && (usedInBareIf || BoolCommentRx.IsMatch(comment) || BoolNameRx.IsMatch(v.Key)))))
                    {
                        v.Type = "bool";
                        v.BoolAsInt = true;
                        v.Default = def != 0;
                        return;
                    }
                    v.Type = "int";
                    v.Default = def;
                    ApplyRange(v, comment, def, true);
                    return;
                }
            case "float":
                v.Type = "float";
                v.Default = values[0];
                ApplyRange(v, comment, values[0], false);
                return;
            default:
                {
                    // Vectors
                    var arr = new JsonArray(values.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
                    v.Default = arr;
                    bool isColor = annType is "col" or "colhdr" or "color" ||
                        (ann == null && values.All(x => x >= 0) && (ColorNameRx.IsMatch(v.Key) || ColorCommentRx.IsMatch(comment)));
                    if (isColor)
                    {
                        v.Type = v.GlslType == "vec4" ? "color4" : v.GlslType == "vec3" ? "color" : v.GlslType;
                        if (v.Type.StartsWith("color"))
                            return;
                    }
                    v.Type = v.GlslType;
                    var maxAbs = values.Max(Math.Abs);
                    if (v.Min == null || v.Max == null)
                    {
                        var m = NiceMax(maxAbs * 3);
                        v.Min ??= values.Any(x => x < 0) ? -m : 0;
                        v.Max ??= m;
                    }
                    v.Step ??= NiceStep(v.Max.Value - v.Min.Value);
                    return;
                }
        }
    }

    static void ApplyRange(EffectVar v, string comment, double def, bool isInt)
    {
        if (v.Min == null || v.Max == null)
        {
            var rm = RangeRx.Match(comment);
            if (rm.Success)
            {
                var a = double.Parse(rm.Groups[1].Success ? rm.Groups[1].Value : rm.Groups[3].Value, CultureInfo.InvariantCulture);
                var b = double.Parse(rm.Groups[2].Success ? rm.Groups[2].Value : rm.Groups[4].Value, CultureInfo.InvariantCulture);
                if (b > a)
                {
                    v.Min ??= Math.Min(a, def);
                    v.Max ??= Math.Max(b, def);
                }
            }
        }
        if (v.Min == null || v.Max == null)
        {
            if (isInt)
            {
                v.Min ??= Math.Min(0, def * 2);
                v.Max ??= def <= 0 ? 4 : Math.Max(def * 2, def + 4);
            }
            else if (def == 0)
            {
                v.Min ??= 0;
                v.Max ??= TimeNameRx.IsMatch(v.Key) ? 600 : 1;
            }
            else if (def < 0)
            {
                var m = NiceMax(-def * 3);
                v.Min ??= -m;
                v.Max ??= m;
            }
            else
            {
                v.Min ??= 0;
                bool unit = (def < 1 && UnitRangeCommentRx.IsMatch(comment)) || (def <= 1 && ProbabilityCommentRx.IsMatch(comment));
                v.Max ??= unit ? 1 : NiceMax(def * 3);
            }
        }
        v.Step ??= isInt ? 1 : NiceStep(v.Max.Value - v.Min.Value);
    }

    /// <summary>
    /// Get the leading comment block of a shader (used as information if no .md file exists)
    /// </summary>
    static string? ExtractHeaderComment(string[] lines)
    {
        var text = new List<string>();
        int i = 0;
        while (i < lines.Length && lines[i].Trim().Length == 0)
            ++i;
        if (i < lines.Length && lines[i].TrimStart().StartsWith("/*"))
        {
            for (; i < lines.Length; ++i)
            {
                var l = lines[i];
                var end = l.Contains("*/");
                l = l.Replace("/*", "").Replace("*/", "");
                var tl = l.TrimStart();
                if (tl.StartsWith('*'))
                    l = tl[1..];
                text.Add(l);
                if (end)
                    break;
            }
        }
        else
        {
            for (; i < lines.Length; ++i)
            {
                var l = lines[i].Trim();
                if (l.Length == 0)
                {
                    text.Add("");
                    continue;
                }
                if (!l.StartsWith("//"))
                    break;
                var c = l[2..];
                if (c.StartsWith(' '))
                    c = c[1..];
                text.Add(c);
            }
        }
        // Remove decoration lines
        text = text.Select(x => x.TrimEnd()).Select(x => x.Trim().Length > 0 && x.Trim().All(ch => "=-*#~_/".Contains(ch)) ? "" : x).ToList();
        while (text.Count > 0 && text[0].Length == 0)
            text.RemoveAt(0);
        while (text.Count > 0 && text[^1].Length == 0)
            text.RemoveAt(text.Count - 1);
        if (text.Count == 0)
            return null;
        var indent = text.Where(x => x.Trim().Length > 0).Min(x => x.Length - x.TrimStart().Length);
        return string.Join("\n", text.Select(x => x.Length >= indent ? x[indent..] : x.TrimStart()));
    }

    #endregion

    #region Building

    public static string FormatFloat(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v))
            v = 0;
        var s = v.ToString("0.0#########", CultureInfo.InvariantCulture);
        return s;
    }

    public static double ToDouble(JsonNode? n, double def)
    {
        if (n is JsonValue jv)
        {
            if (jv.TryGetValue<double>(out var d))
                return double.IsFinite(d) ? d : def;
            if (jv.TryGetValue<bool>(out var b))
                return b ? 1 : 0;
            if (jv.TryGetValue<string>(out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                return d;
        }
        return def;
    }

    public static string FormatValue(EffectVar v, JsonNode? value)
    {
        switch (v.GlslType)
        {
            case "bool":
                return ToDouble(value, ToDouble(v.Default, 0)) != 0 ? "true" : "false";
            case "int":
                {
                    var d = ToDouble(value, ToDouble(v.Default, 0));
                    if (v.BoolAsInt)
                        return d != 0 ? "1" : "0";
                    var i = (long)Math.Round(d);
                    return i < 0 && v.IsDefine ? $"({i})" : i.ToString(CultureInfo.InvariantCulture);
                }
            case "float":
                {
                    var d = ToDouble(value, ToDouble(v.Default, 0));
                    var s = FormatFloat(d);
                    return d < 0 && v.IsDefine ? $"({s})" : s;
                }
            default:
                {
                    var count = v.GlslType[3] - '0';
                    var defs = v.Default as JsonArray;
                    var vals = value as JsonArray;
                    var sb = new StringBuilder(v.GlslType).Append('(');
                    for (int i = 0; i < count; ++i)
                    {
                        if (i > 0)
                            sb.Append(", ");
                        var def = ToDouble(defs != null && i < defs.Count ? defs[i] : null, 0);
                        sb.Append(FormatFloat(ToDouble(vals != null && i < vals.Count ? vals[i] : null, def)));
                    }
                    return sb.Append(')').ToString();
                }
        }
    }

    /// <summary>ShaderToy uniforms, declared by the prefix unless the shader declares them itself</summary>
    static readonly (string Name, string Declaration)[] ShaderToyUniforms =
    [
        ("iResolution", "uniform vec3 iResolution;"),
        ("iTime", "uniform float iTime;"),
        ("iTimeDelta", "uniform float iTimeDelta;"),
        ("iFrameRate", "uniform float iFrameRate;"),
        ("iFrame", "uniform int iFrame;"),
        ("iMouse", "uniform vec4 iMouse;"),
        ("iDate", "uniform vec4 iDate;"),
        ("iSampleRate", "uniform float iSampleRate;"),
        ("iChannelTime", "uniform float iChannelTime[4];"),
        ("iChannelResolution", "uniform vec3 iChannelResolution[4];"),
    ];

    /// <summary>Extensions that are part of GLSL ES 3.00 (their #extension directives are removed)</summary>
    static readonly HashSet<string> CoreExtensions = new(StringComparer.Ordinal)
    {
        "GL_OES_standard_derivatives", "GL_EXT_shader_texture_lod", "GL_EXT_frag_depth", "GL_EXT_draw_buffers",
    };

    const string WebGL2Suffix =
@"
void main()
{
    vec4 c = vec4(0.0, 0.0, 0.0, 1.0);
    mainImage(c, gl_FragCoord.xy);
    effectSaverFragColor = vec4(c.rgb, 1.0);
}
";

    const string WebGL1Suffix =
@"
void main()
{
    vec4 c = vec4(0.0, 0.0, 0.0, 1.0);
    mainImage(c, gl_FragCoord.xy);
    gl_FragColor = vec4(c.rgb, 1.0);
}
";

    // Logos: transparent (premultiplied canvas), effectSaverBlend (set by the page, 0 if not) = the overlay's blend mode:
    // 0 premultiplied (output as is), 1 normal (straight alpha, premultiplied here), 2 additive (alpha 0)
    const string AlphaBlend =
@"
uniform int effectSaverBlend;
vec4 effectSaverApplyBlend(vec4 c)
{
    if (effectSaverBlend == 1)
        return vec4(clamp(c.rgb, 0.0, 1.0) * clamp(c.a, 0.0, 1.0), c.a);
    if (effectSaverBlend == 2)
        return vec4(c.rgb, 0.0);
    return c;
}
";

    const string WebGL2AlphaSuffix = AlphaBlend +
@"
void main()
{
    vec4 c = vec4(0.0);
    mainImage(c, gl_FragCoord.xy);
    effectSaverFragColor = effectSaverApplyBlend(c);
}
";

    const string WebGL1AlphaSuffix = AlphaBlend +
@"
void main()
{
    vec4 c = vec4(0.0);
    mainImage(c, gl_FragCoord.xy);
    gl_FragColor = effectSaverApplyBlend(c);
}
";

    /// <summary>
    /// Build the final fragment shader source for an effect using some variable values.
    /// Shaders with their own main() (GlslSandbox style, may also have a mainImage) are used as is (GLSL ES 1.00 unless they have a #version),
    /// only precision and extensions are added if missing.
    /// ShaderToy style shaders (mainImage only) get the uniforms (not declared by the shader) and a main() calling mainImage.
    /// #version and #extension directives in the shader are moved to the top (removed if not needed), line numbers are kept.
    /// </summary>
    /// <param name="fx">The effect</param>
    /// <param name="vars">Variable values (keyed by name), missing values uses the defaults</param>
    /// <param name="webGL2">True to build for WebGL2 (GLSL ES 3.00), else WebGL1 (GLSL ES 1.00)</param>
    /// <param name="alpha">Keep the alpha output of mainImage (logos, premultiplied), else it's opaque</param>
    /// <returns>The fragment shader; with a vertex stage (<see cref="EffectInfo.UsesVertexStage"/>) followed by <see cref="ShaderStages.Marker"/>
    /// and the vertex shader (<see cref="ShaderStages.SplitProgram"/>)</returns>
    public static string BuildSource(EffectInfo fx, JsonObject? vars, bool webGL2, bool alpha = false)
    {
        var p = BuildParts(fx, vars, webGL2, alpha);
        var fragment = p.Head + "#line 1\n" + p.Body + p.Tail;
        if (!fx.UsesVertexStage)
            return fragment;
        var v = BuildParts(fx, vars, webGL2, alpha, stage: ShaderStages.Stage.Vertex);
        return ShaderStages.JoinProgram(fragment, v.Head + "#line 1\n" + v.Body + v.Tail);
    }

    /// <summary>
    /// The final fragment shader source in parts (see <see cref="BuildSource"/>): head (version, extensions, precision, uniforms ...),
    /// body (the shader) and tail (main() calling mainImage, empty for shaders with their own main).
    /// </summary>
    /// <param name="export">Build for an export: only the ShaderToy uniforms the shader uses, a compact main(), no #line (used by the Effect Saver exports)</param>
    /// <param name="stripVars">Export: remove the declarations of the variables (they are declared at the end of the head when the page runs),
    /// the head ends with a float precision if the variables need one</param>
    /// <param name="stage">With a vertex stage (<see cref="EffectInfo.UsesVertexStage"/>): the stage to build (its code, varyings as in / out),
    /// Vertex: the vertex shader (head without the fragment output, tail = main() calling vertexMain)</param>
    public static SourceParts BuildParts(EffectInfo fx, JsonObject? vars, bool webGL2, bool alpha = false, bool export = false, bool stripVars = false,
        ShaderStages.Stage stage = ShaderStages.Stage.Fragment)
    {
        var lines = (string[])fx.Lines.Clone();
        foreach (var v in fx.Vars)
        {
            var line = lines[v.Line];
            if (stripVars)
            {
                // A define is the whole line (and a comment), a const ends with ';' (keep anything after it except a comment)
                if (v.IsDefine)
                {
                    lines[v.Line] = "";
                    continue;
                }
                var end = line.IndexOf(';', v.ValueStart + v.ValueLength);
                var start = line.Length - line.TrimStart().Length;
                lines[v.Line] = line[..start] + (end < 0 ? "" : SplitComment(line[(end + 1)..]).Value);
                continue;
            }
            JsonNode? value = null;
            if (vars != null && !vars.TryGetPropertyValue(v.Key, out value))
                value = null;
            if (value == null)
                continue;
            lines[v.Line] = line[..v.ValueStart] + FormatValue(v, value) + line[(v.ValueStart + v.ValueLength)..];
        }
        // The Ew snippets the shader calls (functions / EW_ macros it doesn't define) are inserted from the built in library, so effects written
        // in the editor run as is everywhere (an editor build arrives with its snippets already inserted: nothing to do). #line keeps the lines.
        var deriv = fx.UsesDerivatives;
        if (SnippetLibrary.UsedNames(string.Join('\n', lines)).Any())
        {
            var exp = SnippetExpander.Expand(SnippetLibrary.Builtin, string.Join("\n", lines), null, export);
            if (exp.Inserted.Count > 0)
            {
                lines = exp.Body.Split('\n');
                deriv |= DerivRx.IsMatch(CommentRx.Replace(exp.Body, " "));
            }
        }
        bool own = fx.HasMain;
        // A shader with its own main and "#version 300 es" is complete
        string? version = null;
        var extensions = new List<string>();
        for (int i = 0; i < lines.Length; ++i)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith("#version", StringComparison.Ordinal))
            {
                version = t.Trim();
                lines[i] = "";
            }
            else if (t.StartsWith("#extension", StringComparison.Ordinal))
            {
                extensions.Add(t.Trim());
                lines[i] = "";
            }
        }
        // WebGL2 doesn't support derivatives in GLSL ES 1.00, such shaders (with their own main) are converted to GLSL ES 3.00
        bool convert = own && version == null && webGL2 && deriv;
        bool es3 = own ? convert || (version != null && version.Contains("300")) : webGL2;
        var body = string.Join("\n", lines);
        if (convert)
            body = FragColorRx.Replace(body, "effectSaverFragColor");
        var vertex = fx.UsesVertexStage && stage == ShaderStages.Stage.Vertex;
        if (fx.UsesVertexStage)
        {
            var split = ShaderStages.Split(body);
            body = ShaderStages.RewriteVaryings(vertex ? split.Vertex : split.Fragment, vertex ? ShaderStages.Stage.Vertex : ShaderStages.Stage.Fragment, es3);
        }
        if (vertex)
            return VertexParts(fx, body, es3, export);
        var sb = new StringBuilder(body.Length + 1024);
        if (es3)
            sb.Append("#version 300 es\n");
        foreach (var e in extensions)
        {
            var name = e.Split([' ', '\t', ':'], StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1) ?? "";
            if (!es3 || !CoreExtensions.Contains(name))
                sb.Append(e).Append('\n');
        }
        if (!es3)
        {
            if (deriv && !extensions.Any(x => x.Contains("GL_OES_standard_derivatives")))
                sb.Append("#extension GL_OES_standard_derivatives : enable\n");
            if (fx.UsesLod)
            {
                if (!extensions.Any(x => x.Contains("GL_EXT_shader_texture_lod")))
                    sb.Append("#extension GL_EXT_shader_texture_lod : enable\n");
                sb.Append("#define texture2DLod texture2DLodEXT\n");
            }
        }
        if (own)
        {
            // Converted: the output declaration needs a precision (the shader's may come later, a precision statement can be repeated)
            var precision = convert || !PrecisionRx.IsMatch(CommentRx.Replace(body, " "));
            if (precision)
                sb.Append(export ? "precision highp float;\n" : "#ifdef GL_ES\nprecision highp float;\n#endif\n");
            if (convert)
                sb.Append("#define varying in\n#define texture2D texture\n#define textureCube texture\n#define texture2DLod textureLod\n#define texture2DLodEXT textureLod\nout vec4 effectSaverFragColor;\n");
            // Float constants declared before the shader's own precision statement need one
            else if (!precision && stripVars && fx.Vars.Any(x => !x.IsDefine && x.GlslType != "int" && x.GlslType != "bool"))
                sb.Append("precision highp float;\n");
            return new(sb.ToString(), body, "");
        }
        sb.Append("precision highp float;\nprecision highp int;\n");
        if (es3)
        {
            if (fx.UsesTexture2D)
                sb.Append("#define texture2D texture\n");
            if (fx.UsesLod)
                sb.Append("#define texture2DLod textureLod\n");
        }
        // Export: only the uniforms the shader uses (the page sets the ones it finds)
        var code = export ? CommentRx.Replace(body, " ") : null;
        foreach (var (name, decl) in ShaderToyUniforms)
            if (!fx.Uniforms.Contains(name) && (code == null || Regex.IsMatch(code, @"\b" + name + @"\b")))
                sb.Append(decl).Append('\n');
        if (es3)
            sb.Append("out vec4 effectSaverFragColor;\n");
        string tail;
        if (export)
            tail = "\nvoid main(){vec4 c=vec4(0.0" + (alpha ? "" : ",0.0,0.0,1.0") + ");mainImage(c,gl_FragCoord.xy);" +
                (es3 ? "effectSaverFragColor" : "gl_FragColor") + "=" + (alpha ? "c" : "vec4(c.rgb,1.0)") + ";}";
        else
            tail = alpha ? (es3 ? WebGL2AlphaSuffix : WebGL1AlphaSuffix) : (es3 ? WebGL2Suffix : WebGL1Suffix);
        return new(sb.ToString(), body, tail);
    }

    /// <summary>The vertex stage: version, precision, the ShaderToy uniforms (like the fragment stage), the position attribute (GLSL ES 1.00)</summary>
    static SourceParts VertexParts(EffectInfo fx, string body, bool es3, bool export)
    {
        var sb = new StringBuilder(512);
        if (es3)
            sb.Append("#version 300 es\n");
        sb.Append("precision highp float;\nprecision highp int;\n");
        if (es3 && fx.UsesTexture2D)
            sb.Append("#define texture2D texture\n");
        if (es3 && fx.UsesLod)
            sb.Append("#define texture2DLod textureLod\n");
        var code = export ? CommentRx.Replace(body, " ") : null;
        foreach (var (name, decl) in ShaderToyUniforms)
            if (!fx.Uniforms.Contains(name) && (code == null || name == "iResolution" || Regex.IsMatch(code, @"\b" + name + @"\b")))
                sb.Append(decl).Append('\n');
        if (!es3)
            sb.Append("attribute vec2 ").Append(ShaderStages.PositionAttribute).Append(";\n");
        return new(sb.ToString(), body, ShaderStages.VertexMain(body, es3, export));
    }

    /// <summary>
    /// Get the default variables of an effect
    /// </summary>
    public static JsonObject DefaultVars(EffectInfo fx)
    {
        var o = new JsonObject();
        foreach (var v in fx.Vars)
            o[v.Key] = v.Default?.DeepClone();
        return o;
    }

    #endregion
}
