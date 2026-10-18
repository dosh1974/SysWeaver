# SysWeaver.AI.OpenAI

[⬆ SysWeaver overview](../README.md)

> LLM integration for SysWeaver using the OpenAI API (or any compatible endpoint): an AI service and chat provider that can call methods of your services as tools, generate and edit images, tokenize text and keep an AI memory.

| | |
|---|---|
| **Layer** | AI |
| **Kind** | Micro service (AI provider) |

## Purpose

The OpenAI provider for [SysWeaver.AI](../SysWeaver.AI/README.md), built on the official [OpenAI](https://www.nuget.org/packages/OpenAI) package. Since the endpoint is configurable it also works with the OpenAI compatible APIs of other vendors and local servers (Gemini's OpenAI endpoint, LM Studio, etc).

## How it fits into SysWeaver

`OpenAiService` derives from `AiServiceBase` and so is an `IAiService` and an `IChatProvider`: AI chats appear in the [SysWeaver.Chat](../SysWeaver.Chat/README.md) UI, and registered service methods marked `[AiTool]` / `[AiUse]` can be called by the model. It is interchangeable with [SysWeaver.AI.Google](../SysWeaver.AI.Google/README.md) and [SysWeaver.AI.LlamaSharp](../SysWeaver.AI.LlamaSharp/README.md) for code that uses `IAiService` (such as [SysWeaver.AI.HostService](../SysWeaver.AI.HostService/README.md)); [SysWeaver.AI.LlmTranslator](../SysWeaver.AI.LlmTranslator/README.md) and [SysWeaver.LanguageIdentifier.Llm](../SysWeaver.LanguageIdentifier.Llm/README.md) require this provider.

## Key features

- Chat sessions using the Responses API (default) or the Chat Completions API (`DefaultChatApi`, selectable per session), and query sessions
- Model, reasoning effort and service tier selectable per session (`OpenAiSessionParams`)
- Tool calling of annotated service methods, plus the default tools of [SysWeaver.AI](../SysWeaver.AI/README.md)
- Native stateless completion (`Complete`) with caller defined tools and image / file parts, using the API selected by `DefaultChatApi` (Responses: `instructions` + input items, function call / output items, nothing stored; Chat Completions: system, user, assistant tool calls and tool messages), with streaming (`onText`), tool choice, max tokens, temperature and reasoning (when the model supports them)
- Image generation and editing tools (available to users with the `Debug` or `Content` role)
- Token encoding/decoding with TiktokenSharp (token files are cached on disc)
- Model tables (`Debug/AI/Models` menu) and debug APIs (chat, image generation and editing, token encode/decode)
- Configurable endpoint, organization and project, network timeout and concurrency limits for chats and image generation

## Key components

- [`OpenAiService`](OpenAiService.cs) — the AI service: chat provider, session factory, image and token APIs
- [`OpenAiResponseChatSession`](OpenAiResponseChatSession.cs) / [`OpenAiCompletionsChatSession`](OpenAiCompletionsChatSession.cs) — chat sessions using the Responses / Chat Completions API
- [`OpenAiQuerySession`](OpenAiQuerySession.cs) — stateless query session
- [`OpenAiParams`](OpenAiParams.cs) — service parameters

## Using it

```json
{ "Type": "SysWeaver.AI.OpenAiService, SysWeaver.AI.OpenAI", "Params": { "CredFile": "$(KeyFolder)/OpenAI.txt", "DefaultChatModel": "gpt-4.1" } }
```
```csharp
IAiService ai = manager.Get<OpenAiService>();
var chat = ai.CreateChatSession(true, new OpenAiSessionParams { Model = "gpt-4.1", Tier = OpenAiServiceTier.Flex });
```

Set `EndPoint` (e.g. `"http://localhost:1234/v1"`) to use another OpenAI compatible API.

## Limitations and considerations

- Requires an API key (`ApiKey` or `CredFile`) and incurs usage costs; tool calls can multiply requests
- Exposing methods as tools lets the model call them on behalf of the user — only expose methods that are safe for the requesting user
- Stateless completions send tool result attachments (images) as a user message right after the tool results (both APIs only accept text tool results); reasoning items are not round tripped (function calls are sent without their reasoning), and the Chat Completions API disables reasoning when tools are used (on models that can)
- Compatible endpoints of other vendors often implement only parts of the API (e.g. no Responses API, images or service tiers)

## Folder layout

| Folder | Content |
|---|---|
| [`srcWeb/`](srcWeb/README.md) | Agent icon of the provider (`srcWeb/ai/icons/OpenAI.svg`), optimised, compressed and embedded at build time |

## Relationships

- **Project:** [`SysWeaver.AI.OpenAI.csproj`](SysWeaver.AI.OpenAI.csproj)
- **Builds on:** [SysWeaver.AI](../SysWeaver.AI/README.md), [SysWeaver.Chat](../SysWeaver.Chat/README.md), [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md), [SysWeaver.IsoData](../SysWeaver.IsoData/README.md), [SysWeaver.Media.Png](../SysWeaver.Media.Png/README.md), [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md)
- **Used by:** [SysWeaver.AI.LlmTranslator](../SysWeaver.AI.LlmTranslator/README.md), [SysWeaver.LanguageIdentifier.Llm](../SysWeaver.LanguageIdentifier.Llm/README.md), [TranslationService](https://github.com/dosh1974/SysWeaver.Services/blob/main/TranslationService/README.md) (SysWeaver.Services)
- **Third-party:** OpenAI, TiktokenSharp, Newtonsoft.Json
