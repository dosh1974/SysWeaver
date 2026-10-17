# auth

[⬆ srcWeb](../README.md)

> The passkey pages linked from the *User* menu, plus the shared script and styles that drive the WebAuthn calls.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.PassKey](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

The files are compressed into `web/auth` and served as `auth/...`. That is the same folder the other authentication services use, so the pages can include `Auth.js` and `AuthUi.js` from [SysWeaver.MicroService.Auth](../../../SysWeaver.MicroService.Auth/README.md) using plain relative paths. `PassKeyService` adds the pages to the *User* menu and only shows each entry when it applies. The pages call the service API under `auth/passkey`.

## Contents

| Item | Description |
|---|---|
| [`UsePassKey.html`](UsePassKey.html) | Sign in with a passkey (`usePassKeyMain()`) |
| [`AddPassKey.html`](AddPassKey.html) | Add a passkey for this device, either to the signed-in user or through a link token (`addPassKeyMain()`) |
| [`AddPassKeyOnOtherDevice.html`](AddPassKeyOnOtherDevice.html) | Shows a time-limited QR code (`GetQR.svg`) for adding a passkey on another device (`addPassKeyOtherMain()`) |
| [`MyPassKeys.html`](MyPassKeys.html) | List, rename and remove the user's passkeys (`myPassKeysMain()`) |
| [`Passkey.js`](Passkey.js) | `PassKeyChallenge` / `PassKey` classes for the WebAuthn create/get calls and the API, plus the page entry points |
| [`Passkey.css`](Passkey.css) | Icon classes (`IconAddPasskey`, `IconUsePasskey`, `IconPasskeyKey`, `IconPasskeyRename`) and the rename pop-up style |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.MicroService.PassKey](../../README.md)
- **Uses:** [`../icons/`](../icons/README.md), [SysWeaver.MicroService.Auth](../../../SysWeaver.MicroService.Auth/README.md) (`Auth.js`, `AuthUi.js`), [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/README.md) (`common/common.js`, `app/application.js`)

## Behavior (web analysis)

### Loading and initialization

- The four pages are empty shells. Scripts are `defer`-loaded in this order: `../common/common.js`, (`MyPassKeys.html` only: `../app/application.js`), `Auth.js`, `AuthUi.js` (SysWeaver.MicroService.Auth), `Passkey.js`. Styles: `../common/theme.css`, (MyPassKeys only: `../app/application.css`), `Auth.css`, `Passkey.css`.
- Entry points from `<body onload>`:

  | Page | Entry point |
  |---|---|
  | `UsePassKey.html` | `usePassKeyMain()` |
  | `AddPassKey.html` | `addPassKeyMain()` |
  | `AddPassKeyOnOtherDevice.html` | `addPassKeyOtherMain()` |
  | `MyPassKeys.html` | `myPassKeysMain()` |

- `Passkey.js` and `Passkey.css` are also loaded by pages of other projects in the same `auth/` folder: `Login.html` (MicroService.Auth) and `ChoosePassword.html` / `ResetPassword.html` (UserManager). Those pages test `typeof PassKey !== "undefined"`, so this project is an optional plug-in for them.
- The icon masks reference `../icons/passkey_*.svg`. These icons come from this project's own `srcWeb/icons` folder, served as `icons/`.
- No `${Variable}` templates are used.

### Files

**`Passkey.js`**:
- **`class PassKeyChallenge`**: one WebAuthn ceremony bound to a server challenge API (`ChallengeName`, `Arg`), a submit API (`SubmitName`) and a direction (`IsCreate`).
  - `Fetch()`: gets the options from the server and caches them for the publicKey `timeout` minus 15 s (60 s if there is no usable timeout). It applies `RemoveNulls`.
  - `Run()`: uses the cached options (or fetches them), clears the cache (a challenge is single-use) and calls `PassKey.Create` or `PassKey.Get`.
  - `UserName` getter: `publicKey.user.name` from the create options.

  Pre-fetching exists so that `navigator.credentials.*` is called directly inside the click gesture (Safari requirement).
