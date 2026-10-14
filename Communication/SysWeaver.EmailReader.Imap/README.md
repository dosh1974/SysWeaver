# SysWeaver.EmailReader.Imap

[⬆ SysWeaver overview](../../README.md)

> Receives email from a mailbox using IMAP (with IDLE for instant delivery), implementing the framework's `IEmailReceiverService` contract.

| | |
|---|---|
| **Layer** | Networking |
| **Kind** | Micro service |

## Purpose

Let a system receive emails (support requests, replies to sent emails etc) from any mailbox that supports IMAP, without a public endpoint.

## How it fits into SysWeaver

```mermaid
flowchart LR
  App["App"] -->|"GetIncoming (long poll)"| S["ImapEmailReaderService"]
  S -->|"IDLE / check, SEARCH UNSEEN, FETCH, STORE \\Seen / MOVE / EXPUNGE"| I["IMAP server"]
```

Alternatives implementing the same contract: [SysWeaver.EmailReader.Pop3](../SysWeaver.EmailReader.Pop3/README.md) (POP3) and [SysWeaver.Email.Fake](../SysWeaver.Email.Fake/README.md) (development and testing).

## Key features

- Uses [MailKit](https://github.com/jstedfast/MailKit) (IMAP client and MIME parsing): TLS (`SslOnConnect`, `StartTls`, `StartTlsWhenAvailable`), multipart, encodings and charsets are handled.
- Keeps a connection open: with `UseIdle` (default, if the server supports IDLE) new emails are read as soon as they arrive, else the folder is checked every `PollInterval` seconds. Reconnects after `PollRetryDelay` seconds on failures.
- Reads unread (unseen) emails in `Folder` (default the inbox), without marking them as read while downloading. The state is kept on the server, so nothing is read twice after a restart.
- `AfterRead`: `MarkSeen` (default), `Delete` (only the processed emails are expunged if the server supports UIDPLUS), `Move` (to the `MoveTo` folder) or `Nothing` (testing only).
- The plain text body is used if an email has one (else the html body), set `PreferHtml` to use the html body when an email has both.
- `Addresses` maps receiver addresses (ex: aliases delivered to the same mailbox) to systems, matched against `Delivered-To`, `X-Original-To`, `Envelope-To`, `To` and `Cc`. Unmatched emails get `System`.
- The `Message-Id` is used as the id (duplicates are ignored), `In-Reply-To` is kept.
- Emails that fail to process are left as unread (and not retried until the service restarts).
- Message table, stats and perf monitoring.

## Key components

- [`ImapEmailReaderService`](ImapEmailReaderService.cs) - the connection loop, processing and the `IEmailReceiverService` implementation
- [`ImapEmailReaderParams`](ImapEmailReaderParams.cs) - server, security, folder, after read action and address to system mapping

## Using it

```json
{ "Type": "SysWeaver.EmailReader.ImapEmailReaderService, SysWeaver.EmailReader.Imap",
  "Name": "SupportMail",
  "Params": {
    "CredFile": "$(KeyFolder)/SupportImap.txt",
    "Server": "imap.example.com",
    "AfterRead": "Move",
    "MoveTo": "Processed",
    "System": "support",
    "Addresses": [
      { "Address": "sales@example.com", "System": "sales" }
    ] } }
```

`SupportImap.txt` contains a single line: `support@example.com:the-password`.

## Limitations and considerations

- Emails marked as read by someone else (ex: a person reading the mailbox in a mail client) are never seen by the service, use a dedicated mailbox (or folder).
- Without UIDPLUS, `Delete` expunges all emails flagged as deleted in the folder.
- Gmail archives emails instead of deleting them (unless configured otherwise).
- Attachments are ignored, only the body is used.
- Received emails are queued in memory only: emails that were read (and marked, moved or deleted) but not yet fetched using `GetIncoming` are lost if the service restarts.
- `GetIncoming` and `MessageTable` require the `Admin` or `Ops` role when called over http.
- Many providers require an app password, or OAuth2 (not supported, ex: Microsoft 365).

## Relationships

- **Project:** [`SysWeaver.EmailReader.Imap.csproj`](SysWeaver.EmailReader.Imap.csproj)
- **Builds on:** [SysWeaver.Common](../../SysWeaver.Common/README.md), [SysWeaver.MicroServices.ServiceManager](../../SysWeaver.MicroServices.ServiceManager/README.md), [SysWeaver.TableData](../../SysWeaver.TableData/README.md)
- **Third-party:** [MailKit](https://github.com/jstedfast/MailKit)
