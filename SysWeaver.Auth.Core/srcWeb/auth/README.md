# auth

[⬆ srcWeb](../README.md)

> The web pages of `SimpleAuthorizer`: a generator for password hashes and the API key management page, served under `auth/`.

| | |
|---|---|
| **Project** | [SysWeaver.Auth.Core](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

[`SimpleAuthorizer`](../../SimpleAuthorizer.cs) adds two embedded menu entries (`WebMenuEmbedded`):

- `Debug/SimpleAuth/GenPwd`, *Generate password hash*: `auth/index.html` (tokens `debug,ops`)
- `Debug/SimpleAuth/KeyMan`, *API-key management*: `auth/KeyMan.html` (only shown when `CanManageKeys`)

The pages call the web APIs of `SimpleAuthorizer` (`Api/debug/simpleAuth/…` for the salt and password policy, `Api/simpleAuth/…` for API keys). They also load `Auth.js`, `AuthUi.js` and `Auth.css` from the same `auth/` url folder; those files are not in this project but in [SysWeaver.MicroService.Auth](../../../SysWeaver.MicroService.Auth/srcWeb/auth/README.md), so the pages rely on that project being part of the application. Common scripts and styles are loaded from `../common/` and `../app/`.

## Contents

| Item | Description |
|---|---|
| [`index.html`](index.html) | *Generate password hash* page (`pwdGenMain`) |
| [`KeyMan.html`](KeyMan.html) | *API key management* page (`keyManMain`) |
| [`SimpleAuth.js`](SimpleAuth.js) | Page logic: password hash generation using the user salt and password policy, listing, creating and removing API keys |
| [`SimpleAuth.css`](SimpleAuth.css) | Styles of both pages |

## Relationships

- **Part of:** [SysWeaver.Auth.Core](../../README.md)
- **Used by:** [`SimpleAuthorizer.cs`](../../SimpleAuthorizer.cs) (menu entries and web APIs)
- **Uses:** `Auth.js`, `AuthUi.js`, `Auth.css` of [SysWeaver.MicroService.Auth](../../../SysWeaver.MicroService.Auth/srcWeb/auth/README.md)
