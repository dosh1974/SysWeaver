using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    public sealed partial class ComfyUiService
    {
        #region Diagnose

        /// <summary>
        /// Find out why a workflow returned no output.
        /// ComfyUI only runs the output nodes that pass validation; if another output node (ex: a "Preview Any") is valid, the prompt
        /// runs "successfully" without the wanted output, and ComfyUI-Connect returns an empty result without the validation errors.
        /// The most common cause is a model file (or another combo value) that isn't available on the server, or a node type that isn't installed.
        /// This checks every node of the workflow against the node definitions of the server (/object_info).
        /// </summary>
        /// <param name="wf">The workflow</param>
        /// <param name="cancel">Cancellation token</param>
        /// <returns>The problems found (empty if none)</returns>
        internal async Task<IReadOnlyList<String>> DiagnoseWorkflow(ComfyUiWorkflow wf, CancellationToken cancel)
        {
            var problems = new List<String>();
            using var doc = JsonDocument.Parse(wf.Json);
            var nodes = doc.RootElement.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.Object).ToList();
            var defs = new Dictionary<String, JsonElement?>(StringComparer.Ordinal);
            var defDocs = new List<JsonDocument>();
            try
            {
                foreach (var node in nodes)
                {
                    if (!node.Value.TryGetProperty("class_type", out var ct) || (ct.ValueKind != JsonValueKind.String))
                        continue;
                    var cls = ct.GetString();
                    if (!defs.TryGetValue(cls, out var def))
                    {
                        def = await GetNodeDefinition(cls, defDocs, cancel).ConfigureAwait(false);
                        defs[cls] = def;
                    }
                    var title = node.Value.TryGetProperty("_meta", out var meta) && meta.TryGetProperty("title", out var t) && (t.ValueKind == JsonValueKind.String) ? t.GetString() : null;
                    var nodeDesc = String.Concat("Node ", node.Name, " (", cls, String.IsNullOrEmpty(title) ? "" : (" " + title.ToQuoted()), ")");
                    if (def == null)
                    {
                        problems.Add(String.Concat(nodeDesc, ": the node type isn't installed on the server"));
                        continue;
                    }
                    if (!node.Value.TryGetProperty("inputs", out var inputs) || (inputs.ValueKind != JsonValueKind.Object))
                        continue;
                    foreach (var input in inputs.EnumerateObject())
                    {
                        if (input.Value.ValueKind != JsonValueKind.String)
                            continue;
                        var options = GetComboOptions(def.Value, input.Name);
                        if (options == null)
                            continue;
                        var value = input.Value.GetString();
                        if (options.Contains(value))
                            continue;
                        var isFile = value.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase)
                            || value.EndsWith(".ckpt", StringComparison.OrdinalIgnoreCase)
                            || value.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)
                            || value.EndsWith(".pt", StringComparison.OrdinalIgnoreCase)
                            || value.EndsWith(".pth", StringComparison.OrdinalIgnoreCase)
                            || value.EndsWith(".bin", StringComparison.OrdinalIgnoreCase);
                        problems.Add(String.Concat(nodeDesc, ": ", input.Name, " ", value.ToQuoted(), isFile ? " isn't available on the server (the model file is missing)" : " isn't a valid option"));
                    }
                }
            }
            finally
            {
                foreach (var d in defDocs)
                    d.Dispose();
            }
            return problems;
        }

        /// <summary>
        /// Get the definition of a node type from the server
        /// </summary>
        /// <returns>The definition, null if the node type doesn't exist</returns>
        async Task<JsonElement?> GetNodeDefinition(String cls, List<JsonDocument> docs, CancellationToken cancel)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(EndPoint, "object_info/" + Uri.EscapeDataString(cls)));
            if (Auth != null)
                req.Headers.Authorization = Auth;
            using var resp = await Http.SendAsync(req, cancel).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                throw new Exception(String.Concat("Failed to get the node definition of ", cls.ToQuoted(), ", status ", ((int)resp.StatusCode).ToString(), " (", resp.ReasonPhrase, ")"));
            var data = await resp.Content.ReadAsByteArrayAsync(cancel).ConfigureAwait(false);
            var doc = JsonDocument.Parse(data);
            docs.Add(doc);
            var root = doc.RootElement;
            if ((root.ValueKind == JsonValueKind.Object) && root.TryGetProperty(cls, out var def) && (def.ValueKind == JsonValueKind.Object))
                return def;
            return null;
        }

        /// <summary>
        /// Get the options of a combo input (old format: [["a", "b"], {...}], new format: ["COMBO", { "options": ["a", "b"] }])
        /// </summary>
        /// <returns>The options, null if the input isn't a combo (or isn't defined)</returns>
        static HashSet<String> GetComboOptions(JsonElement def, String inputName)
        {
            if (!def.TryGetProperty("input", out var input) || (input.ValueKind != JsonValueKind.Object))
                return null;
            foreach (var section in input.EnumerateObject())
            {
                if (section.Value.ValueKind != JsonValueKind.Object)
                    continue;
                if (!section.Value.TryGetProperty(inputName, out var cfg) || (cfg.ValueKind != JsonValueKind.Array) || (cfg.GetArrayLength() == 0))
                    continue;
                var first = cfg[0];
                JsonElement list;
                if (first.ValueKind == JsonValueKind.Array)
                    list = first;
                else if ((first.ValueKind == JsonValueKind.String) && (first.GetString() == "COMBO") && (cfg.GetArrayLength() > 1)
                    && (cfg[1].ValueKind == JsonValueKind.Object) && cfg[1].TryGetProperty("options", out var o) && (o.ValueKind == JsonValueKind.Array))
                    list = o;
                else
                    return null;
                //  Only string options can be checked (a combo with non-string options is ignored)
                var res = new HashSet<String>(StringComparer.Ordinal);
                foreach (var x in list.EnumerateArray())
                {
                    if (x.ValueKind != JsonValueKind.String)
                        return null;
                    res.Add(x.GetString());
                }
                //  Some nodes accept values outside of an empty list (ex: no files of that kind), don't report those as errors
                return res.Count == 0 ? null : res;
            }
            return null;
        }

        /// <summary>
        /// The error message when a workflow returned no output, with the result of <see cref="DiagnoseWorkflow"/>
        /// </summary>
        internal async Task<String> GetNoOutputError(String name, ComfyUiWorkflow wf, CancellationToken cancel)
        {
            var msg = String.Concat("The workflow ", name.ToQuoted(), " returned no output!");
            try
            {
                var problems = await DiagnoseWorkflow(wf, cancel).ConfigureAwait(false);
                if (problems.Count > 0)
                    return String.Concat(msg, " ComfyUI skipped the output because the workflow failed validation:\n", String.Join('\n', problems));
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Msg?.AddMessage($"{MsgPrefix}Failed to diagnose workflow {name.ToQuoted()}", ex, MessageLevels.Debug);
            }
            return msg;
        }

        #endregion//Diagnose
    }
}
