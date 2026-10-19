# srcWeb

[⬆ SysWeaver.AI.Anthropic](../README.md)

> Source web assets of SysWeaver.AI.Anthropic: the agent icon shown for its AI chats, embedded into the assembly at build time.

| | |
|---|---|
| **Project** | [SysWeaver.AI.Anthropic](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

The `EmbedLocal` target in [`SysWeaver.AI.Anthropic.csproj`](../SysWeaver.AI.Anthropic.csproj) runs before every build: it optimises the SVG files in `ai/icons/` with SvgOpt (`_tools/svg_opt`) into a temporary `ai/icons/.opt/` folder, compresses the result with Brotli using Compress (`_tools/compress`) into `web/ai/icons/` and embeds every `web/**/*.br` file as a resource. The generated `.opt/` and `web/` folders are not in the repository.

The files are served under `ai/`, the same url root as the shared icons of [SysWeaver.AI](../../SysWeaver.AI/srcWeb/README.md).

## Contents

| Item | Description |
|---|---|
| [`ai/`](ai/README.md) | Web assets served under the `ai/` url root |

## Relationships

- **Part of:** [SysWeaver.AI.Anthropic](../README.md)
- **Uses:** [SvgOpt](../../_tools/svg_opt/README.md) and [Compress](../../_tools/compress/README.md) at build time
- **Related:** [SysWeaver.AI/srcWeb](../../SysWeaver.AI/srcWeb/README.md) (shared AI icons under the same url root)
