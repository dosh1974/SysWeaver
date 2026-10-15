# api

[⬆ SysWeaver.TextMessage.WhatsApp](../README.md)

> The WhatsApp Cloud API as used by this project: a `RemoteConnection` interface for the Graph API and the json types of its requests, responses and webhook payloads.

| | |
|---|---|
| **Project** | [SysWeaver.TextMessage.WhatsApp](../README.md) |
| **Kind** | Source code |

## Purpose

There is no official .NET SDK for the WhatsApp Cloud API, so the small part of it that the service needs is declared here. [`IWhatsAppCloudApi`](IWhatsAppCloudApi.cs) is an `IRemoteApi` that [SysWeaver.Remote.Connection](../../../SysWeaver.Remote.Connection/README.md) turns into HTTP calls (JSON serializer); the classes in the other two files map the Graph API json one to one. [`WhatsAppTextMessageService`](../WhatsAppTextMessageService.cs) sends through the interface, and [`WhatsAppTextMessageService_Webhook`](../WhatsAppTextMessageService_Webhook.cs) deserializes the webhook payloads pushed by Meta.

## Contents

| Item | Description |
|---|---|
| [`IWhatsAppCloudApi.cs`](IWhatsAppCloudApi.cs) | The remote API: `Send` and `MarkRead`, both a POST to `messages` of a phone number id |
| [`WaMessages.cs`](WaMessages.cs) | Outgoing types: send request (text, template with language, components and parameters, reply context), send response (contacts, message ids), read receipt request, success and error responses (`WaError`, `WaErrorData`) |
| [`WaWebhook.cs`](WaWebhook.cs) | Webhook types: the `WaWebhook` / entry / change / value envelope, metadata, incoming messages (text, button, interactive reply, media) and delivery statuses (`WaStatus`) |

## Relationships

- **Part of:** [SysWeaver.TextMessage.WhatsApp](../README.md)
- **Uses:** [SysWeaver.Remote.Connection](../../../SysWeaver.Remote.Connection/README.md)
- **Used by:** [`WhatsAppTextMessageService`](../WhatsAppTextMessageService.cs), [`WhatsAppTextMessageService_Webhook`](../WhatsAppTextMessageService_Webhook.cs), [`WhatsAppApiException`](../WhatsAppApiException.cs)
