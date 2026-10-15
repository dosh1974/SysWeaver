# WebDash

[⬆ SysWeaver.MicroServices](../README.md)

> Data types that describe a dashboard and its items.

| | |
|---|---|
| **Project** | [SysWeaver.MicroServices](../README.md) |
| **Kind** | Source code (data types) |

## Purpose

These types are exchanged with the `DashboardService` in [SysWeaver.MicroService.HttpServer](../../SysWeaver.MicroService.HttpServer/README.md). A dashboard is an id and a list of items. Each item has a type, a name and data: a URL for an embedded frame, or inline HTML.

## Contents

| Item | Description |
|---|---|
| [`WebDashItem.cs`](WebDashItem.cs) | `WebDashItem` (type, name, data) and `WebDashboard` (id and items) |
| [`WebDashTypes.cs`](WebDashTypes.cs) | Item kinds: `Frame` (embedded iframe) and `Http` (inline HTML code) |

## Relationships

- **Part of:** [SysWeaver.MicroServices](../README.md)
- **Used by:** `DashboardService` in [SysWeaver.MicroService.HttpServer](../../SysWeaver.MicroService.HttpServer/README.md)
