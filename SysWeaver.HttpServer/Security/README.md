# Security

[⬆ SysWeaver.HttpServer](../README.md)

> Firewall handlers that open the listen ports of the server on Windows and Linux.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer](../README.md) |
| **Kind** | Source code |

## Purpose

[`HttpServerBase`](../HttpServerBase.cs) takes an `IFirewallHandler` (the interface lives in [SysWeaver.Common](../../SysWeaver.Common/Security/IFirewallHandler.cs)) and uses it to add allow rules for the configured listen prefixes. This folder holds the implementations. `FirewallHandler.Instance` picks the handler for the current platform: `WindowsFirewallHandler` on Windows, `LinuxFirewallHandler` on Linux and `NoFirewallHandler` elsewhere (e.g. macOS), where every operation logs a warning and fails.

## Contents

| Item | Description |
|---|---|
| [`WindowsFirewallHandler.cs`](WindowsFirewallHandler.cs) | `WindowsFirewallHandler` (rules through `netsh advfirewall`, needs elevated rights), plus `NoFirewallHandler` and the platform-selecting `FirewallHandler` |
| [`LinuxFirewallHandler.cs`](LinuxFirewallHandler.cs) | `LinuxFirewallHandler`, supporting ufw, firewalld and plain nftables; needs root, never uses `sudo`, does nothing when no firewall is active and never touches rules it didn't add |
| [`LinuxFirewallDetection.cs`](LinuxFirewallDetection.cs) | Detection of the installed and active Linux firewall (`LinuxFirewallKinds`, `LinuxFirewallStates`, `LinuxFirewallInfo`, `LinuxFirewallDetection`) and the `ILinuxFirewallSystem` abstraction of command execution and file access (real implementation `LinuxFirewallSystem`), which allows the handler to be tested without a real system |

## Limitations and considerations

- iptables-only Linux systems aren't supported, and nftables rules added by the handler are runtime only.

## Relationships

- **Part of:** [SysWeaver.HttpServer](../README.md)
- **Implements:** `IFirewallHandler` from [SysWeaver.Common](../../SysWeaver.Common/README.md)
- **Used by:** [`HttpServerBase.cs`](../HttpServerBase.cs) and the listener projects ([SysWeaver.NetHttpServer](../../SysWeaver.NetHttpServer/README.md), [SysWeaver.AspHttpServer](../../SysWeaver.AspHttpServer/README.md))
