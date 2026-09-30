# SysWeaver.Chat.MySql

[⬆ SysWeaver overview](../README.md)

> Chat provider whose rooms and messages are persisted in MySQL.

| | |
|---|---|
| **Layer** | Collaboration |
| **Kind** | Micro service (chat provider) |

## Purpose

Durable chat history for [SysWeaver.Chat](../SysWeaver.Chat/README.md), surviving restarts and shareable between server instances. Other services can contribute rooms through a room provider contract.

## Limitations and considerations

- Requires MySQL via [SysWeaver.DbSimpleStack.MySql](../SysWeaver.DbSimpleStack.MySql/README.md).

## Relationships

- **Project:** [`SysWeaver.Chat.MySql.csproj`](SysWeaver.Chat.MySql.csproj)
- **Builds on:** [SysWeaver.Chat](../SysWeaver.Chat/README.md), [SysWeaver.DbSimpleStack.MySql](../SysWeaver.DbSimpleStack.MySql/README.md)
