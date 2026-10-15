# Db

[⬆ SysWeaver.Chat.MySql](../README.md)

> The database row classes of the MySQL chat provider: the room table and the per room message tables.

| | |
|---|---|
| **Project** | [SysWeaver.Chat.MySql](../README.md) |
| **Kind** | Database rows |

## Purpose

Internal classes mapped to MySQL tables through SimpleStack.Orm attributes (via [SysWeaver.DbSimpleStack.MySql](../../SysWeaver.DbSimpleStack.MySql/README.md)). [`MySqlChat`](../MySqlChat.cs) initialises the `Rooms` table when the service starts, and creates one message table per room on demand, named `Room_<room name>`, all using the `DbChatMessage` layout.

## Contents

| Item | Description |
|---|---|
| [`DbChatRoom.cs`](DbChatRoom.cs) | Row of the `Rooms` table: auto increment id, room name (ASCII, max 64, indexed), created and last used time |
| [`DbChatMessage.cs`](DbChatMessage.cs) | Row of a room's message table: sender name and image url, time, text (max 4096), data links, format and language |

## Relationships

- **Part of:** [SysWeaver.Chat.MySql](../README.md)
- **Used by:** [`MySqlChat.cs`](../MySqlChat.cs)
- **Uses:** [SysWeaver.DbSimpleStack.MySql](../../SysWeaver.DbSimpleStack.MySql/README.md)
