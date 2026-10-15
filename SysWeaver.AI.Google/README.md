# SysWeaver.AI.Google

[⬆ SysWeaver overview](../README.md)

> LLM integration for SysWeaver using the Google Gemini API (or Vertex AI): an AI service and chat provider that can call methods of your services as tools, generate and edit images and keep an AI memory.

| | |
|---|---|
| **Layer** | AI |
| **Kind** | Micro service (AI provider) |

## Purpose

The Google provider for [SysWeaver.AI](../SysWeaver.AI/README.md), built on the [Google.GenAI](https://www.nuget.org/packages/Google.GenAI) package ([API reference](https://ai.google.dev/api)). It uses Gemini natively, with the Gemini Developer API or Vertex AI.

## How it fits into SysWeaver

`GoogleAiService` derives from `AiServiceBase` and so is an `IAiService` and an `IChatProvider`: AI chats appear in the [SysWeaver.Chat](../SysWeaver.Chat/README.md) UI, and registered service methods marked `[AiTool]` / `[AiUse]` can be called by the model. It is interchangeable with [SysWeaver.AI.OpenAI](../SysWeaver.AI.OpenAI/README.md) and [SysWeaver.AI.LlamaSharp](../SysWeaver.AI.LlamaSharp/README.md) for code that uses `IAiService` (such as [SysWeaver.AI.HostService](../SysWeaver.AI.HostService/README.md)).

## Key features

- Chat sessions (streaming) and query sessions
- Model, thinking (reasoning) effort and service tier selectable per session (`GoogleSessionParams`)
- Tool calling of annotated service methods, plus the default tools of [SysWeaver.AI](../SysWeaver.AI/README.md); thought signatures are preserved in the conversation history
- Image generation and editing tools using Gemini image models, and image generation using Imagen models (available to users with the `Debug` or `Content` role)
- Model tables (`Debug/AI/Models` menu) and debug APIs (chat, token counting, image generation and editing)
- Gemini Developer API or Vertex AI (`VertexAI`, `Project`, `Location`), configurable endpoint, API version, network timeout and concurrency limits for chats and image generation

## Key components

- [`GoogleAiService`](GoogleAiService.cs) — the AI service: chat provider, session factory, image and debug APIs
- [`GoogleChatSession`](GoogleChatSession.cs) — chat session
- [`GoogleQuerySession`](GoogleQuerySession.cs) — stateless query session
- [`GoogleAiParams`](GoogleAiParams.cs) — service and session parameters

## Using it

```json
{ "Type": "SysWeaver.AI.GoogleAiService, SysWeaver.AI.Google", "Params": { "CredFile": "$(KeyFolder)/GoogleGemini.txt", "DefaultChatModel": "gemini-2.5-flash" } }
```
```csharp
IAiService ai = manager.Get<GoogleAiService>();
var chat = ai.CreateChatSession(true, new GoogleSessionParams { Model = "gemini-2.5-flash", Tier = GoogleServiceTier.Flex });
```

## Limitations and considerations

- Requires an API key (`ApiKey` or `CredFile`) or Vertex AI credentials and incurs usage costs; tool calls can multiply requests
- Exposing methods as tools lets the model call them on behalf of the user — only expose methods that are safe for the requesting user
- Stateless completions (`IAiService.Complete`) use the base class fallback: the conversation is sent as a transcript and caller defined tools are not supported
- Gemini has no participant names, the user name is added as a text part
- Image editing is only supported by Gemini image models (not Imagen)

## Folder layout

| Folder | Content |
|---|---|
| [`srcWeb/`](srcWeb/README.md) | Agent icon of the provider (`srcWeb/ai/icons/Google.svg`), optimised, compressed and embedded at build time |

## Relationships

- **Project:** [`SysWeaver.AI.Google.csproj`](SysWeaver.AI.Google.csproj)
- **Builds on:** [SysWeaver.AI](../SysWeaver.AI/README.md)
- **Third-party:** Google.GenAI
