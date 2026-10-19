# srcWeb

[⬆ SysWeaver.AI](../README.md)

> Source web assets of SysWeaver.AI: the shared icons used by all AI chats and by the predefined image tool, embedded into the assembly at build time.

| | |
|---|---|
| **Project** | [SysWeaver.AI](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

The `EmbedLocal` target in [`SysWeaver.AI.csproj`](../SysWeaver.AI.csproj) runs before every build:

1. the SVG files directly in `ai/icons/` are optimised with SvgOpt (`_tools/svg_opt`) into a temporary `ai/icons/.opt/` folder,
2. the optimised files are compressed with Brotli using Compress (`_tools/compress`) into `web/ai/icons/`,
3. the files in `ai/icons/no_opt/` are compressed into the same `web/ai/icons/` folder *without* going through SvgOpt,
4. every `web/**/*.br` file is embedded as a resource.

The generated `.opt/` and `web/` folders are not in the repository. The url root of these files is `../ai/` (`AiTools.WebRoot`), icons are referenced as `../ai/icons/<name>.svg` (`AiTools.IconRoot`). The AI provider projects add their agent icon to the same url folder.

## Contents

| Item | Description |
|---|---|
| [`ai/`](ai/README.md) | Web assets served under the `ai/` url root |

## Relationships

- **Part of:** [SysWeaver.AI](../README.md)
- **Uses:** [SvgOpt](../../_tools/svg_opt/README.md) and [Compress](../../_tools/compress/README.md) at build time
- **Related:** the agent icons in [SysWeaver.AI.OpenAI/srcWeb](../../SysWeaver.AI.OpenAI/srcWeb/README.md), [SysWeaver.AI.Google/srcWeb](../../SysWeaver.AI.Google/srcWeb/README.md), [SysWeaver.AI.Anthropic/srcWeb](../../SysWeaver.AI.Anthropic/srcWeb/README.md) and [SysWeaver.AI.LlamaSharp/srcWeb](../../SysWeaver.AI.LlamaSharp/srcWeb/README.md)
