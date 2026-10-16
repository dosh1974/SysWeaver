# SysWeaver.Chat.MySql

[⬆ SysWeaver overview](../README.md)

> Chat provider whose rooms and messages are persisted in MySQL.

| | |
|---|---|
| **Layer** | Chat |
| **Kind** | Micro service (chat provider) |

## Purpose

Durable chat history for [SysWeaver.Chat](../SysWeaver.Chat/README.md) that survives restarts.

## How it fits into SysWeaver

`MySqlChat` is an `IChatProvider`, so the chat service picks it up automatically and shows its rooms in the chat UI. Storage uses [SysWeaver.DbSimpleStack.MySql](../SysWeaver.DbSimpleStack.MySql/README.md): a table with the rooms and one message table per room, created on demand.

## Key features

- Rooms configured in the manifest (`Rooms`, `MySqlChatRoom` with the permissions and options of `ChatSessionParams`)
- Rooms supplied at runtime by other code through a room provider (`IMySqlRoomProvider`, registered with `AddRoomProvider`)
- Optional rate limiting per room and per session
- File uploads in rooms when a user storage service is available (`UploadRepo`, default `UserProtected`)

## Key components

- [`MySqlChat`](MySqlChat.cs) — the chat provider service
- [`MySqlChatRoom`](MySqlChatRoom.cs) — room parameters (permissions, rate limits)
- [`IMySqlRoomProvider`](IMySqlRoomProvider.cs) — contract for code that supplies rooms on demand

## Using it

```json
{ "Type": "SysWeaver.Chat.MySqlChat, SysWeaver.Chat.MySql",
  "Params": { "CredFile": "$(KeyFolder)/MySql.txt", "Rooms": [ { "Name": "Lobby", "Auth": "" } ] } }
```

## Limitations and considerations

- Requires a MySQL server (schema `Chat` by default)
- Room providers are not discovered automatically, they must be added in code

## Folder layout

| Folder | Content |
|---|---|
| [`Db/`](Db/README.md) | Database row classes for the room table and the per room message tables |

## Relationships

- **Project:** [`SysWeaver.Chat.MySql.csproj`](SysWeaver.Chat.MySql.csproj)
- **Builds on:** [SysWeaver.Chat](../SysWeaver.Chat/README.md), [SysWeaver.DbSimpleStack.MySql](../SysWeaver.DbSimpleStack.MySql/README.md)
