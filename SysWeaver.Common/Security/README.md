# Security

[⬆ SysWeaver.Common](../README.md)

> Certificate helpers and the certificate provider and firewall contracts used by the http servers.

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

## Relationships

- **Part of:** [SysWeaver.Common](../README.md)
- **Implemented by:** [SysWeaver.Security](../../SysWeaver.Security/README.md) and [SysWeaver.Security.Acme](../../SysWeaver.Security.Acme/README.md) (certificate providers), [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md) (Windows and Linux firewall handlers)
- **Used by:** the http servers
