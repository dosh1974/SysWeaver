# SysWeaver.Email.Fake

[⬆ SysWeaver overview](../../README.md)

> A fake email provider for development and testing: "sent" emails are kept in memory and shown in a web inbox, failures and incoming emails can be simulated.

| | |
|---|---|
| **Layer** | Users (test double) |
| **Kind** | Micro service |

## Purpose

Exercise sign up, password reset and other email flows without a real email provider.

## Key features

- Implements `IEmailService` and `IEmailReceiverService`.
- Both plain text and html emails.
- Simulated failures: emails sent to addresses in `FailDomains` (sub domains included) fails (`Send` throws), the email is still listed as failed.
- `FakeIncoming` simulates an email from a user to the system (returned by `GetIncoming`), `SendEmail` sends one as the system.
- A standalone web inbox at `fakeEmail/index.html` (live updates, search, html rendered in a sandboxed iframe or as source, compose, reply, delete), no external libraries.
- `MessageTable` lists everything.

## Limitations and considerations

- Development only: its endpoints declare no authentication of their own, so on a server with anonymous API defaults email contents would be public.
- The API is placed at `Api/fakeEmail/`, so only one instance per server is supported.
- Nothing is actually delivered.

## Relationships

- **Project:** [`SysWeaver.Email.Fake.csproj`](SysWeaver.Email.Fake.csproj)
- **Builds on:** [SysWeaver.Common](../../SysWeaver.Common/README.md), [SysWeaver.MicroServices](../../SysWeaver.MicroServices/README.md), [SysWeaver.MicroServices.ServiceManager](../../SysWeaver.MicroServices.ServiceManager/README.md), [SysWeaver.TableData](../../SysWeaver.TableData/README.md)
