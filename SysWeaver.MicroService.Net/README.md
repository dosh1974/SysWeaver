# SysWeaver.MicroService.Net

[⬆ SysWeaver overview](../README.md)

> SMTP e-mail sending service implementing the framework's e-mail contract.

| | |
|---|---|
| **Layer** | Networking |
| **Kind** | Micro service |

## Purpose

Provide `IEmailService` so that features such as account verification, password reset and invitations (see [SysWeaver.MicroService.UserManager](../SysWeaver.MicroService.UserManager/README.md)) can send e-mail without knowing how.

## How it fits into SysWeaver

```mermaid
flowchart LR
  UM["UserManager and other services"] -->|IEmailService| SMTP["SmtpEmailService"]
  SMTP --> Server["SMTP server"]
```

## Key features

- SMTP with TLS, credentials and retries.

## Limitations and considerations

- Plain SMTP relay only; no queueing, templates or delivery tracking (templates live in the services that send mail).

## Using it

```json
{ "Type": "SysWeaver.MicroService.SmtpEmailService, SysWeaver.MicroService.Net",
  "Params": { "Server": "smtp.example.com" } }
```

## Relationships

- **Project:** [`SysWeaver.MicroService.Net.csproj`](SysWeaver.MicroService.Net.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.MicroServices](../SysWeaver.MicroServices/README.md)
