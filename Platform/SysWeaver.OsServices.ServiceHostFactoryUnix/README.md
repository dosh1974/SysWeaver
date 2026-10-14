# SysWeaver.OsServices.ServiceHostFactoryUnix

[⬆ SysWeaver overview](../../README.md)

> Linux back-end for `ServiceHost`: installs the application as a systemd unit (or a SysVinit script on systems without systemd).

| | |
|---|---|
| **Layer** | Runtime |
| **Kind** | Library (platform adapter, resolved by name at runtime) |
| **Platform** | Linux |

## Purpose

Implements the service host contract (`IServiceHostFactory` / `IServiceHost`) of [SysWeaver.OsServices](../../SysWeaver.OsServices/README.md) for Linux init systems. `ServiceHost.Run` finds it by name (`SysWeaver.OsServices.ServiceHostFactoryUnix`), so the executable only has to reference and deploy it.

## How it fits into SysWeaver

```mermaid
flowchart TB
  Run["ServiceHost.Run"] --> Detect{"which init system?"}
  Detect -->|systemd| SD["write unit file<br/>systemctl enable / start"]
  Detect -->|init (SysVinit)| SV["write /etc/init.d script<br/>update-rc.d"]
  Detect -->|other| Unknown["report unsupported"]
  SD --> Daemon["exe daemon"]
  SV --> Daemon
```

## Key features

- Automatic detection of systemd (`systemd-notify --booted`, or PID 1 is `systemd`) vs. SysVinit (PID 1 is `init`)
- **systemd:** generates `/etc/systemd/system/[Name].service` (`Restart=always`, `RestartSec=60`, `ExecStart=[command] daemon`, `User=` the user running the install) with values escaped per systemd's syntax rules, and controls it with `systemctl`; the unit's `ActiveState` is combined with the daemon's own status so a pending auto-restart is reported as starting, and `stop` / `uninstall` cancel pending restarts
- **SysVinit:** generates an LSB script `/etc/init.d/[Name]` using `start-stop-daemon`, registered with `update-rc.d`, with LSB exit codes
- Status, pause (SIGINT) and continue (SIGCONT) through a status/pid file `/var/run/[Name].service.pid` written by the daemon; SIGTERM, SIGHUP and SIGQUIT stop it. The recorded pid is only trusted if the process still looks like this application's daemon
- Restarts requested by the daemon itself (`ServiceManager.RestartProcess`, the manifest auto-recovery) use `systemctl --no-block restart [Name]` under systemd; under SysVinit a new process of the application is started
- Elevated re-execution through `sudo` for commands that need root; if `sudo` can't be started a message is written and the exit code is 1

## Key components

- [`ServiceHostFactoryUnix`](ServiceHostFactoryUnix.cs) — the `IServiceHostFactory`, detects the init system
- [`ServiceHostSystemD`](ServiceHostSystemd.cs) / [`ServiceHostSysVinit`](ServiceHostSysVinit.cs) — internal `IServiceHost` implementations
- [`UnixHelpers`](UnixHelpers.cs) — `IsElevated` (euid 0), `RunElevated` (sudo), `SendSignal` (libc `kill`)

## Using it

Reference it (and [SysWeaver.OsServices](../../SysWeaver.OsServices/README.md)) from your executable, then:

```text
sudo ./MyService install
sudo ./MyService start
./MyService status
```

## Limitations and considerations

- Other init systems (OpenRC, runit, launchd on macOS, …) are not supported; if the init system can't be detected (or `ps` is missing, e.g. in minimal containers) the factory writes a message and returns null, so the service verbs are unavailable.
- The systemd unit's `User=` is the user performing the install; since installing requires root this is normally `root`. Edit the unit to use another service account (daemon-initiated restarts under systemd require root).
- The SysVinit script has no restart-on-failure, and the `ServiceParams` start mode / restart settings are not used on Linux.
- The systemd unit's `Description=` is the display name (`ServiceParams.Description` isn't used); the SysVinit script puts the description in its LSB header comment.
- The systemd unit's executable path must be absolute and can't contain control characters, quotes, backslashes, `*`, `?` or `[`; install then fails with a message.
- A daemon that is killed hard leaves a stale status file; it is reported as stopped once its pid no longer exists or belongs to another program (under systemd as starting while the unit waits to restart it).

## Relationships

- **Project:** [`SysWeaver.OsServices.ServiceHostFactoryUnix.csproj`](SysWeaver.OsServices.ServiceHostFactoryUnix.csproj)
- **Builds on:** [SysWeaver.MicroServices.ServiceManager](../../SysWeaver.MicroServices.ServiceManager/README.md), [SysWeaver.OsServices](../../SysWeaver.OsServices/README.md)
- **Used by:** services [ExchangeRateService](https://github.com/dosh1974/SysWeaver.Services/blob/main/ExchangeRateService/README.md), [FakeEmailService](https://github.com/dosh1974/SysWeaver.Services/blob/main/FakeEmailService/README.md), [FakeTextService](https://github.com/dosh1974/SysWeaver.Services/blob/main/FakeTextService/README.md), [IpLocationService](https://github.com/dosh1974/SysWeaver.Services/blob/main/IpLocationService/README.md), [SwServerManager](https://github.com/dosh1974/SysWeaver.Services/blob/main/SwServerManager/README.md), [WebServerService](https://github.com/dosh1974/SysWeaver.Services/blob/main/WebServerService/README.md)
