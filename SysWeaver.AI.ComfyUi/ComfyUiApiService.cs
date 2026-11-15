using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SysWeaver.Data;
using SysWeaver.MicroService;
using SysWeaver.Net;

namespace SysWeaver.AI
{
    /// <summary>
    /// Exposes the media creation methods of the <see cref="ComfyUiService"/> as web API's (text to image, images to image, text to video, images to video, text to music, text to speech and text to sound).
    /// Each API returns the created file as the response body, with the mime type of the file (ex: image/png, video/mp4 or audio/flac).
    /// A failure (no workflow, a bad parameter, a workflow error etc) is returned as an error response.
    /// The auth of all API's is <see cref="ComfyUiApiParams.Auth"/>.
    /// Note: a request doesn't return until the workflow has completed, which can take minutes (ex: video), so clients must use a long time out.
    /// </summary>
    [WebApiUrl("comfyui")]
    public sealed class ComfyUiApiService : IRunTimeWebApiAuth
    {
        /// <inheritdoc/>
        public override string ToString() => "Provides web API's for the media creation of the ComfyUI service";

        /// <summary>
        /// Create the API service
        /// </summary>
        /// <param name="manager">The service manager, a <see cref="ComfyUiService"/> must be registered</param>
        /// <param name="p">The parameters</param>
        public ComfyUiApiService(ServiceManager manager, ComfyUiApiParams p = null)
        {
            ArgumentNullException.ThrowIfNull(manager);
            p = p ?? new ComfyUiApiParams();
            Comfy = manager.Get<ComfyUiService>();
            var auth = p.Auth;
            var d = new Dictionary<String, String>(StringComparer.Ordinal);
            foreach (var name in ApiNames)
                d[name] = auth;
            MethodAuths = d.Freeze();
        }

        /// <summary>
        /// The names of all API methods (all uses the configured auth)
        /// </summary>
        static readonly String[] ApiNames =
        [
            nameof(TextToImage),
            nameof(ImagesToImage),
            nameof(TextToVideo),
            nameof(ImagesToVideo),
            nameof(TextToMusic),
            nameof(TextToSpeech),
            nameof(TextToSound),
            nameof(WorkflowTable),
            nameof(GetWorkflow),
            nameof(GetDefaultWorkflow),
            nameof(GetWorkflowNames),
            nameof(GetDefaultWorkflows),
        ];

        /// <summary>
        /// A loaded (analyzed) workflow, a row of <see cref="WorkflowTable(TableDataRequest)"/>
        /// </summary>
        [TableDataDefaultOrder(nameof(Name))]
        [TableDataPrimaryKey(nameof(Name))]
        public sealed class WorkflowRow
        {
            /// <summary>
            /// The workflow name (the file name without extension), use as the Workflow parameter
            /// </summary>
            public String Name;

            /// <summary>
            /// The task that the workflow performs, ex: "TextToImage"
            /// </summary>
            public String Type;

            /// <summary>
            /// True if this is the default workflow for the task
            /// </summary>
            public bool Default;

            /// <summary>
            /// The model (family) that the workflow uses, empty if not recognized
            /// </summary>
            [TableDataText]
            public String Model;

            /// <summary>
            /// The parameters that the workflow supports (respects)
            /// </summary>
            [TableDataText]
            public String Parameters;

            /// <summary>
            /// The number of reference files (load image / audio nodes)
            /// </summary>
            public int Files;

            /// <summary>
            /// The model files used by the workflow
            /// </summary>
            [TableDataText]
            public String ModelFiles;

            /// <summary>
            /// The LoRA files used by the workflow
            /// </summary>
            [TableDataText]
            public String Loras;

            /// <summary>
            /// Text that is added before the prompt (ex: a LoRA trigger)
            /// </summary>
            [TableDataText]
            public String PromptPrefix;

            /// <summary>
            /// Text that is added after the prompt
            /// </summary>
            [TableDataText]
            public String PromptSuffix;

            /// <summary>
            /// Text that is added before the prompt when a transparent result is requested (the model makes transparency from the prompt, ex: Qwen-Image 2.1)
            /// </summary>
            [TableDataHide]
            [TableDataText]
            public String TransparentPrefix;

            /// <summary>
            /// Text that is added after the prompt when a transparent result is requested
            /// </summary>
            [TableDataHide]
            [TableDataText]
            public String TransparentSuffix;

