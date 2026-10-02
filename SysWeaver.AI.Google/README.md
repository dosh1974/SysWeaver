# SysWeaver.AI.Google

[⬆ SysWeaver overview](../README.md)

> LLM integration for SysWeaver using the Google Gemini API (or Vertex AI): a chat provider that can call methods of your services as tools, generate and edit images and keep an AI memory.

| | |
|---|---|
| **Layer** | AI |
| **Kind** | Micro service (chat provider, tool host) |

## Purpose

The Google equivalent of [SysWeaver.AI.OpenAI](../SysWeaver.AI.OpenAI/README.md), built on [SysWeaver.AI](../SysWeaver.AI/README.md) using the [Google.GenAI](https://www.nuget.org/packages/Google.GenAI) package ([API reference](https://ai.google.dev/api)).

## Key features

- Chat sessions (streaming) and query sessions, with model, thinking (reasoning) effort and service tier selectable per session.
- Automatic tool calling of annotated service methods (`[OpenAiTool]` / `[OpenAiUse]`), same tools as the OpenAI provider.
- Thought signatures (Gemini 3) are preserved in the conversation history.
- Image generation and editing using Gemini image models (and image generation using Imagen models).
- Token counting (debug API).

## Limitations and considerations

- Requires an API key (or Vertex AI credentials) and incurs usage costs; tool calls can multiply requests.
- Gemini has no participant names, the user name is added as a text part (like the OpenAI responses API).
- Image editing is only supported by Gemini image models (not Imagen).

## Using it

```json
{ "Type": "SysWeaver.AI.GoogleAiService, SysWeaver.AI.Google", "Params": { "ApiKey": "...", "DefaultChatModel": "gemini-2.5-flash" } }
```
```csharp
IAiService ai = manager.Get<GoogleAiService>();
var chat = ai.CreateChatSession(true);
```

## Relationships

- **Project:** [`SysWeaver.AI.Google.csproj`](SysWeaver.AI.Google.csproj)
- **Builds on:** [SysWeaver.AI](../SysWeaver.AI/README.md)
