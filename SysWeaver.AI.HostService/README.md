# SysWeaver.AI.HostService

[⬆ SysWeaver overview](../README.md)

> An OpenAI compatible API (Chat Completions and Responses) in front of one or more SysWeaver AI services, so that any OpenAI client (SDKs, agents, tools) can use them.

| | |
|---|---|
| **Layer** | AI |
| **Kind** | Http server module (micro service) |

## Purpose

Exposes the models of the AI services ([`IAiService`](../SysWeaver.AI/IAiService.cs)) registered in the service manager, ex: local models using [SysWeaver.AI.LlamaSharp](../SysWeaver.AI.LlamaSharp/README.md), using the OpenAI API.
Requests are forwarded to the AI service that provides the requested model.

## Key features

- Endpoints (relative to the `Prefix`, default `v1/`):
  - `GET models`, `GET models/{model}`
  - `POST chat/completions` ([Chat Completions](https://platform.openai.com/docs/api-reference/chat))
  - `POST responses` ([Responses](https://platform.openai.com/docs/api-reference/responses))
- Streaming (server sent events) for both APIs.
- Caller defined (function) tools, tool calls are returned to the client.
- The AI services are selected by instance name (`Instances`) and used in creation order, without instance names the first AI service is used.
- Models are mapped to the AI service that provides them (the first one if several services provides the same model), the model lists are cached by the AI services (updated at most every 5 minutes).
- The default model (when no model is specified) is the last model of the last AI service.
- Errors use the OpenAI error format (ex: `404` with `model_not_found`).
- Access requires the `Auth` tokens, a Bearer token (API key) can be used by clients.

## Limitations and considerations

- Stateless: the Responses API `previous_response_id` and `conversation` are not supported, clients must send the whole conversation (`store: false` style).
- Text only (no images, audio or files) and only function tools (no built in tools like web search).
- `n` must be 1, logprobs are not supported.
- The AI service must implement `IAiService.Complete` natively for tools and token streaming (LLamaSharp does), the default implementation (OpenAI and Google services) sends the conversation as a transcript, ignores caller defined tools and returns the response in a single update.

## Using it

```json
{ "Type": "SysWeaver.AI.HostService, SysWeaver.AI.HostService", "Params": { "Instances": ["local"], "Auth": "ai" } }
```
```csharp
var client = new OpenAIClient(new ApiKeyCredential(bearerToken), new OpenAIClientOptions { Endpoint = new Uri("https://myserver/v1") });
var chat = client.GetChatClient("qwen3-8b");
```

## Relationships

- **Project:** [`SysWeaver.AI.HostService.csproj`](SysWeaver.AI.HostService.csproj)
- **Builds on:** [SysWeaver.AI](../SysWeaver.AI/README.md)
