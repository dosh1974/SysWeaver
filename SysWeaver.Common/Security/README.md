# Security

[⬆ SysWeaver.Common](../README.md)

> Certificate helpers, the certificate provider and firewall contracts used by the http servers, and subject bound one time tokens.

| | |
|---|---|
| **Project** | [SysWeaver.Common](../README.md) |
| **Kind** | Source code (contracts and helpers, namespace `SysWeaver.Security`) |

## Purpose

The http servers need a TLS certificate and may need to open firewall ports for the ports they listen on; they get both through the contracts in this folder, implemented by other projects that are discovered at runtime. `CertificateTools` contains the X.509 helpers shared by those implementations.

## Contents

| Item | Description |
|---|---|
| [`CertificateTools.cs`](CertificateTools.cs) | Load certificates from disc or memory, expiration checks, subject alternative names, self signed check, installing into the local machine store, PEM to DER |
| [`ICertificateProvider.cs`](ICertificateProvider.cs) | Provides a certificate and an event when it changes |
| [`IFirewallHandler.cs`](IFirewallHandler.cs) | Adds and removes firewall rules, with the `FirewallProtcols` and `FirewallDirections` enums |
| [`SubjectTokens.cs`](SubjectTokens.cs) | **One live token per subject and purpose** (use it for every link or code sent to a person: reset links, invitations, confirmations). `SubjectTokens.Create(purpose, subject)` makes a token that starts with a tag of the subject and purpose (16 hex characters), so a database can revoke the older tokens with a "starts with" query (`SubjectTokens.Tag`); `OneTimeTokens<T>` is an in-memory store: `Issue(purpose, subject, data, out expires)` revokes the older token of the subject and purpose, `TryUse(token, purpose, subject, out data)` consumes it (once), `Revoke` / `RevokeSubject` (ex: after a password change), expiry. Used by the UserManager action tokens and the log delete token. |
| [`TokenGroups.cs`](TokenGroups.cs) | **Revocation by purpose group.** Declare once which events revoke which token purposes: `new TokenGroups().Group("Account", "PasswordChanged", "EmailChanged", ...).Purpose("RstPwd", "Account").Purpose("Invite")` (a purpose with no group is declared but never revoked by an event). `RevokedBy(evt)` gives the purposes to revoke (a database: delete by `SubjectTokens.Tag`), `OneTimeTokens<T>(lifetime, groups)` + `RevokeOn(evt, subject, keep)` does it in memory (and refuses an undeclared purpose). `Purposes` x `Events` + `IsRevokedBy` let a unit test generate the whole matrix, so a new token kind can't be forgotten. Used by the UserManager (`ActionTokenGroups`). |

## Relationships

- **Part of:** [SysWeaver.Common](../README.md)
- **Implemented by:** [SysWeaver.Security](../../SysWeaver.Security/README.md) and [SysWeaver.Security.Acme](../../SysWeaver.Security.Acme/README.md) (certificate providers), [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md) (Windows and Linux firewall handlers)
- **Used by:** the http servers
