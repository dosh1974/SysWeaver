using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    /// <summary>
    /// How generated names are formatted
    /// </summary>
    public enum ComfyUiNameCasing
    {
        /// <summary>
        /// Ex: "AspectRatio"
        /// </summary>
        Pascal,
        /// <summary>
        /// Ex: "aspectRatio"
        /// </summary>
        Camel,
        /// <summary>
        /// Ex: "aspect_ratio"
        /// </summary>
        Snake,
        /// <summary>
        /// The workflow name, with invalid characters replaced by '_'
        /// </summary>
        Original,
    }

    /// <summary>
    /// Options for generating the source of the input and output types of a workflow
    /// </summary>
    public sealed class ComfyUiTypeOptions
    {
        #region Names

        /// <summary>
        /// The namespace of the generated types, null or empty for no namespace
        /// </summary>
        public String Namespace = "ComfyUi";

        /// <summary>
        /// If true, a file scoped namespace is used ("namespace X;"), else a block
        /// </summary>
        public bool FileScopedNamespace = true;

        /// <summary>
        /// The workflow name, used for the type names and comments.
        /// If null, the file name is used (or "Workflow" if the stream isn't a file).
        /// </summary>
        public String WorkflowName;

        /// <summary>
        /// The base name of the types, if null the workflow name formatted using TypeCasing is used
        /// </summary>
        public String TypeName;

        /// <summary>
        /// Appended to the base name for the input type
        /// </summary>
        public String InputTypeSuffix = "Input";

        /// <summary>
        /// Appended to the base name for the output type
        /// </summary>
        public String OutputTypeSuffix = "Output";

        /// <summary>
        /// How the type names are formatted (when TypeName is null)
        /// </summary>
        public ComfyUiNameCasing TypeCasing = ComfyUiNameCasing.Pascal;

        /// <summary>
        /// How the member names are formatted.
        /// Matching ignores case and non letters / digits so any casing maps to the same input.
        /// </summary>
        public ComfyUiNameCasing MemberCasing = ComfyUiNameCasing.Pascal;

        /// <summary>
        /// If true, the shortest name that maps unambiguously is used (ex: "Megapixels"), else the tag is included for normal annotated nodes (ex: "ResolutionMegapixels").
        /// Subgraph inputs always use the subgraph input label.
        /// </summary>
        public bool PreferShortNames = true;

        /// <summary>
        /// If true, all members are mapped explicitly using [ComfyUiName("tag.input")], else only members that can't be mapped by name are
        /// </summary>
        public bool ExplicitNames;

        #endregion//Names

        #region Code

        /// <summary>
        /// If true, properties ({ get; set; }) are generated, else fields
        /// </summary>
        public bool UseProperties;

        /// <summary>
        /// If true, the types are sealed
        /// </summary>
        public bool Sealed = true;

        /// <summary>
        /// If true, xml documentation comments are generated (describing the node and input and showing the workflow default value)
        /// </summary>
        public bool Comments = true;

        /// <summary>
        /// If true, input members are initialized with the workflow default value, else they are null (and the workflow default is used)
        /// </summary>
        public bool InitializeDefaults;

        /// <summary>
        /// If true, a bool? member is generated for each annotated node, setting it to false will bypass the node(s)
        /// </summary>
        public bool BypassMembers;

        /// <summary>
        /// The type used for inputs that looks like a media file name (ex: the image of a "Load Image" node), can be "ComfyUiFile", "byte[]", "Uri" or "String"
        /// </summary>
        public String FileInputType = "ComfyUiFile";

        /// <summary>
        /// Number inputs with an integer default are generated as int? (or long? for seeds), unless the input name contains one of these (case insensitive) or the node class contains "Float", then double? is used.
        /// The API format doesn't contain the input types, so this is a heuristic.
        /// </summary>
        public String[] FloatInputNames = ["cfg", "denoise", "strength", "megapixels", "shift", "guidance", "scale", "weight", "ratio", "temperature", "top_p", "sigma", "eta"];

        /// <summary>
        /// The type used for outputs, can be "byte[]" (first result), "byte[][]" / "List&lt;byte[]&gt;" (all results) or "String" / "String[]" / "List&lt;String&gt;" (base64)
        /// </summary>
        public String OutputType = "byte[]";

        /// <summary>
        /// If true, the required using statements are added
        /// </summary>
        public bool Usings = true;

        /// <summary>
        /// The text used for one level of indentation
        /// </summary>
        public String Indent = "    ";

        /// <summary>
        /// The new line sequence
        /// </summary>
        public String NewLine = "\r\n";

        /// <summary>
        /// The max number of characters of default values shown in comments
        /// </summary>
        public int MaxCommentValueLength = 100;

        #endregion//Code

        #region Subgraphs

        /// <summary>
        /// Optional UI format workflow (as saved by ComfyUI using "Workflow > Save"), used to annotate the inputs of subgraphs.
        /// Has precedence over UiWorkflowFileName and UiWorkflowFolder.
        /// </summary>
        public byte[] UiWorkflow;

        /// <summary>
        /// Optional file name of the UI format workflow, used to annotate the inputs of subgraphs.
        /// Has precedence over UiWorkflowFolder.
        /// </summary>
        public String UiWorkflowFileName;

        /// <summary>
        /// Optional folder with UI format workflows (ex: "ComfyUI/user/default/workflows"), the file with the same name as the workflow is used to annotate the inputs of subgraphs.
        /// </summary>
        public String UiWorkflowFolder;

        /// <summary>
        /// Optional message host, the automatic subgraph annotations are reported here
        /// </summary>
        public IMessageHost Msg;

        #endregion//Subgraphs
    }


    /// <summary>
    /// Generates the source code of the input and output types (to use with ComfyUiService.RunWorkflow) of a workflow.
    /// The member names are validated using the same matching rules as RunWorkflow, members that can't be mapped by name uses the [ComfyUiName] attribute.
    /// </summary>
    public static class ComfyUiTypeGenerator
    {
        /// <summary>
        /// Generate the source code of the input and output types of a workflow
        /// </summary>
        /// <param name="s">A stream with the API format workflow json</param>
        /// <param name="options">Optional options</param>
        /// <returns>The source code of the input type and the source code of the output type</returns>
        public static (String Input, String Output) GetInputOutputTypes(Stream s, ComfyUiTypeOptions options = null)
        {
            ArgumentNullException.ThrowIfNull(s);
            options = options ?? new ComfyUiTypeOptions();
            var name = GetName(options, (s as FileStream)?.Name);
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            var ui = options.UiWorkflow;
            if (ui == null)
            {
                var uiFile = GetUiFileName(options, name);
                if (uiFile != null)
                    ui = File.ReadAllBytes(uiFile);
            }
            return GetInputOutputTypes(ComfyUiWorkflow.FromJson(name, ms.ToArray(), ui, options.Msg), options);
        }

        /// <summary>
        /// Generate the source code of the input and output types of a workflow
        /// </summary>
        /// <param name="fileName">The file name of the API format workflow json</param>
        /// <param name="options">Optional options</param>
        /// <returns>The source code of the input type and the source code of the output type</returns>
        public static async Task<(String Input, String Output)> GetInputOutputTypes(String fileName, ComfyUiTypeOptions options = null)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            options = options ?? new ComfyUiTypeOptions();
            var name = GetName(options, fileName);
            var json = await File.ReadAllBytesAsync(fileName).ConfigureAwait(false);
            var ui = options.UiWorkflow;
            if (ui == null)
            {
                var uiFile = GetUiFileName(options, name);
                if (uiFile != null)
                    ui = await File.ReadAllBytesAsync(uiFile).ConfigureAwait(false);
            }
            return GetInputOutputTypes(ComfyUiWorkflow.FromJson(name, json, ui, options.Msg), options);
        }

        /// <summary>
        /// Generate the source code of the input and output types of a loaded workflow (ex: from ComfyUiService.GetWorkflow)
        /// </summary>
        /// <param name="wf">The workflow</param>
        /// <param name="options">Optional options (the subgraph options are ignored, the workflow is already annotated)</param>
        /// <returns>The source code of the input type and the source code of the output type</returns>
        public static (String Input, String Output) GetInputOutputTypes(ComfyUiWorkflow wf, ComfyUiTypeOptions options = null)
        {
            ArgumentNullException.ThrowIfNull(wf);
            options = options ?? new ComfyUiTypeOptions();
            var baseName = String.IsNullOrEmpty(options.TypeName) ? FormatName(wf.Name, options.TypeCasing) : options.TypeName;
            using var doc = JsonDocument.Parse(wf.Json);
            var root = doc.RootElement;
            return (
                GenerateInput(wf, root, baseName + options.InputTypeSuffix, options),
                GenerateOutput(wf, root, baseName + options.OutputTypeSuffix, options));
        }

        static String GetName(ComfyUiTypeOptions o, String fileName)
        {
            if (!String.IsNullOrEmpty(o.WorkflowName))
                return o.WorkflowName;
            return String.IsNullOrEmpty(fileName) ? "Workflow" : Path.GetFileNameWithoutExtension(fileName);
        }

        static String GetUiFileName(ComfyUiTypeOptions o, String name)
        {
            if (!String.IsNullOrEmpty(o.UiWorkflowFileName))
                return o.UiWorkflowFileName;
            var folder = o.UiWorkflowFolder;
            if (String.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return null;
            return Directory.EnumerateFiles(folder, "*.json").FirstOrDefault(x => String.Equals(Path.GetFileNameWithoutExtension(x), name, StringComparison.OrdinalIgnoreCase));
        }

        #region Inputs

        sealed record Member(String Name, String Type, String Attribute, List<String> Comment, String Init);

        static String GenerateInput(ComfyUiWorkflow wf, JsonElement root, String typeName, ComfyUiTypeOptions o)
        {
            var cands = new WorkflowMapper.Candidates(wf);
            var members = new List<Member>();
            var usedNames = new HashSet<String>(StringComparer.Ordinal) { typeName };
            foreach (var g in cands.All.GroupBy(x => x.Target, StringComparer.Ordinal))
            {
                //  The actual tag input (not a subgraph alias)
                var direct = g.FirstOrDefault(x => (x.Tag == x.PTag) && (x.Input == x.PInput)) ?? g.First();
                var targets = g.Key.Split(',').Select(x =>
                {
                    var dot = x.IndexOf('.');
                    return (Node: x.Substring(0, dot), Input: x.Substring(dot + 1));
                }).ToList();
                var nodeId = targets[0].Node;
                root.TryGetProperty(nodeId, out var node);
                var def = default(JsonElement);
                var hasDef = (node.ValueKind == JsonValueKind.Object) && node.TryGetProperty("inputs", out var ni) && (ni.ValueKind == JsonValueKind.Object) && ni.TryGetProperty(targets[0].Input, out def);
                var classType = GetString(node, "class_type");
                var (type, init) = GetInputType(direct.PInput, classType, hasDef ? def : default, o);

                //  Find a name that maps to this input
                var isBool = type.StartsWith("bool", StringComparison.Ordinal);
                var isSubgraph = wf.SubgraphTags.ContainsKey(direct.PTag);
                var names = new List<(String Name, WorkflowMapper.Cand Cand)>();
                void Add(String name, WorkflowMapper.Cand c)
                {
                    name = FormatName(name, o.MemberCasing);
                    if (!usedNames.Contains(name))
                        names.Add((name, c));
                }
                var single = g.Where(x => cands.TagInputCount[x.Tag] == 1).ToList();
                foreach (var c in single.Where(x => wf.SubgraphTags.ContainsKey(x.Tag)))
                    Add(c.Tag, c);
                //  Primitive / constant nodes have generic input names, ex: "$MaxDuration" with the input "value"
                foreach (var c in single.Where(x => !wf.SubgraphTags.ContainsKey(x.Tag) && GenericInputNames.Contains(x.Input)))
                    Add(c.Tag, c);
                if (o.PreferShortNames || isSubgraph)
                    foreach (var c in g)
                        Add(c.Input, c);
                foreach (var c in g)
                    Add(String.Concat(c.Tag, "_", c.Input), c);
                foreach (var c in single.Where(x => !wf.SubgraphTags.ContainsKey(x.Tag)))
                    Add(c.Tag, c);
                (String Name, WorkflowMapper.Cand Cand) found = default;
                if (!o.ExplicitNames)
                {
                    foreach (var n in names)
                    {
                        var m = cands.Match(n.Name, isBool, out _, out _);
                        if ((m.Count == 1) && (m[0].Target == g.Key))
                        {
                            found = n;
                            break;
                        }
                    }
                }
                String attribute = null;
                if (found.Name == null)
                {
                    found = names.Count > 0 ? names[0] : (FormatName(String.Concat(direct.Tag, "_", direct.Input), o.MemberCasing), direct);
                    found.Name = MakeUnique(found.Name, usedNames);
                    attribute = String.Concat("[ComfyUiName(", ToLiteral(found.Cand.Tag + '.' + found.Cand.Input), ")]");
                }
                usedNames.Add(found.Name);

                List<String> comment = null;
                if (o.Comments)
                {
                    comment = new List<String>();
                    var nodes = String.Join(", ", targets.Select(x => x.Node).Distinct(StringComparer.Ordinal));
                    var nodeDesc = String.Concat("node ", nodes, classType == null ? "" : String.Concat(" (", classType, ")"));
                    if (isSubgraph && wf.SubgraphTags.TryGetValue(direct.PTag, out var source))
                        comment.Add(String.Concat("Subgraph input ", source, ", ", nodeDesc, " input ", direct.PInput.ToQuoted()));
                    else
                        comment.Add(String.Concat(("$" + direct.PTag).ToQuoted(), " ", nodeDesc, " input ", direct.PInput.ToQuoted()));
                    if (hasDef)
                        comment.Add(String.Concat("Workflow default: ", FormatValue(def, o.MaxCommentValueLength)));
                }
                members.Add(new Member(found.Name, type, attribute, comment, o.InitializeDefaults ? init : null));
            }
            if (o.BypassMembers)
            {
                foreach (var tag in wf.Tags.Where(x => !wf.SubgraphTags.ContainsKey(x)))
                {
                    var name = MakeUnique(FormatName(tag + "_enabled", o.MemberCasing), usedNames);
                    usedNames.Add(name);
                    var comment = o.Comments ? new List<String> { String.Concat("Set to false to bypass the node(s) annotated with ", ("$" + tag).ToQuoted(), " or ", ("#" + tag).ToQuoted()) } : null;
                    members.Add(new Member(name, "bool?", String.Concat("[ComfyUiName(", ToLiteral(tag), ")]"), comment, null));
                }
            }
            var typeComment = new List<String>
            {
                String.Concat("Inputs of the ", wf.Name.ToQuoted(), " workflow."),
                "Null values are not sent, so the workflow default is used.",
            };
            return WriteType(typeName, typeComment, members, o);
        }

        static readonly HashSet<String> GenericInputNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "value", "string", "text", "int", "float", "number", "boolean", "bool",
        };

        static readonly HashSet<String> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".tif", ".tiff",
            ".mp4", ".webm", ".mov", ".mkv", ".avi",
            ".wav", ".mp3", ".flac", ".ogg", ".m4a", ".opus",
        };

        static bool IsMediaFile(String s)
        {
            if (String.IsNullOrEmpty(s) || (s.IndexOfAny(Path.GetInvalidPathChars()) >= 0))
                return false;
            //  Can have a " [input]" suffix
            var i = s.IndexOf(" [", StringComparison.Ordinal);
            if (i > 0)
                s = s.Substring(0, i);
            return MediaExtensions.Contains(Path.GetExtension(s));
        }

        static bool IsFloat(String input, String classType, ComfyUiTypeOptions o)
        {
            if (classType?.Contains("Float", StringComparison.OrdinalIgnoreCase) ?? false)
                return true;
            var names = o.FloatInputNames;
            return (names != null) && names.Any(x => input.Contains(x, StringComparison.OrdinalIgnoreCase));
        }

        static (String Type, String Init) GetInputType(String input, String classType, JsonElement def, ComfyUiTypeOptions o)
        {
            switch (def.ValueKind)
            {
                case JsonValueKind.String:
                    var s = def.GetString();
                    if (IsMediaFile(s) && (classType?.StartsWith("Load", StringComparison.OrdinalIgnoreCase) ?? false))
                        return (String.IsNullOrEmpty(o.FileInputType) ? "ComfyUiFile" : o.FileInputType, null);
                    return ("String", ToLiteral(s));
                case JsonValueKind.Number:
                    var raw = def.GetRawText();
                    if ((raw.IndexOfAny(['.', 'e', 'E']) < 0) && def.TryGetInt64(out var l) && !IsFloat(input, classType, o))
                    {
                        if ((l > int.MaxValue) || (l < int.MinValue) || input.Contains("seed", StringComparison.OrdinalIgnoreCase))
                            return ("long?", raw);
                        return ("int?", raw);
                    }
                    return ("double?", raw);
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return ("bool?", def.ValueKind == JsonValueKind.True ? "true" : "false");
                case JsonValueKind.Object:
                case JsonValueKind.Array:
                    return ("JsonElement?", null);
            }
            return ("Object", null);
        }

        #endregion//Inputs

        #region Outputs

        static String GenerateOutput(ComfyUiWorkflow wf, JsonElement root, String typeName, ComfyUiTypeOptions o)
        {
            var members = new List<Member>();
            var usedNames = new HashSet<String>(StringComparer.Ordinal) { typeName };
            var outputType = String.IsNullOrEmpty(o.OutputType) ? "byte[]" : o.OutputType;
            var many = outputType.EndsWith("[][]", StringComparison.Ordinal) || outputType.StartsWith("List<", StringComparison.Ordinal) || (outputType == "String[]");
            foreach (var tag in wf.Outputs)
            {
                //  Output names are matched against the tag, a [ComfyUiName] is required if the formatted name differs (ex: a keyword) or if multiple outputs have the same normalized name
                var name = FormatName(tag, o.MemberCasing);
                String attribute = null;
                var n = WorkflowMapper.Norm(tag);
                if (o.ExplicitNames || usedNames.Contains(name) || (WorkflowMapper.Norm(name) != n))
                    attribute = String.Concat("[ComfyUiName(", ToLiteral(tag), ")]");
                name = MakeUnique(name, usedNames);
                usedNames.Add(name);
                List<String> comment = null;
                if (o.Comments)
                {
                    comment = new List<String>();
                    var nodes = new List<String>();
                    foreach (var node in root.EnumerateObject())
                    {
                        var title = GetString(GetProperty(node.Value, "_meta"), "title");
                        if ((title != null) && title.Contains("#" + tag, StringComparison.Ordinal))
                            nodes.Add(String.Concat(node.Name, " (", GetString(node.Value, "class_type"), ")"));
                    }
                    comment.Add(String.Concat(("#" + tag).ToQuoted(), " node ", String.Join(", ", nodes), many ? ", all results" : ", the first result", outputType.StartsWith("String", StringComparison.Ordinal) || outputType.StartsWith("List<String", StringComparison.Ordinal) ? " (base64 encoded)" : ""));
                    if (wf.Outputs.Count(x => WorkflowMapper.Norm(x) == n) > 1)
                        comment.Add("Warning: multiple outputs only differs in case or separators, only the first one can be mapped");
                }
                members.Add(new Member(name, outputType, attribute, comment, null));
            }
            var typeComment = new List<String>
            {
                String.Concat("Outputs of the ", wf.Name.ToQuoted(), " workflow"),
            };
            return WriteType(typeName, typeComment, members, o);
        }

        #endregion//Outputs

        #region Source

        static String WriteType(String typeName, List<String> typeComment, List<Member> members, ComfyUiTypeOptions o)
        {
            var nl = o.NewLine ?? "\r\n";
            var ind = o.Indent ?? "    ";
            var sb = new StringBuilder();
            if (o.Usings)
            {
                var usings = new SortedSet<String>(StringComparer.Ordinal) { "System" };
                foreach (var m in members)
                {
                    if (m.Type.StartsWith("JsonElement", StringComparison.Ordinal))
                        usings.Add("System.Text.Json");
                    if (m.Type.StartsWith("List<", StringComparison.Ordinal))
                        usings.Add("System.Collections.Generic");
                    if ((m.Attribute != null) || m.Type.StartsWith("ComfyUiFile", StringComparison.Ordinal))
                        usings.Add("SysWeaver.AI");
                }
                foreach (var u in usings)
                    sb.Append("using ").Append(u).Append(';').Append(nl);
                sb.Append(nl);
            }
            var prefix = "";
            var ns = o.Namespace;
            var block = !String.IsNullOrEmpty(ns) && !o.FileScopedNamespace;
            if (!String.IsNullOrEmpty(ns))
            {
                sb.Append("namespace ").Append(ns);
                if (block)
                {
                    sb.Append(nl).Append('{').Append(nl);
                    prefix = ind;
                }
                else
                {
                    sb.Append(';').Append(nl).Append(nl);
                }
            }
            if (o.Comments)
                WriteComment(sb, prefix, typeComment, nl);
            sb.Append(prefix).Append("public ").Append(o.Sealed ? "sealed " : "").Append("class ").Append(typeName).Append(nl);
            sb.Append(prefix).Append('{').Append(nl);
            var mp = prefix + ind;
            var first = true;
            foreach (var m in members)
            {
                if (!first && o.Comments)
                    sb.Append(nl);
                first = false;
                if (m.Comment != null)
                    WriteComment(sb, mp, m.Comment, nl);
                if (m.Attribute != null)
                    sb.Append(mp).Append(m.Attribute).Append(nl);
                sb.Append(mp).Append("public ").Append(m.Type).Append(' ').Append(m.Name);
                if (o.UseProperties)
                {
                    sb.Append(" { get; set; }");
                    if (m.Init != null)
                        sb.Append(" = ").Append(m.Init).Append(';');
                }
                else
                {
                    if (m.Init != null)
                        sb.Append(" = ").Append(m.Init);
                    sb.Append(';');
                }
                sb.Append(nl);
            }
            sb.Append(prefix).Append('}').Append(nl);
            if (block)
                sb.Append('}').Append(nl);
            return sb.ToString();
        }

        static void WriteComment(StringBuilder sb, String prefix, List<String> lines, String nl)
        {
            sb.Append(prefix).Append("/// <summary>").Append(nl);
            foreach (var l in lines)
                sb.Append(prefix).Append("/// ").Append(XmlEscape(l)).Append(nl);
            sb.Append(prefix).Append("/// </summary>").Append(nl);
        }

        static String XmlEscape(String s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        static String FormatValue(JsonElement e, int maxLength)
        {
            var s = e.ValueKind == JsonValueKind.String ? ToLiteral(e.GetString()) : e.GetRawText();
            s = String.Join(' ', s.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if ((maxLength > 0) && (s.Length > maxLength))
                s = String.Concat(s.AsSpan(0, maxLength), "..");
            return s;
        }

        static String ToLiteral(String s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\0': sb.Append("\\0"); break;
                    default:
                        if (Char.IsControl(c))
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        static JsonElement GetProperty(JsonElement e, String name)
            => (e.ValueKind == JsonValueKind.Object) && e.TryGetProperty(name, out var v) ? v : default;

        static String GetString(JsonElement e, String name)
        {
            var v = GetProperty(e, name);
            return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }

        static String MakeUnique(String name, HashSet<String> used)
        {
            if (!used.Contains(name))
                return name;
            for (int i = 2; ; ++i)
            {
                var n = name + i.ToString();
                if (!used.Contains(n))
                    return n;
            }
        }

        /// <summary>
        /// Split a name into words, at non letters / digits and at lower to upper case changes, ex: "max_Duration", "MaxDuration" => "max", "Duration"
        /// </summary>
        static List<String> SplitWords(String s)
        {
            var words = new List<String>();
            var sb = new StringBuilder();
            char prev = '\0';
            foreach (var c in s)
            {
                if (!Char.IsLetterOrDigit(c) || (c > 127))
                {
                    if (sb.Length > 0)
                        words.Add(sb.ToString());
                    sb.Clear();
                    prev = '\0';
                    continue;
                }
                if ((sb.Length > 0) && Char.IsUpper(c) && (Char.IsLower(prev) || Char.IsDigit(prev)))
                {
                    words.Add(sb.ToString());
                    sb.Clear();
                }
                sb.Append(c);
                prev = c;
            }
            if (sb.Length > 0)
                words.Add(sb.ToString());
            return words;
        }

        static readonly HashSet<String> Keywords = new(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
        };

        /// <summary>
        /// Format a name as a valid C# identifier
        /// </summary>
        static String FormatName(String s, ComfyUiNameCasing casing)
        {
            String r;
            if (casing == ComfyUiNameCasing.Original)
            {
                var sb = new StringBuilder(s.Length);
                foreach (var c in s)
                    sb.Append(((c <= 127) && Char.IsLetterOrDigit(c)) || (c == '_') ? c : '_');
                r = sb.ToString().Trim('_');
            }
            else
            {
                var words = SplitWords(s);
                switch (casing)
                {
                    case ComfyUiNameCasing.Snake:
                        r = String.Join('_', words.Select(x => x.ToLowerInvariant()));
                        break;
                    case ComfyUiNameCasing.Camel:
                        r = String.Concat(words.Select((x, i) => i == 0 ? x.ToLowerInvariant() : Capitalize(x)));
                        break;
                    default:
                        r = String.Concat(words.Select(Capitalize));
                        break;
                }
            }
            if (r.Length == 0)
                return "_";
            if (Char.IsDigit(r[0]))
                r = "_" + r;
            if (Keywords.Contains(r))
                r = "@" + r;
            return r;
        }

        static String Capitalize(String s) => s.Length == 0 ? s : String.Concat(Char.ToUpperInvariant(s[0]).ToString(), s.AsSpan(1));

        #endregion//Source
    }

}
