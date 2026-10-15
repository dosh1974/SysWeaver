# auth

[⬆ srcWeb](../README.md)

> The browser side of login and logout: the two pages, the request helpers that talk to `LoginService`, and a small UI toolkit for building auth pages.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Auth](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Served under `auth/`. `Login.html` and `Logout.html` are empty shells that load the scripts and call `loginMain()` / `logoutMain()` from `AuthUi.js`, which build the page at run time. Other projects add their own files to the same `auth/` URL folder and reuse these scripts: [SysWeaver.MicroService.PassKey](../../../SysWeaver.MicroService.PassKey/README.md) provides `Passkey.js` / `Passkey.css` (referenced by `Login.html`; the passkey options only appear when that script is loaded and the browser supports passkeys), and [SysWeaver.MicroService.UserManager](../../../SysWeaver.MicroService.UserManager/README.md) and [SysWeaver.Auth.Core](../../../SysWeaver.Auth.Core/README.md) build their pages with the `Auth*` helpers.

Processing (SVG optimisation, Brotli compression, embedding) is described in [`srcWeb/`](../README.md).

## Contents

| Item | Description |
|---|---|
| [`icons/`](icons/README.md) | SVG icons used by the auth services |
| [`Login.html`](Login.html) | Sign in page (`auth/Login.html`), calls `loginMain()` |
| [`Logout.html`](Logout.html) | Sign out page (`auth/Logout.html`), calls `logoutMain()` |
| [`Auth.js`](Auth.js) | Requests: `loginRequest` (salt and one-time pad from `GetUserSalt`, hashed login, fallback to basic auth), `logoutRequest`, password policy text and validation, e-mail and phone number validation |
| [`AuthUi.js`](AuthUi.js) | `loginMain` / `logoutMain` and the `Auth*` building blocks (page frame, labels, inputs, password and phone inputs, tabs, buttons, password generator `GenPassword`) |
| [`Auth.css`](Auth.css) | Styles of the auth pages and their controls |

## Relationships

- **Part of:** [srcWeb](../README.md), [SysWeaver.MicroService.Auth](../../README.md)
- **Talks to:** [`LoginService`](../../LoginService.cs) and [`AuthManagerService`](../../AuthManagerService.cs) web APIs (`Api/auth/...`)
- **Used by:** [SysWeaver.MicroService.PassKey](../../../SysWeaver.MicroService.PassKey/README.md), [SysWeaver.MicroService.UserManager](../../../SysWeaver.MicroService.UserManager/README.md), [SysWeaver.Auth.Core](../../../SysWeaver.Auth.Core/README.md) (`SimpleAuth.js`)
