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

## Behavior (web analysis)

### Loading and initialization

- `Login.html` and `Logout.html` are empty shells; `<body onload>` calls `loginMain()` / `logoutMain()`. Scripts are `defer`-loaded in this order:
  - `Login.html`: `../common/common.js`, `Passkey.js`, `Auth.js`, `AuthUi.js`. Styles: `../common/theme.css`, `Auth.css`, `Passkey.css`.
  - `Logout.html`: the same without the passkey files.
- `Passkey.js` / `Passkey.css` belong to SysWeaver.MicroService.PassKey. When that project is not part of the app, the request for `Passkey.js` returns 404 and `typeof PassKey === "undefined"`; `loginMain` checks for exactly that, so the passkey UI simply does not appear (soft dependency).
- No page loads the app shell (`../app/application.js`). The pages are normally shown either embedded by the app shell (the menu item `User/Login` points at `auth/Login.html?parent=1&loc=.`) or stand-alone (the server's `AuthRedirect` default is `auth/Login.html?to={0}`, and the welcome page links to `../auth/Login.html`). None of the auth scripts read the query parameters `parent`, `loc` or `to`.
- No `${Variable}` template substitution is used.
- `AuthPage(...)` is the common page bootstrap used by every page in all four `auth/` contributions:
  1. Calls `SysWeaverIgnoreUserChanges()` (sets `window.IgnoreUserChanged`, so common.js does not reload this window on `user.login` / `user.logout` / `reload` server events).
  2. Creates the `<form class="SysWeaver-AuthBox">` (`CreateAuthWrapper`, `onsubmit` returns false) and appends it to `<body>`.
  3. Adds a 128x128 `ColorIcon` (or an `<img>` when the icon argument looks like a file path).
  4. Awaits `build(box, img)`.
  5. Calls `PageLoaded()` once through `box.OnLoaded()`; exceptions go to `Fail`.

### Files

**`Login.html` / `Logout.html`**: shells (see above).

**`Auth.js`** (requests and validation, global functions):
- `logoutRequest(apiPrefix = "../")`: `GET ../logout` with the header `Authorization: Basic logout`, so that cached browser Basic credentials are overwritten. Returns the JSON boolean from the server.
- `loginRequest(user, password, apiPrefix = "../Api/auth/")`:
  - **Normal path:**
    1. `GetUserSalt(user)` returns `[salt, oneTimePad]`.
    2. If the salt starts with `|`, it is base64 binary and `hash = base64(SHA-256(saltBytes ++ utf8(password)))`; otherwise `hash = hashString(password + "|" + salt)` (SHA-256, base64).
    3. The hash is bound to the pad: `hashString(hash + "|" + oneTimePad)`.
    4. `POST Login {OneTimePad, Hash}`.
    5. The result (`AuthInfo`) is written to `sessionStorage["SysWeaver.User"]`, or that key is removed.
  - **Fallback when `GetUserSalt` throws** (no LoginService):
    1. `GET ../basic_auth` must return truthy (`AllowAuthorizationAuth`).
    2. A raw `XMLHttpRequest` `GET ../login` is sent with `Authorization: Basic base64(user:password)` and `withCredentials`. The server endpoint `login` requires authentication, so the Basic header authenticates the session.
    3. Completion is awaited through `waitEvent2` on a detached `SysWeaver-Coms` element (`LoginOk` / `LoginErr` custom events).
    4. On success it returns `{Succeeded:true, Username:user}` and does not write to `sessionStorage`.
- `AuthTrim(value)`: null-preserving `String(value).trim()`.
- `AuthPolicyText(policy)`: multi-line, translated description of the password policy (defaults to 8..64 characters, clamped to 1..128).
- `ValidatePassword(user, password, cache, onNewPolicy, apiPrefix, policy)`: returns null or an error text. Without an explicit `policy`, it fetches `GetPasswordPolicy(user)` and caches it on the `cache` object (expando properties `PasswordPolicy`, `PasswordPolicyUser`, `PasswordPolicyStr`). It calls `onNewPolicy` when the policy JSON changes. It checks the length, letters, mixed case, digits and special characters.
- `CheckPassword(el, password)`: leak check. It takes the SHA-1 of the password (common.js `hashStringSha1`, converted to upper-case hex) and fetches `https://api.pwnedpasswords.com/range/<first 5>` with CORS. It writes the result text, tooltip and class (`Good` / `Error` / `Warning`) into `el`. The last result is memoized on the element (`LastCheck`, `PwdState`, `PwdError`).
- `AuthGetEmailError(text)` / `AuthGetPhoneError(text)`: call `ValidateEmailAddress` / `GetPhoneNumberInfo`. They return null on success, or the exception message with a leading `"500\n"` removed.

**`AuthUi.js`** (pages and widget toolkit, global functions):
- `logoutMain()`:
  1. Builds a status text and a hidden *Retry* button.
  2. Calls `target.OnLoaded()` early, then `tryOnce()`.
  3. `tryOnce()` first checks `GetUser`; if `Succeeded` is false it shows "No user is signed in".
  4. Otherwise it calls `logoutRequest()` and shows success or failure; on failure the *Retry* button is shown.
- `loginMain()`: *User ID* input (`autocomplete="username"`, or `"username webauthn"` when passkeys are available), *Password* input with a show/hide icon, an info line, and a button row.
  - **Status updates:** `setStatus` is sequenced (`statusSeq` / `statusPromise`) so that only the newest of several async validations updates the UI. It handles browser autofill, where the value is not yet exposed, through `IsAutoFilled` (`:autofill` / `:-webkit-autofill`). It also polls 7 times between 100 ms and 5 s after load to catch late autofill.
  - **Sign in button:** `loginRequest`. On success it shows "User signed in." and calls `AuthStartPage()`. On failure it shows `Fail` and a temporary text for 5 s.
  - **Passkey integration** (only if `PassKey` is defined and supported):
    - Passkey autofill (`PassKey.Autofill`, conditional mediation) is started when supported.
    - A challenge for "any passkey" is pre-fetched (`PassKey.SignIn().Fetch()`), so that the click handler can open the browser prompt directly. If the server answers `NotSupported`, the passkey button is hidden.
    - Button form: if `PassKey.HaveHint()` (localStorage), a full *Use passkey* button; otherwise a small `ColorIcon` (`IconPasskeyKey`, class `PasskeyIconButton`).
    - `usePassKey` stops the autofill, runs `PassKey.SignIn(user)` (or the pre-fetched any-passkey challenge when no user is entered), and on success calls `AuthStartPage()`. Otherwise it re-fetches the challenge and restarts the autofill.
- `GenPassword(length, minUpper, minLower, minDigits, minSpecials)`: random password from look-alike-free alphabets, using `CryptRandomInt32` and bit masks with a modulo fallback, followed by a Fisher-Yates-like swap shuffle (4 x length swaps).
- `CreateAuthWrapper()`, `AuthPage(icon, build, iconIsFile)`: see *Loading and initialization*.
- `AuthPageValidation(box, messageHtml, linkOnSuccess, timeout, instructions, placeHolder, buttonText)`: the **short-code step** shared by the UserManager pages.
  1. Clears the box except its first child (the icon) and reuses the icon class as the button icon.
  2. Shows the HTML message, a *Code* input (only digits kept, 3..16 characters) and a *Continue* button.
  3. The button calls `validateShortCode` (-1 means expired or too many tries, after which the input is disabled; 0 means wrong; 1 means OK).
  4. When the code is OK it navigates with `Open(linkOnSuccess + code, "_self")`.

  It uses `cleanUpShortCode` / `validateShortCode`, which are defined in **UserManager.js**, not in this project. The `timeout` argument is defaulted but not used.
- Widget factories (each appends to `target` and returns the element):
  - `AuthText`, `AuthLabel`, `AuthError`: custom elements `SysWeaver-AuthText` / `-AuthLabel` / `-AuthError`, with copy-on-click (`ValueFormat.copyOnClick`).
  - `AuthTab`: `SysWeaver-AuthTab` with `SysWeaver-AuthTabHeader` children. Methods `Add(text, title, isHtml, icon)` and `SetIndex(i)`, field `Selected`.
  - `AuthInput`: text input with a copy icon by default.
  - `AuthPassword`: password input with a show/hide icon.
  - `AuthNewPassword`: `AuthPassword` plus a dice icon that runs `GenPassword` according to the policy, puts the result in the input, copies it to the clipboard and fires `OnChange`.
  - `AuthInputPhone`: `tel` input inside `SysWeaver-AuthPhone` plus a flag button `SysWeaver-AuthPhoneCc`. On each change it calls `GetPhonePrefixInfo` and sets the flag background to `../iso_data/country/<ISO>.svg`, with a tooltip listing the matching prefixes. The key filter allows digits, `.`, a single `+` and space. The flag click handler is an empty stub.
  - `AuthButtonRow` (`SysWeaver-CenterBlock`), `AuthButton` (common.js `Button`, receives the button in `onClick`), `AuthHr`, `AuthBr`.
  - `AuthSetText(el, text, isHtml)`: also clears the classes and `LastCheck`. `AuthSetError(el, bool)` toggles the `Error` class.
- `AuthAddIcon(input, iconClass, fn, enabledFn, title, size, index)`: places a `ColorIcon` absolutely inside the right edge of an input (class `InsideInput`), computing its position from `getComputedStyle`. It wraps the input's `onchange` / `oninput` to re-evaluate `enabledFn` and exposes `input.OnChange`, `input.Icon` and `input.Icon<n>`.
- `AuthStartPage()`: the **post-login navigation** used by all flows that end signed in.
  1. Strips the query and the last two path segments (`auth/<page>.html`) from the current URL, which gives the app root.
  2. Waits 3 s (`delay(3000)`).
  3. Calls `history.replaceState` to that root URL and then `location.reload(true)`.
- `IsAutoFilled(input)`: autofill detection.

**`Auth.css`**: layout of the body (flex, centered; `body.Top` aligns to the top) and of the floating box `form.SysWeaver-AuthBox`. The box is at most 600 px wide and 80vh high, uses the `--TM` spacing variable and has a 128 px icon. It also styles:
- the custom elements (`SysWeaver-AuthText`, `-AuthError` with `.Error` / `.Warning` / `.Good`, `-AuthLabel`, `-AuthTab`, `-AuthTabHeader`, `-AuthTabContent`, `-AuthPhone`, `-AuthPhoneCc`, `-AuthList`);
- the state classes `.Fail`, `.Info`, `.Hide`, `.Highlight`, `.NoMargin`, `.Disabled`, `.InsideInput`;
- the icon mask classes (`IconRetry`, `IconWarning`, `IconAuthPassword`, `IconAuthPost`, `IconAuthLogin`, `IconAuthLogout`, `IconAuthCopy`, `IconAuthRandom`, `IconAuthView`, `IconAuthViewOff`, `IconAuthEmail`, `IconAuthPhone`), all from the global `../icons/` folder.

All colors come from theme variables (`--Theme*`).

**`icons/user_not_found.svg`**: not used by the scripts. The server serves it as a fallback user image.

### Flows

**Sign in with a password** (`Login.html`):
1. Load: `AuthPage` builds the form. When passkeys are available it starts the passkey autofill (`Api/auth/passkey/GetAuthChallenge`, conditional `navigator.credentials.get`) and pre-fetches an any-passkey challenge.
2. Typing: `ValidatePassword` fetches `Api/auth/GetPasswordPolicy(user)` whenever the user changes, then validates locally. `CheckPassword` queries pwnedpasswords.
3. *Sign in*: `Api/auth/GetUserSalt(user)` -> client hashing -> `Api/auth/Login {OneTimePad, Hash}`. On the server, `LoginService.Login` consumes the pad, verifies `UserHash`, sets the session's auth, runs the OnLogin hooks and returns `GetUser`.
4. `sessionStorage["SysWeaver.User"]` = result -> "User signed in." -> `AuthStartPage()` (3 s, then reload at the app root).
5. Server side, the login also produces a `user.login` session event. The top-level app window, which does not ignore user changes, reloads all frames itself.

**Sign in, Basic-auth fallback**: if `GetUserSalt` fails, the page uses `../basic_auth`, then `../login` with a Basic header (see `loginRequest`).

**Sign in with a passkey from the login page**: *Use passkey* / key icon / autofill selection -> `PassKey` ceremony (see the PassKey `auth/` README) -> `AuthStartPage()`.

**Sign out** (`Logout.html`, menu `User/Logout`): `Api/auth/GetUser` (must be `Succeeded`) -> `GET ../logout` with `Authorization: Basic logout` -> the server's `ForceLogout` clears the session auth. When `AuthRedirect` is empty, the server answers 401 with `WWW-Authenticate`, so the browser drops its Basic credentials. The page shows "Signed out successfully!" and does not navigate away; the app shell reacts to the `user.logout` event (common.js calls `navigator.credentials.preventSilentAccess()` and reloads).

### Server interaction

| Endpoint | Used by | Server |
|---|---|---|
| `Api/auth/GetUserSalt` (POST, user) | `loginRequest`, UserManager.js | `LoginService.GetUserSalt`: `[salt, oneTimePad]` |
| `Api/auth/Login` (POST `{OneTimePad, Hash}`) | `loginRequest` | `LoginService.Login`: `AuthInfo` (`Succeeded`, `Username`, `Tokens`, ...) |
| `Api/auth/GetPasswordPolicy` (POST, user) | `ValidatePassword` | `LoginService.GetPasswordPolicy` |
| `Api/auth/GetUser` | `logoutMain` (also PassKey and UserManager pages) | `AuthManagerService.GetUser` |
| `Api/auth/ValidateEmailAddress`, `GetPhoneNumberInfo`, `GetPhonePrefixInfo` | `AuthGetEmailError`, `AuthGetPhoneError`, `AuthInputPhone` | `AuthManagerService` |
| `../logout` (GET, header `Basic logout`) | `logoutRequest` | HttpServer forced endpoint `HandleLogout` |
| `../basic_auth`, `../login` | Basic fallback | HttpServer optional endpoints `HandleBasicAuth` / `HandleLogin` |
| `https://api.pwnedpasswords.com/range/xxxxx` | `CheckPassword` | External |
| `../iso_data/country/<ISO>.svg` | `AuthInputPhone` (CSS background) | Static data |

Requests go through common.js `sendRequest` (POST by default for API calls, JSON body) and `getRequest`. The session itself is the HttpServer session cookie (default name `SysWeaver.Session.<AppName>`), sent implicitly; the client never handles a token.

### State and persistence

- `sessionStorage["SysWeaver.User"]`: JSON of the last login result. Written by `loginRequest`, and also by `Passkey.js` and `UserManager.js`. No current reader exists (the only reference in common.js is commented out), so it works as an informational, write-only cache.
- The policy cache is held in expando properties on the form element (`ValidatePassword` with `cache = target`). The leak-check result is memoized on the info element.
- `window.IgnoreUserChanged = true` (through `AuthPage`) protects the auth page from being reloaded by the server events its own action causes.
- The only redirect is `AuthStartPage` (history replace plus a hard reload to the app root after 3 s). `AuthPageValidation` navigates with `Open(url, "_self")`.

### Dependencies

- **common.js:**
  - requests: `sendRequest`, `getRequest`;
  - hashing and encoding: `hashString`, `hashData`, `hashStringSha1`, `bufferToBase64`, `bufferToHex`, `base64ToArray`, `concatArrays`, `Base64EncodeString`, `CryptRandomInt32`;
  - translation and HTML: `_T`, `_TF`, `_TH`, `makeHtmlSafe`;
  - UI: `ColorIcon`, `Button`, `ValueFormat`, `Fail`, `SetTemporaryInnerText`, `tabToNextOnEnter`, `keyboardClick`, `badClick`, `createImg`, `Open`;
  - page and timing: `PageLoaded`, `SysWeaverIgnoreUserChanges`, `delay`, `waitEvent2`.
- **Soft:** `PassKey` (`Passkey.js`, SysWeaver.MicroService.PassKey).
- **Hidden:** `cleanUpShortCode` and `validateShortCode` (`UserManager.js`, SysWeaver.MicroService.UserManager) for `AuthPageValidation`.
- **Consumers:** every page of the PassKey and UserManager `auth/` folders and both Auth.Core pages load `Auth.js` + `AuthUi.js` + `Auth.css`. `welcome.js` (app) links to `Login.html`. The menus `User/Login` (LoginService) and `User/Logout` (AuthManagerService) embed the pages.

### Structural observations

- `AuthUi.js` is effectively the **shared UI library** for all `auth/` pages. It is published as global functions (`Auth*` prefix by convention), with no module system and no namespace. Load order (`defer`) is the only contract.
- Two-way coupling across projects: this project's `Login.html` references `Passkey.js` / `Passkey.css` from the PassKey project (guarded by `typeof`), and `AuthPageValidation` calls functions from the UserManager project (unguarded).
- The login hashing protocol (salt / one-time pad / double SHA-256) is implemented here and again, partly, in `UserManager.js` (7 copies without the binary-salt branch) and `SimpleAuth.js`.
- The password-policy clamping logic (default 8..64, maximum 128) appears three times: `AuthPolicyText`, `ValidatePassword` and `AuthNewPassword`.
- The "Copy" default for the `AuthInput` and `AuthInputPhone` icons is duplicated code.
- Widgets keep their state in DOM expando properties (`OnChange`, `Icon`, `Icon0`, `LastCheck`, `PwdState`, `OnLoaded`, `Selected`, `SetIndex`, `Add`, `SetStatus`, `GetValue`). Consumers rely on these.
- `AuthStartPage` assumes that the page URL is exactly `<root>/auth/<page>.html`.
- The `sessionStorage["SysWeaver.User"]` write is repeated in three projects with no reader.
- The Basic-auth fallback uses a raw XHR plus custom events instead of the common request helpers.

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function; "expando" means a non-standard property set on a DOM element. `_T` / `_TF` are the common.js translation helpers (`_T(text, args..., description)` formats `{0}`-style placeholders, `_TF(text, description)` translates fixed text); every user-visible string in these files goes through them. "Used by" names callers in the other `auth/` contributions: **Auth.Core** = `SysWeaver.Auth.Core/srcWeb/auth/SimpleAuth.js`, **PassKey** = `SysWeaver.MicroService.PassKey/srcWeb/auth/Passkey.js`, **UserManager** = `SysWeaver.MicroService.UserManager/srcWeb/auth/UserManagerUi.js` / `UserManager.js`.

### `Auth.js`

Request and validation helpers for authentication: hashed password login against `LoginService`, logout, password policy text and validation, password leak check, and e-mail / phone number validation. Loaded with a `defer` `<script>` by `Login.html` / `Logout.html` and by every page in the PassKey, UserManager and Auth.Core `auth/` folders (always before `AuthUi.js`). Defines only global functions; no top-level state and no immediately executed code. Depends on common.js (`sendRequest`, `getRequest`, `hashString`, `hashData`, `hashStringSha1`, `bufferToBase64`, `bufferToHex`, `base64ToArray`, `concatArrays`, `Base64EncodeString`, `waitEvent2`, `_T`, `_TF`).

#### Globals

None besides the functions below.

#### Functions

| Function | Signature | Description |
|---|---|---|
| `logoutRequest` | async `logoutRequest(apiPrefix = "../")` -> Promise of server JSON (boolean) | `GET <apiPrefix>logout` through `getRequest` (with credentials) and a request-configurator callback that sets the header `Authorization: Basic logout`, overwriting any cached browser Basic credentials. Called by `logoutMain`. Not called from other folders. |
| `loginRequest` | async `loginRequest(user, password, apiPrefix = "../Api/auth/")` -> Promise of `AuthInfo` (`{Succeeded, Username, ...}`) or falsy | Trims both inputs (`AuthTrim`). Normal path: `GetUserSalt(user)` -> `[salt, oneTimePad]`; if `salt` starts with `|` the rest is base64 binary and the hash is `bufferToBase64(hashData(saltBytes ++ utf8(password)))`, otherwise `hashString(password + "|" + salt)`; then `hashString(hash + "|" + oneTimePad)` is POSTed to `Login` as `{OneTimePad, Hash}`. A truthy result is stored as JSON in `sessionStorage["SysWeaver.User"]`, otherwise that key is removed. Fallback when `GetUserSalt` throws: requires `GET <apiPrefix>../../basic_auth` to be truthy (else throws "Nor login or basic auth is enabled!"), then sends a raw `XMLHttpRequest` `GET <apiPrefix>../../login` with `withCredentials` and `Authorization: Basic base64(user:password)`; its `onload` / `onerror` handlers dispatch `LoginOk` / `LoginErr` custom events on a detached `SysWeaver-Coms` element awaited with `waitEvent2`, and `onreadystatechange` records the response text when status is 200. Throws on transport failure or non-200; returns `{Succeeded:true, Username:user}` (no `sessionStorage` write). Called by `loginMain`. UserManager.js and SimpleAuth.js re-implement the hashing instead of calling this. |
| `AuthTrim` | `AuthTrim(value)` -> `null` or string | Returns `null` unchanged, otherwise `("" + value).trim()` (note: `undefined` becomes `"undefined"`). Used by Auth.Core, PassKey and UserManager (`UserManager.js`, `UserManagerUi.js`). |
| `AuthPolicyText` | `AuthPolicyText(policy)` -> string | Builds a multi-line translated description ("Password policy:" then `- ` bullet lines) of a password policy object (`MinLength`, `MaxLength`, `MixedCase`, `MixedNumerical`, `MixedSpecial`). Without a policy it describes the defaults 8..64 only; lengths are clamped to min >= 1, max >= min, max <= 128. A letter requirement is listed when special or numerical mixing is required and mixed case is not. Used as tooltip text by `loginMain`, Auth.Core and UserManager. |
| `ValidatePassword` | async `ValidatePassword(u, p, cache, onNewPolicy, apiPrefix = "../Api/auth/", policy)` -> Promise of `null` (valid) or error text | `u`: user name (policy is per user/authorizer); `p`: password; `cache`: any object that stores the fetched policy in expandos `PasswordPolicy`, `PasswordPolicyUser`, `PasswordPolicyStr`; `onNewPolicy(policy)`: awaited when a fetched policy's JSON differs from the cached one (or when the fetch yields null and the cached policy was also null); `policy`: if given, no request/cache is used. When `cache.PasswordPolicyUser !== u` it POSTs `GetPasswordPolicy(user)` (errors -> `null` policy). Then checks blank, length (defaults/clamps as `AuthPolicyText`), letters (via case comparison), digits (`parseInt`), special characters, and the `MixedCase` / `MixedNumerical` / `MixedSpecial` flags, returning the first translated failure. With a null policy only the blank and default length checks apply (the flag checks are skipped). Used by `loginMain`, Auth.Core and UserManager. |
| `CheckPassword` | async `CheckPassword(el, p)` -> Promise (no value) | Password leak check against haveibeenpwned (k-anonymity). If `p === el.LastCheck` it re-applies the memoized state. Otherwise clears `el.className`, sets "Checking password .." text/tooltip, computes the SHA-1 (`hashStringSha1` -> `base64ToArray` -> `bufferToHex`, upper-case), fetches `https://api.pwnedpasswords.com/range/<first 5 hex>` (CORS) and searches the response for the remaining 35 hex chars, parsing the following digit run as the leak count. Updates `el` through inner `setState`. Side effects on `el`: `innerText`, `title`, class `Good` / `Error` / `Warning`, expandos `LastCheck`, `PwdState`, `PwdError`. Called without await by `loginMain`; used by Auth.Core and UserManager. |
| `setState` (inner of `CheckPassword`) | `setState(state, e)` | Applies a result: `1` safe (class `Good`), `2` leaked `e` times (class `Error`, singular/plural texts), `3` non-200 response (class `Warning`), `4` exception `e` (class `Warning`, error appended to the tooltip). Stores `state` / `e` in `el.PwdState` / `el.PwdError`. |
| `AuthGetEmailError` | async `AuthGetEmailError(emailAddress)` -> Promise of `null` or error text | POSTs `../Api/auth/ValidateEmailAddress`; returns `null` on success, else the exception message with a leading `"500\n"` stripped. Fixed API path (no prefix parameter). Used by UserManager (`UserManagerUi.js`). |
| `AuthGetPhoneError` | async `AuthGetPhoneError(phoneNumber)` -> Promise of `null` or error text | Same pattern with `../Api/auth/GetPhoneNumberInfo` (number must include the country calling code). Used by UserManager (`UserManagerUi.js`). |

### `AuthUi.js`

The page entry points `loginMain` / `logoutMain` and the shared `Auth*` UI toolkit used by every page in all four `auth/` contributions (this folder, PassKey, UserManager, Auth.Core). Loaded with a `defer` `<script>` after `Auth.js` (and after `Passkey.js` on `Login.html`); `Login.html` / `Logout.html` call `loginMain()` / `logoutMain()` from `<body onload>`. Defines only global functions; no top-level state, no immediately executed code. Widgets keep state in DOM expandos (`OnChange`, `Icon`, `Icon<n>`, `Selected`, `Add`, `SetIndex`, `OnLoaded`, `LastCheck`). Depends on common.js (`ColorIcon`, `Button`, `ValueFormat`, `Fail`, `SetTemporaryInnerText`, `tabToNextOnEnter`, `keyboardClick`, `badClick`, `createImg`, `Open`, `PageLoaded`, `SysWeaverIgnoreUserChanges`, `delay`, `CryptRandomInt32`, `makeHtmlSafe`, `sendRequest`), on `Auth.js`, softly on `PassKey` (PassKey `Passkey.js`) and unguardedly on `cleanUpShortCode` / `validateShortCode` (UserManager `UserManager.js`).

#### Globals

None besides the functions below.

#### Functions

| Function | Signature | Description |
|---|---|---|
| `logoutMain` | async `logoutMain()` | Entry point of `Logout.html`. Runs `AuthPage("IconAuthLogout", ...)`: adds a status `AuthText`, a button row and a hidden *Retry* `AuthButton` (`IconRetry`, calls `tryOnce`), calls `target.OnLoaded()` and awaits `tryOnce()`. |
| `tryOnce` (inner of `logoutMain`) | async `tryOnce()` | Sets the text to "Signing out .." (class `Info`), hides *Retry* (`Hide`). POSTs `../Api/auth/GetUser`; if `Succeeded` is false shows and `Fail`s "No user is signed in!". Otherwise awaits `logoutRequest()`: success shows "Signed out successfully!" and returns; failure/exception shows "Failed to sign out!" via `Fail`. On any failure switches the text to class `Fail` and shows *Retry*. |
| `loginMain` | async `loginMain()` | Entry point of `Login.html`. Runs `AuthPage("IconAuthLogin", ...)` and builds: *User ID* `AuthLabel` + `AuthInput` (`autocomplete="username"`), *Password* label + `AuthPassword` (`current-password`, Enter triggers the sign-in button when `setStatus` returns true), an info `AuthError`, and a button row with the *Sign in* `AuthButton` (`IconAuthPassword`, initially disabled). Sign in: if a field is empty re-validates (autofill) and focuses the empty one; else makes inputs read-only, disables the passkey button, awaits `loginRequest`; on `Succeeded` shows "User signed in." and awaits `AuthStartPage()`; otherwise `Fail`, re-validates and shows a 5 s temporary "Failed to login, try again!?" (`SetTemporaryInnerText`). If `PassKey` is defined and `PassKey.IsSupported()`: sets `autocomplete="username webauthn"`, starts `PassKey.Autofill(...)` when `PassKey.IsAutofillSupported()` resolves true (successful result -> `AuthStartPage()`, error -> `Fail(PassKey.ErrorText(res))`), pre-fetches `PassKey.SignIn().Fetch()` (hides the passkey button if the challenge reports `PassKey.Errors.NotSupported`), and adds either a *Use passkey* `AuthButton` (`IconUsePasskey`, when `PassKey.HaveHint()`) or a 32 px `ColorIcon` `IconPasskeyKey` with class `PasskeyIconButton` (both styled by PassKey `Passkey.css`). Finally calls `setStatus`, focuses the user input and schedules re-validation at 100/250/500/1000/2000/3500/5000 ms when the autofill/length state string changed. |
| `setStatus` (inner of `loginMain`) | `setStatus(uText, pwText)` -> Promise of boolean | Increments `statusSeq`, starts `updateStatus` with that sequence number, stores the promise in `statusPromise` and returns it, so only the latest validation updates the UI. |
| `updateStatus` (inner of `loginMain`) | async `updateStatus(uText, pwText, seq)` -> boolean (can sign in) | Trims inputs and awaits `ValidatePassword(uText, pwText, target, onNewPolicy)` (the form element is the policy cache; `onNewPolicy` sets `AuthPolicyText` as tooltip on the password label and info line). If superseded (`seq !== statusSeq`) returns the newest promise's result. Treats an empty but autofilled password (`IsAutoFilled`) as valid; an empty user id marks the user input `Error` and disables *Sign in*; otherwise marks the password input by the validation result, shows the error or starts `CheckPassword(info, pwText)` (not awaited) and enables *Sign in* when valid. |
| `usePassKey` (inner of `loginMain`) | async `usePassKey(button)` | Passkey sign-in click handler: stops the autofill (`autofill?.Stop()`), sets the button working, makes inputs read-only, disables *Sign in*, shows "Select your passkey ..", and runs `PassKey.SignIn(user).Run()` when a user id is entered, else the pre-fetched any-passkey challenge's `Run()`. `res.Error == 0` -> "User signed in." and `AuthStartPage()`. Other errors (except `PassKey.Errors.Cancelled`) are `Fail`ed. Afterwards re-fetches the any-passkey challenge, restarts autofill (`Start()`), re-validates and (unless cancelled) shows the 5 s retry text. |
| `GenPassword` | `GenPassword(length = 16, minUpper = 4, minLower = 4, minDigits = 4, minSpecials = 0)` -> string | Random password. Falsy/invalid arguments get the defaults (length < 8 -> 16; max 256; at least the sum of the upper/lower/digit minimums). Alphabets avoid look-alikes: lower `abcdefghjkmnpqrstuvwxyz`, upper `ACDEFGHJKLMNOPQRSTUVWXY`, digits `2345679`, specials `!.?_` (specials are in the "any" pool only when `minSpecials > 0`). Draws the minimum counts per class, fills the rest from the pool, then performs `4 * length` random swaps. Randomness from `CryptRandomInt32` via power-of-two masks. Called only by `AuthNewPassword`. |
| `getRandom` (inner of `GenPassword`) | `getRandom(cl, mask)` -> int in `[0, cl)` | Takes one `CryptRandomInt32()` value and tries `ri & mask`, then bits shifted by 8 and 16, accepting the first value `< cl`; falls back to `(ri & 0xffffff) % cl`. |
| `getRandomChar` (inner of `GenPassword`) | `getRandomChar(chrs, cl, mask)` -> char | `chrs.charAt(getRandom(cl, mask))`. |
| `Add` (inner of `GenPassword`) | `Add(chrs, count, index)` | Pushes `count` random characters from `chrs` (length/mask taken from slot `index` of the precomputed tables) onto the password array. |
| `CreateAuthWrapper` | `CreateAuthWrapper()` -> `HTMLFormElement` | Creates (does not attach) a `<form class="SysWeaver-AuthBox">` whose `onsubmit` returns `false`. Used by `AuthPage`; also by Auth.Core and PassKey. |
| `AuthPage` | async `AuthPage(icon, build, iconIsFile)` | Common page bootstrap. Calls `SysWeaverIgnoreUserChanges()`, creates the box with `CreateAuthWrapper`, defines `box.OnLoaded()` (calls `PageLoaded()` once, then replaces itself with a no-op), appends the box to `document.body`. `icon`: a 128x128 `ColorIcon` (color `IconColorThemeMain`) for an icon class, or an `<img>` (`createImg`, not draggable) when the string contains `.` or `/` after position 0 or `iconIsFile` is truthy. Awaits `build(box, img)` (`img` is the `ColorIcon` or `<img>`), then calls `box.OnLoaded()`; on exception calls `OnLoaded` and `Fail(e)`. Used by Auth.Core, PassKey and UserManager. |
| `AuthPageValidation` | async `AuthPageValidation(authPageElement, messageHtml, linkOnSuccess, timeout = 900, instructions = "Code", placeHolder = "Enter the sent code", buttonText = "Continue")` | Replaces the content of an `AuthPage` box (all children except the first, the icon) with a short-code entry step. The first non-`IconColor*` class of the icon element becomes the button icon. Adds the HTML message (`AuthText` + `AuthBr`), a label, a code `AuthInput` (min 3 / max 16 chars) whose change handler normalizes the value with `cleanUpShortCode(value, true)` and enables the button when `cleanUpShortCode(value)` is non-null, a *Continue* button and an error line. The button calls `validateShortCode(code)`: `< 0` disables the input ("Code expired or too many tries"), `0` shows "Invalid code, try again", otherwise `Open(linkOnSuccess + code, "_self")`. `timeout` is defaulted but unused. `cleanUpShortCode` / `validateShortCode` are defined in UserManager `UserManager.js`; called only by UserManager `UserManagerUi.js`. |
| `AuthUserHtml` | `AuthUserHtml(emailOrUsernameArray)` -> HTML string | A string becomes `<em>` + HTML-escaped trimmed text + `</em>`; an array becomes the escaped items each wrapped in `<em>` and joined by `, `. Used by UserManager. |
| `AuthText` | `AuthText(target, text, title, isHtml)` -> element | Appends a `SysWeaver-AuthText` element with `text` (as `innerHTML` when `isHtml`, else `innerText`) and optional tooltip; makes it copy-on-click (`ValueFormat.copyOnClick`). Used by Auth.Core, PassKey, UserManager. |
| `AuthLabel` | `AuthLabel(target, text, title, isHtml)` -> element | Same as `AuthText` but creates `SysWeaver-AuthLabel` (bold accent label above inputs). Used by Auth.Core, PassKey, UserManager. |
| `AuthTab` | `AuthTab(target, title, onChangeFn)` -> element | Appends a `SysWeaver-AuthTab` bar. Expandos: `Selected` (index, -1 until the first header), `Add(text, title, isHtml, icon)` and `SetIndex(i, noFn)` (= inner `activateIndex`). `Add` appends a keyboard-clickable `SysWeaver-AuthTabHeader`; the first one added becomes selected (class `Selected`, `tabIndex = -1`); an optional 24x24 `ColorIcon` is stored as header expando `Icon`; header clicks (ignoring `badClick`) call `activateIndex`. Used by UserManager. |
| `activateIndex` (inner of `AuthTab`) | `activateIndex(newIndex, noFn)` | Sets `Selected`, marks the chosen header `Selected` (icon color `IconColorThemeBackground`, `tabIndex -1`) and the others unselected (`IconColorThemeAcc2`, `tabIndex 0`); calls `onChangeFn(newIndex)` unless `noFn`. |
| `AuthError` | `AuthError(target, text, title)` -> element | Appends a `SysWeaver-AuthError` status/error line (plain text, copy-on-click). Classes `Error` / `Warning` / `Good` are set by `CheckPassword` / `AuthSetError`. Used by Auth.Core, UserManager. |
| `AuthNewPassword` | `AuthNewPassword(target, placeholder, title, autocomplete, onChange, onNextItemFn, passwordPolicy)` -> input | An `AuthPassword` plus a second inside icon (`IconAuthRandom`, index 1) that generates `GenPassword(len, 1, 1, 1, minSpecials)`, writes it to the input, copies it to the clipboard (`ValueFormat.copyToClipboardInfo`) and awaits `input.OnChange()`. `len` is 16 clamped into the policy's `MinLength..MaxLength` (max 128); `minSpecials` is 1 when `MixedSpecial`. Used by Auth.Core, UserManager. |
| `AuthSetText` | `AuthSetText(target, text, isHtml)` | Clears `target.className` and `target.LastCheck` (resets `CheckPassword` memo), then sets `&nbsp;` for empty text, `innerHTML` when `isHtml`, else `innerText`. Used by Auth.Core, UserManager. |
| `AuthSetError` | `AuthSetError(target, haveError)` | Adds or removes class `Error`. Used by Auth.Core, UserManager. |
| `AuthAddIcon` | `AuthAddIcon(e, iconClass, iconFn, iconEnabledFn = () => true, iconTitle, size = 32, index = 0)` | Adds a clickable `ColorIcon` (color `IconColorThemeMain`, not a tab stop, class `InsideInput`) positioned inside the right edge of input `e` (icon slot `index`, 4 px spacing), computing `top` from the input's computed height and bottom margin and widening `paddingRight`; the icon is appended to the input's parent. Clicking awaits `iconFn(e)`. Wraps the existing `onchange` / `oninput` so each change re-evaluates `iconEnabledFn(e)` (`SetEnabled`) before calling the original; exposes inner `onChange` as `e.OnChange`, the icon as `e["Icon" + index]` and, for index 0, `e.Icon`. Used by `AuthPassword`, `AuthInput`, `AuthInputPhone`, `AuthNewPassword` and Auth.Core. |
| `onChange` (inner of `AuthAddIcon`) | async `onChange(ev)` | Updates the icon's enabled state and awaits the previous `onchange` handler. |
| `AuthPassword` | `AuthPassword(target, placeholder, title, autocomplete, onChange, onNextItemFn, iconClass, iconFn, iconEnabledFn, iconTitle)` -> input | Appends a required `<input type="password">` (length 8..128). `onChange(input)` is wired to `onchange`, `oninput` and expando `OnChange` (local wrapper `oc`). `tabToNextOnEnter(e, onNextItemFn)`. Without `iconClass` adds the show/hide toggle (`IconAuthView` <-> `IconAuthViewOff`, switching `type` between `password` and `text` and updating the tooltip); `iconClass "-"` means no icon. Used by `loginMain`, `AuthNewPassword`, UserManager. |
| `AuthInput` | `AuthInput(target, placeholder, title, autocomplete, onChange, onNextItemFn, iconClass = "IconAuthCopy", iconFn, iconEnabledFn, iconTitle)` -> input | Appends a required `<input type="text">` (length 1..128), wiring `onChange` like `AuthPassword` (local `oc`). Without `iconFn` the icon copies the value to the clipboard and is enabled only when the input has a value; `iconClass "-"` means no icon. Used by Auth.Core, PassKey, UserManager. |
| `AuthInputPhone` | `AuthInputPhone(target, placeholder, title, autocomplete, onChange, onNextItemFn, iconClass = "IconAuthCopy", iconFn, iconEnabledFn, iconTitle)` -> input | Appends a `SysWeaver-AuthPhone` group holding a required `<input type="tel" class="Phone">` and a keyboard-clickable `SysWeaver-AuthPhoneCc` flag. The change handler (local `oc`, also `OnChange`) ignores unchanged values, POSTs `../Api/auth/GetPhonePrefixInfo(value)`, builds a tooltip of matching `+CountryCode RegionPrefixes Name` lines and calls inner `setCountry` with the first `IsoCountry`, then awaits `onChange(input)`. `onkeydown` allows only digits, `.`, space, control/named keys, Ctrl combinations and a single `+`. The flag `onclick` only filters `badClick` (empty stub). Copy icon as in `AuthInput`. Used by UserManager. |
| `setCountry` (inner of `AuthInputPhone`) | `setCountry(iso = "_", title, allSame)` | Sets the flag background to `../iso_data/country/<ISO>.svg`, the tooltip to `title` + "Click to select country code prefix", and toggles class `AllSame` (true when all matching prefixes share one country code; no CSS rule uses it). |
| `oc` (local in `AuthPassword`, `AuthInput`, `AuthInputPhone`) | async `oc()` | Function-valued local wired to `onchange` / `oninput` / `OnChange`; awaits the caller's `onChange(input)` (with the extra prefix lookup in `AuthInputPhone`). |
| `AuthButtonRow` | `AuthButtonRow(target)` -> element | Appends a `SysWeaver-CenterBlock` container for buttons. Used by Auth.Core, PassKey, UserManager. |
| `AuthButton` | `AuthButton(target, name, title, icon, onClick, disabled)` -> common.js `Button` | Creates a `Button` (enabled unless `disabled`) whose click awaits `onClick(button)`, appends `button.Element` and returns the `Button` (methods used: `SetEnabled`, `StartWorking`, `StopWorking`, `Element`). Used by Auth.Core, PassKey, UserManager. |
| `AuthHr` | `AuthHr(target)` -> `<hr>` | Appends a horizontal rule. Used by PassKey. |
| `AuthBr` | `AuthBr(target)` -> `<br>` | Appends a line break. Used by `AuthPageValidation`. |
| `AuthStartPage` | async `AuthStartPage()` | Post-login navigation: removes the query and the last two path segments (`auth/<page>.html`) from `location.href`, ensures a trailing `/`, waits 3 s (`delay(3000)`), then `history.replaceState` to that URL and `location.reload(true)`. Used by `loginMain`, PassKey and UserManager. |
| `IsAutoFilled` | `IsAutoFilled(inputElement)` -> boolean | True when the input matches `:autofill` or `:-webkit-autofill` (selector errors swallowed); false for null. Used only by `loginMain`. |

#### Element expandos set by the toolkit

| Member | Set by | Description |
|---|---|---|
| `box.OnLoaded` | `AuthPage` | Calls `PageLoaded()` once, then becomes a no-op. Build callbacks may call it early (e.g. `logoutMain`). |
| `input.OnChange` | `AuthPassword`, `AuthInput`, `AuthInputPhone`, `AuthAddIcon` | Async change handler; callers invoke it after setting a value programmatically (e.g. `AuthNewPassword`). |
| `input.Icon`, `input.Icon0`, `input.Icon1` | `AuthAddIcon` | The `ColorIcon` inside the input (slot 0 is also `Icon`). |
| `tab.Selected`, `tab.Add`, `tab.SetIndex`; header `.Icon` | `AuthTab` | Tab bar state and API (see `AuthTab`). |
| `LastCheck`, `PwdState`, `PwdError` | `CheckPassword`, `AuthSetText` | Leak-check memo on the info element. |
| `PasswordPolicy`, `PasswordPolicyUser`, `PasswordPolicyStr` | `ValidatePassword` | Policy cache on the `cache` object. |

## CSS reference

### `Auth.css`

Styles the auth pages: centered body, the floating `form.SysWeaver-AuthBox` (built by `AuthPage` / `CreateAuthWrapper` in `AuthUi.js`), the `SysWeaver-Auth*` custom elements created by the `Auth*` factories, input styling and inline input icons, the phone input, the tab bar and the auth icon mask classes. Loaded by `Login.html` and `Logout.html` and by every page of the PassKey, UserManager and Auth.Core `auth/` folders, after `../common/theme.css` (which provides all `--Theme*` variables). Not templated. Icon classes are combined with common.js `ColorIcon` elements (`icon-<w>x<h>` custom elements), which supply size, color and mask setup; this file only sets `mask-image`.

#### Page and box

| Selector(s) | Description |
|---|---|
| `html`, `body` | No margins/padding; body is a centered wrapping flex container, full size, `user-select: none`. |
| `body.Top` | Aligns the box to the top instead of the center; set statically by Auth.Core `KeyMan.html` (`<body class="Top">`). |
| `form.SysWeaver-AuthBox` | The floating auth box: absolute, `width: var(--boxWidth)` (at most 600 px or viewport minus 2em), `max-height: 80vh`, semi-transparent theme background, thin main-color border, theme radius, large drop shadow. Defines `--TM` and `--boxWidth`. Created by `CreateAuthWrapper`. |
| `form.SysWeaver-AuthBox > Icon-128x128` | The page icon added by `AuthPage` (128x128 `ColorIcon`), rescaled to `--size` = min(20vh, 15vw, 70% of the box width) and centered. |
| `hr` | Top margin `--TM` (rules from `AuthHr`). |

#### Text elements

| Selector(s) | Description |
|---|---|
| `SysWeaver-AuthText` | Centered block text with top margin `--TM` (`AuthText`). |
| `SysWeaver-AuthLabel` | Bold `--ThemeAcc2` block label with top margin `--TM` (`AuthLabel`). |
| `SysWeaver-AuthError` | Italic centered status line, min height 2.5em, scrolls vertically (`AuthError`). |
| `SysWeaver-AuthError.Error`, `SysWeaver-AuthError.Warning`, `SysWeaver-AuthError.Good` | Error / warning / ok colors (`--ThemeError`, `--ThemeWarning`, `--ThemeOk`); classes set by `CheckPassword` and `AuthSetError`. |
| `.Highlight` | Accent color `--ThemeAcc1`; added by PassKey `Passkey.js` (passkey list names). |
| `.NoMargin` | Resets `margin-top`; added by PassKey `Passkey.js` (list rows, `AuthHr` separators). |
| `form.SysWeaver-AuthBox *.Fail`, `form.SysWeaver-AuthBox *.Info` | Status colors `--ThemeAcc1` / `--ThemeAcc2`; toggled by `logoutMain`. |
| `form.SysWeaver-AuthBox *.Hide` | `display: none`; used by `logoutMain` (*Retry*) and `loginMain` (passkey button when unsupported). |

#### Inputs and buttons

| Selector(s) | Description |
|---|---|
| `form.SysWeaver-AuthBox SysWeaver-CenterBlock` | Top padding `--TM` for button rows (`AuthButtonRow`). |
| `form.SysWeaver-AuthBox input` | Full-width large bold inputs with theme colors, rounded, with a 2 px outline (25% main color) animated on hover (75%) and focus (`--ThemeAcc1`), via `:hover` / `:focus`. |
| `form.SysWeaver-AuthBox input.Error`, `form.SysWeaver-AuthBox *.Error input` | Red glow (`--ThemeError` box shadow) for an invalid input or any input inside an element with class `Error` (`AuthSetError`). |
| `form.SysWeaver-AuthBox input::placeholder` | Faint italic smaller placeholder. |
| `.InsideInput` | `position: relative` for icons placed inside inputs by `AuthAddIcon` (offsets are set inline). |
| `.InsideInput.IconDisabled` (and `:hover`) | Disabled inside-input icon at 10% opacity; `IconDisabled` is the class common.js `ColorIcon.SetEnabled(false)` applies. |

#### Tabs

| Selector(s) | Description |
|---|---|
| `SysWeaver-AuthTab` | Bold block tab bar with top margin `--TM`, opacity transition (`AuthTab`). |
| `SysWeaver-AuthTab.Disabled` | 50% opacity, no pointer events; toggled by UserManager `UserManagerUi.js` while a request runs. |
| `SysWeaver-AuthTabHeader` | Inline-flex rounded pill (`--ThemeRadInp`) with `--ThemeAcc250` border and `--ThemeAcc2` text; `:hover` uses `--ThemeAcc225` background, `:focus` a `--ThemeAcc1` outline. |
| `SysWeaver-AuthTabHeader.Selected` (and `.Selected:focus`) | Filled `--ThemeAcc2` with background-colored text, not clickable, no focus outline; class set by `AuthTab`. |
| `SysWeaver-AuthTabHeader > icon-24x24` | Left margin for the header's 24x24 `ColorIcon`. |
| `SysWeaver-AuthTabContent` | 64 px high relative block for tab content; created by UserManager `UserManagerUi.js`. |
| `SysWeaver-AuthTabContent.Disabled` | 50% opacity, no pointer events (UserManager). |
| `SysWeaver-AuthTabContent Icon-32x32` | Inside-input 32x32 icons in tab content forced `position: absolute`, `top: 10px`. |

#### Phone input and lists

| Selector(s) | Description |
|---|---|
| `form.SysWeaver-AuthBox input.Phone` | 64 px left padding to make room for the flag (`AuthInputPhone` adds `Phone`). |
| `SysWeaver-AuthPhone` | Relative wrapper of the phone input and flag (`AuthInputPhone`). |
| `SysWeaver-AuthPhoneCc` | 48x40 absolute flag button at the input's left; background image (`../iso_data/country/<ISO>.svg`) set inline by `setCountry`; `:hover` `--ThemeAcc1` background, `:focus` `--ThemeAcc2` outline. |
| `SysWeaver-AuthList` | Scrollable (max 50vh) bordered list box with faint main-color background; created by PassKey `Passkey.js` (passkey list). |
| `SysWeaver-AuthList SysWeaver-CenterBlock` | Button rows inside the list: 1em top margin, no top padding. |

#### Icon mask classes

| Selector(s) | Description |
|---|---|
| `.IconRetry` | `../icons/reload.svg`; *Retry* in `logoutMain`. |
| `.IconWarning` | `../icons/warning.svg`; used by PassKey `Passkey.js` (error pages) and common.js. |
| `.IconAuthPassword` | `../icons/lock.svg`; *Sign in* button. |
| `.IconAuthPost` | `../icons/send.svg`; UserManager submit buttons. |
| `.IconAuthLogin`, `.IconAuthLogout` | `../icons/user.svg`, `../icons/user_logout.svg`; page icons of `loginMain` / `logoutMain`. |
| `.IconAuthCopy` | `../icons/copy.svg`; default copy icon of `AuthInput` / `AuthInputPhone`. |
| `.IconAuthRandom` | `../icons/dice.svg`; password generator in `AuthNewPassword`. |
| `.IconAuthView`, `.IconAuthViewOff` | `../icons/eye.svg`, `../icons/eye_off.svg`; show/hide toggle of `AuthPassword`. |
| `.IconAuthEmail`, `.IconAuthPhone` | `../icons/email.svg`, `../icons/phone.svg`; UserManager e-mail / phone buttons and tabs. |

`IconUsePasskey`, `IconPasskeyKey` and `PasskeyIconButton` (used by `loginMain`) are defined in PassKey `Passkey.css`, not here.

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--TM` | `2em`, on `form.SysWeaver-AuthBox` | Vertical spacing (top margins of texts, labels, tabs, lists, `hr`, button rows; quarter of it above inputs). |
| `--boxWidth` | `min(100vw - 2em, 600px)`, on `form.SysWeaver-AuthBox` | Box width and icon size computation. |
| `--size` | `min(20vh, 15vw, 0.7 * --boxWidth)`, on the box icon | Page icon width/height/mask size. |
| `--ThemeBackgroundRGB`, `--ThemeMainRGB`, `--ThemeBackground`, `--ThemeMain`, `--ThemeMain25`, `--ThemeAcc1`, `--ThemeAcc2`, `--ThemeAcc225`, `--ThemeAcc250`, `--ThemeError`, `--ThemeWarning`, `--ThemeOk`, `--ThemeRad`, `--ThemeRadInp`, `--ThemeFadeDuration` | `../common/theme.css` | All colors, radii and transition durations. |

#### Animations, media queries and theme variants

None: no `@keyframes`, no media queries and no `[data-theme]` rules; theming comes entirely from the `--Theme*` variables. Transitions: input outline / box-shadow (0.1 s), tab and phone-flag color/outline/opacity (`--ThemeFadeDuration`).
