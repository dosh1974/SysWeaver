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
