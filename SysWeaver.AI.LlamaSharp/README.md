# SysWeaver.AI.LlamaSharp

[⬆ SysWeaver overview](../README.md)

> Local LLM integration for SysWeaver using LLamaSharp (llama.cpp): an AI service and chat provider that runs GGUF models in process and can call methods of your services as tools.

| | |
|---|---|
| **Layer** | AI |
| **Kind** | Micro service (AI provider) |

## Purpose

The local provider for [SysWeaver.AI](../SysWeaver.AI/README.md), built on the [LLamaSharp](https://www.nuget.org/packages/LLamaSharp) package ([project](https://github.com/SciSharp/LLamaSharp)). No API key, no usage costs and no data leaves the machine.

## How it fits into SysWeaver

`LlamaSharpAiService` derives from `AiServiceBase` and so is an `IAiService` and an `IChatProvider`: AI chats appear in the [SysWeaver.Chat](../SysWeaver.Chat/README.md) UI, and registered service methods marked `[AiTool]` / `[AiUse]` can be called by the model. It is interchangeable with [SysWeaver.AI.OpenAI](../SysWeaver.AI.OpenAI/README.md) and [SysWeaver.AI.Google](../SysWeaver.AI.Google/README.md) for code that uses `IAiService`; it is the only provider with native stateless completions, which [SysWeaver.AI.HostService](../SysWeaver.AI.HostService/README.md) uses to serve local models through OpenAI and Google compatible APIs.

## Key features

- Chat sessions (streaming) and query sessions; the model is selected per session by its configured name (model code)
- Any number of GGUF models, all loaded when the service is created, each with its own load and inference parameters (context size, GPU layers, batch sizes, flash attention, KV cache types, sampling, concurrency)
- Prompts are formatted with the model's chat template or a configured one; a built in `gemma4` template adds Gemma 4 support
- Tool calling of annotated service methods, plus the default tools of [SysWeaver.AI](../SysWeaver.AI/README.md), using the Hermes / Qwen `<tool_call>` format (or the native Gemma 4 format)
- Stateless completions (`IAiService.Complete`) with caller defined tools (tool calls are returned, not executed) and token streaming
- Thinking is removed from responses; the oldest messages are dropped when a conversation doesn't fit in the context
- Models with multi token prediction layers that the bundled llama.cpp can't load are automatically loaded from a stripped copy
- CPU and Vulkan (GPU) backends are included, Vulkan is used if available (`UseVulkan`); llama.cpp log messages can be forwarded to the log (`LogNative`)
- Model tables (`Debug/AI/Models` menu) and debug APIs (chat, token counting)

## Key components

- [`LlamaSharpAiService`](LlamaSharpAiService.cs) — the AI service: model loading, chat provider, session factory, completions
- [`LlamaSharpModel`](LlamaSharpModel.cs) — a loaded model: prompt formatting, inference, tool call parsing
- [`LlamaSharpChatSession`](LlamaSharpChatSession.cs) / [`LlamaSharpQuerySession`](LlamaSharpQuerySession.cs) — chat and query sessions
- [`LlamaSharpAiParams`](LlamaSharpAiParams.cs) — service, model (`LlamaSharpLlm`) and session parameters

## Using it

```json
{
  "Type": "SysWeaver.AI.LlamaSharpAiService, SysWeaver.AI.LlamaSharp",
  "Params": {
    "Llms": [
      { "Name": "qwen3-8b", "FilePath": "$(CommonApplicationData)\\Models\\Qwen3-8B-Q4_K_M.gguf", "ContextSize": 16384, "CanReason": true },
      { "Name": "gemma3-4b", "FilePath": "D:\\Models\\gemma-3-4b-it-Q4_K_M.gguf", "SupportSystemRole": false }
    ]
  }
}
```
```csharp
IAiService ai = manager.Get<LlamaSharpAiService>();
var chat = ai.CreateChatSession(true, new LlamaSharpSessionParams { Model = "qwen3-8b" });
```

## Limitations and considerations

- Models are kept in memory (RAM / VRAM) for the lifetime of the service, and every concurrent request allocates a context of `ContextSize` tokens
- The full conversation is processed on every request (no KV cache reuse between requests)
- Tool calling relies on the model following the tool prompt, it works best with models trained on the Hermes / Qwen format (Qwen 2.5+, Hermes etc) or Gemma 4
- Text only: attachments other than text are replaced by a note, image generation is not supported
- The backend is selected process wide when the first model is loaded. To use CUDA, reference a `LLamaSharp.Backend.Cuda*` package from the application
- The reasoning effort parameter isn't used

## Relationships

- **Project:** [`SysWeaver.AI.LlamaSharp.csproj`](SysWeaver.AI.LlamaSharp.csproj)
- **Builds on:** [SysWeaver.AI](../SysWeaver.AI/README.md)
- **Third-party:** LLamaSharp, LLamaSharp.Backend.Cpu, LLamaSharp.Backend.Vulkan
