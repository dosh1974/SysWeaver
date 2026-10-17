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

## Behavior (web analysis)

### Loading and initialization

- Both pages are empty HTML shells (`<body onload=...>`); the whole UI is built from script. Scripts are loaded with `defer` in this order: `../common/common.js`, (`KeyMan.html` only: `../app/application.js`), `Auth.js`, `AuthUi.js`, `SimpleAuth.js`. Styles: `../common/theme.css`, (`KeyMan.html` only: `../app/application.css`), `Auth.css`, `SimpleAuth.css`.
- `index.html` calls `pwdGenMain()`; `KeyMan.html` calls `keyManMain()` and sets `class="Top"` on `<body>` (rule in `Auth.css`: aligns content to the top instead of centering it).
- `Auth.js`, `AuthUi.js` and `Auth.css` are **not in this project**; they are served from the same `auth/` URL folder by SysWeaver.MicroService.Auth (merged at runtime). Without that project the pages cannot work.
- No `${Variable}` template substitution is used in these files.

### Files

**`index.html`** (*Generate password hash*): shell, entry `pwdGenMain()`.

**`KeyMan.html`** (*API key management*): shell, entry `keyManMain()`. It is the only page here that loads the app shell (`application.js` / `.css`), because it uses `PopUp` and `Confirm`.

**`SimpleAuth.js`**: plain global functions (no module, no class).
- `getUserSalt(user)` / `getPasswordPolicy()`: wrappers for `../Api/debug/simpleAuth/GetAuthSalt` (`sendRequest`) and `../Api/debug/simpleAuth/GetPasswordPolicy` (`getRequest`).
- `pwdGenMain()`: builds an auth box with `AuthPage("IconLock", ...)` containing a *Username* input (`AuthInput`), a *Password* input with a random-password icon (`AuthNewPassword`, receives the policy), an info/error line (`AuthError`) and a read-only *Generated hash* text (`AuthText`) with a copy icon (`AuthAddIcon(..., "IconAuthCopy", ...)`). An inner `setStatus(user, pw)` runs on every input/change. It checks that the user name is not empty, validates the password with `ValidatePassword` against the fixed policy and starts the haveibeenpwned check (`CheckPassword`, not awaited). It then fetches the salt for the user, cached per user name in the closure variables `userHash` / `userHashName`, and shows `user : hashString(password + "|" + salt)`. The policy text (`AuthPolicyText`) is the tooltip of the label and the info line. The computed `havePasskey` variable is never used.
- `isApiKeysSupported()`: calls `../Api/simpleAuth/ApiKeysSupported` and shows a `Fail` toast (20 s) if keys are not supported or the session is not authorized (exception).
- `getApiKeys()` / `removeApiKey(name)`: `GetApiKeys` (returns `"name:key"` strings) and `RemoveApiKey` (after a `Confirm` dialog).
- `createApiKey(rebuildFn)`: opens a `PopUp` containing an auth box (`CreateAuthWrapper()` plus class `SysWeaver-SimpleAuth`, which resets the floating-box styling). The name input is filtered as the user types: only letters (`isLetter`), digits, `_` and `-` are kept, up to 64 characters. The *Create new API-key* button calls `AddApiKey`, closes the pop-up, copies the returned `"name:key"` to the clipboard and calls `rebuildFn`.
- `keyManMain()`: checks support, fetches `AppInfo` (`[AppName, AppDisplayName]`) and derives `baseUrl` from `location.href` (everything before `/auth/`). It then calls `rebuild()`, which renders a `<table>` appended directly to `<body>` and updates it in place on later calls. The table has a header row, a title row, one row per key and a trailing `action` row with a *Create new key* `Button`. Each key row has the name plus copy, download and remove icons (all `ColorIcon`). Rows are reused when name and key are unchanged. `rebuild()` schedules itself again every 30 s (`setTimeout`). Download uses `downloadText("<name>_<AppName>.txt", "# API credentials ... <baseUrl>\n\n<name:key>")`. The function calls `PageLoaded()` itself (it does not use `AuthPage`).

**`SimpleAuth.css`**:
- The `SysWeaver-AuthText` look of the hash output: mono font, accent color, and a 40 px right border that leaves room for the copy icon.
- `form.SysWeaver-AuthBox.SysWeaver-SimpleAuth` resets all box styles so that an auth box can sit inside a pop-up.
- Icon mask classes `IconLock`, `IconCopy`, `IconRemove`, `IconAdd`, `IconDownload` (from `../icons/`).
- Global (unscoped) `table` / `td` / `th` / `tr` rules for the key table, with padding chosen by row position (`nth-child`).

### Flows

