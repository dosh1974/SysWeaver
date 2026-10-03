using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SysWeaver.AI
{
    /// <summary>
    /// A ComfyUI workflow (API format) loaded from disc, with the ComfyUI-Connect annotations parsed.
    /// Node titles are annotated with:
    ///   "$tag" exposes all inputs of the node, "$tag(a,b)" exposes only the listed inputs, "$tag()" exposes no inputs.
    ///   "#tag" returns the (image) output of the node.
    /// If the UI format workflow is supplied, all inputs of subgraphs are annotated automatically (one tag per subgraph input).
    /// </summary>
    public sealed partial class ComfyUiWorkflow
    {
        public override string ToString() => String.Concat(Name.ToQuoted(), " (", Inputs.Count.ToString(), " input tags, ", Outputs.Count.ToString(), " output tags)");

        /// <summary>
        /// The name of the workflow (base file name, used as the endpoint name)
        /// </summary>
        public readonly String Name;

        /// <summary>
        /// The full file name of the workflow
        /// </summary>
        public readonly String FileName;

        /// <summary>
        /// The input tags (case sensitive), each with the inputs that can be set (input name, the json value type of the default value).
        /// Inputs that are connected to other nodes are excluded.
        /// </summary>
        public readonly IReadOnlyDictionary<String, IReadOnlyDictionary<String, JsonValueKind>> Inputs;

        /// <summary>
        /// The output tags (case sensitive)
        /// </summary>
        public readonly IReadOnlyCollection<String> Outputs;

        /// <summary>
        /// All tags (input and output), these can be bypassed
        /// </summary>
        public readonly IReadOnlyCollection<String> Tags;

        /// <summary>
        /// The tags that was added automatically for subgraph inputs (tag => description of the subgraph input)
        /// </summary>
        public readonly IReadOnlyDictionary<String, String> SubgraphTags;

        /// <summary>
        /// Subgraph inputs by the subgraph node tag ("$tag" in the title or the title) and input label, the value is the actual "tag.input" to set.
        /// This makes a subgraph node behave like an annotated node with the subgraph inputs as inputs, ex: "Prompt.sampler" => "sampler.sampler_name".
        /// </summary>
        public readonly IReadOnlyDictionary<String, IReadOnlyDictionary<String, String>> SubgraphInputs;

        /// <summary>
        /// The workflow json (utf8, no BOM), including any automatic annotations
        /// </summary>
        internal readonly byte[] Json;

        /// <summary>
        /// Uniquely identifies this version of the workflow
        /// </summary>
        internal readonly String CacheKey;

        /// <summary>
        /// Key is "tag.input", value identifies the node input(s) that are changed, used to detect when different tags refer to the same thing
        /// </summary>
        internal readonly IReadOnlyDictionary<String, String> InputTargets;

        [GeneratedRegex(@"([\$#!])([a-zA-Z0-9_-]+)(?:\(([^)]*)\))?")]
        private static partial Regex TagRegex();

        ComfyUiWorkflow(String name, String fileName, byte[] json, String cacheKey, Parsed p, IReadOnlyDictionary<String, String> subgraphTags, Dictionary<String, Dictionary<String, String>> subgraphInputs)
        {
            SubgraphInputs = subgraphInputs.ToDictionary(x => x.Key, x => (IReadOnlyDictionary<String, String>)x.Value, StringComparer.Ordinal);
            Name = name;
            FileName = fileName;
            Json = json;
            CacheKey = cacheKey;
            Inputs = p.Inputs.ToDictionary(x => x.Key, x => (IReadOnlyDictionary<String, JsonValueKind>)x.Value, StringComparer.Ordinal);
            Outputs = p.Outputs;
            Tags = p.Tags;
            InputTargets = p.Targets.ToDictionary(x => x.Key, x => String.Join(',', x.Value.Order(StringComparer.Ordinal)), StringComparer.Ordinal);
            SubgraphTags = subgraphTags;
        }

        /// <summary>
        /// Load a workflow from disc
        /// </summary>
        /// <param name="name">The workflow name</param>
        /// <param name="fileName">The full file name</param>
        /// <param name="uiJson">Optional UI format workflow, used to annotate all subgraph inputs</param>
        /// <param name="msg">Optional message host, the automatic annotations are reported here</param>
        /// <returns>The workflow</returns>
        public static ComfyUiWorkflow Load(String name, String fileName, byte[] uiJson = null, IMessageHost msg = null)
        {
            if (!File.Exists(fileName))
                throw new FileNotFoundException("Workflow " + name.ToQuoted() + " not found!", fileName);
            var fi = new FileInfo(fileName);
            var cacheKey = String.Join('|', fileName, fi.LastWriteTimeUtc.Ticks, fi.Length);
            var data = StripBom(File.ReadAllBytes(fileName));
            var p = Parse(data, fileName);
            Dictionary<String, String> subgraphTags = new(StringComparer.Ordinal);
            Dictionary<String, Dictionary<String, String>> subgraphInputs = new(StringComparer.Ordinal);
            if (uiJson != null)
            {
                uiJson = StripBom(uiJson);
                cacheKey = String.Concat(cacheKey, '|', Convert.ToHexStringLower(SHA256.HashData(uiJson), 0, 16));
                var annotated = ComfyUiSubgraphs.Annotate(data, uiJson, p.Tags, out var annotations);
                if (annotated != null)
                {
                    data = annotated;
                    p = Parse(data, fileName);
                    var prefix = String.Concat("ComfyUi workflow ", name.ToQuoted(), ": ");
                    foreach (var a in annotations)
                    {
                        subgraphTags[a.Tag] = a.Source;
                        if (!subgraphInputs.TryGetValue(a.AliasTag, out var si))
                            subgraphInputs.Add(a.AliasTag, si = new Dictionary<String, String>(StringComparer.Ordinal));
                        si.TryAdd(a.AliasInput, String.Concat(a.Tag, '.', a.Input));
                        msg?.AddMessage(String.Concat(prefix, "Subgraph input ", (a.AliasTag + '.' + a.AliasInput).ToQuoted(), " => ", (a.Tag + '.' + a.Input).ToQuoted(), " (node ", String.Join(", ", a.NodeIds), ")"), MessageLevels.Debug);
                    }
                }
            }
            return new ComfyUiWorkflow(name, fileName, data, cacheKey, p, subgraphTags, subgraphInputs);
        }

        static byte[] StripBom(byte[] data)
            => data.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xef, 0xbb, 0xbf]) ? data[3..] : data;

        /// <summary>
        /// Check if some json is an UI format workflow (and not an API format workflow)
        /// </summary>
        internal static bool IsUiFormat(JsonElement root)
            => (root.ValueKind == JsonValueKind.Object) && root.TryGetProperty("nodes", out var uiNodes) && (uiNodes.ValueKind == JsonValueKind.Array);

        sealed class Parsed
        {
            public readonly Dictionary<String, Dictionary<String, JsonValueKind>> Inputs = new(StringComparer.Ordinal);
            public readonly HashSet<String> Outputs = new(StringComparer.Ordinal);
            public readonly HashSet<String> Tags = new(StringComparer.Ordinal);
            public readonly Dictionary<String, HashSet<String>> Targets = new(StringComparer.Ordinal);
        }

        static Parsed Parse(byte[] data, String fileName)
        {
            var p = new Parsed();
            var inputs = p.Inputs;
            var outputs = p.Outputs;
            var tags = p.Tags;
            using (var doc = JsonDocument.Parse(data, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }))
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    throw new Exception("Workflow " + fileName.ToFilename() + " must be a json object!");
                if (IsUiFormat(root))
                    throw new Exception("Workflow " + fileName.ToFilename() + " is in the UI format, save it using \"Workflow > Save API Endpoint\" or \"Export (API)\"!");
                var tagRegex = TagRegex();
                foreach (var node in root.EnumerateObject())
                {
                    var n = node.Value;
                    if ((n.ValueKind != JsonValueKind.Object) || !n.TryGetProperty("class_type", out _))
                        throw new Exception("Workflow " + fileName.ToFilename() + " node " + node.Name.ToQuoted() + " is not a valid API format node!");
                    if (!n.TryGetProperty("_meta", out var meta) || (meta.ValueKind != JsonValueKind.Object))
                        continue;
                    if (!meta.TryGetProperty("title", out var titleE) || (titleE.ValueKind != JsonValueKind.String))
                        continue;
                    var title = titleE.GetString();
                    foreach (Match m in tagRegex.Matches(title))
                    {
                        var tag = m.Groups[2].Value;
                        var hasFilter = m.Groups[3].Success;
                        switch (m.Groups[1].Value[0])
                        {
                            case '$':
                                tags.Add(tag);
                                if (!inputs.TryGetValue(tag, out var d))
                                    inputs.Add(tag, d = new Dictionary<String, JsonValueKind>(StringComparer.Ordinal));
                                if (!n.TryGetProperty("inputs", out var nodeInputs) || (nodeInputs.ValueKind != JsonValueKind.Object))
                                    break;
                                HashSet<String> filter = null;
                                if (hasFilter)
                                    filter = m.Groups[3].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
                                foreach (var input in nodeInputs.EnumerateObject())
                                {
                                    var kind = input.Value.ValueKind;
                                    //  Connected to another node
                                    if (kind == JsonValueKind.Array)
                                        continue;
                                    if ((filter != null) && !filter.Contains(input.Name))
                                        continue;
                                    if (kind == JsonValueKind.False)
                                        kind = JsonValueKind.True;
                                    d[input.Name] = kind;
                                    var key = String.Concat(tag, '.', input.Name);
                                    if (!p.Targets.TryGetValue(key, out var t))
                                        p.Targets.Add(key, t = new HashSet<String>(StringComparer.Ordinal));
                                    t.Add(String.Concat(node.Name, '.', input.Name));
                                }
                                break;
                            case '#':
                                //  ComfyUI-Connect ignores "#tag(..)"
                                if (hasFilter)
                                    break;
                                tags.Add(tag);
                                outputs.Add(tag);
                                break;
                        }
                    }
                }
            }
            return p;
        }
    }

}
