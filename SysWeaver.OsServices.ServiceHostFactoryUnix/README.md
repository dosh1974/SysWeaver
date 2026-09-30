# SysWeaver.OsServices.ServiceHostFactoryUnix

[⬆ SysWeaver overview](../README.md)

> Linux back-end for `ServiceHost`: installs the application as a systemd unit (or a SysVinit script on systems without systemd).

| | |
|---|---|
| **Layer** | Hosting (platform adapter) |
| **Kind** | Library, resolved by name at runtime |
| **Platform** | Linux |

## Purpose

Implements the service host contract of [SysWeaver.OsServices](../SysWeaver.OsServices/README.md) for Linux init systems.

## How it fits into SysWeaver

```mermaid
flowchart TB
  Run["ServiceHost.Run"] --> Detect{"which init system?"}
  Detect -->|systemd| SD["write unit file<br/>systemctl enable / start"]
  Detect -->|init (SysVinit)| SV["write /etc/init.d script"]
  Detect -->|other| Unknown["report unsupported"]
  SD --> Daemon["exe daemon"]
  SV --> Daemon
```

## Key features

- Automatic detection of systemd vs. SysVinit.
- Generated systemd unit that restarts the service automatically and runs it as the installing user.
- Elevated re-execution for commands that need root.

## Limitations and considerations

- Other init systems (OpenRC, runit, launchd on macOS, …) are not supported.
- The service runs as the user who performed the installation, so install with the intended service account.

## Using it

Reference it from your executable and run `sudo ./MyService install`, then `sudo ./MyService start`.

## Relationships

- **Project:** [`SysWeaver.OsServices.ServiceHostFactoryUnix.csproj`](SysWeaver.OsServices.ServiceHostFactoryUnix.csproj)
- **Builds on:** [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md), [SysWeaver.OsServices](../SysWeaver.OsServices/README.md)