1. **Generate password hash** (menu *Debug/SimpleAuth/GenPwd*, tokens `debug,ops`):
   1. On load the page calls `GET Api/debug/simpleAuth/GetPasswordPolicy`.
   2. The user types a user name and a password. On each change the password is validated locally against the policy.
   3. `Api/debug/simpleAuth/GetAuthSalt(user)` is called once per user name.
   4. The page computes the SHA hash (`hashString`, common.js) of `password|salt` and shows `user : hash`, which can be copied.
   5. In parallel, the password is checked for leaks against `api.pwnedpasswords.com` (k-anonymity: only the first 5 hex characters of the SHA-1 are sent).

   The result line is meant to be pasted into the `SimpleAuthorizer` user configuration.
2. **API key management** (menu *Debug/SimpleAuth/KeyMan*, shown only when `CanManageKeys`):
   1. On load: `ApiKeysSupported`, then `AppInfo`, then `GetApiKeys`, and the table is built.
   2. Create: button -> pop-up -> `AddApiKey(name)` -> the key is copied to the clipboard -> the table is rebuilt.
   3. Remove: icon -> `Confirm` -> `RemoveApiKey(name)` -> the table is rebuilt.
   4. Copy and download are local only. The table refreshes itself every 30 s.

### Server interaction

| Call | Client helper | Server side |
|---|---|---|
| `../Api/debug/simpleAuth/GetPasswordPolicy` | `getRequest` | `SimpleAuthorizer.GetPasswordPolicy()` (`[WebApi("debug/simpleAuth/{0}")]`) |
| `../Api/debug/simpleAuth/GetAuthSalt` | `sendRequest(user)` | `SimpleAuthorizer.GetAuthSalt(user)` |
| `../Api/simpleAuth/ApiKeysSupported`, `AppInfo` | `getRequest` | `[WebApi("simpleAuth/{0}")]`, `[WebApiAuth(ApiKeyAuth)]`, static client/request cache |
| `../Api/simpleAuth/GetApiKeys` | `getRequest` | Returns a `"name:key"` array or null |
| `../Api/simpleAuth/AddApiKey`, `RemoveApiKey` | `sendRequest(name)` | Audited |

The leak check (`CheckPassword` in `Auth.js`) calls `https://api.pwnedpasswords.com/range/<5 hex>` directly from the browser (CORS `fetch`). The pages read no URL query parameters and use no templated values.

### State and persistence

- No local or session storage and no cookies are used directly. Authentication relies on the server session cookie, which the browser sends with every API request. The API key endpoints are gated on the server by `ApiKeyAuth`; an unauthorized session gets a `Fail` message and an empty page.
- State lives in closures: the cached salt per user name (`pwdGenMain`), and the table element and 30 s timer handle (`keyManMain`). Each key cell keeps its key in an expando property (`cell.Pwd`), used to detect changes.
- There are no redirects. `AuthPage` (used by `pwdGenMain`) calls `SysWeaverIgnoreUserChanges()`, so that page is not reloaded on server user-change events; `keyManMain` does not make that call.

### Dependencies

- **common.js:** `sendRequest`, `getRequest`, `hashString`, `_TF` / `_T`, `Fail`, `ValueFormat.copyToClipboardInfo`, `ColorIcon`, `Button`, `isLetter`, `downloadText`, `PageLoaded`.
- **app/application.js** (KeyMan only): `PopUp`, `Confirm`.
- **`auth/` files of SysWeaver.MicroService.Auth:**
  - `AuthUi.js`: `AuthPage`, `AuthLabel`, `AuthInput`, `AuthNewPassword`, `AuthError`, `AuthText`, `AuthAddIcon`, `AuthButton`, `AuthButtonRow`, `AuthSetText`, `AuthSetError`, `CreateAuthWrapper`.
  - `Auth.js`: `AuthTrim`, `ValidatePassword`, `AuthPolicyText`, `CheckPassword`.
  - `Auth.css`: base styles (`SysWeaver-Auth*` elements, `IconAuthCopy`).
- **Consumers:** only the two `WebMenuEmbedded` entries of `SimpleAuthorizer` (Debug menu).

### Structural observations

- The pages depend on another project's files through the shared served folder. Nothing at build time checks that SysWeaver.MicroService.Auth is present.
- The salt and hash scheme (`password + "|" + salt` through `hashString`) repeats the non-binary branch of `loginRequest` in `Auth.js` and the hashing in `UserManager.js`. The "binary salt" branch of `loginRequest` (salt prefixed with `|`) has no counterpart here.
- The API prefix strings are repeated as local constants inside each function instead of being shared.
- In `keyManMain`, the code that builds a new row and the code that updates a changed row construct the same `ColorIcon`s (copy, download, remove) separately.
- `SimpleAuth.css` has global, unscoped `table` / `td` / `th` rules that apply to any table on a page that loads it. The class names `IconCopy` / `IconRemove` / `IconAdd` are generic and can collide with app-shell icon classes of the same name.
- The two pages use different page frameworks: `index.html` uses the `AuthPage` box, while `KeyMan.html` builds a raw table into `<body>` and uses app-shell pop-ups.
