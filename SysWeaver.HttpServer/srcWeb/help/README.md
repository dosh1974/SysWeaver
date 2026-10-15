# help

[⬆ srcWeb](../README.md)

> Help text for the server's template system, served as `help/Templates.txt`.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer](../../README.md) |
| **Kind** | Embedded web assets |

## Contents

| Item | Description |
|---|---|
| [`Templates.txt`](Templates.txt) | How files are served as templates: template patterns per disc folder (wildcards, `$` regular expressions, `#` case insensitive), `${VarName}` variables, static vs dynamic templates, the value modifiers (`_`, `^`, `@`, `#`, `%`, `$`, `£`, `¤`, `~`, `*`) and the built-in variable groups (`Env.*`, `EnvInfo.*`, `Session.*`, `Server.*`) |

## Relationships

- **Part of:** [SysWeaver.HttpServer](../../README.md)
- **Describes:** the template handling in [`HttpServerBase`](../../HttpServerBase.cs) and [`FileHttpServerModule`](../../FileHttpServerModule.cs)
