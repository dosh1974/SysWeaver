# icons

[⬆ chat](../README.md)

> Monochrome icons of the chat UI, used as CSS masks by `chat.css`.

| | |
|---|---|
| **Project** | [SysWeaver.Chat](../../../README.md) |
| **Kind** | Icons |

## Purpose

At build time the icons are optimised by SvgOpt with `-RemoveFill -RemoveSize` (so they can be coloured by the theme through `mask-image`), Brotli compressed into `web/chat/icons/` and embedded (see [srcWeb](../../README.md)). They are referenced from [`chat.css`](../chat.css) as `icons/<name>.svg`; general icons such as send, menu and help come from the shared `../icons/` folder instead.

## Contents

| Item | Description |
|---|---|
| [`ChatFormatMD.svg`](ChatFormatMD.svg) | `.IconChatMD`: Markdown message format |
| [`ChatFormatText.svg`](ChatFormatText.svg) | `.IconChatText`: plain text message format |
| [`ChatHidePreview.svg`](ChatHidePreview.svg) | `.IconChatHidePreview`: hide the preview |
| [`ChatShowPreview.svg`](ChatShowPreview.svg) | `.IconChatShowPreview`: show the preview |
| [`ChatInput.svg`](ChatInput.svg) | Speech input button of the chat input, `IconDisabled` state |
| [`ChatRecording.svg`](ChatRecording.svg) | Speech input button of the chat input, `IconWorking` state |

## Relationships

- **Part of:** [SysWeaver.Chat](../../../README.md)
- **Used by:** [`chat.css`](../chat.css)
