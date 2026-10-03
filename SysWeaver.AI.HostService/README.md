# SysWeaver.AI.HostService

[⬆ SysWeaver overview](../README.md)

> OpenAI compatible (Chat Completions and Responses) and Google compatible (Gemini API and Vertex AI) APIs in front of one or more SysWeaver AI services, so that OpenAI and Google GenAI clients (SDKs, agents, tools) can use them.

| | |
|---|---|
| **Layer** | AI |
| **Kind** | Http server module (micro service) |

## Purpose

Exposes the models of the AI services ([`IAiService`](../SysWeaver.AI/IAiService.cs)) registered in the service manager, ex: local models using [SysWeaver.AI.LlamaSharp](../SysWeaver.AI.LlamaSharp/README.md), using the OpenAI and Google APIs.
Requests are forwarded to the AI service that provides the requested model.

## Key features

- OpenAI end points (relative to `OpenAiPrefix`, default `openai/v1/`, this is the base url for OpenAI clients):
  - `GET models`, `GET models/{model}`
  - `POST chat/completions` ([Chat Completions](https://platform.openai.com/docs/api-reference/chat))
  - `POST responses` ([Responses](https://platform.openai.com/docs/api-reference/responses))
- Google end points (relative to `GooglePrefix`, default `google/`, this is the base url for Google GenAI clients), any API version (`v1`, `v1beta`, `v1beta1` etc):
  - Gemini API ([reference](https://ai.google.dev/api)): `GET {version}/models`, `GET {version}/models/{model}`, `POST {version}/models/{model}:generateContent`, `POST {version}/models/{model}:streamGenerateContent`
  - Vertex AI ([reference](https://cloud.google.com/vertex-ai/generative-ai/docs/reference/rest)): `POST {version}/projects/{project}/locations/{location}/publishers/{publisher}/models/{model}:generateContent` (and `:streamGenerateContent`), express mode: `POST {version}/publishers/{publisher}/models/{model}:generateContent` (and `:streamGenerateContent`)
  - Camel case and snake case request properties, OpenAPI style schemas (`"type": "OBJECT"`) and json schemas (`parametersJsonSchema`).
- The OpenAI and Google APIs have conflicting end points (ex: `v1/models`), so they must use different prefixes (checked on creation), a prefix can be set to null to disable that API.
- Streaming for all APIs (server sent events, Google `streamGenerateContent` without `alt=sse` returns a streamed json array).
- Caller defined (function) tools, tool calls are returned to the client.
- The AI services are selected by instance name (`Instances`) and used in creation order, without instance names the first AI service is used.
- Models are mapped to the AI service that provides them (the first one if several services provides the same model), the model lists are cached by the AI services (updated at most every 5 minutes).
- The default model (when no model is specified, OpenAI only) is the last model of the last AI service.
- Errors use the OpenAI or Google error format (ex: `404` with `model_not_found` or `NOT_FOUND`).
- Access requires the `Auth` tokens, clients can authenticate using an API key: `Authorization: Bearer <key>` (OpenAI, Vertex AI) or `x-goog-api-key: <key>` (Gemini API, Vertex AI express mode).

## Limitations and considerations

- Stateless: the Responses API `previous_response_id` and `conversation` are not supported, clients must send the whole conversation (`store: false` style).
- Text only (no images, audio or files) and only function tools (no built in tools like web search or code execution).
- OpenAI: `n` must be 1, logprobs are not supported.
- Google: `candidateCount` must be 1, `countTokens`, caching, embeddings, structured output (`responseSchema`) and stop sequences are not supported, the API key query parameter (`?key=`) is not supported (use the `x-goog-api-key` header).
- Vertex AI using Google credentials (OAuth access tokens) can't be used, use an API key (express mode) or a Bearer token known by the server.
- The AI service must implement `IAiService.Complete` natively for tools and token streaming (LLamaSharp does), the default implementation (OpenAI and Google services) sends the conversation as a transcript, ignores caller defined tools and returns the response in a single update.

## Using it

```json
{ "Type": "SysWeaver.AI.HostService, SysWeaver.AI.HostService", "Params": { "Instances": ["local"], "Auth": "ai" } }
```
```csharp
//  OpenAI
var openAi = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri("https://myserver/openai/v1") });
var chat = openAi.GetChatClient("qwen3-8b");
//  Google Gemini API (or vertexAI: true for Vertex AI express mode)
var google = new Google.GenAI.Client(apiKey: apiKey, httpOptions: new HttpOptions { BaseUrl = "https://myserver/google/" });
var r = await google.Models.GenerateContentAsync("qwen3-8b", "Hello");
```

## Relationships

- **Project:** [`SysWeaver.AI.HostService.csproj`](SysWeaver.AI.HostService.csproj)
- **Builds on:** [SysWeaver.AI](../SysWeaver.AI/README.md)
