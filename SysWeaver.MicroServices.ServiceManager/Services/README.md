# Services

[⬆ SysWeaver.MicroServices.ServiceManager](../README.md)

> Two built-in micro services (CPU control and wait-for-network), plus the shared contracts and data types for text message and e-mail receiving providers.

| | |
|---|---|
| **Project** | [SysWeaver.MicroServices.ServiceManager](../README.md) |
| **Kind** | Source code |

## Purpose

Everything in this folder is in the `SysWeaver.MicroService` namespace. The two services can be added to any manifest, because every application references the service manager. The messaging interfaces live here too, so that providers (Twilio, Amazon, fake senders, IMAP/POP3 readers, routers) and their consumers share one contract without depending on each other.

## Contents

| Item | Description |
|---|---|
| [`CpuService.cs`](CpuService.cs) | Micro service that controls the process CPU usage: priority class, affinity mask or core slicing |
| [`CpuServiceParams.cs`](CpuServiceParams.cs) | Parameters of `CpuService` (e.g. `SliceCount`, `SliceIndex`) |
| [`NetworkService.cs`](NetworkService.cs) | Micro service that waits until the computer has a LAN IP address |
| [`NetworkServiceParams.cs`](NetworkServiceParams.cs) | Parameters of `NetworkService` |
| [`ITextMessageSender.cs`](ITextMessageSender.cs) | Interface for services that send text messages to a phone number |
| [`ITextMessageReceiverService.cs`](ITextMessageReceiverService.cs) | Interface for services that receive text messages (long polling with a cursor) |
| [`ITextMessageStatusService.cs`](ITextMessageStatusService.cs) | Interface for services that report the delivery status of sent text messages |
| [`ITextMessageEvents.cs`](ITextMessageEvents.cs) | Synchronous events for incoming messages and status changes (local instances only) |
| [`TextMessageQueue.cs`](TextMessageQueue.cs) | `TextMessageQueue<T>`: thread safe in-memory queue with long polling, used by providers to implement the receiver and status interfaces |
| [`TextMessageTypes.cs`](TextMessageTypes.cs) | Enums `TextMessageChannels`, `TextMessagePurpose`, `TextMessageStatuses` and related types |
| [`TextMessageExt.cs`](TextMessageExt.cs) | Extension methods: `Send`, `Reply`, `Failed`, `Allows` |
| [`OutgoingTextMessage.cs`](OutgoingTextMessage.cs) | A text message to send: phone number, text, system, allowed channels |
| [`IncomingTextMessage.cs`](IncomingTextMessage.cs) | A received text message |
| [`GetIncomingTextMessageRequest.cs`](GetIncomingTextMessageRequest.cs) | Request for incoming text messages after `LastId`, optionally for one `System` |
| [`IEmailReceiverService.cs`](IEmailReceiverService.cs) | Interface for services that receive e-mails |
| [`IncomingEmail.cs`](IncomingEmail.cs) | A received e-mail |
| [`GetIncomingEmailRequest.cs`](GetIncomingEmailRequest.cs) | Request for incoming e-mails after `LastId`, optionally for one `System` |

## Limitations and considerations

- The XML doc summary of `CpuService` ("Service that wait for the computer to get a LAN ip.") is copied from `NetworkService` and does not describe the class.

## Relationships

- **Part of:** [SysWeaver.MicroServices.ServiceManager](../README.md)
- **Implemented by:** text message providers such as [SysWeaver.TextMessage.Twilio](../../Communication/SysWeaver.TextMessage.Twilio/README.md), [SysWeaver.TextMessage.AmazonEndUserMessaging](../../Communication/SysWeaver.TextMessage.AmazonEndUserMessaging/README.md), [SysWeaver.TextMessage.Fake](../../Communication/SysWeaver.TextMessage.Fake/README.md), [SysWeaver.TextMessage.Router](../../Communication/SysWeaver.TextMessage.Router/README.md), [SysWeaver.TextMessage.WhatsApp](../../Communication/SysWeaver.TextMessage.WhatsApp/README.md), [SysWeaver.Email.Fake](../../Communication/SysWeaver.Email.Fake/README.md), and e-mail readers [SysWeaver.EmailReader.Imap](../../Communication/SysWeaver.EmailReader.Imap/README.md), [SysWeaver.EmailReader.Pop3](../../Communication/SysWeaver.EmailReader.Pop3/README.md)
- **Used by:** [SysWeaver.MicroService.UserManager](../../SysWeaver.MicroService.UserManager/README.md) (`ITextMessageSender` in `PhoneComs`)
