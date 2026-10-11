# SysWeaver.Security

[⬆ SysWeaver overview](../README.md)

> Certificate providers for HTTPS: certificates from files, self-signed certificates, certificates signed by your own CA, and LAN certificates issued by a central SysWeaver certificate manager.

| | |
|---|---|
| **Layer** | Security |
| **Kind** | Services (`ICertificateProvider` implementations) |

## Purpose

Decouple where certificates come from and how they are renewed from the web server that uses them. HTTPS prefixes of the HTTP server services select a registered provider by name (or `"*"` for the first one), and the servers pick up renewals through the provider's `OnChanged` event.

## How it fits into SysWeaver

```mermaid
flowchart LR
  File["FileCertificateProvider<br/>pfx on disc, reload on change"] --> Srv["HTTP server<br/>https prefixes"]
  Self["SelfSignedCertificateProvider"] --> Srv
  Signed["SignedCertificateProvider<br/>signed by own CA"] --> Srv
  Lan["LanCertificateProvider"] --> Srv
  Acme["AcmeCertificateProvider<br/>Security.Acme"] --> Srv
  Lan -->|remote API| LCM["LanCertificateManager service"]
  Self --> Creator["SignedCertificateCreator"]
  Signed --> Creator
  Lan -.->|fallback| Creator
```

## Key types

| Type | Description |
|---|---|
| `FileCertificateProvider` | Loads a certificate from a `.pfx` (via `ManagedFile`, so the file is monitored); the password can be given directly or as the name of a file containing it. |
| `SelfSignedCertificateProvider` | Generates a self-signed RSA certificate, caches it as `.pfx` (plus a PEM `.crt`) and regenerates it when the parameters change or it is about to expire. |
| `SignedCertificateProvider` | Generates a certificate signed by a local root (CA) `.pfx`; can publish the public root certificate (`.pem`/`.crt`) on the web server (it is also an `IHttpServerModule`) so clients can install it. Monitors the root file. |
| `LanCertificateProvider` | Fetches the certificate for a LAN domain from a central `ILanCertificateManager` service (polls hourly); falls back to a self-signed certificate while the manager is unavailable. |
| `SignedCertificateCreator` | Builds the subject and subject alternative names (localhost, machine name, configured names, LAN IPs) and creates self-signed or CA-signed TLS server certificates. |
| `CertificateBaseParams` / `CertificateParams` / `CertificateProviderParams` | Shared parameters: cache file and password, subject fields, SAN options, key size, validity and renewal timing. |
| `ILanCertificateManager`, `GetLanCertRequest`, `GetLanCertResponse` | The remote API (`Api/Lcm/`) used by `LanCertificateProvider`. |

## Key features

- Several certificate strategies with a common contract (`ICertificateProvider` from SysWeaver.Common).
- Change notifications so servers rebind renewed certificates without restarts.
- Named providers so different prefixes can use different certificates.
- Generated certificates are cached between executions (default under `$(CommonApplicationData)\SysWeaver_AppData_$(AppName)`) and reused while they still match the configuration.

## Limitations and considerations

- Self-signed and privately signed certificates are only trusted by clients that trust the issuer.
- Providers must be registered before the HTTP server service.
- Generated certificates are RSA (SHA256) with a fixed 4 day back-dating; the cached `.pfx` password defaults to the application name, so protect the cache folder with file system permissions.
- Certificates are loaded with machine key storage (`CertificateTools.InMemoryKeyStorageFlags` = `MachineKeySet | Exportable`), which typically requires elevated rights on Windows. The key isn't persisted: the temporary key file in `%ProgramData%\Microsoft\Crypto\...\MachineKeys` is deleted when the certificate is disposed (so renewals and LAN polls no longer accumulate key files). `EphemeralKeySet` isn't used since SslStream (SChannel) can't use ephemeral keys on Windows. `CertificateTools.Install` (used by the HttpListener / http.sys binding) adds a copy with a persisted key to the machine store.
- With `IncludeLanIPs` enabled, a change of the local IP addresses causes a new certificate to be generated.
- A cached certificate is reused when it matches the parameters (same SAN's in the same order, and every non-empty subject field equal) and is valid for at least `MinValidHours`; the effective `MinValidHours` is at least the effective `RenewBeforeExpirationHours` (at least half of `MinValidHours`), so a certificate due for renewal is never reused. An empty or null `Filename` disables caching.
- Subject fields may not contain distinguished name special characters (`=`, `,`, `+`, `;`, `"`, `#`, `<`, `>`) or control characters, or start / end with white space (`ArgumentException` from the constructor, the parameter name is the field name), so every accepted value is used as is in the subject (and the cached certificate is reused). `Country` is a two letter ISO 3166 code or a country name / nick name (`SE`, `se`, `Sweden`, `UK`, looked up with `IsoCountry.TryGetName`, the ISO code is used), an unknown country throws an `ArgumentException`; null / empty uses the current region (no country if that's unknown).
- `SignedCertificateProvider` only reuses a cached certificate that was signed by the current root (the issuer name and the signature are verified with the root's public key, RSA PKCS#1 or ECDSA), so replacing the root with a new key but the same subject creates a new certificate. The root file is monitored (also when the cached certificate was reused): a change raises `OnChanged`. The published root (`.pem` / `.crt`) is the public certificate only.
- **Security**: the cached `.pfx` files are written with the default permissions of the folder (by default in `$(CommonApplicationData)`, readable by all local users on Windows and Linux) and the default password is the application name, so effectively any local user can read the private key (security-cov-7). Configure a protected folder and a real password.
- `LanCertificateProvider` keeps polling the manager after a failed renewal check (ex: the fallback certificate can't be written): the failure is reported to the message host and the next check is scheduled in 15 minutes; checks stop when the provider is disposed. Its self-signed fallback uses the configured `CommonName` / `Names`, not the LAN domain name.

## Known bugs

See `_docs/BugReview-2026-10-06.md`: security-cov-7.

## Using it

```json
[
  { "Type": "SysWeaver.Security.SelfSignedCertificateProvider, SysWeaver.Security" },
  { "Type": "SysWeaver.MicroService.AspHttpServerService, SysWeaver.MicroService.AspHttpServer",
    "Params": { "ListenOn": [ { "Prefix": "https://*:8443", "Certificate": "*" } ] } }
]
```

## Relationships

- **Project:** [`SysWeaver.Security.csproj`](SysWeaver.Security.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md), [SysWeaver.IsoData](../SysWeaver.IsoData/README.md), [SysWeaver.Remote.Connection](../SysWeaver.Remote.Connection/README.md)
- **Used by:** [SysWeaver.Security.Acme](../SysWeaver.Security.Acme/README.md)
