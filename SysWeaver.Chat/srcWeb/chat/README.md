# chat

[⬆ srcWeb](../README.md)

> The web chat UI of SysWeaver.Chat: the chat page, its script and styles, served under `chat/`.

| | |
|---|---|
| **Project** | [SysWeaver.Chat](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

`Chat.html` is the single page that shows any chat of any provider (simple rooms, MySQL rooms, AI chats). All logic lives in `chat.js`, which talks to the web APIs of [`ChatService`](../../ChatService.cs) and receives new messages, edits and removals through the HTTP server's session push messages. The page builds on the shared scripts and styles of the HTTP server (`../common/`, `../app/`) and uses images served by other projects (`../icons/`, the `../iso_data/language/` flags, the `../auth/UserImages/` user images).

The page can be embedded in an iframe and controlled with `postMessage` (e.g. `chat.clear`, `chat.scrolltop`, `chat.showall`), which `ChatTest.html` demonstrates.

## Contents

| Item | Description |
|---|---|
| [`icons/`](icons/README.md) | Chat specific mask icons (speech input, text format, preview) |
| [`Chat.html`](Chat.html) | The chat page (`chatMain`) |
| [`ChatTest.html`](ChatTest.html) | Test page that hosts `Chat.html?id=Test` in an iframe, with buttons that send `postMessage` commands |
| [`chat.js`](chat.js) | The chat client: message list, input, menus, speech input, translation, file and link sharing |
| [`chat.css`](chat.css) | Styles of the chat UI, including the `IconChat…` icon classes |

## Relationships

- **Part of:** [SysWeaver.Chat](../../README.md)
- **Uses:** [`ChatService.cs`](../../ChatService.cs) (web APIs and push messages), the common web assets of [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/README.md)
