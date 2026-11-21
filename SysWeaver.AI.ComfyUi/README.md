# SysWeaver.AI.ComfyUi

[⬆ SysWeaver overview](../README.md)

> Run ComfyUI workflows with typed inputs and outputs through the ComfyUI-Connect REST API, workflows loaded from disc with compiled and cached mappings.

| | |
|---|---|
| **Layer** | AI |
| **Kind** | Micro service (ComfyUI client) |

## Purpose

Call [ComfyUI](https://github.com/comfyanonymous/ComfyUI) workflows (image, video and audio generation) as if they were typed methods, using the [ComfyUI-Connect](https://github.com/Good-Dream-Studio/ComfyUI-Connect) REST API: one class describes the inputs, another the outputs, and the mapping to the workflow is generated (as compiled LINQ expressions) and cached the first time a workflow is used with those types.

## How it fits into SysWeaver

`ComfyUiService` is a plain service registered in the manifest; application code gets it from the service manager and calls `RunWorkflow`. It has no dependency on the LLM projects (it only shares the `SysWeaver.AI` namespace).

## Key features

- Workflows are API format JSON files loaded from `WorkflowFolder` (name = file name) and uploaded to the ComfyUI-Connect server on first use (optional); `ReloadWorkflows()` picks up changed files
- `RunWorkflow<T, R>(input, workflowName)`: public fields / properties of `T` map to the inputs of nodes tagged `$tag` in their title, those of `R` to the outputs of nodes tagged `#tag`
- Forgiving name matching (`TagInput`, `Tag` or `Input`; case insensitive, ignoring non letters/digits), explicit mapping with `[ComfyUiName]`, `[ComfyUiIgnore]` to skip a member, and a `bool` named after a tag bypasses that node; unmappable or ambiguous members are reported as warnings
- Automatic annotation of subgraph inputs, using the matching UI format workflow (from `UiWorkflowFolder` or fetched from the ComfyUI server)
- File inputs (`byte[]`, `ComfyUiFile`, `Uri`), enum and nullable inputs (null keeps the workflow default); outputs as bytes or base64 strings, first or all results
- `ComfyUiTypeGenerator` generates the C# source of the input and output types for a workflow (`GetInputOutputTypes`: two sources, `GetSource`: one file with both), optionally with nested types for object inputs (`NestedTypes`) and a base type for the input type (`InputBaseType`); the [ComfyApiToCs](https://github.com/dosh1974/SysWeaver.Tools/blob/main/ComfyApiToCs/README.md) tool converts workflow files to `.cs` files
- Generic task methods that work with any (unannotated) workflow: `TextToImage`, `ImagesToImage`, `TextToVideo`, `ImagesToVideo`, `TextToMusic`, `TextToSpeech` and `TextToSound`, with common parameters (`Prompt`, `NegativePrompt`, `Seed`, `Steps` (0 = the workflow default; several samplers keep their ratio and split samplers their split point), `EnhancePrompt`, `Width` / `Height` / `Transparent`, `Files`, `Duration`, `Lyrics`, `Voice`). `AddWorkflows(pathAndMask)` loads and analyzes workflows: the task type is determined from the output and load nodes (audio is classified by names), each parameter is mapped by following the node graph (ex: the prompt is the text that reaches the positive conditioning, links from a resolution node are replaced when a size is given, text combined with the prompt by concatenate / replace nodes is kept, and a `{prompt}` placeholder or a LoRA trigger in the default prompt becomes a prefix / suffix; `Transparent` on a model that makes transparency from the prompt, ex: Qwen-Image 2.1 or later, adds the model's transparency prefix / suffix to the prompt). Annotating a node `$Prompt`, `$Seed`, `$Width` etc overrides the analysis. The workflow is selected by name (`Workflow`), else the default for the task (the first in alphabetical order, or `DefaultTextToImage` etc)
- `GetPromptEnhancement(workflow)` returns a system prompt for an LLM that rewrites a user request into a prompt optimized for the workflow: the model is recognized from the model files (FLUX.1/.2, Kontext, Qwen-Image (Edit), Z-Image, SD 1.5 / SDXL / Pony / Illustrious / SD 3.5, Chroma, HiDream, Lumina, HunyuanImage, OmniGen2, Wan, LTX, HunyuanVideo, CogVideoX, Mochi, ACE-Step, YuE, Stable Audio, MMAudio and several TTS models) and its prompting guidance is combined with the task, the prefix / suffix, the negative prompt support, the workflow's default prompt as a format example and output rules
- Implements `IAiImageGenService` and `IAiImageEditService` of SysWeaver.AI (the model is the workflow name, `DefaultTextToImage` / `DefaultImagesToImage` and `DefaultImageWidth` / `DefaultImageHeight` in the parameters, an unavailable default workflow falls back to the first workflow of the task)
- Optional API key (`Authorization` header, e.g. for an authenticating proxy) and ComfyUI token
- An input type can inherit `ComfyUiParams` (members declared by it are connection parameters, never mapped to inputs), ex: the [RunComfyUi](https://github.com/dosh1974/SysWeaver.Tools/blob/main/RunComfyUi/README.md) tool parses both from the command line; `ComfyUiFile.GetExtension(data)` gets a file extension from file data

## Key components

- [`ComfyUiService`](ComfyUiService.cs) — the service: workflow loading, upload and `RunWorkflow`
- [`ComfyUiWorkflow`](ComfyUiWorkflow.cs) — a loaded API format workflow with its tagged nodes
- [`ComfyUiSubgraphs`](ComfyUiSubgraphs.cs) — annotation of subgraph inputs from the UI format workflow
- [`ComfyUiTypeGenerator`](ComfyUiTypeGenerator.cs) — C# source generation of input and output types
- [`ComfyTaskWorkflow`](ComfyTaskWorkflow.cs) — a workflow analyzed for the task methods (type and parameter mappings, `Describe()` shows them)
- [`ComfyTaskParams`](ComfyTaskParams.cs) — the task parameters (`TextToImageParams` etc) and `ComfyResult`
- [`ComfyUiService.Tasks`](ComfyUiService.Tasks.cs) — the task methods, `AddWorkflows`, the workflow lists and defaults
- [`ComfyUiApiService`](ComfyUiApiService.cs) — web API's for the task methods (`Api/comfyui/TextToImage` etc, returning the file with its mime type; a png image gets the prompt, the workflow and seed and the user as png information chunks, see `AiImageTools.AddPngInfo` of SysWeaver.AI) and the workflow lookups: `GetWorkflow(name)` and `GetDefaultWorkflow(type)` (the details of a workflow, [`ComfyWorkflowInfo`](ComfyWorkflowInfo.cs)), `GetWorkflowNames(type)`, `GetDefaultWorkflows()` and `WorkflowTable`, a table of the loaded workflows (type, model, parameters, model files, LoRA's, prefix / suffix, mappings, the prompt enhancement and more), in the menu as *ComfyUI > ComfyUI workflows*. All its API's use one auth, `Auth` of [`ComfyUiApiParams`](ComfyUiApiParams.cs) (default: any logged in user)

## Using it

```json
{ "Type": "SysWeaver.AI.ComfyUiService, SysWeaver.AI.ComfyUi", "Params": { "EndPoint": "http://127.0.0.1:8188", "WorkflowFolder": "$(ExecutableDir)/ComfyUi" } }
```
```csharp
public sealed class Txt2ImgIn
{
    public long? Seed;                              // the input "seed", if unique among all tags
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

Task methods (no types needed, the workflows are analyzed). The task workflows can be loaded from the configuration using `TaskWorkflowFolders` (folders or file masks, variables supported):

```json
{ "Type": "SysWeaver.AI.ComfyUiService, SysWeaver.AI.ComfyUi", "Params": { "EndPoint": "http://127.0.0.1:8188", "TaskWorkflowFolders": [ "$(ExecutableDir)/ComfyUi/Tasks" ] } }
```

Or from code:

```csharp
comfy.AddWorkflows("$(ExecutableDir)/ComfyUi");             // a folder, or a mask like "D:/Workflows/video_*.json"
var img = await comfy.TextToImage(new TextToImageParams { Prompt = "A red fox in the snow", Width = 1024, Height = 768 });
if (img.IsSuccess)
    File.WriteAllBytes(img.Name, img.Data);                   // ex: "image_t2i_1234.png", img.Seed is the seed that was used
var edit = await comfy.ImagesToImage(new ImagesToImageParams { Prompt = "Make it a watercolor", Files = [img], Workflow = "image_edit" });
```

To expose the task methods as web API's (`Api/comfyui/...`), also register the API service after the ComfyUI service:

```json
{ "Type": "SysWeaver.AI.ComfyUiApiService, SysWeaver.AI.ComfyUi", "Params": { "Auth": "" } }
```

The `ComfyUiExample` program in the SysWeaver.Tests repository has complete examples (image, image edit, image to video, text to video and music).

## Limitations and considerations

- Only the API workflow format can be run (the UI format is only used for subgraph annotation); subgraph annotation requires the UI and API workflows to be in sync
- ComfyUI-Connect only returns the files listed under `"images"`: images and videos work, audio is returned empty unless ComfyUI-Connect is patched to also collect `"audio"` (and `"videos"`, `"gifs"`)
- ComfyUI caches node results, so identical inputs return the cached result — expose and randomize a seed to get new results
- ComfyUI-Connect caches input files by name, so a supplied `ComfyUiFile.Name` must be unique for its content
- A request doesn't return until the workflow has completed (`NetworkTimeoutSeconds`, a parameter of the service, default 12 hours: the longest time a workflow may run, including the time in the ComfyUI queue)
- Task workflows are analyzed using heuristics (node class names, input names and titles), check `ComfyTaskWorkflow.Describe()` for new workflows and annotate nodes (`$Prompt` etc) or use `AddWorkflow(file, type)` where the analysis is wrong. They are always uploaded (as `sw_[name]`, plus `sw_[name]_sized` when a size requires replacing links or flipping switches), regardless of `UploadWorkflows`
- Task seeds use the lower 31 bits (valid for all nodes), a negative seed (the default) is random

## Relationships

- **Project:** [`SysWeaver.AI.ComfyUi.csproj`](SysWeaver.AI.ComfyUi.csproj)
- **Builds on:** [SysWeaver.AI](../SysWeaver.AI/README.md), [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md)
