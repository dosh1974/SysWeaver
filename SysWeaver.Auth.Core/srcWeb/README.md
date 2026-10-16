# srcWeb

[⬆ SysWeaver.Auth.Core](../README.md)

> Source web assets of SysWeaver.Auth.Core: the debug pages of `SimpleAuthorizer`, embedded into the assembly at build time.

| | |
|---|---|
| **Project** | [SysWeaver.Auth.Core](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

The `EmbedLocal` target in [`SysWeaver.Auth.Core.csproj`](../SysWeaver.Auth.Core.csproj) runs `Compress.exe -Methods br .\srcWeb\*+ .\web` before every build: everything below this folder is compressed with Brotli (`_tools/compress`) into the generated `web/` folder, keeping the folder structure, and every `web/**/*.br` file is embedded as a resource. The `web/` folder is not in the repository.

## Contents

| Item | Description |
|---|---|
| [`auth/`](auth/README.md) | Password hash generator and API key management pages, served under `auth/` |

## Relationships

- **Part of:** [SysWeaver.Auth.Core](../README.md)
- **Uses:** [Compress](../../_tools/compress/README.md) at build time
