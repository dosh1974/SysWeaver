using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SysWeaver.AI
{
    /// <summary>
    /// Annotates the inputs of subgraphs.
    /// The API format flattens subgraphs (a node "452" inside the subgraph instance "459" becomes "459:452") and the subgraph inputs are lost.
    /// The UI format workflow (as saved by ComfyUI) contains the subgraph definitions, so it's used to find the inner node input(s) that each subgraph input is connected to.
    /// Each subgraph input becomes a tag (named from the label or name of the subgraph input), ex: "$seed(seed)" is added to the title of node "459:458".
    /// A tag per subgraph input is required since ComfyUI-Connect requires all nodes with a tag to have the input that is set.
    /// </summary>
    static partial class ComfyUiSubgraphs
    {
        [GeneratedRegex(@"\$([a-zA-Z0-9_-]+)")]
        private static partial Regex InstanceTagRegex();

        /// <summary>
        /// An annotation to add
        /// </summary>
        /// <param name="Tag">The tag name (without '$')</param>
        /// <param name="Input">The input name of the API node(s)</param>
        /// <param name="NodeIds">The API node id's</param>
        /// <param name="Source">Description of the source (for messages)</param>
        /// <param name="AliasTag">The tag of the subgraph instance ("$tag" in the title, or the sanitized title)</param>
        /// <param name="AliasInput">The (sanitized) label of the subgraph input</param>
        public sealed record Annotation(String Tag, String Input, IReadOnlyList<String> NodeIds, String Source, String AliasTag, String AliasInput);

        const int MaxDepth = 32;

        /// <summary>
        /// Annotate an API workflow with the subgraph inputs found in an UI workflow
        /// </summary>
        /// <param name="apiJson">The API format workflow</param>
        /// <param name="uiJson">The UI format workflow</param>
        /// <param name="existingTags">Tags that already exist in the API workflow</param>
        /// <param name="annotations">The annotations that was added</param>
        /// <returns>The annotated API workflow (utf8 json), or null if nothing was annotated</returns>
        public static byte[] Annotate(byte[] apiJson, byte[] uiJson, IReadOnlySet<String> existingTags, out List<Annotation> annotations)
        {
            annotations = null;
            var api = JsonNode.Parse(apiJson, null, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) as JsonObject;
            if (api == null)
                return null;
            using var uiDoc = JsonDocument.Parse(uiJson, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var found = Find(uiDoc.RootElement);
            if (found.Count == 0)
                return null;
            var usedTags = new HashSet<String>(existingTags, StringComparer.OrdinalIgnoreCase);
            var res = new List<Annotation>();
            foreach (var f in found)
            {
                //  Only use API nodes that exist and where the input is a value (not connected)
                var nodeIds = f.Targets.Where(x =>
                    (api[x.NodeId] is JsonObject n)
                    &&
                    (n["inputs"] is JsonObject i)
                    &&
                    (i[x.Input] is JsonValue)).ToList();
                if (nodeIds.Count == 0)
                    continue;
                //  All nodes with a tag must have the input, so use one tag per input name
                var byInput = nodeIds.GroupBy(x => x.Input, StringComparer.Ordinal).ToList();
                foreach (var g in byInput)
                {
                    var baseTag = byInput.Count == 1 ? f.Name : String.Concat(f.Name, '_', Sanitize(g.Key));
                    var tag = baseTag;
                    if (usedTags.Contains(tag))
                    {
                        tag = String.Concat(f.InstanceName, '_', baseTag);
                        var baseTag2 = tag;
                        for (int i = 2; usedTags.Contains(tag); ++i)
                            tag = String.Concat(baseTag2, '_', i.ToString());
                    }
                    usedTags.Add(tag);
                    var ids = g.Select(x => x.NodeId).Distinct(StringComparer.Ordinal).ToList();
                    foreach (var id in ids)
                    {
                        var n = (JsonObject)api[id];
                        if (n["_meta"] is not JsonObject meta)
                            n["_meta"] = meta = new JsonObject();
                        var title = (meta["title"] is JsonValue tv) && tv.TryGetValue<String>(out var t) ? t : "";
                        meta["title"] = String.Concat(title, title.Length > 0 ? " $" : "$", tag, '(', g.Key, ')');
                    }
                    res.Add(new Annotation(tag, g.Key, ids, f.Source, f.InstanceName, baseTag));
                }
            }
            if (res.Count == 0)
                return null;
            annotations = res;
            return JsonSerializer.SerializeToUtf8Bytes(api);
        }

        /// <summary>
        /// Create a valid tag name, ComfyUI-Connect only allows [a-zA-Z0-9_-]
        /// </summary>
        static String Sanitize(String s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (((c >= 'a') && (c <= 'z')) || ((c >= 'A') && (c <= 'Z')) || ((c >= '0') && (c <= '9')) || (c == '_') || (c == '-'))
                    sb.Append(c);
                else if (Char.IsWhiteSpace(c) || (c == '.') || (c == ':'))
                    sb.Append('_');
            }
            return sb.ToString().Trim('_');
        }

        sealed record Target(String NodeId, String Input);

        sealed record Found(String Name, String InstanceName, List<Target> Targets, String Source);

        sealed class Subgraph
        {
            public Subgraph(JsonElement e)
            {
                Def = e;
                if (e.TryGetProperty("nodes", out var nodes) && (nodes.ValueKind == JsonValueKind.Array))
                    foreach (var n in nodes.EnumerateArray())
                        if (n.TryGetProperty("id", out var id))
                            Nodes[IdToString(id)] = n;
                if (e.TryGetProperty("links", out var links) && (links.ValueKind == JsonValueKind.Array))
                {
                    foreach (var l in links.EnumerateArray())
                    {
                        if (l.ValueKind == JsonValueKind.Object)
                        {
                            //  { "id", "origin_id", "origin_slot", "target_id", "target_slot", "type" }
                            if (l.TryGetProperty("id", out var lid) && l.TryGetProperty("target_id", out var tid) && l.TryGetProperty("target_slot", out var ts) && (ts.ValueKind == JsonValueKind.Number))
                                Links[IdToString(lid)] = (IdToString(tid), ts.GetInt32());
                        }
                        else if ((l.ValueKind == JsonValueKind.Array) && (l.GetArrayLength() >= 5))
                        {
                            //  [ id, origin_id, origin_slot, target_id, target_slot, type ]
                            var ts = l[4];
                            if (ts.ValueKind == JsonValueKind.Number)
                                Links[IdToString(l[0])] = (IdToString(l[3]), ts.GetInt32());
                        }
                    }
                }
            }

            public readonly JsonElement Def;
            public readonly Dictionary<String, JsonElement> Nodes = new(StringComparer.Ordinal);
            public readonly Dictionary<String, (String TargetId, int TargetSlot)> Links = new(StringComparer.Ordinal);
        }

        static String IdToString(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText();

        static String GetString(JsonElement e, String name)
            => e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.String) ? v.GetString() : null;

        /// <summary>
        /// Find all inputs of all top level subgraph instances
        /// </summary>
        static List<Found> Find(JsonElement ui)
        {
            var res = new List<Found>();
            if (ui.ValueKind != JsonValueKind.Object)
                return res;
            if (!ui.TryGetProperty("definitions", out var defs) || !defs.TryGetProperty("subgraphs", out var sgs) || (sgs.ValueKind != JsonValueKind.Array))
                return res;
            var subgraphs = new Dictionary<String, Subgraph>(StringComparer.Ordinal);
            foreach (var sg in sgs.EnumerateArray())
            {
                var id = GetString(sg, "id");
                if (id != null)
                    subgraphs[id] = new Subgraph(sg);
            }
            if (!ui.TryGetProperty("nodes", out var nodes) || (nodes.ValueKind != JsonValueKind.Array))
                return res;
            foreach (var n in nodes.EnumerateArray())
            {
                var type = GetString(n, "type");
                if ((type == null) || !subgraphs.TryGetValue(type, out var sg))
                    continue;
                if (!n.TryGetProperty("id", out var idE))
                    continue;
                var instanceId = IdToString(idE);
                //  Skip muted (2) or bypassed (4) instances
                if (n.TryGetProperty("mode", out var mode) && (mode.ValueKind == JsonValueKind.Number) && (mode.GetInt32() != 0))
                    continue;
                var instanceTitle = GetString(n, "title") ?? GetString(sg.Def, "name") ?? instanceId;
                //  Use "$tag" from the title if present (as for a normal node), else the title
                var instanceTag = InstanceTagRegex().Match(instanceTitle);
                var instanceName = instanceTag.Success ? instanceTag.Groups[1].Value : Sanitize(instanceTitle);
                if (instanceName.Length == 0)
                    instanceName = "subgraph" + instanceId;
                //  Inputs of the instance connected to something outside of the subgraph can't be set
                var linked = new HashSet<String>(StringComparer.Ordinal);
                if (n.TryGetProperty("inputs", out var instInputs) && (instInputs.ValueKind == JsonValueKind.Array))
                    foreach (var ii in instInputs.EnumerateArray())
                        if (ii.TryGetProperty("link", out var link) && (link.ValueKind != JsonValueKind.Null))
                        {
                            var name = GetString(ii, "name");
                            if (name != null)
                                linked.Add(name);
                        }
                if (!sg.Def.TryGetProperty("inputs", out var sgInputs) || (sgInputs.ValueKind != JsonValueKind.Array))
                    continue;
                foreach (var si in sgInputs.EnumerateArray())
                {
                    var name = GetString(si, "name");
                    if ((name == null) || linked.Contains(name))
                        continue;
                    var label = GetString(si, "label");
                    var tag = Sanitize(String.IsNullOrWhiteSpace(label) ? name : label);
                    if (tag.Length == 0)
                        tag = Sanitize(name);
                    if (tag.Length == 0)
                        continue;
                    var targets = new List<Target>();
                    Resolve(subgraphs, sg, si, instanceId, targets, 0);
                    if (targets.Count > 0)
                        res.Add(new Found(tag, instanceName, targets, String.Concat(instanceTitle.ToQuoted(), '.', (label ?? name).ToQuoted())));
                }
            }
            return res;
        }

        /// <summary>
        /// Follow the links of a subgraph input to the inner node input(s), recursing into nested subgraphs
        /// </summary>
        static void Resolve(Dictionary<String, Subgraph> subgraphs, Subgraph sg, JsonElement sgInput, String path, List<Target> targets, int depth)
        {
            if (depth > MaxDepth)
                return;
            if (!sgInput.TryGetProperty("linkIds", out var linkIds) || (linkIds.ValueKind != JsonValueKind.Array))
                return;
            foreach (var lid in linkIds.EnumerateArray())
            {
                if (!sg.Links.TryGetValue(IdToString(lid), out var l))
                    continue;
                if (!sg.Nodes.TryGetValue(l.TargetId, out var node))
                    continue;
                if (!node.TryGetProperty("inputs", out var inputs) || (inputs.ValueKind != JsonValueKind.Array) || (l.TargetSlot < 0) || (l.TargetSlot >= inputs.GetArrayLength()))
                    continue;
                var input = inputs[l.TargetSlot];
                var nodePath = String.Concat(path, ":", l.TargetId);
                var type = GetString(node, "type");
                if ((type != null) && subgraphs.TryGetValue(type, out var nested))
                {
                    //  Nested subgraph, continue with the matching input of that subgraph
                    var inputName = GetString(input, "name");
                    if ((inputName == null) || !nested.Def.TryGetProperty("inputs", out var nestedInputs) || (nestedInputs.ValueKind != JsonValueKind.Array))
                        continue;
                    foreach (var ni in nestedInputs.EnumerateArray())
                        if (GetString(ni, "name") == inputName)
                            Resolve(subgraphs, nested, ni, nodePath, targets, depth + 1);
                    continue;
                }
                //  Only widgets have values that can be set
                if (!input.TryGetProperty("widget", out var widget) || (widget.ValueKind != JsonValueKind.Object))
                    continue;
                var widgetName = GetString(widget, "name");
                if (widgetName != null)
                    targets.Add(new Target(nodePath, widgetName));
            }
        }
    }

}
