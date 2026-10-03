using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.MicroService;

namespace SysWeaver.AI
{
    /// <summary>
    /// Run ComfyUI workflows using typed inputs and outputs, through the ComfyUI-Connect REST API.
    /// See: https://github.com/Good-Dream-Studio/ComfyUI-Connect
    /// Workflows are loaded from a local folder (API format json files) and uploaded to the server on first use (optional).
    /// </summary>
    public sealed class ComfyUiService
    {
        public override string ToString() => String.Concat("End point: ", EndPoint.ToString().ToQuoted(), ", Workflow folder: ", WorkflowFolder.ToFilename());

        /// <summary>
        /// Create a ComfyUI service
        /// </summary>
        /// <param name="sm">The service manager</param>
        /// <param name="p">The parameters</param>
        /// <param name="msg">Optional message host (if null the service manager is used)</param>
        public ComfyUiService(ServiceManager sm = null, ComfyUiParams p = null, IMessageHost msg = null)
        {
            p = p ?? new ComfyUiParams();
            Msg = msg ?? sm;
            var ep = p.EndPoint;
            if (String.IsNullOrEmpty(ep))
                throw new Exception(nameof(p.EndPoint) + " parameter may not be empty!");
            if (!ep.EndsWith('/'))
                ep += '/';
            EndPoint = new Uri(ep, UriKind.Absolute);
            var apiKey = p.GetApiKey(false);
            if (!String.IsNullOrEmpty(apiKey))
                Auth = new AuthenticationHeaderValue(String.IsNullOrEmpty(p.AuthScheme) ? "Bearer" : p.AuthScheme, apiKey);
            ComfyUiToken = String.IsNullOrEmpty(p.ComfyUiToken) ? null : p.ComfyUiToken;
            UploadWorkflows = p.UploadWorkflows;
            var folder = p.WorkflowFolder;
            if (String.IsNullOrEmpty(folder))
                throw new Exception(nameof(p.WorkflowFolder) + " parameter may not be empty!");
            folder = PathTemplate.Resolve(folder);
            folder = EnvInfo.MakeAbsoulte(folder);
            WorkflowFolder = Path.GetFullPath(folder);
            if (!Directory.Exists(WorkflowFolder))
                Msg?.AddMessage(MsgPrefix + "The workflow folder " + WorkflowFolder.ToFilename() + " doesn't exist!", MessageLevels.Warning);
            Http = WebTools.GetHttpClient(Math.Max(5, p.NetworkTimeoutSeconds));
            AnnotateSubgraphInputs = p.AnnotateSubgraphInputs;
            FetchUiWorkflows = p.FetchUiWorkflows;
            var uiFolder = p.UiWorkflowFolder;
            if (!String.IsNullOrEmpty(uiFolder))
                UiWorkflowFolder = Path.GetFullPath(EnvInfo.MakeAbsoulte(PathTemplate.Resolve(uiFolder)));
        }

        readonly bool AnnotateSubgraphInputs;
        readonly bool FetchUiWorkflows;

        /// <summary>
        /// The full path of the folder where UI format workflows are loaded from (used to annotate subgraph inputs), null if not used
        /// </summary>
        public readonly String UiWorkflowFolder;

        const String MsgPrefix = "ComfyUi: ";

        readonly IMessageHost Msg;
        readonly HttpClient Http;
        readonly AuthenticationHeaderValue Auth;
        readonly String ComfyUiToken;
        readonly bool UploadWorkflows;

        /// <summary>
        /// The ComfyUI end point (always ends with a '/')
        /// </summary>
        public readonly Uri EndPoint;

        /// <summary>
        /// The full path of the folder where workflows are loaded from
        /// </summary>
        public readonly String WorkflowFolder;

        #region Run

