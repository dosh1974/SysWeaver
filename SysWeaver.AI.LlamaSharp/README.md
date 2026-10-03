# SysWeaver.AI.LlamaSharp

[⬆ SysWeaver overview](../README.md)

> Local LLM integration for SysWeaver using [LLamaSharp](https://github.com/SciSharp/LLamaSharp) (llama.cpp): a chat provider that runs GGUF models in process and can call methods of your services as tools.

| | |
|---|---|
| **Layer** | AI |
| **Kind** | Micro service (chat provider, tool host) |

## Purpose

The local equivalent of [SysWeaver.AI.OpenAI](../SysWeaver.AI.OpenAI/README.md) and [SysWeaver.AI.Google](../SysWeaver.AI.Google/README.md), built on [SysWeaver.AI](../SysWeaver.AI/README.md) using the [LLamaSharp](https://www.nuget.org/packages/LLamaSharp) package.
No API key, no usage costs and no data leaves the machine.

## Key features

- Any number of models (GGUF files) are loaded when the service is created, each with its own load time parameters (context size, GPU layers, batch sizes, flash attention, KV cache types etc).
- Models are selected per session using the model name (model code) from the configuration.
- Chat sessions (streaming) and query sessions, prompts are formatted using the chat template of the model (or a configured template).
- Tool calling of annotated service methods (`[OpenAiTool]` / `[OpenAiUse]`), using the Hermes / Qwen `<tool_call>` format (described in the system prompt).
- Thinking (`<think>` tags) is removed from responses.
- The oldest messages are dropped when a conversation doesn't fit in the context.
- CPU and Vulkan (GPU) backends are included, the Vulkan backend is used if available.

## Limitations and considerations

- Models are loaded into memory (RAM / VRAM) for the lifetime of the service, every concurrent request allocates a context of `ContextSize` tokens.
- The full conversation is processed on every request (no KV cache reuse between requests).
- Tool calling relies on the model following the tool prompt, works best with models trained on the Hermes / Qwen format (Qwen 2.5+, Hermes etc).
- Text only, image attachments and image generation are not supported.
- The backend is selected process wide when the first model is loaded. To use CUDA, reference a `LLamaSharp.Backend.Cuda*` package from the application.
- The reasoning effort parameter isn't used.

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
var chat = ai.CreateChatSession(true, new AiSessionParams { Model = "qwen3-8b" });
```

## Relationships

- **Project:** [`SysWeaver.AI.LlamaSharp.csproj`](SysWeaver.AI.LlamaSharp.csproj)
- **Builds on:** [SysWeaver.AI](../SysWeaver.AI/README.md)
