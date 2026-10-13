# SysWeaver.EmailReader.Pop3

[⬆ SysWeaver overview](../../README.md)

> Receive emails from a mailbox using POP3 (implements `IEmailReceiverService`).

| | |
|---|---|
| **Layer** | Users |
| **Kind** | Micro service |

## Purpose

Let a system receive emails (support requests, replies to sent emails etc) from any mailbox that supports POP3, without a public endpoint.

## How it fits into SysWeaver

```mermaid
flowchart LR
  App["App"] -->|"GetIncoming (long poll)"| S["Pop3EmailReaderService"]
  S -->|"check every PollInterval: UIDL, RETR, DELE"| P["POP3 server"]
```

## Key features

- Uses [MailKit](https://github.com/jstedfast/MailKit) (POP3 client and MIME parsing): TLS (`SslOnConnect`, `StartTls`, `StartTlsWhenAvailable`), multipart, encodings and charsets are handled.
- The mailbox is checked every `PollInterval` seconds, new emails are queued for `GetIncoming`.
- `DeleteAfterRead` (default true) deletes emails once they have been processed. The deletes are committed when the session ends, if that fails the emails are deleted (not processed again) on the next check. With `DeleteAfterRead` false, emails are left on the server and the unique ids (UIDL) of read emails are remembered in memory.
- The plain text body is used if an email have one (else the html body), set `PreferHtml` to use the html body when an email have both.
- `Addresses` maps receiver addresses (ex: aliases delivered to the same mailbox) to systems, matched against `Delivered-To`, `X-Original-To`, `Envelope-To`, `To` and `Cc`. Unmatched emails gets `System`.
- The `Message-Id` is used as the id (duplicates are ignored), `In-Reply-To` is kept.
- Emails that fails to process are left on the server (and not retried until the service restarts).
- Message table, stats and perf monitoring.

## Limitations and considerations

- POP3 only reads the inbox, and have no push: new emails are seen after up to `PollInterval` seconds.
- With `DeleteAfterRead` false, all emails on the server are read again after a restart (and on every check if the server doesn't support UIDL).
- Attachments are ignored, only the body is used.
- `GetIncoming` and `MessageTable` require the `Admin` or `Ops` role when called over http.
- Many providers require an app password, or have POP3 disabled by default (ex: Gmail, Outlook.com).

## Using it

```json
{ "Type": "SysWeaver.EmailReader.Pop3EmailReaderService, SysWeaver.EmailReader.Pop3",
  "Name": "SupportMail",
  "Params": {
    "CredFile": "$(KeyFolder)/SupportPop3.txt",
    "Server": "pop.example.com",
    "System": "support",
    "Addresses": [
      { "Address": "sales@example.com", "System": "sales" }
    ] } }
```

`SupportPop3.txt` contains a single line: `support@example.com:the-password`.

## Relationships

- **Project:** [`SysWeaver.EmailReader.Pop3.csproj`](SysWeaver.EmailReader.Pop3.csproj)
- **Builds on:** [SysWeaver.Common](../../SysWeaver.Common/README.md), [SysWeaver.MicroServices.ServiceManager](../../SysWeaver.MicroServices.ServiceManager/README.md), [SysWeaver.TableData](../../SysWeaver.TableData/README.md)
- **Alternative to:** [SysWeaver.EmailReader.Imap](../SysWeaver.EmailReader.Imap/README.md), [SysWeaver.Email.Fake](../SysWeaver.Email.Fake/README.md)
