# SysWeaver.Auth.Core

[⬆ SysWeaver overview](../README.md)

> Authentication core: the `AuthManager` that validates users across any number of pluggable authorizers and caches Authorization header results, the authorizer contract, a configuration-based `SimpleAuthorizer` with API keys, password policies and localizable message templates.

| | |
|---|---|
| **Layer** | Security |
| **Kind** | Library |

## Purpose

Separate *who can log in and with which tokens* from the web server. The server only asks the `AuthManager`; the manager consults its authorizers (a static user list, a database-backed user manager, …) in order, and caches the results of HTTP `Authorization` headers.

## How it fits into SysWeaver

```mermaid
flowchart TB
  Http["HTTP server<br/>sessions and WebApiAuth checks"] --> AM["AuthManager<br/>header cache"]
  AM --> A1["SimpleAuthorizer<br/>users from config or file"]
  AM --> A2["UserManagerService<br/>MySQL users"]
  AM --> A3["other AuthorizerBase implementations"]
  A1 --> Keys["API keys<br/>Bearer / Basic Authorization header"]
  A1 --> KVS["KeyValueStore<br/>stored API keys"]
```

## Key concepts

| Concept | Description |
|---|---|
| **Authorization** | The result of a successful login (`Authorization`, extends `AuthorizationInfo`): user identity, guid, the set of lower case *tokens* (roles) that `[WebApiAuth]` checks against (`AuthExt.IsValid`), the authorizer and shared per-user data. `WeakMethod` tells if basic auth / a bearer token was used. |
| **Authorizer** | A source of users (`AuthorizerBase`). Several can be active; the manager asks them in turn. Supported methods: basic auth, bearer token, one-time-pad secure login, one-time tokens. Each has a unique guid prefix and a `ChangeCounter` used to invalidate cached results. |
| **Password hashing** | Clients compute `SHA256(password + "|" + salt)` with a salt from the authorizer (`AuthManager.GetSalt`); the secure login sends `SHA256(base64(hash) + "|" + oneTimePad)` so the password hash is never replayable (`AuthTools`). |
| **Realm** | The name sent in the `WWW-Authenticate` header for basic auth (defaults to the entry assembly name). |
| **Simple authorizer** | Users defined as `user:password[:tokens[:domain]]` strings (clear text or pre-computed hash) in parameters or a watched file; optional basic auth; API key management (stored in `KeyValueStore.AllApp`, usable as bearer tokens) with runtime-configurable permissions, plus a web page to generate password hashes. |
| **Password policy** | Length and character class requirements (`PasswordPolicy`, checked with `PasswordPolicyExt.Check`), reported to clients so the UI can validate early. |
| **Managed texts** | Localizable text/mail templates with variables (`ManagedMessages`, `ManagedTexts`, `ManagedTextTemplate`, …), loaded from per-language folders, embedded resources or literal text and reloaded when files change; used by account flows. |
| **Name generator** | `NameGen` creates random human friendly nick names (deterministic per user guid). |

## Key features

- Multiple authorizers behind one manager.
- Result caching to keep per-request Authorization header checks cheap.
- Client-side salted hashing support, constant time hash comparisons in `SimpleAuthorizer`.
- API keys for machine-to-machine access.

## Limitations and considerations

- `SimpleAuthorizer` is meant for small, static user sets (operators, services); use [SysWeaver.MicroService.UserManager](../SysWeaver.MicroService.UserManager/README.md) for self-service accounts.
- Clear-text passwords in configuration are supported but should be replaced with hashes, generated on the *Debug/SimpleAuth/Generate password hash* page (requires the debug or ops token).
- Password hashes are a single SHA256 (fast to brute force if leaked); keep user files and configuration private.
- API keys are stored in clear text in the application key/value store.
- Only successful Authorization header results are cached, for `CacheDuration` seconds (default 30) and at most `MaxCachedHeaders` (default 10 000) entries, keyed by a SHA256 hash of the header.
  Cached results are re-validated when the owning authorizer's `ChangeCounter` changes (`SimpleAuthorizer` increments it whenever users or API keys are updated).
- Authorization header schemes (`Basic`, `Bearer`, `*key`) are case insensitive. Basic credentials are UTF-8 + base64 encoded `user:password`.
- `AuthManager.UserHash` (secure login) doesn't validate the one-time pad itself; the caller (the login service) must issue and consume pads.
- Authorizers must have unique guid prefixes (`AddAuth` / the constructor throw an `ArgumentException`).
- `SimpleAuthorizer` user lines: user names are case insensitive (the last definition wins), `:` and `|` separate the parts (so passwords can't contain them),
  any 44 char base64 string of 32 bytes is taken as a hash, clear text passwords that fail the password policy are skipped (with a warning).
  Lines with a too long user name (over 128 chars) or domain (over 256) are skipped with a warning naming the user, the other users are still loaded.
  If a later update (file reload, API key change) fails, an error is logged and the previous users are kept.
  When users change (file reload, API key removal) both the normal and the weak (basic auth / API key) authorizations of changed or removed users get a logout request.
- Per-user data (`Authorization.GetOrCreate`, `Set`, ...) is shared by all `Authorization` instances of a user from the same authorizer, a reference is taken on first use and released by `Dispose`.
- Managed texts: a string that is an existing file is read from the file, else (with a type) an embedded resource with that name (case insensitive, optionally prefixed with the namespace of the type;
  a compressed resource is also found by the name without the compression extension, ex: `web.auth.index.html` finds `SysWeaver.Auth.web.auth.index.html.br`),
  else the string itself is the text. A missing file therefore silently becomes the literal text (ex: the file path).
  Only existing files are monitored for changes (the first change triggers a reload), a file created later is not picked up.

## Known bugs

- security-storage-auth-10: password hashes are a single fast SHA256 (see above).

## Using it

```json
[
  { "Type": "SysWeaver.Auth.SimpleAuthorizer, SysWeaver.Auth.Core",
    "Params": { "Users": [ "admin:<hash-or-password>:Admin, Ops" ] } },
  { "Type": "SysWeaver.MicroService.AuthManagerService, SysWeaver.MicroService.Auth" }
]
```

## Relationships

- **Project:** [`SysWeaver.Auth.Core.csproj`](SysWeaver.Auth.Core.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.Compression](../SysWeaver.Compression/README.md), [SysWeaver.MicroServices](../SysWeaver.MicroServices/README.md), [SysWeaver.Storage](../SysWeaver.Storage/README.md), [SysWeaver.TableData](../SysWeaver.TableData/README.md)
- **Used by:** [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md), [SysWeaver.MicroService.AspHttpServer](../SysWeaver.MicroService.AspHttpServer/README.md), [SysWeaver.MicroService.Auth](../SysWeaver.MicroService.Auth/README.md), [SysWeaver.MicroService.HttpServer](../SysWeaver.MicroService.HttpServer/README.md), [SysWeaver.MicroService.NetHttpServer](../SysWeaver.MicroService.NetHttpServer/README.md), [SysWeaver.MicroService.UserStorage](../SysWeaver.MicroService.UserStorage/README.md)
