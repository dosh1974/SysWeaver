# _external — vendored third-party libraries

[⬆ SysWeaver overview](../README.md)

Copies of open source libraries kept in the repository (each with its own upstream README and MIT license file) instead of being consumed as NuGet packages, which allows local modifications.

| Library | Used for | Used by |
|---|---|---|
| [`simplestack.orm`](simplestack.orm/README.md) | Lightweight ORM and SQL dialects; SysWeaver uses the core and the MySQL (MySqlConnector) dialect | [SysWeaver.DbSimpleStack](../SysWeaver.DbSimpleStack/README.md), [SysWeaver.DbSimpleStack.MySql](../SysWeaver.DbSimpleStack.MySql/README.md) |
| `fido2-net-lib` | No longer used (WebAuthn / FIDO2 server library), [SysWeaver.MicroService.PassKey](../SysWeaver.MicroService.PassKey/README.md) now uses the Fido2 NuGet package | - |

Only the parts SysWeaver uses are included in `SysWeaver.slnx`; the rest of each library (other dialects, ASP.NET integrations, demos, tests) is carried along unused. Updating these libraries means merging upstream changes into the repository copy.

## Folder layout

The README at the root of a vendored library is the upstream one and is left untouched, so its sub folders are listed here.

| Folder | Content |
|---|---|
| [`simplestack.orm/src/`](simplestack.orm/src/README.md) | Library projects: the ORM core and its SQL dialects (only the core and the MySqlConnector dialect are used by SysWeaver) |
| [`simplestack.orm/test/`](simplestack.orm/test/README.md) | Upstream test projects, not part of `SysWeaver.slnx` |
| [`simplestack.orm/.vscode/`](simplestack.orm/.vscode/README.md) | Upstream VS Code settings |
