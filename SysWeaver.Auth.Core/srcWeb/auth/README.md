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

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function. Functions marked *(MicroService.Auth)* are defined in `Auth.js` / `AuthUi.js` of [SysWeaver.MicroService.Auth](../../../SysWeaver.MicroService.Auth/srcWeb/auth/README.md), served from the same `auth/` url folder. All user-visible strings are localized with `_TF(text, context)` / `_T(format, args..., context)` from common.js (calls, not definitions).

### `SimpleAuth.js`

Page logic for the two `SimpleAuthorizer` pages. Loaded as a static `<script defer>` by `index.html` (entry `pwdGenMain`) and `KeyMan.html` (entry `keyManMain`), after `../common/common.js`, (`../app/application.js` on KeyMan only), `Auth.js` and `AuthUi.js`. It defines only plain global functions; there are no global variables, classes or immediately executed code. None of its functions are called from any other folder; they are only referenced by the `onload` attributes of the two pages in this folder.

#### Globals

| Name | Kind | Description |
|---|---|---|
| *(none)* | | The file declares no top-level variables. API prefixes (`"../Api/debug/simpleAuth/"`, `"../Api/simpleAuth/"`) are local `const apiPrefix` inside each function. |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `getUserSalt` | async `getUserSalt(user)` -> salt string | `sendRequest("../Api/debug/simpleAuth/GetAuthSalt", user)`; returns the per-user salt from `SimpleAuthorizer.GetAuthSalt`. Throws on request failure. |
| `getPasswordPolicy` | async `getPasswordPolicy()` -> policy object | `getRequest("../Api/debug/simpleAuth/GetPasswordPolicy")`; the policy is passed to `ValidatePassword`, `AuthPolicyText` and `AuthNewPassword` *(MicroService.Auth)*. |
| `pwdGenMain` | async `pwdGenMain()` -> undefined | `onload` entry of `index.html`. Calls `AuthPage("IconLock", builder)` *(MicroService.Auth, AuthUi.js)*; the builder fetches the policy, then creates in the auth box: *Username* label + `AuthInput` (autocomplete `username`), *Password* label (tooltip = `AuthPolicyText(policy)`) + `AuthNewPassword` (autocomplete `new-password`, with policy), an `AuthError` info line (tooltip = policy text), a *Generated hash* label + `AuthText` output with an `IconAuthCopy` icon added by `AuthAddIcon` (size 24) that copies the text via `ValueFormat.copyToClipboardInfo` unless it is `"-"`. Both inputs call inner `setStatus` on change; it is run once initially and the username input is focused. Closure variables `userHash` / `userHashName` cache the salt for the last user name; `havePasskey` (`typeof PassKey !== "undefined"`) is computed but unused. |
| `isApiKeysSupported` | async `isApiKeysSupported()` -> boolean | `getRequest("../Api/simpleAuth/ApiKeysSupported")`. Returns true when the server says so; otherwise shows `Fail` ("Can't manage API keys for this service!", 20 s) or, on exception, `Fail` ("Not authorized to manage API keys!" + error, 20 s) and returns false. |
| `getApiKeys` | async `getApiKeys()` -> string[] or null | `getRequest("../Api/simpleAuth/GetApiKeys")`; each entry is `"name:key"`. |
| `removeApiKey` | async `removeApiKey(keyName)` -> boolean / server result | Shows a `Confirm` dialog (app shell; buttons "Yes, remove it!" / "No, keep it!", icons `IconCancel` / `IconOk`). If confirmed, returns `sendRequest("../Api/simpleAuth/RemoveApiKey", keyName)`; on exception shows `Fail(e)` and returns false. Returns false when cancelled. |
| `createApiKey` | async `createApiKey(rebuildFn)` -> boolean | Opens a `PopUp` (app shell, closable). Inside it creates an auth box via `CreateAuthWrapper()` *(MicroService.Auth)* plus class `SysWeaver-SimpleAuth`, a *Name* `AuthLabel`, an `AuthInput` (maxLength 64) and an `AuthButton` in an `AuthButtonRow` ("Create new API-key", `IconAdd`, initially disabled). The input change handler filters the value to letters (`isLetter`), digits, `_` and `-` (via a local `validMap`), writes the filtered value back (calling `inp.OnChange()`), and enables the button only when non-empty; an `isInternal` flag prevents re-entry. Enter / button run inner `CreateKey`. Resolves to true if a key was created (`didCreate`). |
| `keyManMain` | async `keyManMain()` -> undefined | `onload` entry of `KeyMan.html`. Returns early if `isApiKeysSupported()` is false. Fetches `../Api/simpleAuth/AppInfo` (`appName` = `[AppName, AppDisplayName]`), derives `baseUrl` as `location.href` up to `/auth/`, calls inner `rebuild()` (not awaited) and then `PageLoaded()`. Closure state: `autoUpdate` (timer handle) and `table` (the `<table>` element). |

#### Inner functions of `pwdGenMain`

| Function | Signature | Description |
|---|---|---|
| `setStatus` | async `setStatus(uText, pwText)` -> boolean / undefined | Trims both values (`AuthTrim`). Empty user name: marks the username input as error (`AuthSetError`), shows "Enter a valid username", sets the result to `-`, returns false. Otherwise validates with `ValidatePassword(uText, pwText, null, null, null, policy)`; on a policy error shows it in the info line and `-` as result. When valid: starts `CheckPassword(info, pwText)` (leak check, not awaited), shows "Computing..", fetches the salt with `getUserSalt` if the user name changed (on error clears the info line, shows `Fail` and returns undefined), then shows `"<user> : " + hashString(pwText + "|" + salt)`. Returns true when the password is valid. |

