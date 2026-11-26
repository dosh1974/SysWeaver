# auth

[⬆ srcWeb](../README.md)

> The web pages of `SimpleAuthorizer`: a generator for password verifiers (`$s1$...`), the API key management page and the stored users page, served under `auth/`.

| | |
|---|---|
| **Project** | [SysWeaver.Auth.Core](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Three developer / operator pages built with the auth page framework (`Sw.AuthUi`) of [SysWeaver.MicroService.Auth](../../../SysWeaver.MicroService.Auth/srcWeb/auth/README.md), which is merged into the same `auth/` url folder at run time.

## Contents

| Item | Description |
|---|---|
| [`index.html`](index.html) | "Generate password hash" (menu Admin/GenPwd, role ops), calls `pwdGenMain()` |
| [`KeyMan.html`](KeyMan.html) | "API key management" (menu Admin/KeyMan "API-key management", only when `CanManageKeys`), calls `keyManMain()` |
| [`UserMan.html`](UserMan.html) | "Stored users" (menu Admin/UserMan, only when `CanManageUsers`: the "admin" or "debug" token), calls `userManMain()` |
| [`cards.html`](cards.html) | Not a page: the card template (`user`) of the "Built-in users" table (`SimpleAuthorizer.UsersTable`, `[TableDataCard("+user@auth/cards")]`) |
| [`SimpleAuth.js`](SimpleAuth.js) | The three pages (`Sw.SimpleAuth`) |
| [`SimpleAuth.css`](SimpleAuth.css) | The hash generator form, the key and user lists and the key value styles |

## Relationships

- **Part of:** [SysWeaver.Auth.Core](../../README.md)
- **Used by:** [`SimpleAuthorizer.cs`](../../SimpleAuthorizer.cs) (menu entries), [`SimpleAuthorizer_ApiKeys.cs`](../../SimpleAuthorizer_ApiKeys.cs) and [`SimpleAuthorizer_StoredUsers.cs`](../../SimpleAuthorizer_StoredUsers.cs) (web APIs)
- **Uses:** `Auth.js` (`Sw.Auth`), `AuthUi.js` (`Sw.AuthUi`), `Auth.css` of [SysWeaver.MicroService.Auth](../../../SysWeaver.MicroService.Auth/srcWeb/auth/README.md); `sw/core.js`, `sw/ui.js`, `sw/ui.css`, `common/theme.css` of SysWeaver.HttpServer

## Behavior

Includes: `../sw/core.js`, `../sw/ui.js`, `Auth.js`, `AuthUi.js`, `SimpleAuth.js`; `../common/theme.css`, `../sw/ui.css`, `Auth.css`, `SimpleAuth.css`.

### Password hash generator (`pwdGenMain`)

A form page (`Sw.PageLayout` type "form", `body.Top`) with two cards. The *User* card has *Username* and *Password* (`Sw.TextBox`, show / hide), the rules of the server's fixed policy (`Api/debug/simpleAuth/GetPasswordPolicy`) listed under the password, a *Generate password* button (fills in a random password that meets the policy and copies it to the clipboard) and the leak check as a badge next to it. `Sw.Form` shows the messages under the fields ("Enter a user name.", "Enter a password.", policy errors), only for fields the user touched. The *Configuration line* card (sub title "user : hash", a *Copy* action in its head) shows "Computing.." and then `user : $s1$iterations$salt$storedKey`, the line for the SimpleAuthorizer user configuration (and for *Add user* on the stored users page): a SCRAM verifier (see `ScramVerifier` in [`Scram.cs`](../../Scram.cs)) of `b64(sha256(password|salt))` with a random 16 byte salt, computed in the page (PBKDF2, `Sw.Auth.Pbkdf2`), which can't be used to sign in. The salt is fetched once per user name (`Api/debug/simpleAuth/GetAuthSalt`), the iteration count and binding string once (`Api/debug/simpleAuth/GetVerifierParams` -> `{Iterations, Binding}`, from `ScramIterations` / `AppHashString`); a failure shows a fail toast. While the input is invalid the line shows the "empty" token and *Copy* is disabled with the reason. The leak check runs for a valid password once a user name is entered, once per password. Newest computation wins. The page ignores user changes (`Sw.Session.IgnoreUserChanges`).

### API keys (`keyManMain`)

`Api/simpleAuth/ApiKeysSupported` must return the tokens of a new key (null: fail toast "Can't manage API keys for this service!", exception: "Not authorized to manage API keys!"; a warning icon). The page description warns that anyone who has a key can call the service. The card lists the keys of `GetApiKeys` (`{Name, Key, Tokens, Addresses}`, `Key` is `"name:key"`): name, key value (monospace; masked with dots by default, a *Show the key* toggle (eye) reveals it, selectable only when shown; screen readers hear "Key hidden" instead of the dots), a *Tokens* line (ex: "Tokens: service, service_translate"), an *Allowed from* line ("any address" when empty) and icon buttons:

- *copy* (`name:key`; the button confirms in place and the announcement is `The API key "name" copied` through `Sw.Clipboard.Copy` `What`, never the secret, with no toast; tool tips never contain the secret),
- *download* (accessible name `Download the credentials file of "name"`, the tool tip warns that the file holds the key as plain text; `<name>_<AppName>.txt` with `# API credentials for accessing the <AppDisplayName> service at <baseUrl>` and the key line; baseUrl = page url up to `/auth/`, names from `AppInfo`),
- *edit tokens* (`IconLock`, the "Tokens of "<name>"" dialog: comma separated, at least one, each `service` or `service_<scope>` with a scope of letters, digits, `_`, `-` and `.`, at most 64 chars; checked while typing, the reason at the field and *Save* disabled with it; saved with `SetApiKeyTokens {Name, Tokens}`, a refusal is shown at the field),
- *edit addresses* (`IconEdit`, the "Allowed addresses of "<name>"" dialog: a comma separated list of IP addresses or networks, ex: `10.0.0.5, 192.168.1.0/24`, empty = any address; saved with `SetApiKeyAddresses {Name, Addresses}`, a refusal is shown at the field),
- *remove* (`Sw.Dialog.Confirm`: title "Remove key", "The key "<name>" will be removed! Services and scripts that use it stop working at once, and a removed key can't be restored. Are you sure?", "Yes, remove it!" / "No, keep it!"; a danger dialog: "No, keep it!" has the focus, Enter and Escape never remove; `RemoveApiKey`; then the focus goes to the Remove button of the next key, else the previous one, else *Create new key*).

*Create new key* opens a dialog with a *Name* field (max 64, filtered live to letters, digits, `_`, `-`; the dialog says so in a visible note, and left out characters are named at the field, ex: "Left out spaces, "!": a name may only have letters, digits, "_" and "-".") and a *Tokens* field (prefilled with the tokens of `ApiKeysSupported`, checked as in the tokens dialog). Enter or the button submits; the button is disabled with the reason while the name is empty or the tokens are invalid. `AddApiKey {Name, Tokens}` returns the new `name:key`, which is copied to the clipboard (toast `Key "name" created and copied to the clipboard.`; removing a key closes its copy toast); a refusal (ex: the name exists) is a fail toast with the server's reason. The list refreshes every 30 s and after changes, keeping unchanged items (an item is rebuilt when its key, tokens or addresses change). The GETs (`ApiKeysSupported`, `AppInfo`, `GetApiKeys`) are plain requests (the server's cache headers apply, no cache bypass). A refused refresh (the role was taken away) shows the shared "not allowed" state once and the refresh goes on every 30 s; when a later refresh is allowed again the page loads again. The page keeps the default user change reloads (`KeepUserChanges`).