            /// <summary>
            /// The default (positive) prompt of the workflow
            /// </summary>
            [TableDataHide]
            [TableDataText]
            public String DefaultPrompt;

            /// <summary>
            /// The default negative prompt of the workflow
            /// </summary>
            [TableDataHide]
            [TableDataText]
            public String DefaultNegativePrompt;

            /// <summary>
            /// The id of the output node
            /// </summary>
            [TableDataHide]
            [TableDataText]
            public String OutputNode;

            /// <summary>
            /// True if a size change uses a separate (sized) variant of the workflow, with links replaced by values and switches flipped
            /// </summary>
            [TableDataHide]
            public bool SizedVariant;

            /// <summary>
            /// The names of the annotated workflow(s) uploaded to the ComfyUI-Connect server
            /// </summary>
            [TableDataHide]
            [TableDataText]
            public String ServerNames;

            /// <summary>
            /// How the parameters are mapped to the node inputs (one per line)
            /// </summary>
            [TableDataHide]
            [TableDataText]
            public String Mappings;

            /// <summary>
            /// The node classes used by the workflow
            /// </summary>
            [TableDataHide]
            [TableDataText]
            public String NodeClasses;

            /// <summary>
            /// The system prompt for an LLM that enhances a user request into a prompt for this workflow
            /// </summary>
            [TableDataHide]
            [TableDataWordWrap]
            [TableDataText]
            public String PromptEnhancement;

            /// <summary>
            /// The full file name of the workflow
            /// </summary>
            [TableDataHide]
            [TableDataText]
            public String FileName;

            /// <summary>
            /// The size of the workflow file in bytes
            /// </summary>
            [TableDataHide]
            public long FileSize;

            /// <summary>
            /// When the workflow file was last changed (UTC)
            /// </summary>
            [TableDataHide]
            public DateTime FileChanged;
        }

        /// <summary>
        /// All loaded (analyzed) workflows of the task methods, with the task type, model, supported parameters, mappings and more
        /// </summary>
        /// <param name="r">Table request parameters (paging, sorting, filtering)</param>
        /// <returns>A table with one row per workflow</returns>
        [WebApi]
        [WebApiClientCache(10)]
        [WebMenuTable(null, "ComfyUI/{0}", "ComfyUI workflows", "The loaded ComfyUI workflows of the media creation API's")]
        public TypedTableData<WorkflowRow> WorkflowTable(TableDataRequest r)
        {
            var comfy = Comfy;
            var rows = comfy.TaskWorkflows.Select(w =>
            {
                var i = ComfyWorkflowInfo.From(comfy, w);
                return new WorkflowRow
                {
                    Name = i.Name,
                    Type = i.Type.ToString(),
                    Default = i.IsDefault,
                    Model = i.Model ?? "",
                    Parameters = String.Join(", ", i.Parameters),
                    Files = i.FileCount,
                    ModelFiles = String.Join(", ", i.ModelFiles),
                    Loras = String.Join(", ", i.Loras),
                    PromptPrefix = i.PromptPrefix,
                    PromptSuffix = i.PromptSuffix,
                    TransparentPrefix = i.TransparentPrefix,
                    TransparentSuffix = i.TransparentSuffix,
                    DefaultPrompt = i.DefaultPrompt,
                    DefaultNegativePrompt = i.DefaultNegativePrompt,
                    OutputNode = i.OutputNode,
                    SizedVariant = i.SizedVariant,
                    ServerNames = String.Join(", ", i.ServerNames),
                    Mappings = String.Join("\n", i.Mappings),
                    NodeClasses = String.Join(", ", i.NodeClasses),
                    PromptEnhancement = i.PromptEnhancement,
                    FileName = i.FileName,
                    FileSize = i.FileSize,
                    FileChanged = i.FileChanged,
                };
            }).ToList();
            return TableDataTools.GetTyped(r, 10000, rows, "ComfyUI workflows");
        }

