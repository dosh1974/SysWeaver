# SysWeaver.TextMessage.Fake

[⬆ SysWeaver overview](../../README.md)

> A fake text message provider for development and testing: "sent" messages are kept in memory, delivery, failures and incoming messages can be simulated.

| | |
|---|---|
| **Layer** | Users (test double) |
| **Kind** | Micro service |

## Purpose

Exercise phone verification and other SMS flows without a real SMS gateway.

## Key features

- Implements the full text message contract: `ITextMessageSender`, `ITextMessageReceiverService`, `ITextMessageStatusService` and `ITextMessageEvents`.
- Simulates any channel (`Channel`, default SMS), so two instances can stand in for WhatsApp + SMS behind the [router](../SysWeaver.TextMessage.Router/README.md).
- Simulated delivery: sent messages get `Sent` and `Delivered` status updates (`AutoDeliver`, `StatusDelay`).
- Simulated failures: numbers starting with `NotOnChannelPrefixes` fail with `NotOnChannel`, either in the send result or as a later status update (`FailAsync`, like WhatsApp does).
- `FakeIncoming` simulates a received message, `FakeStatus` a status update, `MessageTable` lists everything.

## Limitations and considerations

- Development only: its endpoints declare no authentication of their own, so on a server with anonymous API defaults message contents would be public.
- Nothing is actually delivered.

## Relationships

- **Project:** [`SysWeaver.TextMessage.Fake.csproj`](SysWeaver.TextMessage.Fake.csproj)
- **Builds on:** [SysWeaver.Common](../../SysWeaver.Common/README.md), [SysWeaver.IsoData](../../SysWeaver.IsoData/README.md), [SysWeaver.MicroServices.ServiceManager](../../SysWeaver.MicroServices.ServiceManager/README.md), [SysWeaver.TableData](../../SysWeaver.TableData/README.md)
- **Used with:** [SysWeaver.TextMessage.Router](../SysWeaver.TextMessage.Router/README.md)
