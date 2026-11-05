# auth

[⬆ srcWeb](../README.md)

> The web pages of `SimpleAuthorizer`: a generator for password hashes and the API key management page, served under `auth/`.

| | |
|---|---|
| **Project** | [SysWeaver.Auth.Core](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Two developer / operator pages built with the auth page framework (`Sw.AuthUi`) of [SysWeaver.MicroService.Auth](../../../SysWeaver.MicroService.Auth/srcWeb/auth/README.md), which is merged into the same `auth/` url folder at run time.

## Contents

| Item | Description |
|---|---|
| [`index.html`](index.html) | "Generate password hash" (menu Debug/SimpleAuth/GenPwd, tokens debug, ops), calls `pwdGenMain()` |
| [`KeyMan.html`](KeyMan.html) | "API key management" (menu Debug/SimpleAuth/KeyMan, only when `CanManageKeys`), calls `keyManMain()` |
| [`SimpleAuth.js`](SimpleAuth.js) | The two pages (`Sw.SimpleAuth`) |
| [`SimpleAuth.css`](SimpleAuth.css) | The hash line and key value styles |

## Relationships

- **Part of:** [SysWeaver.Auth.Core](../../README.md)
- **Used by:** [`SimpleAuthorizer.cs`](../../SimpleAuthorizer.cs) (menu entries and web APIs)
- **Uses:** `Auth.js` (`Sw.Auth`), `AuthUi.js` (`Sw.AuthUi`), `Auth.css` of [SysWeaver.MicroService.Auth](../../../SysWeaver.MicroService.Auth/srcWeb/auth/README.md); `sw/core.js`, `sw/ui.js`, `sw/ui.css`, `common/theme.css` of SysWeaver.HttpServer

## Behavior

Includes: `../sw/core.js`, `../sw/ui.js`, `Auth.js`, `AuthUi.js`, `SimpleAuth.js`; `../common/theme.css`, `../sw/ui.css`, `Auth.css`, `SimpleAuth.css`.

### Password hash generator (`pwdGenMain`)

The card sits at the top of the page (`body.Top`, like KeyMan.html). Lock icon card with *, *Password* (show / hide, "generate" button using the server's fixed policy from `Api/debug/simpleAuth/GetPasswordPolicy`, "?" policy popup), a status line and the read-only *Generated hash* field with a copy button. Every change validates (empty user name: "Enter a valid username", only after the first input, so an untouched form shows no hint; policy errors), runs the leak check, fetches the salt once per user name (`Api/debug/simpleAuth/GetAuthSalt`, a failure shows a fail toast), shows "Computing.." and then `user : b64(sha256(password|salt))`, the line for the SimpleAuthorizer user configuration. Invalid input shows `-` (copy disabled). Newest computation wins.

### API keys (`keyManMain`)

`Api/simpleAuth/ApiKeysSupported` must be true (false: fail toast "Can't manage API keys for this service!", exception: "Not authorized to manage API keys!"; a warning icon). The card lists the keys of `GetApiKeys` (`"name:key"`): name, key value (monospace; masked with dots by default, a *Show the key* toggle (eye) reveals it, selectable only when shown) and icon buttons *copy* (`name:key`; the button confirms in place and the announcement is `The API key "name" copied` through `Sw.Clipboard.Copy` `What`, never the secret, with no toast; tool tips never contain the secret), *download* (`<name>_<AppName>.txt` with `# API credentials for accessing the <AppDisplayName> service at <baseUrl>` and the key line; baseUrl = page url up to `/auth/`, names from `AppInfo`), *remove* (`Sw.Dialog.Confirm` with the old texts: title "Remove key", "The key "<name>" will be removed! You won't be able to restore it with the same password, ever! Are you sure?", "Yes, remove it!" / "No, keep it!" with their old icons and tool tips; a danger dialog: "No, keep it!" has the focus, Enter and Escape never remove; `RemoveApiKey`; then the focus goes to the Remove button of the next key, else the previous one, else *Create new key*). *Create new key* opens a dialog with a *Name* field (max 64, filtered live to letters, digits, `_`, `-`; the dialog says so in a visible note, and left out characters are named at the field, ex: "Left out spaces, "!": a name may only have letters, digits, "_" and "-"."; Enter or the button submits, disabled with the reason "Enter the name of the service first" while empty); `AddApiKey` copies the new `name:key` to the clipboard (toast `Key "name" created and copied to the clipboard.`; removing a key closes its copy toast; null result: "Failed to create API-key!"). The list refreshes every 30 s and after changes, keeping unchanged items. The GETs (`ApiKeysSupported`, `AppInfo`, `GetApiKeys`) are plain requests as before (the server's cache headers apply, no cache bypass). A refused refresh (the role was taken away) shows the shared "not allowed" state once and the refresh goes on every 30 s as before; when a later refresh is allowed again the page loads again. The page keeps the default user change reloads (`KeepUserChanges`).

## JavaScript reference (`SimpleAuth.js`)

| Member | Description |
|---|---|
| `pwdGenMain()` / `keyManMain()` | Page entry points (globals; also `Sw.SimpleAuth.*`). |
| `Sw.SimpleAuth.isApiKeysSupported(onDenied)` | Support / authorization gate (fail toasts; a refused request calls `onDenied(error)` instead when given: KeyMan shows the shared "not allowed" state, `Sw.Page.ShowNotAllowed`, naming the user and the role). |
| `Sw.SimpleAuth.removeApiKey(name)` | Confirm and remove; true if removed. |
| `Sw.SimpleAuth.createApiKey()` | The create dialog; true if a key was created. |

Server: `Api/debug/simpleAuth/{GetAuthSalt, GetPasswordPolicy}`, `Api/simpleAuth/{ApiKeysSupported, AppInfo, GetApiKeys, AddApiKey, RemoveApiKey}`.

## CSS reference (`SimpleAuth.css`)

| Selector | Description |
|---|---|
| `.auth-field .sw-input.auth-hash` | The generated hash line (monospace, bold, accent color, select all). |
| `.auth-key` | API key value in the list (monospace, wraps anywhere, select all). |

Icon classes `IconLock`, `IconKey`, `IconCopy`, `IconDownload`, `IconRemove`, `IconAdd` come from `sw/ui.css`; the card, list and dialog styles from `Auth.css`.