        /// <summary>
        /// Run a workflow.
        /// The public properties and fields of T are mapped to the workflow inputs (case insensitive, ignoring non letters or digits):
        ///   "TagInput" (or "Tag_Input" etc) maps to the input "input" of the node(s) annotated with "$tag".
        ///   "Tag" maps to the input of the "$tag" node(s) if the tag only has one input (subgraph inputs are annotated like this).
        ///   A bool named "Tag" maps to the node(s) annotated with "$tag" or "#tag", false will bypass the node(s).
        ///   "Input" maps to the input "input" if it's unique among all tags.
        ///   Use [ComfyUiName("tag.input")] or [ComfyUiName("tag")] to map explicitly, [ComfyUiIgnore] to ignore.
        ///   Null values are not sent (the workflow default is used).
        ///   byte[], ComfyUiFile and Uri values are sent as files (ex: for a "Load Image" node).
        /// The public settable properties and fields of R are mapped to the outputs (nodes annotated with "#tag").
        ///   Supported types are: byte[] (first result), byte[][] / List&lt;byte[]&gt; (all results), String / String[] / List&lt;String&gt; (base64 encoded).
        /// Mapping warnings are reported (once) to the message host.
        /// </summary>
        /// <typeparam name="T">The input type</typeparam>
        /// <typeparam name="R">The output type</typeparam>
        /// <param name="p">The input values, may be null</param>
        /// <param name="workflowName">The name of the workflow (base filename, the ".json" extension is added)</param>
        /// <param name="cancel">Optional cancellation token</param>
        /// <returns>The output values</returns>
        public Task<R> RunWorkflow<T, R>(T p, String workflowName, CancellationToken cancel = default) where R : new()
        {
            if (Workflows.TryGetValue(workflowName, out var wt) && wt.IsCompletedSuccessfully)
                return WorkflowCache<T, R>.Get(wt.Result, Msg)(p, new ComfyUiRunContext(this, cancel));
            return RunWorkflowLoad<T, R>(p, workflowName, cancel);
        }

        async Task<R> RunWorkflowLoad<T, R>(T p, String workflowName, CancellationToken cancel) where R : new()
        {
            var wf = await GetWorkflow(workflowName).ConfigureAwait(false);
            return await WorkflowCache<T, R>.Get(wf, Msg)(p, new ComfyUiRunContext(this, cancel)).ConfigureAwait(false);
        }

        /// <summary>
        /// Called by the compiled workflow delegates
        /// </summary>
        internal async Task<R> Execute<T, R>(CancellationToken cancel, ComfyUiWorkflow wf, T p, Action<T, Utf8JsonWriter> write, Func<JsonElement, R> read)
        {
            await EnsureUploaded(wf).ConfigureAwait(false);
            var buf = new ArrayBufferWriter<byte>(4096);
            using (var w = new Utf8JsonWriter(buf))
            {
                w.WriteStartObject();
                write(p, w);
                var token = ComfyUiToken;
                if (token != null)
                    w.WriteString("_token", token);
                w.WriteEndObject();
            }
            using var req = new HttpRequestMessage(HttpMethod.Post, GetWorkflowUri(wf.Name));
            req.Content = new ReadOnlyMemoryContent(buf.WrittenMemory);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var doc = await Send(req, wf.Name, cancel).ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("result", out var result) || (result.ValueKind != JsonValueKind.Object))
                throw new Exception(String.Concat(MsgPrefix, "Workflow ", wf.Name.ToQuoted(), " returned no result!"));
            return read(result);
        }

