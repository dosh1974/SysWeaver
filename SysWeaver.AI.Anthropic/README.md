# SysWeaver.AI.Anthropic

[⬆ SysWeaver overview](../README.md)

> LLM integration for SysWeaver using the Anthropic Claude API: an AI service and chat provider that can call methods of your services as tools and keep an AI memory.

| | |
|---|---|
| **Layer** | AI |
| **Kind** | Micro service (AI provider) |

## Purpose

The Anthropic provider for [SysWeaver.AI](../SysWeaver.AI/README.md), built on the official [Anthropic](https://www.nuget.org/packages/Anthropic) package ([API reference](https://platform.claude.com/docs/en/api/overview)). It uses the Claude Messages API natively. Anthropic's OpenAI compatible endpoint (usable with [SysWeaver.AI.OpenAI](../SysWeaver.AI.OpenAI/README.md)) is meant for testing and comparison, and it lacks features such as prompt caching and thinking blocks.

## How it fits into SysWeaver

`AnthropicAiService` derives from `AiServiceBase`, so it is both an `IAiService` and an `IChatProvider`. AI chats appear in the [SysWeaver.Chat](../SysWeaver.Chat/README.md) UI, and the model can call registered service methods marked `[AiTool]` / `[AiUse]`. Code that uses `IAiService` can swap it for [SysWeaver.AI.OpenAI](../SysWeaver.AI.OpenAI/README.md), [SysWeaver.AI.Google](../SysWeaver.AI.Google/README.md) or [SysWeaver.AI.LlamaSharp](../SysWeaver.AI.LlamaSharp/README.md) (such as [SysWeaver.AI.HostService](../SysWeaver.AI.HostService/README.md)).

## Key features

- Chat sessions and query sessions; responses are always streamed, so long responses don't time out
- Model, reasoning effort and max output tokens selectable per session (`AnthropicSessionParams`). `AiReasoning` maps to each model's thinking setup:
  - adaptive thinking and `effort` (`low` to `max`) on current models; `None` disables thinking where the model allows it (`between_tools` on Claude Sonnet 5.5)
  - thinking budgets on Claude 3.7 to 4.5
- Tool calling of annotated service methods, plus the default tools of [SysWeaver.AI](../SysWeaver.AI/README.md). Failed tool calls are sent back as error results (`is_error`)
- Native stateless completion (`Complete`) with caller defined tools and image / pdf / text parts:
  - system prompt, user / assistant messages, tool use and tool results (consecutive results are merged); images and documents can be part of tool results
  - tool choice (auto / none / any), streaming (`onText`), max tokens, temperature and reasoning
  - the thinking blocks of a response are returned in `AiCompletionToolCall.Signature` and replayed in the original order
- Automatic prompt caching (`PromptCaching`, on by default), with cached input tokens reported in `AiCompletionResult.CachedTokens`
- Model table (`Debug/AI/Models` menu) and debug APIs (chat, token counting)
- Configurable endpoint, network timeout, retries and concurrency limit for chats

## Key components

- [`AnthropicAiService`](AnthropicAiService.cs): the AI service, with the chat provider, session factory and debug APIs
- [`AnthropicChatSession`](AnthropicChatSession.cs): chat session
- [`AnthropicQuerySession`](AnthropicQuerySession.cs): stateless query session
- [`AnthropicAiParams`](AnthropicAiParams.cs): service and session parameters
- [`AnthropicModels`](AnthropicModels.cs): model capabilities, conversions and the request loop

## Using it

```json
{ "Type": "SysWeaver.AI.AnthropicAiService, SysWeaver.AI.Anthropic", "Params": { "CredFile": "$(KeyFolder)/Anthropic.txt", "DefaultChatModel": "claude-opus-5-5" } }
```
```csharp
IAiService ai = manager.Get<AnthropicAiService>();
var chat = ai.CreateChatSession(true, new AnthropicSessionParams { Model = "claude-sonnet-5-5", Reasoning = AiReasoning.Medium });
```

If neither `ApiKey` nor `CredFile` is set, the client falls back to the `ANTHROPIC_API_KEY` environment variable (or another default Anthropic credential).

Model ids: `claude-opus-5-5` (the default), `claude-sonnet-5-5`, `claude-haiku-5-5`, `claude-fable-5-1` (most capable). Older ids (Claude 4.x, 3.x) also work. Platform prefixes such as `anthropic.` are ignored when looking up model capabilities, but this provider talks to the Claude API. Use `EndPoint` only for gateways that speak the Claude API.

## Limitations and considerations

- Requires an API key and incurs usage costs; tool calls can multiply requests
- Exposing methods as tools lets the model call them on behalf of the user. Only expose methods that are safe for the requesting user
- No image generation (the API has none). Images and documents are input only
- Supported attachments:
  - images: png, jpeg, gif, webp
  - pdf documents (bytes or url)
  - text files (bytes)
  - other file types throw a `NotSupportedException`
- Model capabilities (temperature, forced tool use, thinking and effort setup) come from a model id prefix table in `AnthropicModels`. Unknown ids are treated like the latest models (adaptive thinking that can't be disabled, no temperature, no forced tool use)
- `Required` tool choice is only forced (`any`) when thinking is off and the model supports it. Otherwise the system prompt tells the model to call a tool
- Thinking blocks are bound to the conversation that produced them. Newer models reject them if an earlier turn, the system prompt or the tools changed. So chat sessions keep thinking blocks during a turn (required to continue after tool calls) and remove them from the history when the turn completes. This lets the system prompt be rebuilt and tool calls be removed (`AutoRemoveToolCalls`) between turns.
- Stateless completions: return `Signature` unchanged. Tool calls without it (created by the caller or another model) are sent without thinking blocks, which some models reject when thinking is on
- Refusals (`stop_reason` `refusal`) return an error (chat / query) or throw `AiCompletionException` with code `content_filter` (`Complete`). Server side refusal fallbacks (a beta feature) are not used
- Claude has no participant names, so the user name is added as a text block

## Folder layout

| Folder | Content |
|---|---|
| [`srcWeb/`](srcWeb/README.md) | Agent icon of the provider (`srcWeb/ai/icons/Anthropic.svg`), optimised, compressed and embedded at build time |

## Relationships

- **Project:** [`SysWeaver.AI.Anthropic.csproj`](SysWeaver.AI.Anthropic.csproj)
- **Builds on:** [SysWeaver.AI](../SysWeaver.AI/README.md)
- **Third-party:** Anthropic (official C# SDK)
