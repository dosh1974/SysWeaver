# Services

[⬆ SysWeaver.HttpServer](../README.md)

> Service contracts for user images and per-user file storage, defined here so the web layer and other services can use them without depending on the implementing projects.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer](../README.md) |
| **Kind** | Source code |

## Purpose

Only interfaces and small data types live here; the implementations are separate micro services that are found through the service manager at run time.

## Contents

| Item | Description |
|---|---|
| [`IUserImageHandler.cs`](IUserImageHandler.cs) | `IUserImageHandler` (supplies square user images in a set of fixed sizes, and deletes a user's images) and `IDefaultUserImageHandler` (fall-back used when no handler supplies an image); used by the auth manager to serve user images |
| [`IUserStorageService.cs`](IUserStorageService.cs) | `IUserStorageService`: store private, protected or public files and links for the request's user, read files, get scopes, list a user's files and links; plus `IChatStoreLinkHandler`, which services can implement to handle the chat's storing of links |
| [`IUserStoragePerUserHandler.cs`](IUserStoragePerUserHandler.cs) | Optional service returning a `UserStorageDataRetention` per user (e.g. by subscription level) |
| [`UserStorageDataRetention.cs`](UserStorageDataRetention.cs) | Retention policy: days to keep private, protected and public files after the last view, and the maximum storage per user in MB |
| [`UserStorageScopes.cs`](UserStorageScopes.cs) | `Private`, `Protected` (visible to other users with a link, optionally requiring auth tokens) and `Public` |

## Relationships

- **Part of:** [SysWeaver.HttpServer](../README.md)
- **Implemented by:** [SysWeaver.MicroService.UserStorage](../../SysWeaver.MicroService.UserStorage/README.md) (`IUserStorageService`), [SysWeaver.MicroService.CustomUserImage](../../SysWeaver.MicroService.CustomUserImage/README.md) (`IUserImageHandler`), [SysWeaver.MicroService.Avatar](../../SysWeaver.MicroService.Avatar/README.md) (`IDefaultUserImageHandler`)
- **Used by:** [SysWeaver.MicroService.Auth](../../SysWeaver.MicroService.Auth/README.md) (user images); [SysWeaver.AI](../../SysWeaver.AI/README.md), [SysWeaver.Chat](../../SysWeaver.Chat/README.md), [SysWeaver.MicroService.ChartJs](../../SysWeaver.MicroService.ChartJs/README.md) and [SysWeaver.MicroService.HttpServer](../../SysWeaver.MicroService.HttpServer/README.md) (user storage)
