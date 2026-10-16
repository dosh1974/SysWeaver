# ServiceManager

[⬆ SysWeaver.OsServices.ServiceHostFactoryWin32NT](../README.md)

> Internal interop with the Windows Service Control Manager (advapi32): `Win32ServiceManager` and the managed versions of the Win32 enums and structures it passes to the API.

| | |
|---|---|
| **Project** | [SysWeaver.OsServices.ServiceHostFactoryWin32NT](../README.md) |
| **Kind** | Source code (native interop) |

## Purpose

[`Win32ServiceManager`](Win32ServiceManager.cs) installs, uninstalls, configures, controls and queries Windows services through the advapi32 Service Control Manager API, using the Unicode (`...W`) entry points. Each call opens and closes its own SCM and service handles, success is taken from the API return values and errors from `Marshal.GetLastWin32Error`. Most operations need administrator rights. It is used by [`ServiceHostWindows`](../ServiceHostWindows.cs), which implements the install / control / status verbs of `ServiceHost`.

The other files are one-to-one managed declarations of Win32 constants and structures (`SERVICE_*`, `SC_MANAGER_*`, `SC_ACTION` and so on).

## Contents

| Item | Description |
|---|---|
| [`Win32ServiceManager.cs`](Win32ServiceManager.cs) | The P/Invoke declarations and all SCM operations (create, delete, configure description / start type / delayed auto start / failure actions, start, stop, pause, continue, status, dependencies) |
| [`AccessMask.cs`](AccessMask.cs) | Win32 generic, standard and object specific access rights (only `GENERIC_*` is used, to define the two rights enums) |
| [`ServiceManagerRights.cs`](ServiceManagerRights.cs) | SCM access rights (`SC_MANAGER_*`) |
| [`ServiceRights.cs`](ServiceRights.cs) | Service object access rights (`SERVICE_*`) |
| [`ServiceTypes.cs`](ServiceTypes.cs) | Service types (own process, shared process, drivers), plus `SERVICE_NO_CHANGE` |
| [`StartTypes.cs`](StartTypes.cs) | Service start types, plus `SERVICE_NO_CHANGE` |
| [`ServiceBootFlag.cs`](ServiceBootFlag.cs) | Start types as passed to `CreateService` (same values as `StartTypes`) |
| [`ServiceErrors.cs`](ServiceErrors.cs) | Error control values (severity of a start failure), plus `SERVICE_NO_CHANGE` |
| [`ServiceControl.cs`](ServiceControl.cs) | `SERVICE_CONTROL_*` codes sent with `ControlService` |
| [`ServiceStates.cs`](ServiceStates.cs) | Win32 current state values (as in `SERVICE_STATUS_PROCESS`) |
| [`ServiceState.cs`](ServiceState.cs) | The state reported by the manager: the Win32 states plus `Unknown` and `NotFound` |
| [`ServiceStatus.cs`](ServiceStatus.cs) | `SERVICE_STATUS` (filled in place by `QueryServiceStatus` / `ControlService`) |
| [`ServiceStatusProcess.cs`](ServiceStatusProcess.cs) | `SERVICE_STATUS_PROCESS` (marked as currently unused) |
| [`QueryServiceConfig.cs`](QueryServiceConfig.cs) | `QUERY_SERVICE_CONFIG` (returned by `QueryServiceConfig`, including the dependency list pointer) |
| [`ServiceDescription.cs`](ServiceDescription.cs) | `SERVICE_DESCRIPTION` |
| [`ServiceDelayedAutoStartInfo.cs`](ServiceDelayedAutoStartInfo.cs) | `SERVICE_DELAYED_AUTO_START_INFO` |
| [`ServiceFailureActions.cs`](ServiceFailureActions.cs) | `SERVICE_FAILURE_ACTIONS` (reset period, reboot message, command and the action array) |
| [`ScAction.cs`](ScAction.cs) | `SC_ACTION`, one failure action with its delay |
| [`ScActionTypes.cs`](ScActionTypes.cs) | `SC_ACTION_TYPE` values: none, reboot, restart, run command |

## Relationships

- **Part of:** [SysWeaver.OsServices.ServiceHostFactoryWin32NT](../README.md)
- **Used by:** [`ServiceHostWindows`](../ServiceHostWindows.cs)
- **Linux counterpart:** [SysWeaver.OsServices.ServiceHostFactoryUnix](../../SysWeaver.OsServices.ServiceHostFactoryUnix/README.md)
