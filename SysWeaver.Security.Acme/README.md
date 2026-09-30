# SysWeaver.Security.Acme

[⬆ SysWeaver overview](../README.md)

> Automatic public certificates via ACME (Let's Encrypt by default), validated through HTTP challenges served by SysWeaver itself.

| | |
|---|---|
| **Layer** | Security |
| **Kind** | Certificate provider |

## Purpose

Obtain and renew browser-trusted certificates for public domains with no manual steps.

## How it fits into SysWeaver

```mermaid
sequenceDiagram
  participant P as AcmeCertificateProvider
  participant CA as ACME CA
  participant W as SysWeaver HTTP on port 80
  P->>CA: order certificate for domain
  CA-->>P: HTTP challenge
  P->>W: publish challenge file
  CA->>W: fetch challenge over HTTP
  CA-->>P: certificate issued
  P->>P: cache and schedule renewal
```

## Limitations and considerations

- The domain must resolve to the server and port 80 must be reachable from the internet.
- Subject to the CA's rate limits; certificates are cached to avoid unnecessary orders.

## Using it

```json
{ "Type": "SysWeaver.Security.AcmeCertificateProvider, SysWeaver.Security.Acme",
  "Params": { "DomainName": "www.example.com" } }
```

## Relationships

- **Project:** [`SysWeaver.Security.Acme.csproj`](SysWeaver.Security.Acme.csproj)
- **Builds on:** [SysWeaver.NetHttpServer](../SysWeaver.NetHttpServer/README.md), [SysWeaver.Security](../SysWeaver.Security/README.md)
- **Used by:** [SysWeaver.MicroService.LanCertificateManager](../SysWeaver.MicroService.LanCertificateManager/README.md)