        /// <summary>
        /// Get the details of a loaded workflow: the task type, model, supported parameters, model files, LoRA's, prompt prefix / suffix,
        /// default prompts, how the parameters are mapped, the prompt enhancement (a system prompt for an LLM) and more.
        /// </summary>
        /// <param name="name">The workflow name (case insensitive), as used in the Workflow parameter</param>
        /// <returns>The workflow details</returns>
        [WebApi]
        [WebApiClientCache(10)]
        public ComfyWorkflowInfo GetWorkflow(String name)
        {
            var wf = String.IsNullOrWhiteSpace(name) ? null : Comfy.GetTaskWorkflow(name.Trim());
            if (wf == null)
                throw new Exception("No workflow named " + (name ?? "").ToQuoted() + " found!");
            return ComfyWorkflowInfo.From(Comfy, wf);
        }

        /// <summary>
        /// Get the details of the default workflow of a task (the one used when no workflow is specified), see <see cref="GetWorkflow(string)"/>.
        /// </summary>
        /// <param name="type">The task type, ex: "TextToImage"</param>
        /// <returns>The workflow details, null if there is no workflow for the task</returns>
        [WebApi]
        [WebApiClientCache(10)]
        public ComfyWorkflowInfo GetDefaultWorkflow(ComfyTaskTypes type)
        {
            var name = Comfy.GetDefaultWorkflow(type);
            var wf = name == null ? null : Comfy.GetTaskWorkflow(name);
            return wf == null ? null : ComfyWorkflowInfo.From(Comfy, wf);
        }

        /// <summary>
        /// Get the names of the workflows that performs a task (in alphabetical order)
        /// </summary>
        /// <param name="type">The task type, ex: "TextToImage"</param>
        /// <returns>The workflow names (empty if there is none)</returns>
        [WebApi]
        [WebApiClientCache(10)]
        public String[] GetWorkflowNames(ComfyTaskTypes type)
            => Comfy.GetWorkflows(type).ToArray();

        /// <summary>
        /// Get the default workflow and the number of workflows of every task type
        /// </summary>
        /// <returns>One item per task type</returns>
        [WebApi]
        [WebApiClientCache(10)]
        public ComfyTaskDefault[] GetDefaultWorkflows()
            => Enum.GetValues<ComfyTaskTypes>().Select(t => new ComfyTaskDefault
            {
                Type = t,
                Workflow = Comfy.GetDefaultWorkflow(t),
                Count = Comfy.GetWorkflows(t).Count,
            }).ToArray();

        readonly ComfyUiService Comfy;

        /// <inheritdoc/>
        public IReadOnlyDictionary<String, String> MethodAuths { get; init; }

        /// <summary>
        /// Return the file of a result, or throw the error.
        /// A png image gets information chunks (the prompt as the description, the workflow and seed as the source, the user as the author), see <see cref="AiImageTools.AddPngInfo"/>.
        /// </summary>
        static async Task<MemoryFile> Get(Task<ComfyResult> t, ComfyParams p, HttpServerRequest request)
        {
            var r = await t.ConfigureAwait(false);
            if (r.Error != null)
                throw new Exception(r.Error);
            if (String.Equals(r.Mime, MimeTypeMap.Png, StringComparison.OrdinalIgnoreCase))
            {
                var source = String.Concat("ComfyUI ", r.Workflow, " seed ", r.Seed.ToString());
                if (p.Steps > 0)
                    source = String.Concat(source, " steps ", p.Steps.ToString());
                r.Data = AiImageTools.AddPngInfo(r.Data, source, p.Prompt, null, request).ToArray();
            }
            return r;
        }

        /// <summary>
        /// Create an image from a text prompt.
        /// The workflow is the one named in the parameters, or the default text to image workflow.
        /// Width / Height (both or none), Transparent, EnhancePrompt and NegativePrompt are only respected if the workflow supports them.
        /// </summary>
        /// <param name="p">The parameters, ex: { "Prompt": "A red fox in the snow", "Width": 1024, "Height": 768 }</param>
        /// <param name="request">The request (the user is stored as the author of a png image)</param>
        /// <returns>The image (ex: image/png)</returns>
        [WebApi]
        [WebApiRaw]
        public Task<MemoryFile> TextToImage(TextToImageParams p, HttpServerRequest request)
            => Get(Comfy.TextToImage(p), p, request);

