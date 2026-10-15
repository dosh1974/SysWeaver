# srcWeb

[⬆ SysWeaver.Chat](../README.md)

> Source web assets of SysWeaver.Chat: the chat page, its script, styles and icons, embedded into the assembly at build time.

| | |
|---|---|
| **Project** | [SysWeaver.Chat](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

The `EmbedLocal` target in [`SysWeaver.Chat.csproj`](../SysWeaver.Chat.csproj) runs before every build:

1. the SVG files in `chat/icons/` are optimised with SvgOpt (`_tools/svg_opt`, with `-RemoveFill -RemoveSize`) into a temporary `chat/icons/.opt/` folder,
2. the files directly in `chat/` are compressed with Brotli using Compress (`_tools/compress`) into `web/chat/`,
3. the optimised icons are compressed into `web/chat/icons/`,
4. every `web/**/*.br` file is embedded as a resource.

The generated `.opt/` and `web/` folders are not in the repository.

## Contents

| Item | Description |
|---|---|
| [`chat/`](chat/README.md) | The web chat UI, served under `chat/` |

## Relationships

- **Part of:** [SysWeaver.Chat](../README.md)
- **Uses:** [SvgOpt](../../_tools/svg_opt/README.md) and [Compress](../../_tools/compress/README.md) at build time
