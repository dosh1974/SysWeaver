# icons

[⬆ ai](../README.md)

> Icons shared by all AI chat sessions (error, debug and working indicators) plus the predefined smiley images, served as `../ai/icons/<name>.svg`.

| | |
|---|---|
| **Project** | [SysWeaver.AI](../../../README.md) |
| **Kind** | Icons |

## Purpose

`AiChatSessionBase` builds its image urls from `AiTools.IconRoot` (`"../ai/icons/"`): `ErrorImageUrl`, `DebugImageUrl` and `WorkingImageUrl` point to the three icons in this folder, while `AgentImageUrl` points to the agent icon that each provider project adds to the same url folder (e.g. `OpenAI.svg`).

The SVG files directly in this folder are optimised by SvgOpt before being compressed and embedded; the files in [`no_opt/`](no_opt/README.md) are compressed and embedded as they are (see [srcWeb](../../README.md)).

## Contents

| Item | Description |
|---|---|
| [`no_opt/`](no_opt/README.md) | Smiley images that are embedded without SVG optimisation |
| [`debug.svg`](debug.svg) | Sender image of debug messages in an AI chat (`DebugImageUrl`) |
| [`error.svg`](error.svg) | Sender image of error messages in an AI chat (`ErrorImageUrl`) |
| [`working.svg`](working.svg) | Animated placeholder shown in the reply message while the AI is working (`WorkingImageUrl`) |

## Relationships

- **Part of:** [SysWeaver.AI](../../../README.md)
- **Used by:** [`AiChatSessionBase.cs`](../../../AiChatSessionBase.cs)
- **Related:** the agent icons in [SysWeaver.AI.OpenAI](../../../../SysWeaver.AI.OpenAI/srcWeb/ai/icons/README.md), [SysWeaver.AI.Google](../../../../SysWeaver.AI.Google/srcWeb/ai/icons/README.md), [SysWeaver.AI.Anthropic](../../../../SysWeaver.AI.Anthropic/srcWeb/ai/icons/README.md) and [SysWeaver.AI.LlamaSharp](../../../../SysWeaver.AI.LlamaSharp/srcWeb/ai/icons/README.md)
