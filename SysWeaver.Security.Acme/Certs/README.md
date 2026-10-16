# Certs

[⬆ SysWeaver.Security.Acme](../README.md)

> Embedded issuer certificates that are added to the certificate chain for specific ACME directories.

| | |
|---|---|
| **Project** | [SysWeaver.Security.Acme](../README.md) |
| **Kind** | Embedded data (PEM certificates) |

## Purpose

When no `ImportCertFiles` are configured, `AcmeCertificateProvider` takes the ACME directory URL, turns it into a safe file name (`PathExt.SafeFilename`) and looks for an embedded resource `Certs.<name>.pem`. If one exists, its certificate is added as an issuer to the `.pfx` it builds. The file is included with `<EmbeddedResource>` in [`SysWeaver.Security.Acme.csproj`](../SysWeaver.Security.Acme.csproj). There is no compression step.

To support another CA the same way, add a PEM named after its directory URL and include it as an embedded resource in the project file.

## Contents

| Item | Description |
|---|---|
| [`https___acme-staging-v02.api.letsencrypt.org_directory.pem`](https___acme-staging-v02.api.letsencrypt.org_directory.pem) | Let's Encrypt **staging** root `(STAGING) Bogus Broccoli X2` (self-signed, valid until 2040), used for the staging directory only |

## Limitations and considerations

- The certificate is a staging (test) root and is not trusted by browsers. It only completes the chain of staging certificates.

## Relationships

- **Part of:** [SysWeaver.Security.Acme](../README.md)
- **Used by:** [`AcmeCertificateProvider.cs`](../AcmeCertificateProvider.cs)
