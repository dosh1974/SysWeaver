# Communication

[⬆ SysWeaver overview](../README.md)

> Groups the e-mail sender, e-mail reader and text message provider projects: interchangeable implementations of the framework's messaging contracts.

| | |
|---|---|
| **Repository** | [SysWeaver](../README.md) |
| **Kind** | Group of projects |

## Purpose

Each project implements one or more of the messaging contracts defined in the core projects: `IEmailSender` ([SysWeaver.MicroServices](../SysWeaver.MicroServices/README.md)), `IEmailReceiverService` and `ITextMessageSender` and the related text message interfaces ([SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md)). An application picks a provider by adding it to its manifest; the fake providers stand in for the real ones during development and testing. The folder itself only holds the project folders.

## Contents

| Item | Description |
|---|---|
| [`SysWeaver.Email.AmazonSes/`](SysWeaver.Email.AmazonSes/README.md) | Sends email through Amazon SES (Simple Email Service), implementing the framework's `IEmailSender` contract. |
| [`SysWeaver.Email.Fake/`](SysWeaver.Email.Fake/README.md) | A fake email provider for development and testing: "sent" emails are kept in memory and shown in a web inbox, failures and incoming emails can be simulated. |
| [`SysWeaver.Email.MicrosoftAcs/`](SysWeaver.Email.MicrosoftAcs/README.md) | Sends email through Azure Communication Services (ACS), implementing the framework's `IEmailSender` contract. |
| [`SysWeaver.Email.Smtp/`](SysWeaver.Email.Smtp/README.md) | Sends email through any SMTP server, implementing the framework's `IEmailSender` contract. |
| [`SysWeaver.EmailReader.Imap/`](SysWeaver.EmailReader.Imap/README.md) | Receives email from a mailbox using IMAP (with IDLE for instant delivery), implementing the framework's `IEmailReceiverService` contract. |
| [`SysWeaver.EmailReader.Pop3/`](SysWeaver.EmailReader.Pop3/README.md) | Receives email from a mailbox using POP3, implementing the framework's `IEmailReceiverService` contract. |
| [`SysWeaver.TextMessage.AmazonEndUserMessaging/`](SysWeaver.TextMessage.AmazonEndUserMessaging/README.md) | Send and receive SMS using AWS End User Messaging SMS (formerly Pinpoint SMS), with delivery events and two-way SMS read from SQS. |
| [`SysWeaver.TextMessage.Fake/`](SysWeaver.TextMessage.Fake/README.md) | A fake text message provider for development and testing: "sent" messages are kept in memory, delivery, failures and incoming messages can be simulated. |
| [`SysWeaver.TextMessage.Router/`](SysWeaver.TextMessage.Router/README.md) | Combines text message providers (ex: WhatsApp and SMS): routing by channel, country and system, fallback when a provider can't deliver, and merged incoming messages and status updates. |
| [`SysWeaver.TextMessage.Twilio/`](SysWeaver.TextMessage.Twilio/README.md) | Send and receive text messages (SMS, MMS, WhatsApp, RCS) using Twilio Programmable Messaging on multiple senders. |
| [`SysWeaver.TextMessage.WhatsApp/`](SysWeaver.TextMessage.WhatsApp/README.md) | Send and receive text messages using the WhatsApp Cloud API on multiple business numbers (combine with SMS using the router). |

## Relationships

- **Part of:** [SysWeaver](../README.md)
- **Contracts from:** [SysWeaver.MicroServices](../SysWeaver.MicroServices/README.md), [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md)
- **Used by:** in SysWeaver.Services, [FakeEmailService](https://github.com/dosh1974/SysWeaver.Services/blob/main/FakeEmailService/README.md) and [FakeTextService](https://github.com/dosh1974/SysWeaver.Services/blob/main/FakeTextService/README.md) host the fake providers
