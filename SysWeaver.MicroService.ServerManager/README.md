# SysWeaver.MicroService.ServerManager

[⬆ SysWeaver overview](../README.md)

> Remote server administration for SysWeaver hosts: manage deployed SysWeaver services and folders (versions, backups, configs), watch processes, CPU, memory and drives, view and edit text files and the hosts file, manage key files, and reboot.

| | |
|---|---|
| **Layer** | Administration |
| **Kind** | Micro service |

## Purpose

Operate a fleet of SysWeaver machines from the browser: deploy new service versions through folder sync, keep backups and configurations, and monitor the host. It is the core of the [SwServerManager](https://github.com/dosh1974/SysWeaver.Services/blob/main/SwServerManager/README.md) application.

## How it fits into SysWeaver

```mermaid
flowchart TB
  UI["server manager pages<br/>server, services, keys"] --> SMS["ServerManagerService"]
  SMS --> FSS["FolderSyncService<br/>managed service folders"]
  SMS --> FU["FileUploaderService<br/>key and config uploads"]
  SMS --> Charts["ChartJs<br/>CPU, memory, drive history"]
  SMS --> OS["OsServices verbs<br/>start, stop, restart, uninstall"]
```

## Key features

- **Managed services**: each SysWeaver service is a managed folder (pushed with `SwSyncTool`); versions can be activated, compressed, expanded, verified and deleted; backups are kept for `RemoveServiceBackupsDays`
- Service control by running the service host verbs (start, stop, pause, continue, restart, uninstall, kill) and per-service CPU and memory charts
- Master configuration files copied into new versions, config and log file viewing, editing and deletion
- Additional managed folders (`Folders`) and chunk storage statistics
- Host monitoring: CPU, memory and drive charts with history, process table with per-process charts and kill (Kill and Reboot need a token that is only sent with the process list / `GetServerInfo` (`SmProcessKillToken`: HMAC, random key per process, bound to the process start time, valid at least 15 minutes), so a crafted link or an image GET can't use them; the idle and system processes and the server manager itself can't be killed)
- Configurable text files (`TextFiles`) and the hosts file (`AllowHosts`, `AllowHostsEdit`) viewable/editable in the browser
- Key file management for the key folder, and reboot of the machine
- Row actions give a clear outcome (never a bare false): `KillProcess` answers 403 with the reason (token missing / expired, a process that can't be killed) or 409 (the process has already ended, its id now belongs to another process); `RemoveService` answers 409 when the service is not managed (any more)

## Key components

- [`ServerManagerService`](ServerManagerService.cs) — the micro service and all its APIs
- [`ServerManagerParams`](ServerManagerParams.cs) — service folder, managed services and folders, text files, hosts access
- [`ManagedService`](ManagedService.cs) — configuration of one managed service

## Using it

```json
{
  "Type": "SysWeaver.MicroService.ServerManagerService, SysWeaver.MicroService.ServerManager",
  "Params": {
    "SyncAuth": "Admin",
    "Services": [ { "Name": "MyService" } ]
  }
}
```

`FolderSyncService`, `FileUploaderService` and the `FileHttpServerModule` must be registered before it. Services can also be added and removed at runtime (persisted in the app key-value store).

## Limitations and considerations

- Powerful operations (kill processes, reboot, edit hosts and key files) — restrict to administrators; most host APIs require `AdminOps`.
- The per service APIs (service verbs, charts, details, versions, configs, logs) declare the union of the roles of all managed services (`ManagedService.SyncAuth`, default `ServerManagerParams.SyncAuth`, through `IRunTimeWebApiAuth`), so users without any of them get 403 before the call and `Api/auth/CanCall` reports it; the per service check still applies and refuses with 403 (an unknown service is 404), not 500. A service added at run time with a new role is only usable by users with one of the declared roles until a restart.
- Managed services are expected to be SysWeaver service hosts ([SysWeaver.OsServices](../SysWeaver.OsServices/README.md)) so the service verbs work.

## Folder layout

| Folder | Content |
|---|---|
| [`srcWeb/`](srcWeb/README.md) | Embedded server manager pages (server, services, service/version details, metrics, key files) |

## Relationships

- **Project:** [`SysWeaver.MicroService.ServerManager.csproj`](SysWeaver.MicroService.ServerManager.csproj)
- **Builds on:** [SysWeaver.MicroService.ChartJs](../SysWeaver.MicroService.ChartJs/README.md), [SysWeaver.MicroService.FileUploader](../SysWeaver.MicroService.FileUploader/README.md), [SysWeaver.MicroService.FolderSync](../SysWeaver.MicroService.FolderSync/README.md), [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md), [SysWeaver.OsServices](../SysWeaver.OsServices/README.md)
- **Used by:** [SwServerManager](https://github.com/dosh1974/SysWeaver.Services/blob/main/SwServerManager/README.md) (SysWeaver.Services)
