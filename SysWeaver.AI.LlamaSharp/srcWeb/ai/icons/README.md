# icons

[⬆ ai](../README.md)

> The agent icon of the LlamaSharp provider, shown as the image of the AI in its chats.

| | |
|---|---|
| **Project** | [SysWeaver.AI.LlamaSharp](../../../README.md) |
| **Kind** | Icons |

## Purpose

The chat sessions of the provider pass `LlamaSharpAiService.ServiceName + ".svg"` (`"LlamaSharp.svg"`) as the agent icon to `AiChatSessionBase` in [SysWeaver.AI](../../../../SysWeaver.AI/README.md), which turns it into the url `../ai/icons/LlamaSharp.svg` (`AgentImageUrl`). The same url is returned by the `GetPredefinedImage` default tool for the agent logo.

At build time the file is optimised by SvgOpt into `.opt/`, Brotli compressed into `web/ai/icons/` and embedded (see [srcWeb](../../README.md)). The error, debug and working icons used by the same sessions come from [SysWeaver.AI](../../../../SysWeaver.AI/srcWeb/ai/icons/README.md).

## Contents

| Item | Description |
|---|---|
| [`LlamaSharp.svg`](LlamaSharp.svg) | Agent icon of the LlamaSharp provider |

## Relationships

- **Part of:** [SysWeaver.AI.LlamaSharp](../../../README.md)
- **Used by:** [`AiChatSessionBase`](../../../../SysWeaver.AI/AiChatSessionBase.cs) (agent image url)
- **Related:** [SysWeaver.AI/srcWeb/ai/icons](../../../../SysWeaver.AI/srcWeb/ai/icons/README.md)
