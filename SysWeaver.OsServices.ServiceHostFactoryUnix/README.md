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

- Automatic detection of systemd (`systemd-notify --booted`, or PID 1 is `systemd`) vs. SysVinit (PID 1 is `init`).
- Generated systemd unit `/etc/systemd/system/[Name].service` (`Restart=always`, `RestartSec=60`, `ExecStart=[command] daemon`, `User=` the user running the install), enabled with `systemctl`.
- Generated LSB script `/etc/init.d/[Name]` using `start-stop-daemon`, registered with `update-rc.d`.
- Status, pause (SIGINT) and continue (SIGCONT) handled through a status/pid file `/var/run/[Name].service.pid` written by the daemon; SIGTERM, SIGHUP and SIGQUIT stop it.
- Elevated re-execution (`sudo`) for commands that need root.

| Type | Role |
|---|---|
| `ServiceHostFactoryUnix` | `IServiceHostFactory` found by name by `ServiceHost.Run` |
| `ServiceHostSystemD` / `ServiceHostSysVinit` | Internal `IServiceHost` implementations |
| `UnixHelpers` | `IsElevated` (euid 0), `RunElevated` (sudo), `SendSignal` (libc kill) |
- Elevated re-execution for commands that need root.

## Limitations and considerations

- Other init systems (OpenRC, runit, launchd on macOS, …) are not supported.
- The systemd unit's `User=` is the user name of the process performing the install; since installing requires root (re-run with `sudo`), this is normally `root`. Edit the unit to use another service account.
- The SysVinit script has no restart-on-failure, and `ServiceParams` start mode / restart settings are not used on Linux.
- The status file is only updated by the daemon; a daemon that is killed hard can leave a stale status until its pid no longer exists.

## Using it

Reference it from your executable and run `sudo ./MyService install`, then `sudo ./MyService start`.

## Relationships

- **Project:** [`SysWeaver.OsServices.ServiceHostFactoryUnix.csproj`](SysWeaver.OsServices.ServiceHostFactoryUnix.csproj)
- **Builds on:** [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md), [SysWeaver.OsServices](../SysWeaver.OsServices/README.md)
