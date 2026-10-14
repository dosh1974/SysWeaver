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
- `ComfyUiTypeGenerator` generates the C# source of the input and output types for a workflow
- Optional API key (`Authorization` header, e.g. for an authenticating proxy) and ComfyUI token

## Key components

- [`ComfyUiService`](ComfyUiService.cs) — the service: workflow loading, upload and `RunWorkflow`
- [`ComfyUiWorkflow`](ComfyUiWorkflow.cs) — a loaded API format workflow with its tagged nodes
- [`ComfyUiSubgraphs`](ComfyUiSubgraphs.cs) — annotation of subgraph inputs from the UI format workflow
- [`ComfyUiTypeGenerator`](ComfyUiTypeGenerator.cs) — C# source generation of input and output types

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

The `ComfyUiExample` program in the SysWeaver.Tests repository has complete examples (image, image edit, image to video, text to video and music).

## Limitations and considerations

- Only the API workflow format can be run (the UI format is only used for subgraph annotation); subgraph annotation requires the UI and API workflows to be in sync
- ComfyUI-Connect only returns the files listed under `"images"`: images and videos work, audio is returned empty unless ComfyUI-Connect is patched to also collect `"audio"` (and `"videos"`, `"gifs"`)
- ComfyUI caches node results, so identical inputs return the cached result — expose and randomize a seed to get new results
- ComfyUI-Connect caches input files by name, so a supplied `ComfyUiFile.Name` must be unique for its content
- A request doesn't return until the workflow has completed (`NetworkTimeoutSeconds`, default 10 minutes)

## Relationships

- **Project:** [`SysWeaver.AI.ComfyUi.csproj`](SysWeaver.AI.ComfyUi.csproj)
- **Builds on:** [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md)
