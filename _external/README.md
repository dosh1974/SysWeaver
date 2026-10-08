# _external — vendored third-party libraries

[⬆ Back to the SysWeaver overview](../README.md)

Copies of open source libraries kept in the repository (each with its own upstream README and MIT license file) instead of being consumed as NuGet packages, which allows local modifications.

| Library | Used for | Used by |
|---|---|---|
| `simplestack.orm` | Lightweight ORM and SQL dialects; SysWeaver uses the core and the MySQL (MySqlConnector) dialect | [SysWeaver.DbSimpleStack](../SysWeaver.DbSimpleStack/README.md), [SysWeaver.DbSimpleStack.MySql](../SysWeaver.DbSimpleStack.MySql/README.md) |
| `fido2-net-lib` | No longer used (WebAuthn / FIDO2 server library), [SysWeaver.MicroService.PassKey](../SysWeaver.MicroService.PassKey/README.md) now uses the Fido2 NuGet package | - |

Only the parts SysWeaver uses are included in `SysWeaver.sln`; the rest of each library (other dialects, ASP.NET integrations, demos, tests) is carried along unused. Updating these libraries means merging upstream changes into the repository copy.