### Stored users (`userManMain`)

For users with the "admin" or "debug" token. `Api/simpleAuth/StoredUsersSupported` must be true (false: fail toast "Can't manage users for this service!", exception: "Not authorized to manage users!"; a warning icon). The page lists the stored users of `GetStoredUsers` (`{Name, Tokens, CanChange}`; the users of the configuration and the user file are not listed): name, a *Tokens* line ("none" for no tokens) and icon buttons *edit tokens* (`IconLock`, the "Tokens of "<name>"" dialog: comma separated, no spaces, `:` or `|` in a token, may be empty; saved with `SetStoredUserTokens {Name, Tokens}`, a refusal is shown at the field) and *remove* (a danger confirmation "Remove user", Cancel has the focus; `RemoveStoredUser`; then the focus goes to the Remove button of the next user, else the previous one, else *Add user*). When `CanChange` is false (the user has the "debug" token and the caller doesn't) both buttons are disabled with the reason "Only a user with the "debug" token may change this user".

*Add user* opens a dialog with a *User line* field (paste `username : $s1$...` from the *Generate password hash* page, linked in the dialog text; checked for a name and a `$s1$` verifier while typing) and a *Tokens* field; `AddStoredUser {Auth, Tokens}`, a refusal (ex: the name exists, the "debug" token given by a user without it) is a fail toast with the server's reason. The list refreshes every 30 s and after changes, keeping unchanged items; refused refreshes and user changes are handled as on the API keys page.

## JavaScript reference (`SimpleAuth.js`)

| Member | Description |
|---|---|
| `pwdGenMain()` / `keyManMain()` / `userManMain()` | Page entry points (globals; also `Sw.SimpleAuth.*`). |
| `Sw.SimpleAuth.isApiKeysSupported(onDenied)` | API key support / authorization gate: the tokens of a new key, or false (fail toasts; a refused request calls `onDenied(error)` instead when given: KeyMan shows the shared "not allowed" state, `Sw.Page.ShowNotAllowed`, naming the user and the role). |
| `Sw.SimpleAuth.isStoredUsersSupported(onDenied)` | The same gate for the stored users (true or false). |
| `Sw.SimpleAuth.removeApiKey(name)` | Confirm and remove an API key; true if removed. |
| `Sw.SimpleAuth.createApiKey(defaultTokens)` | The create key dialog; true if a key was created. |
| `Sw.SimpleAuth.removeStoredUser(name)` | Confirm and remove a stored user; true if removed. |
| `Sw.SimpleAuth.addStoredUser()` | The add user dialog; true if a user was added. |

Server: `Api/debug/simpleAuth/{GetAuthSalt, GetVerifierParams, GetPasswordPolicy}`, `Api/simpleAuth/{ApiKeysSupported, AppInfo, GetApiKeys, AddApiKey, RemoveApiKey, SetApiKeyAddresses, SetApiKeyTokens, StoredUsersSupported, GetStoredUsers, AddStoredUser, RemoveStoredUser, SetStoredUserTokens}`.

## CSS reference (`SimpleAuth.css`)

| Selector | Description |
|---|---|
| `.auth-hashgen-form`, `.auth-hashgen-pwd`, `.auth-hashgen-rules`, `.auth-hashgen-tools`, `.auth-hashgen-leak` | The password hash generator form: field stack, policy rules under the password, generate button and leak badge row. |
| `.auth-hashgen-line` (`.auth-hashgen-empty`, `.auth-hashgen-busy`), `.auth-hashgen-note` | The configuration line (monospace, select all; the "empty" token, the computing state) and the note under it. |
| `.auth-tool .auth-list:is(.auth-keys, .auth-users)` | The key list of KeyMan.html and the user list of UserMan.html (on phones the action buttons wrap and are 44 px touch targets). |
| `.auth-key` (`.auth-key-masked`) | API key value in the list (monospace, wraps anywhere, select all); masked: one line of dots, cut with an ellipsis, not selectable. |

Icon classes (`IconKey`, `IconUsers`, `IconLock`, `IconEdit`, `IconCopy`, `IconDownload`, `IconRemove`, `IconAdd`, `IconWarning`) come from `sw/ui.css`; the card, list and dialog styles from `Auth.css`.