        /// <summary>
        /// Create an image from one or more reference images and a text prompt (ex: an image edit).
        /// The workflow is the one named in the parameters, or the default images to image workflow.
        /// At least one file is required, in the order of the load image nodes of the workflow.
        /// </summary>
        /// <param name="p">The parameters, ex: { "Prompt": "Make it a watercolor painting", "Files": [ { "Name": "photo.png", "Mime": "image/png", "Data": "[base64]" } ] }</param>
        /// <param name="request">The request (the user is stored as the author of a png image)</param>
        /// <returns>The image (ex: image/png)</returns>
        [WebApi]
        [WebApiRaw]
        public Task<MemoryFile> ImagesToImage(ImagesToImageParams p, HttpServerRequest request)
            => Get(Comfy.ImagesToImage(p), p, request);

        /// <summary>
        /// Create a video from a text prompt.
        /// The workflow is the one named in the parameters, or the default text to video workflow.
        /// Duration is in seconds (0 = the workflow default), Width / Height (both or none) are rounded to what the model accepts.
        /// </summary>
        /// <param name="p">The parameters, ex: { "Prompt": "A paper boat on a calm lake at sunset", "Width": 1280, "Height": 704, "Duration": 5 }</param>
        /// <param name="request">The request (the user is stored as the author of a png image)</param>
        /// <returns>The video (ex: video/mp4)</returns>
        [WebApi]
        [WebApiRaw]
        public Task<MemoryFile> TextToVideo(TextToVideoParams p, HttpServerRequest request)
            => Get(Comfy.TextToVideo(p), p, request);

        /// <summary>
        /// Create a video from one or more reference images (ex: the first frame) and a text prompt.
        /// The workflow is the one named in the parameters, or the default images to video workflow.
        /// At least one file is required, in the order of the load image nodes of the workflow.
        /// </summary>
        /// <param name="p">The parameters, ex: { "Prompt": "The fox slowly turns its head", "Files": [ { "Name": "fox.png", "Mime": "image/png", "Data": "[base64]" } ], "Duration": 5 }</param>
        /// <param name="request">The request (the user is stored as the author of a png image)</param>
        /// <returns>The video (ex: video/mp4)</returns>
        [WebApi]
        [WebApiRaw]
        public Task<MemoryFile> ImagesToVideo(ImagesToVideoParams p, HttpServerRequest request)
            => Get(Comfy.ImagesToVideo(p), p, request);

        /// <summary>
        /// Create music from a text prompt (the style / description of the music) and optional lyrics.
        /// The workflow is the one named in the parameters, or the default text to music workflow.
        /// Duration is in seconds (for some models a maximum duration).
        /// </summary>
        /// <param name="p">The parameters, ex: { "Prompt": "Upbeat synthwave, retro 80s", "Lyrics": "[verse]\nNeon lights tonight", "Duration": 60 }</param>
        /// <param name="request">The request (the user is stored as the author of a png image)</param>
        /// <returns>The audio (ex: audio/flac)</returns>
        [WebApi]
        [WebApiRaw]
        public Task<MemoryFile> TextToMusic(TextToMusicParams p, HttpServerRequest request)
            => Get(Comfy.TextToMusic(p), p, request);

        /// <summary>
        /// Create speech, the prompt is the text to speak.
        /// The workflow is the one named in the parameters, or the default text to speech workflow.
        /// Voice is a description of the voice (if supported), Files is an optional reference voice to clone (if the workflow is a voice clone workflow).
        /// </summary>
        /// <param name="p">The parameters, ex: { "Prompt": "Hello and welcome!", "Voice": "female, warm, british accent" }</param>
        /// <param name="request">The request (the user is stored as the author of a png image)</param>
        /// <returns>The audio (ex: audio/flac)</returns>
        [WebApi]
        [WebApiRaw]
        public Task<MemoryFile> TextToSpeech(TextToSpeechParams p, HttpServerRequest request)
            => Get(Comfy.TextToSpeech(p), p, request);

        /// <summary>
        /// Create a sound (effect) from a text prompt.
        /// The workflow is the one named in the parameters, or the default text to sound workflow.
        /// Duration is in seconds (0 = the workflow default).
        /// </summary>
        /// <param name="p">The parameters, ex: { "Prompt": "A heavy wooden door creaking open", "Duration": 4 }</param>
        /// <param name="request">The request (the user is stored as the author of a png image)</param>
        /// <returns>The audio (ex: audio/flac)</returns>
        [WebApi]
        [WebApiRaw]
        public Task<MemoryFile> TextToSound(TextToSoundParams p, HttpServerRequest request)
            => Get(Comfy.TextToSound(p), p, request);
    }
}