#### Inner functions of `createApiKey`

| Function | Signature | Description |
|---|---|---|
| `CreateKey` | async `CreateKey()` | Puts the button in working state (`StartWorking`) and the input read-only, calls `sendRequest("../Api/simpleAuth/AddApiKey", inp.value)`. On a non-null result: sets `didCreate`, awaits the pop-up `closeFn()`, copies the returned `"name:key"` to the clipboard (`ValueFormat.copyToClipboardInfo`) and calls `rebuildFn()`. A null result shows `Fail("Failed to create API-key!")`; exceptions show `Fail(e)`. Finally restores the input and `StopWorking()`. |

#### Inner functions of `keyManMain`

| Function | Signature | Description |
|---|---|---|
| `rebuild` | async `rebuild()` | Clears any pending `autoUpdate` timer, fetches keys (`getApiKeys() ?? []`) and renders or updates the key table. On first call creates the `<table>` with a header row (`th.header`, colspan 4, "API keys") and a title row (`th.title` "Name" and `th.title` colspan 3 "Key"), appended to `document.body`. Each key (`"name:key"`, split at the first `:`) occupies row index i+2: new rows get a name cell, a copy cell (`ColorIcon` `IconCopy`/`IconColorThemeMain` 32x32, copies the whole `"name:key"`, key stored in expando `cell.Pwd`), a download cell (`ColorIcon` `IconDownload`/`IconColorThemeAcc1`, `downloadText("<name>_<AppName>.txt", "# API credentials for accessing the <AppDisplayName> service at <baseUrl>\n\n<name:key>\n")`) and a remove cell (`ColorIcon` `IconRemove`, calls `removeApiKey(name)` then `rebuild()`). Existing rows are updated in place: name text replaced if changed, copy/download icons rebuilt if name or key changed, remove icon rebuilt if name changed. If an `action` row sits where a key row is needed (or a key row sits where the action row belongs) all rows from that index are removed. A trailing row with class `action` holds a `SysWeaver-CenterBlock` containing a `Button` "Create new key" (`IconAdd`) that awaits `createApiKey(rebuild)`. Errors show `Fail(e)`. Always reschedules itself with `setTimeout(rebuild, 30000)`. |

## CSS reference

### `SimpleAuth.css`

Styles for `index.html` and `KeyMan.html` of this folder, loaded after `../common/theme.css`, (`../app/application.css` on KeyMan) and `Auth.css` (from SysWeaver.MicroService.Auth, same `auth/` url folder), so its rules override those. Not templated. Uses theme variables from `theme.css`; defines no custom properties, animations or media queries.

#### Auth box overrides

| Selector(s) | Description |
|---|---|
| `SysWeaver-AuthText` | Read-only text element created by `AuthText` *(MicroService.Auth, AuthUi.js)*; used by `pwdGenMain` for the generated hash. Overrides `Auth.css`: mono font (`--ThemeFontMono`), bold, `--ThemeAcc1` text on `--ThemeBackground`, input radius `--ThemeRadInp`, 32 px high, 8 px padding, no wrapping, and a 40 px transparent right border leaving room for the `IconAuthCopy` icon. |
| `form.SysWeaver-AuthBox.SysWeaver-SimpleAuth` | The auth box created by `createApiKey` (`CreateAuthWrapper()` adds `SysWeaver-AuthBox`, the script adds `SysWeaver-SimpleAuth`). Resets background, size, display, position, left/top, transform, radius, shadow, border, padding, margin and box-sizing to `initial`, so the floating centered box of `Auth.css` renders as plain content inside the `PopUp`. |

#### Icons

| Selector(s) | Description |
|---|---|
| `.IconLock`, `.IconCopy`, `.IconRemove`, `.IconAdd`, `.IconDownload` | Mask-image icon classes: `IconLock` = `../icons/lock.svg` (page icon passed to `AuthPage` by `pwdGenMain`), `IconCopy` = `../icons/copy.svg` (copy key), `IconRemove` = `../icons/close.svg` (remove key), `IconAdd` = `../icons/plus.svg` (create key buttons), `IconDownload` = `../icons/download.svg` (download credentials). Used via `ColorIcon` / `Button` / `AuthButton` in `SimpleAuth.js`. `IconLock` and `IconDownload` are also defined in `app/application.css`, `IconCopy` in other modules. |

#### API key table (KeyMan.html)

| Selector(s) | Description |
|---|---|
| `table` | Global (unscoped) rule for the key table built by `rebuild`: centered with 1em vertical margin, collapsed borders, translucent `--ThemeMainRGB` background, `--ThemeBackgroundRGB` border, `--ThemeRad` radius, 125% font, min width 16em. |
| `td`, `td:first-child` | Cells: border, `0.25em 1em` padding, centered; the first (name) column is left aligned. |
| `th`, `th.title`, `th.header` | Header cells on `--ThemeAcc2` background. `.header` (class set by `rebuild` on the "API keys" caption cell) uses 125% font, near-opaque background-color text; `.title` (column titles "Name" / "Key") is small (75%), bold, uppercase, left aligned, half-transparent. |
| `tr:first-child th` | Rounds the top corners of the caption cell. |
| `tr:nth-child(3) td`, `tr:nth-last-child(2) td` | Extra top padding on the first key row and extra bottom padding on the last key row (the row before the `action` row). |
| `tr:last-child SysWeaver-CenterBlock` | Vertical margin around the `SysWeaver-CenterBlock` (common.css element) holding the "Create new key" button in the `action` row. |
