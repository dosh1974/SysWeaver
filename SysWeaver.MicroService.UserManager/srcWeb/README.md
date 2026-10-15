# srcWeb

[⬆ SysWeaver.MicroService.UserManager](../README.md)

> Source of the embedded account management pages.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.UserManager](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

This folder only contains the `auth/` sub folder. The `EmbedLocal` target in [`SysWeaver.MicroService.UserManager.csproj`](../SysWeaver.MicroService.UserManager.csproj) runs `Compress.exe -Methods br .\srcWeb\*+ .\web` before each build. The generated `web/**/*.br` files (not in git) are embedded as resources and served as `auth/...` relative to the site root.

## Contents

| Item | Description |
|---|---|
| [`auth/`](auth/README.md) | Account pages (sign-up, passwords, e-mail, phone, invite, delete account) and their scripts |

## Relationships

- **Part of:** [SysWeaver.MicroService.UserManager](../README.md)
