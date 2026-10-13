# SysWeaver.OsServices.ServiceHostFactoryUnix

[⬆ SysWeaver overview](../../README.md)

> Linux back-end for `ServiceHost`: installs the application as a systemd unit (or a SysVinit script on systems without systemd).

| | |
|---|---|
| **Layer** | Hosting (platform adapter) |
| **Kind** | Library, resolved by name at runtime |
| **Platform** | Linux |

## Purpose

Implements the service host contract of [SysWeaver.OsServices](../../SysWeaver.OsServices/README.md) for Linux init systems.

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
- Generated systemd unit `/etc/systemd/system/[Name].service` (`Restart=always`, `RestartSec=60`, `ExecStart=[command] daemon`, `User=` the user running the install), enabled with `systemctl`. All values are escaped as described in systemd.syntax(7) / systemd.unit(5) / systemd.service(5): `%` becomes `%%` (specifiers), control characters / line breaks in `Description=` and `User=` become spaces, every `ExecStart=` word is double quoted when needed (white space, quotes, backslashes, control characters as C escapes), `$` in the arguments becomes `$$` (no variable substitution), a lone `;` argument is written `\;`. If the executable path contains a `$`, the `@` prefix passes argv[0] separately (the path itself isn't variable substituted, argv[0] is).
- Generated LSB script `/etc/init.d/[Name]` (`#!/bin/sh`) using `start-stop-daemon`, registered with `update-rc.d`. The name, display name, executable, argument and apphost entry `.dll` are single quoted shell variables (`NAME`, `DESC`, `DAEMON`, `DAEMON_ARGS`, `APP_DLL`), so paths with spaces and names with shell characters work; the description is only put in the LSB header. LSB exit codes: 0 = success (also start of a running and stop of a stopped service), 1 = start / stop / restart failed, 2 = unknown action, 5 = the executable, the application file it runs (`DAEMON_ARGS`, ex: the `.dll` run by `dotnet`) or, for an apphost install, the managed entry `.dll` it loads (`APP_DLL`: the entry assembly at install time; empty for a single-file / native AOT publish without a separate `.dll`) is missing (`stop` then exits with 0 and `status` with 4), `status` returns the LSB `pidofproc` result (0 = running, 1 = dead but the pid file exists, 3 = not running, 4 = unknown; called with quoted arguments, Debian's `status_of_proc` would split an executable path with spaces).
- Status, pause (SIGINT) and continue (SIGCONT) handled through a status/pid file `/var/run/[Name].service.pid` written by the daemon; SIGTERM, SIGHUP and SIGQUIT stop it. The recorded process is only trusted if it exists and (when `/proc/[pid]/cmdline` is readable) its last argument is `daemon` and one of its arguments has the file name (without extension) of this application; else the service is reported as stopped (a reused pid of a killed daemon isn't sent signals).
- systemd: the status of the status file is combined with the unit's `ActiveState` (`systemctl show`): a daemon that isn't alive while systemd is (re)starting the unit (ex: `activating (auto-restart)` after a crash, `Restart=always`) is reported as starting (153), while systemd stops it as stopping (154). `stop` stops the unit with `systemctl stop` unless systemd reports it inactive or failed, so a pending restart after a crash is cancelled. `uninstall` runs `systemctl disable --now`, removes the unit and the status file, then `systemctl daemon-reload` and `systemctl reset-failed`, so systemd can't restart a removed unit from memory. `install` runs `systemctl daemon-reload` before `systemctl enable`.
- Restarts requested by the daemon itself (`ServiceManager.RestartProcess`, the manifest auto-recovery) are done by `systemctl --no-block restart [Name]` under systemd (a child process of the daemon would be killed with the unit's processes when the unit stops; the daemon must run as root, the default `User=`); SysVinit starts a new process of the application with the `restart` / `start` verb.
- Elevated re-execution (`sudo`) for commands that need root; if `sudo` can't be started (ex: not installed) a message is written and the exit code is 1.

| Type | Role |
|---|---|
| `ServiceHostFactoryUnix` | `IServiceHostFactory` found by name by `ServiceHost.Run` |
| `ServiceHostSystemD` / `ServiceHostSysVinit` | Internal `IServiceHost` implementations |
| `UnixHelpers` | `IsElevated` (euid 0), `RunElevated` (sudo), `SendSignal` (libc kill), `Signals` (Linux signal numbers) |

## Limitations and considerations

- Other init systems (OpenRC, runit, launchd on macOS, …) are not supported.
- The systemd unit's `User=` is the user name of the process performing the install; since installing requires root (re-run with `sudo`), this is normally `root`. Edit the unit to use another service account.
- The SysVinit script has no restart-on-failure, and `ServiceParams` start mode / restart settings are not used on Linux.
- The status file is only updated by the daemon; a daemon that is killed hard leaves a stale status file, it's reported as stopped once its pid no longer exists or belongs to another program (under systemd as starting while the unit waits to restart it).
- If the init system can't be detected (neither systemd nor `init` as PID 1, or `ps` is missing, e.g. in minimal containers) the factory writes a message and returns null, so the service verbs are unavailable.
- The systemd unit's `Description=` is the display name (`ServiceParams.Description` isn't used); the SysVinit script puts the description in the LSB header comment.
- The SysVinit script handles `start`, `stop`, `status`, `restart`, `reload`, `force-reload` and `condrestart` (the last four restart).
- The systemd unit's executable path must be absolute and can't contain control characters, quotes, backslashes, `*`, `?` or `[` (systemd refuses such an executable path): install then fails with a message, without writing the unit.
- The systemd unit doesn't check that the application `.dll` exists (neither the `.dll` run by `dotnet` nor the entry `.dll` loaded by an apphost): if it was removed after installing, the process exits at once, so `start` reports a failed start, and systemd keeps retrying every 60 seconds (`Restart=always`; `status` then reports starting, 153) until the file is restored or the service is stopped.

## Using it

Reference it from your executable and run `sudo ./MyService install`, then `sudo ./MyService start`.

## Relationships

- **Project:** [`SysWeaver.OsServices.ServiceHostFactoryUnix.csproj`](SysWeaver.OsServices.ServiceHostFactoryUnix.csproj)
- **Builds on:** [SysWeaver.MicroServices.ServiceManager](../../SysWeaver.MicroServices.ServiceManager/README.md), [SysWeaver.OsServices](../../SysWeaver.OsServices/README.md)