- **`class PassKey`** (static only):
  - `ApiPrefix = "../Api/auth/passkey/"`; `Call(name, arg)` is a POST through `sendRequest(..., reload=true, usePost=true)`.
  - `Errors`: mirrors the server `PassKeyErrors` (0..11), plus the client-only codes `Cancelled` (-1) and `AlreadyOnDevice` (-2). `ErrorText(result)` returns translated messages.
  - `IsSupported()`: `PublicKeyCredential`, `navigator.credentials` and `isSecureContext` must all be present. `IsAutofillSupported()`: `isConditionalMediationAvailable()`.
  - `GetLoggedInUser()`: `GET ../Api/auth/GetUser` (MicroService.Auth); returns the `AuthInfo` when `Succeeded`, otherwise null.
  - `Get(c, submitName, extra)` (assertion):
    1. Decodes `challenge` and `allowCredentials[].id` from base64.
    2. Calls `navigator.credentials.get({publicKey, ...extra})`.
    3. POSTs `{challengeId, authenticatorAttachment, id, rawId, response:{authenticatorData, clientDataJSON, signature, userHandle}, type}` (base64) to `submitName`.
    4. On `UnknownCredential` it calls `PublicKeyCredential.signalUnknownCredential` when the browser has it.
    5. On success it records device hints and stores `result.User` in `sessionStorage`.
  - `Create(c, submitName)` (registration): decodes `challenge`, `user.id` and `excludeCredentials[].id`, then calls `navigator.credentials.create` and POSTs `attestationObject`, `clientDataJSON` and `transports`. On success it does the same hint and user bookkeeping as `Get`.
  - Browser exceptions are mapped by `#FromBrowserError`: `NotAllowedError` / `AbortError` -> `Cancelled`, `InvalidStateError` -> `AlreadyOnDevice`, `SecurityError` -> `NotSupported`. Anything else is re-thrown.
  - Ceremony factories:

    | Factory | Challenge API | Submit API |
    |---|---|---|
    | `SignIn(userId?)` | `GetUserAuthChallenge(userId)` or `GetAuthChallenge` | `Auth` |
    | `Attach()` | `GetCreateChallenge` | `Create` |
    | `AttachUsingToken(token)` | `GetResetChallenge(token)` | `New` |
    | `NewAccount(token)` | `GetNewChallenge(token)` | `New` |

  - Management: `GetPassKeys()`, `Rename(id, name)` (`RenamePassKey {Id, Name}`), `Remove(id, userGuid)` (`DeletePassKey`; also forgets the local hints).
  - `Autofill(onResult)`: a loop that fetches `GetAuthChallenge` and runs a conditional-mediation `Get`. An `AbortController` aborts and restarts it before the challenge expires. A cancelled run restarts immediately; after a failure the loop waits 1 s and starts again. It returns `{Start(), Stop()}`; a run-id counter makes sure that only one loop is active at a time.
  - Device hints in `localStorage` (only hints, because browsers do not list passkeys):

    | Key | Content | Set when |
    |---|---|---|
    | `SysWeaver.PassKey.Hint` | `"1"` | A platform (not `cross-platform`) passkey was created or used here |
    | `SysWeaver.PassKey.Local` | Array of credential ids (base64url, at most 64) | Same |
    | `SysWeaver.PassKey.UserOnDevice` | Array of user GUIDs | The browser refused a second passkey for that user |

    Readers are `HaveHint()`, `IsOnDevice(id)` (converts the server's standard base64 to base64url) and `IsUserOnDevice(guid)`. `SetUserOnDevice(guid)` is public; `#ForgetOnDevice` is called on remove.
- **`addPassKeyMain()`**: reads `?token=` with `getUrlParams()`.
  - With a token (QR or link): no user may be signed in, and the ceremony is `AttachUsingToken`.
  - Without a token: a user must be signed in, and the ceremony is `Attach`.

  It pre-fetches the challenge and shows "Add a passkey for {user}" (the name comes from the create options or from `GetUser`). The *Add passkey* button runs the ceremony. On success the page navigates with `AuthStartPage()` when a token was used or the server signed a user in; otherwise it shows "Passkey added!". `TokenExpired` / `AlreadyRegistered` / `AlreadyOnDevice` end the page with a warning icon.
- **`usePassKeyMain()`**: refuses if a user is already signed in. It pre-fetches an any-passkey challenge; the *Sign in with passkey* button runs it and calls `AuthStartPage()` on success.
- **`addPassKeyOtherMain()`**: requires a signed-in user. It reads `GetQRLifeTime` (minutes, default 15) and calls `AuthPage("../Api/auth/passkey/GetQR.svg", ..., iconIsFile=true)`, so the page icon *is* the QR code `<img>`. The server encodes `auth/AddPassKey.html?token=<share-device token>` into the QR. Text: "may only be used once and expires in N minutes".
- **`myPassKeysMain()`**: requires a signed-in user. It builds a `SysWeaver-AuthList`, and its `rebuild()` renders one entry per key. Each entry has the name (plus "(this device)" when `key.ThisDevice` or `PassKey.IsOnDevice`), live "Created" / "Last used" times (`ValueFormat.updateDateTimeLive` with an "{0} ago" format), the type (Synced / Device bound) and *Rename* / *Remove* buttons.
  - Rename opens a `PopUp` with an auth box (class `SysWeaver-PassKeyPopUp`).
  - Remove asks with `Confirm`; `LastSignInMethod` gets a dedicated message.
  - An *Add passkey* button at the bottom pre-fetches `Attach()`. It is disabled with an explanatory title when a passkey of this user is known to be on the device. When the add fails with `AlreadyOnDevice` / `AlreadyRegistered`, the page records `SetUserOnDevice` and rebuilds.
  - This page needs `PopUp` and `Confirm` from `application.js`.

**`Passkey.css`**: icon masks `IconAddPasskey`, `IconUsePasskey`, `IconPasskeyKey`, `IconPasskeyRename`. `form.SysWeaver-AuthBox.SysWeaver-PassKeyPopUp` resets the floating-box style inside pop-ups (the same pattern as `SysWeaver-SimpleAuth` in Auth.Core). `.PasskeyIconButton` centers the small key icon on the login page.

**HTML pages**: shells only. Menu entries (`PassKeyService`):
- `User/UsePassKey`: only when no one is signed in and the origin supports passkeys.
- `User/AddPassKey`: signed in and no passkey assigned to this device id.
- `User/AddPassKeyOther`: signed in and QR support is available.
- `User/MyPassKeys`: the user has passkeys.

### Flows

**Passkey sign in** (`UsePassKey.html`, or from `Login.html`):
1. `GetUser` must not be signed in.
2. `POST Api/auth/passkey/GetAuthChallenge` (or `GetUserAuthChallenge(userId)` when a user id was typed on the login page).
3. Click -> `navigator.credentials.get`.
4. `POST Api/auth/passkey/Auth` with the assertion. The server verifies it and signs the session in.
5. The result is `{Error:0, User}`: `sessionStorage["SysWeaver.User"]` is set and the local hints are recorded.
6. `AuthStartPage()`: after 3 s, reload at the app root.

**Passkey autofill** (`Login.html`): the same as passkey sign in, but with `mediation:"conditional"` running in the background, restarted until the user picks a passkey from the user-id autofill list.

**Add a passkey to the signed-in user** (`AddPassKey.html` without a token, or *Add passkey* on `MyPassKeys.html`):
1. `GetCreateChallenge` (`[WebApiAuth]`).
2. `navigator.credentials.create`.
3. `Create`.
4. Rebuild the list, or show "Passkey added!".

**Add a passkey on another device**:
1. Device A opens `AddPassKeyOnOtherDevice.html`, which shows the QR from `GetQR.svg` (`GetQRLifeTime` for the text).
2. Device B scans it and opens `auth/AddPassKey.html?token=...`.
3. `GetResetChallenge(token)`.
4. Create.
5. `New` signs device B in.
6. `AuthStartPage()`.

**Account creation or password reset with a passkey** (pages of UserManager): `NewAccount(token)` (`GetNewChallenge` then `New`) on `ChoosePassword.html`; `AttachUsingToken(token)` (`GetResetChallenge` then `New`) on `ResetPassword.html`.

**Manage passkeys** (`MyPassKeys.html`): `GetPassKeys`, `RenamePassKey`, `DeletePassKey`. Delete also clears the local hints for that credential and user.

### Server interaction

All calls go to `PassKeyService` (`[WebApiUrl("auth/passkey")]`) as POST with a JSON body (`PassKey.Call`). The only exception is `GetQR.svg`, which is loaded as an `<img>`.

| API | Auth | Arg |
|---|---|---|
| `GetAuthChallenge` | none | null |
| `GetUserAuthChallenge` | none | user id |
| `Auth` | none | assertion |
| `GetCreateChallenge` | `[WebApiAuth]` | null |
| `Create` | `[WebApiAuth]` | attestation |
| `GetResetChallenge` | none | token |
| `GetNewChallenge` | none | token |
| `New` | none | attestation |
| `GetPassKeys` | `[WebApiAuth]` | null |
| `RenamePassKey` | `[WebApiAuth]` | `{Id, Name}` |
| `DeletePassKey` | `[WebApiAuth]` | id |
| `GetQRLifeTime` | none | null |
| `GetQR.svg` | session user | none |

The challenge options carry `challengeId`, `publicKey` and `error`; results carry `Error`, `User` and `Message`. The pages also call `../Api/auth/GetUser` (MicroService.Auth). The URL parameter `token` is read by `AddPassKey.html` only.

### State and persistence

- `localStorage`: the three `SysWeaver.PassKey.*` keys (see above). Every access is wrapped in try/catch.
- `sessionStorage["SysWeaver.User"]`: written after a successful ceremony that returns a `User`.
- Cached challenges live in `PassKeyChallenge` instances (in memory, single-use).
- Sign-in state is the server session (cookie). Every flow that signs a user in finishes with `AuthStartPage()` (reload at the root). `AuthPage` sets `IgnoreUserChanged`, so the page is not reloaded by the resulting `user.login` event.

### Dependencies

- **common.js:** `sendRequest`, `getRequest`, `getUrlParams`, `RemoveNulls`, `base64ToArray`, `bufferToBase64`, `delay`, `_T` / `_TF`, `makeHtmlSafe`, `Fail`, `ValueFormat`.
- **application.js** (MyPassKeys): `PopUp`, `Confirm`.
- **MicroService.Auth `auth/`:** `AuthPage`, `AuthText`, `AuthLabel`, `AuthInput`, `AuthButton`, `AuthButtonRow`, `AuthHr`, `CreateAuthWrapper`, `AuthStartPage`, `AuthTrim`; `Auth.css` (`SysWeaver-AuthList`, `.Highlight`, `.NoMargin`, `.Hide`, `.Info`, `.Fail`, `IconWarning`); the `GetUser` API.
- **Consumers:** `AuthUi.js` `loginMain` (`PassKey.IsSupported`, `HaveHint`, `IsAutofillSupported`, `Autofill`, `SignIn`, `ErrorText`, `Errors`); `UserManagerUi.js` `choosePasswordMain` / `resetPasswordMain` (`NewAccount`, `AttachUsingToken`). `UserManagerService_PassKey.cs` provides the storage side on the server.

### Structural observations

- This is the only `auth/` contribution written with ES classes (static members, private `#` fields). The other three use plain global functions. `PassKey` and `PassKeyChallenge` are globals.
- The "pre-fetch the challenge, then run it on click" pattern is repeated in each page and in the consumer pages (`Login.html`, `ChoosePassword.html`, `ResetPassword.html`), each followed by a re-fetch after a failure.
- The page `done(message, icon)` helpers are duplicated (with slight differences) between `addPassKeyMain` and `usePassKeyMain`.
- The error codes are mirrored by hand from the server enum `PassKeyErrors`.
- Token conventions differ inside `auth/`: these pages read the named parameter `?token=`, while the UserManager pages treat the whole query string as the token.
- `#SetUser` duplicates the `sessionStorage["SysWeaver.User"]` write found in `Auth.js` and `UserManager.js`.
- `PopUp` / `Confirm` come from the app shell, so only `MyPassKeys.html` loads `application.js`. The other pages rely on not calling them.
