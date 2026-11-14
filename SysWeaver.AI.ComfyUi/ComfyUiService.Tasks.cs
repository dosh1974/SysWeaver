using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.AI
{
    public sealed partial class ComfyUiService
    {
        #region Tasks

        /// <summary>
        /// Create an image from a text prompt
        /// </summary>
        /// <param name="p">The parameters</param>
        /// <param name="cancel">Optional cancellation token</param>
        /// <returns>The image, or an error (Error is set)</returns>
        public Task<ComfyResult> TextToImage(TextToImageParams p, CancellationToken cancel = default) => RunTask(p, cancel);

        /// <summary>
        /// Create an image from one or more reference images and a text prompt (ex: image edit)
        /// </summary>
        /// <param name="p">The parameters, at least one file is required</param>
        /// <param name="cancel">Optional cancellation token</param>
        /// <returns>The image, or an error (Error is set)</returns>
        public Task<ComfyResult> ImagesToImage(ImagesToImageParams p, CancellationToken cancel = default) => RunTask(p, cancel);

        /// <summary>
        /// Create a video from a text prompt
        /// </summary>
        /// <param name="p">The parameters</param>
        /// <param name="cancel">Optional cancellation token</param>
        /// <returns>The video, or an error (Error is set)</returns>
        public Task<ComfyResult> TextToVideo(TextToVideoParams p, CancellationToken cancel = default) => RunTask(p, cancel);

        /// <summary>
        /// Create a video from one or more reference images (ex: the first frame) and a text prompt
        /// </summary>
        /// <param name="p">The parameters, at least one file is required</param>
        /// <param name="cancel">Optional cancellation token</param>
        /// <returns>The video, or an error (Error is set)</returns>
        public Task<ComfyResult> ImagesToVideo(ImagesToVideoParams p, CancellationToken cancel = default) => RunTask(p, cancel);

        /// <summary>
        /// Create music from a text prompt (the style) and optional lyrics
        /// </summary>
        /// <param name="p">The parameters</param>
        /// <param name="cancel">Optional cancellation token</param>
        /// <returns>The audio, or an error (Error is set)</returns>
        public Task<ComfyResult> TextToMusic(TextToMusicParams p, CancellationToken cancel = default) => RunTask(p, cancel);

        /// <summary>
        /// Create speech from a text (the text to speak)
        /// </summary>
        /// <param name="p">The parameters</param>
        /// <param name="cancel">Optional cancellation token</param>
        /// <returns>The audio, or an error (Error is set)</returns>
        public Task<ComfyResult> TextToSpeech(TextToSpeechParams p, CancellationToken cancel = default) => RunTask(p, cancel);

        /// <summary>
        /// Create a sound (effect) from a text prompt
        /// </summary>
        /// <param name="p">The parameters</param>
        /// <param name="cancel">Optional cancellation token</param>
        /// <returns>The audio, or an error (Error is set)</returns>
        public Task<ComfyResult> TextToSound(TextToSoundParams p, CancellationToken cancel = default) => RunTask(p, cancel);

        /// <summary>
        /// Run a task using the workflow named in the parameters, or the default workflow for the task.
        /// Errors are returned as a result with Error set, cancellation throws an OperationCanceledException.
        /// </summary>
        /// <param name="p">The parameters</param>
        /// <param name="cancel">Optional cancellation token</param>
        /// <returns>The created file, or an error (Error is set)</returns>
        public async Task<ComfyResult> RunTask(ComfyParams p, CancellationToken cancel = default)
        {
            ArgumentNullException.ThrowIfNull(p);
            var type = p.TaskType;
            String wfName = p.Workflow;
            var seed = p.Seed < 0 ? Random.Shared.Next(1, int.MaxValue) : (p.Seed & 0x7fffffff);
            try
            {
                var tasks = Tasks;
                ComfyTaskWorkflow tw;
                if (String.IsNullOrEmpty(wfName))
                {
                    wfName = GetDefaultWorkflow(type);
                    if (wfName == null)
                        return new ComfyResult($"No {type} workflow available!", null, seed);
                    tw = tasks.ByName[wfName];
                }
                else
                {
                    if (!tasks.ByName.TryGetValue(wfName, out tw))
                        return new ComfyResult($"No workflow named {wfName.ToQuoted()} found! Available {type} workflows: {Available(tasks, type)}", wfName, seed);
                    if (tw.Type != type)
                        return new ComfyResult($"The workflow {tw.Name.ToQuoted()} is a {tw.Type} workflow, not a {type} workflow! Available {type} workflows: {Available(tasks, type)}", tw.Name, seed);
                }
                wfName = tw.Name;
                var values = new List<(String Tag, String Input, Object Value)>();
                void Set(String param, Func<ComfyTaskWorkflow.Target, Object> value)
                {
                    if (tw.Targets.TryGetValue(param, out var l))
                        foreach (var t in l)
                            values.Add((t.Tag, t.Input, value(t)));
                }
                if (p.Prompt != null)
                    Set(ComfyTaskWorkflow.PPrompt, t => t.Affix(p.Prompt));
                if (p.NegativePrompt != null)
                    Set(ComfyTaskWorkflow.PNegative, _ => p.NegativePrompt);
                Set(ComfyTaskWorkflow.PSeed, _ => seed);
                if ((p.Steps > 0) && tw.Targets.TryGetValue(ComfyTaskWorkflow.PSteps, out var stepTargets) && (stepTargets.Count > 0))
                {
                    //  Several samplers keep their ratio to the one with the most steps (ex: a refine pass with fewer steps), a single sampler gets the steps
                    var steps = p.Steps;
                    var max = stepTargets.Max(x => x.Orig);
                    long Scale(double orig, double of) => (long)Math.Max(1, Math.Round(orig * steps / of));
                    Set(ComfyTaskWorkflow.PSteps, t => Scale(t.Orig, max));
                    //  The start / end step of a split sampler is scaled with the steps of its node ("until the end" values like 10000 are kept)
                    Set(ComfyTaskWorkflow.PStepsSplit, t =>
                    {
                        if (t.Orig >= t.OrigSteps)
                            return (long)t.Orig;
                        if (t.Orig <= 0)
                            return 0L;
                        var nodeSteps = Scale(t.OrigSteps, max);
                        return (long)Math.Max(1, Math.Min(nodeSteps - 1, Math.Round(t.Orig * nodeSteps / t.OrigSteps)));
                    });
                }
                Set(ComfyTaskWorkflow.PEnhance, _ => p.EnhancePrompt);
                var wf = tw.Workflow;
                if (p is IVisualComfyParams v)
                {
                    Set(ComfyTaskWorkflow.PTransparent, _ => v.Transparent);
                    if ((v.Width != 0) || (v.Height != 0))
                    {
                        if ((v.Width <= 0) || (v.Height <= 0))
                            return new ComfyResult($"Both Width and Height must be greater than zero to change the size (or both zero to use the workflow default), got {v.Width}x{v.Height}!", wfName, seed);
                        if (tw.Targets.ContainsKey(ComfyTaskWorkflow.PWidth))
                        {
                            wf = tw.SizedWorkflow ?? wf;
                            Set(ComfyTaskWorkflow.PWidth, t => RoundTo(v.Width, t.Multiple));
                            Set(ComfyTaskWorkflow.PHeight, t => RoundTo(v.Height, t.Multiple));
                        }
                    }
                }
                if ((p is IDurationComfyParams d) && (d.Duration > 0))
                    Set(ComfyTaskWorkflow.PDuration, t => GetDuration(t, d.Duration));
                if (p is IReferenceComfyParams r)
                {
                    var files = r.Files?.Where(x => x != null).ToArray() ?? [];
                    if ((files.Length == 0) && ((type == ComfyTaskTypes.ImagesToImage) || (type == ComfyTaskTypes.ImagesToVideo)))
                        return new ComfyResult($"At least one file is required for {type}!", wfName, seed);
                    if (tw.Targets.TryGetValue(ComfyTaskWorkflow.PFiles, out var fl))
                    {
                        var c = Math.Min(fl.Count, files.Length);
                        for (int i = 0; i < c; ++i)
                        {
                            var f = files[i];
                            ComfyUiFile cf;
                            if (f.Data != null)
                                cf = ComfyUiFile.FromData(f.Data);
                            else if (!String.IsNullOrEmpty(f.Name))
                                cf = ComfyUiFile.FromUrl(f.Name);
                            else
                                return new ComfyResult($"File {i} has no data or url!", wfName, seed);
                            values.Add((fl[i].Tag, fl[i].Input, cf));
                        }
                    }
                }
                if ((p is TextToMusicParams m) && (m.Lyrics != null))
                    Set(ComfyTaskWorkflow.PLyrics, _ => m.Lyrics);
                if ((p is TextToSpeechParams s) && (s.Voice != null))
                    Set(ComfyTaskWorkflow.PVoice, _ => s.Voice);
                Msg?.AddMessage(MessageLevels.Debug, $"{MsgPrefix}{type} using workflow {wfName.ToQuoted()} ({wf.Name.ToQuoted()} on the server): {p}");
                var data = await ExecuteCore(cancel, wf, values, WriteTaskValues, x => ComfyUiJson.ReadBytes(x, tw.OutputTag), true).ConfigureAwait(false);
                if ((data == null) || (data.Length == 0))
                    return new ComfyResult($"The workflow {wfName.ToQuoted()} returned no output!", wfName, seed);
                var ext = ComfyUiJson.GetExt(data);
                var mime = MimeTypeMap.GetMimeType(ext, false).Item1;
                return new ComfyResult(String.Concat(wfName, "_", seed.ToString(), ext), mime, data, wfName, seed);
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Msg?.AddMessage($"{MsgPrefix}{type} using workflow {wfName.ToQuoted()} failed", ex, MessageLevels.Warning);
                return new ComfyResult(ex.Message, wfName, seed);
            }
        }

        static void WriteTaskValues(List<(String Tag, String Input, Object Value)> values, Utf8JsonWriter w)
        {
            foreach (var (tag, input, value) in values)
            {
                w.WritePropertyName(tag);
                w.WriteStartObject();
                switch (value)
                {
                    case String s:
                        w.WriteString(input, s);
                        break;
                    case bool b:
                        w.WriteBoolean(input, b);
                        break;
                    case long l:
                        w.WriteNumber(input, l);
                        break;
                    case double d:
                        w.WriteNumber(input, d);
                        break;
                    case ComfyUiFile f:
                        ComfyUiJson.WriteInput(w, input, f);
                        break;
                    default:
                        throw new Exception("Unsupported value type " + value?.GetType().Name);
                }
                w.WriteEndObject();
            }
        }

        static Object RoundTo(int v, int multiple)
        {
            if (multiple <= 1)
                return (long)v;
            return (long)Math.Max(multiple, (int)Math.Round(v / (double)multiple) * multiple);
        }

        static Object GetDuration(ComfyTaskWorkflow.Target t, double seconds)
        {
            //  Video models generally require 8n+1 frames (that is also 4n+1)
            if (t.Fps > 0)
                return (long)(Math.Max(1, (int)Math.Round(seconds * t.Fps / 8)) * 8 + 1);
            if (t.IsInt)
                return (long)Math.Max(1, Math.Round(seconds));
            return seconds;
        }

        #endregion//Tasks

        #region Task workflows

        sealed class TaskSet
        {
            public static readonly TaskSet Empty = new(new Dictionary<String, ComfyTaskWorkflow>(StringComparer.OrdinalIgnoreCase));

            public TaskSet(Dictionary<String, ComfyTaskWorkflow> byName)
            {
                ByName = byName;
                var c = Enum.GetValues<ComfyTaskTypes>().Length;
                var byType = new IReadOnlyList<String>[c];
                for (int i = 0; i < c; ++i)
                    byType[i] = byName.Values.Where(x => (int)x.Type == i).Select(x => x.Name).Order(StringComparer.OrdinalIgnoreCase).ThenBy(x => x, StringComparer.Ordinal).ToArray();
                ByType = byType;
                All = byName.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Name, StringComparer.Ordinal).ToArray();
            }

            public readonly IReadOnlyDictionary<String, ComfyTaskWorkflow> ByName;
            public readonly IReadOnlyList<String>[] ByType;
            public readonly IReadOnlyList<ComfyTaskWorkflow> All;
        }

        volatile TaskSet Tasks = TaskSet.Empty;
        readonly Object TaskLock = new();
        readonly String[] TaskDefaults = new String[Enum.GetValues<ComfyTaskTypes>().Length];

        static String Available(TaskSet tasks, ComfyTaskTypes type)
        {
            var l = tasks.ByType[(int)type];
            return l.Count == 0 ? "none" : String.Join(", ", l.Select(x => x.ToQuoted()));
        }

        /// <summary>
        /// Scan, load and analyze the workflows in a folder (API format json files) for use with the task methods (TextToImage etc).
        /// Each workflow is analyzed to determine what it is (the task type) and how the parameters maps to it, see <see cref="ComfyTaskWorkflow"/>.
        /// The workflow name (to use as <see cref="ComfyParams.Workflow"/>) is the file name without path and extension, adding a workflow with the same name replaces it.
        /// Files that can't be used are reported as warnings to the message host.
        /// </summary>
        /// <param name="pathAndMask">A folder (all "*.json" files are added), a file or a folder with a file mask, ex: "D:\Workflows\video_*.json".
        /// Variables can be used, ex: "$(ExecutableDir)/ComfyUi", multiple can be separated by ';'.</param>
        /// <returns>The workflows that was added</returns>
        public IReadOnlyList<ComfyTaskWorkflow> AddWorkflows(String pathAndMask)
        {
            ArgumentNullException.ThrowIfNull(pathAndMask);
            var res = new List<ComfyTaskWorkflow>();
            foreach (var part in pathAndMask.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var p = Path.GetFullPath(EnvInfo.MakeAbsoulte(PathTemplate.Resolve(part)));
                String dir, mask;
                if (Directory.Exists(p))
                {
                    dir = p;
                    mask = "*.json";
                }
                else
                {
                    if ((p.IndexOfAny(['*', '?']) < 0) && !File.Exists(p))
                    {
                        Msg?.AddMessage(MessageLevels.Warning, $"{MsgPrefix}The workflow folder or file {p.ToFilename()} doesn't exist!");
                        continue;
                    }
                    dir = Path.GetDirectoryName(p);
                    mask = Path.GetFileName(p);
                    if (String.IsNullOrEmpty(mask))
                        mask = "*.json";
                }
                if ((dir == null) || !Directory.Exists(dir))
                {
                    Msg?.AddMessage(MessageLevels.Warning, $"{MsgPrefix}The workflow folder {(dir ?? p).ToFilename()} doesn't exist!");
                    continue;
                }
                var files = Directory.GetFiles(dir, mask).Order(StringComparer.OrdinalIgnoreCase).ThenBy(x => x, StringComparer.Ordinal).ToList();
                if (files.Count == 0)
                    Msg?.AddMessage(MessageLevels.Warning, $"{MsgPrefix}No workflows found matching {Path.Combine(dir, mask).ToFilename()}");
                foreach (var fn in files)
                {
                    try
                    {
                        res.Add(ComfyTaskWorkflow.Load(fn));
                    }
                    catch (Exception ex)
                    {
                        Msg?.AddMessage(MessageLevels.Warning, $"{MsgPrefix}Workflow {fn.ToFilename()} can't be used for a task and is ignored: {ex.Message}");
                    }
                }
            }
            Add(res);
            return res;
        }

        /// <summary>
        /// Load and analyze a single workflow file (API format json) for use with the task methods (TextToImage etc).
        /// Adding a workflow with the same name replaces it.
        /// </summary>
        /// <param name="fileName">The workflow file, variables can be used, ex: "$(ExecutableDir)/ComfyUi/my_t2i.json"</param>
        /// <param name="type">Force the task type (if the analysis gets it wrong), null to determine it from the workflow</param>
        /// <returns>The workflow that was added</returns>
        /// <exception cref="Exception">The workflow can't be used for a task</exception>
        public ComfyTaskWorkflow AddWorkflow(String fileName, ComfyTaskTypes? type = null)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            var wf = ComfyTaskWorkflow.Load(Path.GetFullPath(EnvInfo.MakeAbsoulte(PathTemplate.Resolve(fileName))), type);
            Add([wf]);
            return wf;
        }

        /// <summary>
        /// Add an analyzed workflow (ex: created using <see cref="ComfyTaskWorkflow.FromJson(String, byte[], ComfyTaskTypes?)"/>).
        /// Adding a workflow with the same name replaces it.
        /// </summary>
        /// <param name="wf">The workflow</param>
        public void AddWorkflow(ComfyTaskWorkflow wf)
        {
            ArgumentNullException.ThrowIfNull(wf);
            Add([wf]);
        }

        void Add(IReadOnlyList<ComfyTaskWorkflow> wfs)
        {
            if (wfs.Count == 0)
                return;
            lock (TaskLock)
            {
                var d = new Dictionary<String, ComfyTaskWorkflow>(Tasks.ByName, StringComparer.OrdinalIgnoreCase);
                foreach (var wf in wfs)
                {
                    d.Remove(wf.Name);
                    d.Add(wf.Name, wf);
                    Msg?.AddMessage(MessageLevels.Info, $"{MsgPrefix}Added workflow {wf}");
                    Msg?.AddMessage(MessageLevels.Debug, $"{MsgPrefix}{wf.Describe()}");
                }
                Tasks = new TaskSet(d);
            }
        }

        /// <summary>
        /// Get an analyzed workflow
        /// </summary>
        /// <param name="name">The workflow name (case insensitive)</param>
        /// <returns>The workflow or null if not found</returns>
        public ComfyTaskWorkflow GetTaskWorkflow(String name)
            => (name != null) && Tasks.ByName.TryGetValue(name, out var wf) ? wf : null;

        /// <summary>
        /// Get a system prompt for an LLM that rewrites a user request (and any media details, such as the size or duration) into a prompt that is optimized for a workflow.
        /// The instructions include the model (family) and how to best prompt it, the task, the prefix / suffix to apply, how the negative prompt is handled,
        /// an example prompt (the default prompt of the workflow) and rules to only output the prompt.
        /// Intended flow: prompt = LLM(system: GetPromptEnhancement(workflow), user: request + media details), then run the task with the prompt and the same workflow.
        /// </summary>
        /// <param name="workflow">The workflow name (case insensitive), see <see cref="ComfyParams.Workflow"/></param>
        /// <returns>The system prompt</returns>
        /// <exception cref="ArgumentException">The workflow isn't found</exception>
        public String GetPromptEnhancement(String workflow)
        {
            ArgumentNullException.ThrowIfNull(workflow);
            if (!Tasks.ByName.TryGetValue(workflow, out var wf))
                throw new ArgumentException($"No workflow named {workflow.ToQuoted()} found!", nameof(workflow));
            return wf.GetPromptEnhancement();
        }

        /// <summary>
        /// All analyzed workflows (ordered by name)
        /// </summary>
        public IReadOnlyList<ComfyTaskWorkflow> TaskWorkflows => Tasks.All;

        /// <summary>
        /// Get the names of the workflows that performs a task
        /// </summary>
        /// <param name="type">The task type</param>
        /// <returns>The workflow names in alphabetical order (case insensitive)</returns>
        public IReadOnlyList<String> GetWorkflows(ComfyTaskTypes type) => Tasks.ByType[(int)type];

        /// <summary>
        /// Get the default workflow for a task: the one set explicitly (if it exists), else the first in alphabetical order (case insensitive)
        /// </summary>
        /// <param name="type">The task type</param>
        /// <returns>The workflow name or null if there are no workflows for the task</returns>
        public String GetDefaultWorkflow(ComfyTaskTypes type)
        {
            var tasks = Tasks;
            var d = Volatile.Read(ref TaskDefaults[(int)type]);
            if ((d != null) && tasks.ByName.TryGetValue(d, out var wf) && (wf.Type == type))
                return wf.Name;
            var l = tasks.ByType[(int)type];
            return l.Count > 0 ? l[0] : null;
        }

        /// <summary>
        /// Set the default workflow for a task
        /// </summary>
        /// <param name="type">The task type</param>
        /// <param name="name">The workflow name (must be added and perform the task), null to use the first workflow in alphabetical order</param>
        /// <exception cref="ArgumentException">The workflow doesn't exist or doesn't perform the task</exception>
        public void SetDefaultWorkflow(ComfyTaskTypes type, String name)
        {
            if (name != null)
            {
                if (!Tasks.ByName.TryGetValue(name, out var wf))
                    throw new ArgumentException($"No workflow named {name.ToQuoted()} found! Available {type} workflows: {Available(Tasks, type)}", nameof(name));
                if (wf.Type != type)
                    throw new ArgumentException($"The workflow {wf.Name.ToQuoted()} is a {wf.Type} workflow, not a {type} workflow!", nameof(name));
                name = wf.Name;
            }
            Volatile.Write(ref TaskDefaults[(int)type], name);
        }

        /// <summary>
        /// The names of the text to image workflows (alphabetical order)
        /// </summary>
        public IReadOnlyList<String> TextToImageWorkflows => GetWorkflows(ComfyTaskTypes.TextToImage);

        /// <summary>
        /// The names of the images to image workflows (alphabetical order)
        /// </summary>
        public IReadOnlyList<String> ImagesToImageWorkflows => GetWorkflows(ComfyTaskTypes.ImagesToImage);

        /// <summary>
        /// The names of the text to video workflows (alphabetical order)
        /// </summary>
        public IReadOnlyList<String> TextToVideoWorkflows => GetWorkflows(ComfyTaskTypes.TextToVideo);

        /// <summary>
        /// The names of the images to video workflows (alphabetical order)
        /// </summary>
        public IReadOnlyList<String> ImagesToVideoWorkflows => GetWorkflows(ComfyTaskTypes.ImagesToVideo);

        /// <summary>
        /// The names of the text to music workflows (alphabetical order)
        /// </summary>
        public IReadOnlyList<String> TextToMusicWorkflows => GetWorkflows(ComfyTaskTypes.TextToMusic);

        /// <summary>
        /// The names of the text to speech workflows (alphabetical order)
        /// </summary>
        public IReadOnlyList<String> TextToSpeechWorkflows => GetWorkflows(ComfyTaskTypes.TextToSpeech);

        /// <summary>
        /// The names of the text to sound workflows (alphabetical order)
        /// </summary>
        public IReadOnlyList<String> TextToSoundWorkflows => GetWorkflows(ComfyTaskTypes.TextToSound);

        /// <summary>
        /// The default text to image workflow (used when <see cref="ComfyParams.Workflow"/> is null).
        /// Defaults to the first in alphabetical order, set to null to restore that.
        /// </summary>
        public String DefaultTextToImage { get => GetDefaultWorkflow(ComfyTaskTypes.TextToImage); set => SetDefaultWorkflow(ComfyTaskTypes.TextToImage, value); }

        /// <summary>
        /// The default images to image workflow (used when <see cref="ComfyParams.Workflow"/> is null).
        /// Defaults to the first in alphabetical order, set to null to restore that.
        /// </summary>
        public String DefaultImagesToImage { get => GetDefaultWorkflow(ComfyTaskTypes.ImagesToImage); set => SetDefaultWorkflow(ComfyTaskTypes.ImagesToImage, value); }

        /// <summary>
        /// The default text to video workflow (used when <see cref="ComfyParams.Workflow"/> is null).
        /// Defaults to the first in alphabetical order, set to null to restore that.
        /// </summary>
        public String DefaultTextToVideo { get => GetDefaultWorkflow(ComfyTaskTypes.TextToVideo); set => SetDefaultWorkflow(ComfyTaskTypes.TextToVideo, value); }

        /// <summary>
        /// The default images to video workflow (used when <see cref="ComfyParams.Workflow"/> is null).
        /// Defaults to the first in alphabetical order, set to null to restore that.
        /// </summary>
        public String DefaultImagesToVideo { get => GetDefaultWorkflow(ComfyTaskTypes.ImagesToVideo); set => SetDefaultWorkflow(ComfyTaskTypes.ImagesToVideo, value); }

        /// <summary>
        /// The default text to music workflow (used when <see cref="ComfyParams.Workflow"/> is null).
        /// Defaults to the first in alphabetical order, set to null to restore that.
        /// </summary>
        public String DefaultTextToMusic { get => GetDefaultWorkflow(ComfyTaskTypes.TextToMusic); set => SetDefaultWorkflow(ComfyTaskTypes.TextToMusic, value); }

        /// <summary>
        /// The default text to speech workflow (used when <see cref="ComfyParams.Workflow"/> is null).
        /// Defaults to the first in alphabetical order, set to null to restore that.
        /// </summary>
        public String DefaultTextToSpeech { get => GetDefaultWorkflow(ComfyTaskTypes.TextToSpeech); set => SetDefaultWorkflow(ComfyTaskTypes.TextToSpeech, value); }

        /// <summary>
        /// The default text to sound workflow (used when <see cref="ComfyParams.Workflow"/> is null).
        /// Defaults to the first in alphabetical order, set to null to restore that.
        /// </summary>
        public String DefaultTextToSound { get => GetDefaultWorkflow(ComfyTaskTypes.TextToSound); set => SetDefaultWorkflow(ComfyTaskTypes.TextToSound, value); }

        #endregion//Task workflows
    }
}
