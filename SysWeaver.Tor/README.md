# SysWeaver.Tor

[⬆ SysWeaver overview](../README.md)

> An `HttpClient` that routes requests through the Tor network.

| | |
|---|---|
| **Layer** | Networking |
| **Kind** | Library |

## Purpose

Allow services to fetch remote content anonymously or from networks that block direct access. The HTTP file proxy of the web server can route through it, and the common library exposes a Tor service when the assembly is present.

## How it fits into SysWeaver

```mermaid
flowchart LR
  Svc["service or FileProxy"] --> TC["TorHttpClient"]
  TC --> Tor["local Tor and proxy processes<br/>managed by TorSharp"]
  Tor --> Net["Tor network"]
```

## Limitations and considerations

- Tor adds substantial latency and variable throughput.
- The underlying library downloads and runs Tor tooling, which requires network access and suitable permissions.

## Relationships

- **Project:** [`SysWeaver.Tor.csproj`](SysWeaver.Tor.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md)