        async Task<JsonDocument> Send(HttpRequestMessage req, String workflowName, CancellationToken cancel)
        {
            if (Auth != null)
                req.Headers.Authorization = Auth;
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
            var data = await resp.Content.ReadAsByteArrayAsync(cancel).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var text = Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 2048));
                throw new Exception(String.Concat(MsgPrefix, "Workflow ", workflowName.ToQuoted(), " request ", req.Method.ToString(), " failed with status ", ((int)resp.StatusCode).ToString(), " (", resp.ReasonPhrase, "): ", text));
            }
            var doc = JsonDocument.Parse(data);
            try
            {
                var root = doc.RootElement;
                if ((root.ValueKind != JsonValueKind.Object) || !root.TryGetProperty("status", out var status) || (status.ValueKind != JsonValueKind.String) || (status.GetString() != "success"))
                    throw new Exception(String.Concat(MsgPrefix, "Workflow ", workflowName.ToQuoted(), " request ", req.Method.ToString(), " failed: ", Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 2048))));
                return doc;
            }
            catch
            {
                doc.Dispose();
                throw;
            }
        }

        Uri GetWorkflowUri(String name) => new Uri(EndPoint, "api/connect/workflows/" + Uri.EscapeDataString(name));

        #endregion//Run

        #region Workflows

        readonly ConcurrentDictionary<String, Task<ComfyUiWorkflow>> Workflows = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Get a workflow (loaded from disc on first use and cached).
        /// If AnnotateSubgraphInputs is enabled, the UI format workflow is loaded (or fetched from the server) to annotate the subgraph inputs.
        /// </summary>
        /// <param name="name">The name of the workflow (base filename, the ".json" extension is added)</param>
        /// <returns>The workflow</returns>
        public Task<ComfyUiWorkflow> GetWorkflow(String name)
        {
            var w = Workflows;
            if (w.TryGetValue(name, out var t) && !(t.IsFaulted || t.IsCanceled))
                return t;
            if (String.IsNullOrWhiteSpace(name) || (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) || name.Contains(".."))
                throw new ArgumentException("Invalid workflow name " + name.ToQuoted() + "!", nameof(name));
            lock (w)
            {
                if (w.TryGetValue(name, out t) && !(t.IsFaulted || t.IsCanceled))
                    return t;
                t = LoadWorkflow(name);
                w[name] = t;
                return t;
            }
        }

        async Task<ComfyUiWorkflow> LoadWorkflow(String name)
        {
            //  Use the casing of the file name (the server is case sensitive)
            var fileName = Path.Combine(WorkflowFolder, name + ".json");
            if (Directory.Exists(WorkflowFolder))
                fileName = Directory.EnumerateFiles(WorkflowFolder, "*.json").FirstOrDefault(x => String.Equals(Path.GetFileNameWithoutExtension(x), name, StringComparison.OrdinalIgnoreCase)) ?? fileName;
            name = Path.GetFileNameWithoutExtension(fileName);
            byte[] ui = null;
            if (AnnotateSubgraphInputs)
                ui = await GetUiWorkflow(name).ConfigureAwait(false);
            return ComfyUiWorkflow.Load(name, fileName, ui, Msg);
        }

        /// <summary>
        /// Get the UI format version of a workflow, from the UiWorkflowFolder or the ComfyUI server
        /// </summary>
        /// <param name="name">The name of the workflow</param>
        /// <returns>The UI workflow json or null if not found</returns>
        async Task<byte[]> GetUiWorkflow(String name)
        {
            var folder = UiWorkflowFolder;
            if ((folder != null) && Directory.Exists(folder))
            {
                var fn = Directory.EnumerateFiles(folder, "*.json").FirstOrDefault(x => String.Equals(Path.GetFileNameWithoutExtension(x), name, StringComparison.OrdinalIgnoreCase));
                if (fn != null)
                {
                    Msg?.AddMessage(MsgPrefix + "Using UI workflow " + fn.ToFilename() + " to annotate subgraph inputs of " + name.ToQuoted(), MessageLevels.Debug);
                    return await File.ReadAllBytesAsync(fn).ConfigureAwait(false);
                }
            }
            if (!FetchUiWorkflows)
            {
                Msg?.AddMessage(MsgPrefix + "No UI workflow found for " + name.ToQuoted() + ", subgraph inputs are not annotated", MessageLevels.Debug);
                return null;
            }
            //  The user's saved workflows, ex: "/api/userdata/workflows%2Fmy_workflow.json"
            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(EndPoint, "api/userdata/" + Uri.EscapeDataString("workflows/" + name + ".json")));
            if (Auth != null)
                req.Headers.Authorization = Auth;
            using var resp = await Http.SendAsync(req).ConfigureAwait(false);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Msg?.AddMessage(MsgPrefix + "No UI workflow named " + name.ToQuoted() + " found on the server, subgraph inputs are not annotated", MessageLevels.Debug);
                return null;
            }
            if (!resp.IsSuccessStatusCode)
                throw new Exception(String.Concat(MsgPrefix, "Failed to fetch UI workflow ", name.ToQuoted(), ", status ", ((int)resp.StatusCode).ToString(), " (", resp.ReasonPhrase, ")"));
            var data = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            //  Make sure that it's a UI workflow
            try
            {
                using var doc = JsonDocument.Parse(data);
                if (!ComfyUiWorkflow.IsUiFormat(doc.RootElement))
                    return null;
            }
            catch (JsonException)
            {
                return null;
            }
            Msg?.AddMessage(MsgPrefix + "Using UI workflow " + name.ToQuoted() + " from the server to annotate subgraph inputs", MessageLevels.Debug);
            return data;
        }

        /// <summary>
        /// Get the names of all workflows in the workflow folder
        /// </summary>
        /// <returns>The workflow names</returns>
        public IReadOnlyList<String> GetWorkflowNames()
        {
            if (!Directory.Exists(WorkflowFolder))
                return [];
            return Directory.GetFiles(WorkflowFolder, "*.json").Select(Path.GetFileNameWithoutExtension).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// <summary>
        /// Clear the loaded workflows, so that they are re-loaded (and uploaded) on next use.
        /// Use if the workflow files have changed.
        /// </summary>
        public void ReloadWorkflows()
        {
            Workflows.Clear();
            Uploads.Clear();
        }

        readonly ConcurrentDictionary<String, Task> Uploads = new(StringComparer.Ordinal);

        Task EnsureUploaded(ComfyUiWorkflow wf)
        {
            if (!UploadWorkflows)
                return Task.CompletedTask;
            var u = Uploads;
            var key = wf.CacheKey;
            if (u.TryGetValue(key, out var t) && !(t.IsFaulted || t.IsCanceled))
                return t;
            lock (u)
            {
                if (u.TryGetValue(key, out t) && !(t.IsFaulted || t.IsCanceled))
                    return t;
                t = UploadWorkflow(wf);
                u[key] = t;
                return t;
            }
        }

        /// <summary>
        /// Upload (save) a workflow to the ComfyUI-Connect server
        /// </summary>
        /// <param name="name">The name of the workflow (base filename, the ".json" extension is added)</param>
        /// <param name="cancel">Optional cancellation token</param>
        /// <returns></returns>
        public async Task UploadWorkflow(String name, CancellationToken cancel = default)
            => await UploadWorkflow(await GetWorkflow(name).ConfigureAwait(false), cancel).ConfigureAwait(false);

        async Task UploadWorkflow(ComfyUiWorkflow wf, CancellationToken cancel = default)
        {
            var buf = new ArrayBufferWriter<byte>(wf.Json.Length + 256);
            using (var w = new Utf8JsonWriter(buf))
            {
                w.WriteStartObject();
                w.WriteString("name", wf.Name);
                w.WritePropertyName("workflow");
                w.WriteRawValue(wf.Json, true);
                w.WriteEndObject();
            }
            using var req = new HttpRequestMessage(HttpMethod.Put, new Uri(EndPoint, "api/connect/workflows"));
            req.Content = new ReadOnlyMemoryContent(buf.WrittenMemory);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var doc = await Send(req, wf.Name, cancel).ConfigureAwait(false);
            Msg?.AddMessage(MsgPrefix + "Uploaded workflow " + wf.Name.ToQuoted() + " from " + wf.FileName.ToFilename(), MessageLevels.Debug);
        }

        #endregion//Workflows
    }
}
