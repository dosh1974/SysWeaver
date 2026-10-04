# SysWeaver.AI.ComfyUi

[⬆ SysWeaver overview](../README.md)

> Run ComfyUI workflows with typed inputs and outputs through the [ComfyUI-Connect](https://github.com/Good-Dream-Studio/ComfyUI-Connect) REST API.

| | |
|---|---|
| **Layer** | AI |
| **Kind** | Service (image / media generation client) |

## Purpose

Call ComfyUI workflows as if they were typed methods: a class (or struct) describes the inputs, another class describes the outputs, and the mapping to the workflow is generated (using Linq expressions) and cached the first time a workflow is used with those types.

## Key features

- Workflows are loaded from a local folder (API format json, as saved by `Workflow > Save API Endpoint` or `Export (API)`) and uploaded to the server on first use (optional).
- `RunWorkflow<T, R>(T p, String workflowName)`, where the public properties / fields of `T` are mapped to the inputs of nodes annotated with `$tag` and the ones of `R` to the outputs of nodes annotated with `#tag`.
- Automatic annotation of subgraph inputs: the API format flattens subgraphs, so the UI format workflow with the same name (from `UiWorkflowFolder`, or fetched from the ComfyUI server's saved workflows) is used to follow each subgraph input to the inner node input(s). Each subgraph input becomes a tag named from its label, ex: a subgraph input labeled `seed` adds `$seed(seed)` to the title of the inner `KSampler` node (in the uploaded copy, the file isn't changed). The subgraph node itself behaves like an annotated node with the subgraph inputs as its inputs: for a subgraph node titled `$Prompt`, `PromptSeed` or `[ComfyUiName("Prompt.sampler")]` work (without a `$tag` the sanitized title is used).
- Case insensitive matching that also ignores non letters / digits (`SamplerSeed`, `Sampler_Seed` and `sampler-seed` are all equal):
  - `TagInput` maps to the input `input` of the `$tag` node(s).
  - `Tag` maps to the input of the `$tag` node(s) if the tag has a single input (this is how subgraph inputs are matched by their label).
  - A `bool` named `Tag` controls node bypassing (false bypasses the `$tag` / `#tag` node(s)).
  - `Input` maps to the input `input` if it's unique among all tags (tags exposing the same node input are not considered ambiguous).
  - `[ComfyUiName("tag.input")]`, `[ComfyUiName("tag")]` maps explicitly, `[ComfyUiIgnore]` skips a member.
- Warnings (once per workflow and type pair) for members that can't be mapped, ambiguous matches and type mismatches.
- `null` inputs are not sent (the workflow default is used), enums are sent as their name.
- `byte[]`, `ComfyUiFile` and `Uri` inputs are sent as files (ex: for a `Load Image` node), data is named by a hash of the content.
- Outputs can be `byte[]` (first result), `byte[][]` / `List<byte[]>` (all results) or `String` / `String[]` / `List<String>` (base64).
- `ComfyUiTypeGenerator.GetInputOutputTypes` generates the C# source of the input and output types for a workflow (from a file, a stream or a loaded `ComfyUiWorkflow`). Member names are validated against the matching rules above (`[ComfyUiName]` is added when a name can't map unambiguously), and `ComfyUiTypeOptions` controls the namespace, type / member name casing, fields vs properties, default initialization, bypass members and the UI workflow used for subgraph inputs.

## Limitations and considerations

- Only the API workflow format is supported (the UI format with `nodes` and `links` is rejected).
- ComfyUI-Connect returns the files a node lists under `"images"` (base64 encoded). Images and videos (ex: `SaveVideo`) work as is, but audio (ex: `SaveAudio`) is listed under `"audio"` and is returned empty unless ComfyUI-Connect is patched: in `services/comfyui_service.py` `run_workflow`, collect the files of the keys `"images"`, `"audio"`, `"videos"` and `"gifs"` instead of only `"images"` (restart ComfyUI after the change).
- ComfyUI caches node results, so running a workflow with the same inputs completes instantly and returns the cached result. Expose and randomize a seed (ex: annotate the sampler `$Sampler(seed)`) to get a new result for each run.
- ComfyUI-Connect caches input files by name, so a supplied `ComfyUiFile.Name` must be unique for its content.
- Call `ReloadWorkflows()` if workflow files are changed while running.
- Subgraph annotation requires the UI and API workflows to be in sync (node ids must match), only top level subgraph instances are annotated (nested subgraphs are followed), and inputs connected outside the subgraph are skipped.

## Using it

```json
{ "Type": "SysWeaver.AI.ComfyUiService, SysWeaver.AI.ComfyUi", "Params": { "EndPoint": "http://127.0.0.1:8188", "WorkflowFolder": "$(ExecutableDir)/ComfyUi" } }
```
```csharp
public sealed class Txt2ImgIn
{
    public long? Seed;                              // "$sampler" node, input "seed"
    public int? SamplerSteps;                       // "$sampler" node, input "steps"
    [ComfyUiName("positive.text")]
    public String Prompt;
    public bool? Upscale;                           // false bypasses the "$upscale" node
}

public sealed class Txt2ImgOut
{
    public byte[] Output;                           // "#output" node
}

var comfy = manager.Get<ComfyUiService>();
var r = await comfy.RunWorkflow<Txt2ImgIn, Txt2ImgOut>(new Txt2ImgIn { Seed = 42, Prompt = "A cat" }, "txt2img");
```

Generate the input and output types of a workflow (the UI workflow folder is used to annotate subgraph inputs):

```csharp
var (input, output) = await ComfyUiTypeGenerator.GetInputOutputTypes("ComfyUi/txt2img.json", new ComfyUiTypeOptions
{
    Namespace = "MyApp",
    UiWorkflowFolder = @"C:\ComfyUI\user\default\workflows",
});
```

See the `ComfyUiExample` program in SysWeaver.Tests for complete examples (image, image edit, image to video, text to video and music).

## Relationships

- **Project:** [`SysWeaver.AI.ComfyUi.csproj`](SysWeaver.AI.ComfyUi.csproj)
- **Builds on:** [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md)
