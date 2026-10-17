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

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function; `#` marks ES private class members; "static" members are called on the class (`PassKey.X`), never on an instance.

### `Passkey.js`

Client side of the passkey (WebAuthn) service. It is loaded with a static deferred `<script src="Passkey.js">` after `common.js`, `Auth.js` and `AuthUi.js` by this folder's pages (`AddPassKey.html`, `AddPassKeyOnOtherDevice.html`, `MyPassKeys.html`, `UsePassKey.html`), and also by pages of other folders: `Login.html` (MicroService.Auth) and `ChoosePassword.html` / `ResetPassword.html` (MicroService.UserManager). It defines two global classes, `PassKeyChallenge` and `PassKey`, plus four page entry points called from `<body onload>`. No code runs at load time. It talks to `../Api/auth/passkey/*` and `../Api/auth/GetUser`, and uses `localStorage` keys `SysWeaver.PassKey.Hint`, `SysWeaver.PassKey.Local`, `SysWeaver.PassKey.UserOnDevice` and `sessionStorage["SysWeaver.User"]`.

#### Globals
| Name | Kind | Description |
|---|---|---|
| `PassKeyChallenge` | class | Pre-fetchable server challenge plus the ceremony that uses it (see below). |
| `PassKey` | class | Static-only facade for all passkey operations, error codes and device hints (see below). Never instantiated. |

#### Functions
| Function | Signature | Description |
|---|---|---|
| `addPassKeyMain` | async `addPassKeyMain()` | Entry point of `AddPassKey.html`. Calls `AuthPage("IconAddPasskey", ...)`; reads the optional `token` URL parameter (`getUrlParams`). Without a token a signed-in user is required (`PassKey.Attach()`); with a token no user may be signed in (`PassKey.AttachUsingToken(token)`). Pre-fetches the challenge, shows "Add a passkey for {user}" (name from `ceremony.UserName` or the signed-in user) and adds an "Add passkey" `AuthButton` that runs the ceremony. On success shows "Passkey added!" and calls `AuthStartPage()` if a token was used or a user was returned; `TokenExpired`, `AlreadyRegistered`, `AlreadyOnDevice` end the page with a warning; other non-cancel errors call `Fail`. Text element gets class `Info`/`Fail`, the button `Hide`. |
| `done` (inner of `addPassKeyMain`) | `done(message, icon)` | Ends the page: optionally swaps the header image (`img.ChangeImage(icon)`), shows `message` with class `Info` (removing `Fail`), and hides the add button if created. |
| `usePassKeyMain` | async `usePassKeyMain()` | Entry point of `UsePassKey.html`. Calls `AuthPage("IconUsePasskey", ...)`. Refuses if passkeys are unsupported or a user is already signed in. Pre-fetches `PassKey.SignIn()` and adds a "Sign in with passkey" button; on success hides the button, shows "Signed in!" and calls `AuthStartPage()`; errors are shown in the text (class `Fail`) and, unless cancelled, via `Fail`. |
| `done` (inner of `usePassKeyMain`) | `done(message, icon)` | Optionally swaps the header image and shows `message` with class `Info`. |
| `addPassKeyOtherMain` | async `addPassKeyOtherMain()` | Entry point of `AddPassKeyOnOtherDevice.html`. Requires a signed-in user (otherwise an `IconWarning` page). Reads the QR lifetime in minutes (`PassKey.Call("GetQRLifeTime")`, default 15) and calls `AuthPage(PassKey.ApiPrefix + "GetQR.svg", ..., true)` so the header image is the server-generated QR code; adds an HTML label naming the user (escaped with `makeHtmlSafe`) and a text with the expiry. |
| `myPassKeysMain` | async `myPassKeysMain()` | Entry point of `MyPassKeys.html`. Calls `AuthPage("IconUsePasskey", ...)`; requires a signed-in user. Creates a `<SysWeaver-AuthList>` element, an optional button row (only when `PassKey.IsSupported()`, class `NoMargin`) and calls `rebuild()`. |
| `stat` (inner of `myPassKeysMain`) | `stat(parent, label, value, isDate, title)` | Appends an `AuthText` line (class `NoMargin`) with `label` and a `<span>`; dates are rendered live with `ValueFormat.updateDateTimeLive(s, value, agoFormat)` ("{0} ago"), otherwise as text. Optional tooltip `title`. Returns the element. |
| `remove` (inner of `myPassKeysMain`) | async `remove(key, button)` | Asks for confirmation (`Confirm` from application.js), then `PassKey.Remove(key.Id, user.Guid)`; on success `rebuild()`, otherwise `Fail` with a special text for `LastSignInMethod`. Shows working state on `button`. |
| `rename` (inner of `myPassKeysMain`) | async `rename(key)` | Opens a `PopUp` (application.js) containing a `CreateAuthWrapper()` box with class `SysWeaver-PassKeyPopUp`, a name `AuthInput` (max 64 chars, prefilled) and a "Rename" button (`IconPasskeyRename`) enabled only when the trimmed name is non-empty and changed. |
| `newName` (inner of `rename`) | `newName()` | Returns `AuthTrim(inp.value)`. |
| `canSave` (inner of `rename`) | `canSave()` | True if the new name is non-empty and differs from the old one. |
| `save` (inner of `rename`) | async `save()` | Calls `PassKey.Rename(key.Id, newName())`; on truthy result closes the pop-up and `rebuild()`s, otherwise `Fail` ("doesn't exist any more"). Input is read-only while working. |
| `rebuild` (inner of `myPassKeysMain`) | async `rebuild()` | Reloads `PassKey.GetPassKeys()` and re-renders the list: per key an `AuthHr` separator, an `AuthLabel` (class `Highlight`, "(this device)" suffix when on device), Created / Last used / Type stats and Rename / Remove buttons. Then rebuilds the add-button row: if any passkey is known to be on this device (`onDevice` or `PassKey.IsUserOnDevice`) the "Add passkey" button is disabled with an explanatory `title`; otherwise a `PassKey.Attach()` challenge is pre-fetched and the button runs it, calling `PassKey.SetUserOnDevice(user.Guid)` on `AlreadyOnDevice` / `AlreadyRegistered`. |
| `onDevice` (inner of `rebuild`) | `onDevice(key)` | True if `key.ThisDevice` (server says it was created from this session's device) or `PassKey.IsOnDevice(key.Id)`. |

#### class `PassKeyChallenge`
A server challenge fetched before the user clicks, so that `navigator.credentials.get` / `create` can be called directly from the user gesture (required by Safari). Created only through the `PassKey` factory methods `SignIn`, `Attach`, `AttachUsingToken`, `NewAccount`; each `Run()` consumes the cached options, so callers re-`Fetch()` after a failure. Used by `AuthUi.js` `loginMain` (MicroService.Auth) and `UserManagerUi.js` `choosePasswordMain` / `resetPasswordMain` (MicroService.UserManager).

| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `new PassKeyChallenge(challengeName, arg, submitName, isCreate)` | Stores the challenge API name, its argument, the API to submit the browser response to, and whether this is a create (registration) ceremony. |
| `ChallengeName`, `Arg`, `SubmitName`, `IsCreate` | field | | Constructor arguments as above. |
| `Options` | field | | Cached server options (`{challengeId, publicKey, error}`) or `null`. |
| `Fetched` | field | | `Date.now()` of the last fetch (0 initially). |
| `Fetch` | async method | `Fetch()` → options | Returns the cached options if they have no `error` and are younger than `publicKey.timeout - 15000` ms (or 60 s if the timeout is missing or ≤ 20 s); otherwise calls `PassKey.Call(ChallengeName, Arg)`, strips nulls (`RemoveNulls`) and caches the result. Throws if the server returns nothing. Callers must check `error`. |
| `Run` | async method | `Run()` → `{Error, User?, Message?}` | Returns `NotSupported` if passkeys are unavailable; otherwise fetches (if needed), clears the cache, returns `{Error: c.error}` on a challenge error, else delegates to `PassKey.Create` or `PassKey.Get` with `SubmitName`. |
| `UserName` | getter | | `Options.publicKey.user.name` (user name for create ceremonies) or `null`. |

#### class `PassKey`
Static-only class: API access, error codes and texts, WebAuthn ceremonies, and per-browser hints about which passkeys exist on this device. Consumed from MicroService.Auth `AuthUi.js` (`IsSupported`, `IsAutofillSupported`, `Autofill`, `SignIn`, `HaveHint`, `ErrorText`, `Errors`) and MicroService.UserManager `UserManagerUi.js` (`IsSupported`, `NewAccount`, `AttachUsingToken`, `ErrorText`, `Errors`).

| Member | Kind | Signature | Description |
|---|---|---|---|
| `ApiPrefix` | static field | | `"../Api/auth/passkey/"`, base URL of the passkey API (also used for `GetQR.svg`). |
| `Errors` | static field | | Error codes mirroring server `PassKeyErrors`: `None` 0, `NotSupported` 1, `AlreadySignedIn` 2, `NotSignedIn` 3, `ChallengeExpired` 4, `UnknownCredential` 5, `NoPassKeys` 6, `VerificationFailed` 7, `AlreadyRegistered` 8, `TokenExpired` 9, `AccountExists` 10, `LastSignInMethod` 11; client-only `Cancelled` -1, `AlreadyOnDevice` -2. |
| `IsSupported` | static method | `IsSupported()` → boolean | True if `window.PublicKeyCredential` and `navigator.credentials` exist and the context is secure. |
| `ErrorText` | static method | `ErrorText(result)` → string | Localized (`_TF`) user text for `result.Error`; `""` for `None`, `AlreadyRegistered` and `AlreadyOnDevice` share a text, unknown codes get a generic failure text. |
| `Call` | static async method | `Call(name, arg)` → response | `sendRequest(ApiPrefix + name, arg, true, null, false, null, true)` (common.js). |
| `GetLoggedInUser` | static async method | `GetLoggedInUser()` → user or null | `getRequest("../Api/auth/GetUser")` (MicroService.Auth API); returns the object only if `Succeeded`. |
| `#SetUser` | private static method | `#SetUser(result)` | On `Error == 0` with a `User`, writes it as JSON to `sessionStorage["SysWeaver.User"]`. |
| `#HintKey` | private static field | | `"SysWeaver.PassKey.Hint"` localStorage key. |
| `HaveHint` | static method | `HaveHint()` → boolean | True if the hint key is set, i.e. a passkey was created or used in this browser on this device (a hint only; storage errors give false). Used by `loginMain` to choose between the full "Use passkey" button and the small `PasskeyIconButton`. |
| `#SetHint` | private static method | `#SetHint(authenticatorAttachment, credentialId)` | Unless the attachment is `"cross-platform"` (phone / security key), sets the hint key and moves `credentialId` (base64url) to the end of the `#LocalKey` list. Errors ignored. |
| `#LocalKey` | private static field | | `"SysWeaver.PassKey.Local"`: JSON array of base64url credential ids created or used on this device. |
| `#UserOnDeviceKey` | private static field | | `"SysWeaver.PassKey.UserOnDevice"`: JSON array of user guids for which the browser refused to add another passkey. |
| `#GetList` | private static method | `#GetList(key)` → array | Parses a JSON array from localStorage; `[]` on missing, non-array or error. |
| `#SetList` | private static method | `#SetList(key, list)` | Trims the list to the last 64 entries and stores it as JSON. Errors ignored. |
| `#ToBase64Url` | private static method | `#ToBase64Url(s)` → string | Converts standard base64 (server ids) to base64url (browser ids): `+`→`-`, `/`→`_`, strips `=`. |
| `IsOnDevice` | static method | `IsOnDevice(id)` → boolean | True if the server credential id (standard base64) is in the `#LocalKey` list. |
| `IsUserOnDevice` | static method | `IsUserOnDevice(userGuid)` → boolean | True if the guid is in the `#UserOnDeviceKey` list. |
| `SetUserOnDevice` | static method | `SetUserOnDevice(userGuid)` | Moves the guid to the end of the `#UserOnDeviceKey` list (no-op for empty guid). |
| `#ForgetOnDevice` | private static method | `#ForgetOnDevice(id, userGuid)` | After removal: drops the credential id from `#LocalKey` and, if given, the user guid from `#UserOnDeviceKey` (the browser's refusal may have referred to the removed key). |
| `IsAutofillSupported` | static async method | `IsAutofillSupported()` → boolean | True if supported and `PublicKeyCredential.isConditionalMediationAvailable()` resolves true; false on any error. |
| `Autofill` | static method | `Autofill(onResult)` → `{Start(), Stop()}` | Starts conditional-mediation sign-in (passkeys shown in the autofill of inputs with `autocomplete="username webauthn"`) and returns a controller; already started on return. Internal `state` holds a run id (`Run`) and the current `AbortController` (`Abort`). |
| `run` (inner of `Autofill`) | async `run(runId)` | | Loop while `state.Run === runId`: fetch `GetAuthChallenge` (exit on error), arm a timer that aborts before the challenge expires (same age rule as `Fetch`), call `PassKey.Get(c, "Auth", {mediation: "conditional", signal})`. Browser exceptions become `VerificationFailed` with `Message`. `Cancelled` (incl. abort) restarts with a new challenge; other results go to `await onResult(r)`; stops on success, otherwise waits 1 s (`delay`) and restarts. |
| `Start` (method of the `Autofill` controller) | method | `Start()` | Increments the run id and starts a new `run` loop. |
| `Stop` (method of the `Autofill` controller) | method | `Stop()` | Increments the run id (ending the loop) and aborts the pending `navigator.credentials.get`. Must be called before starting another WebAuthn request. |
| `#FromBrowserError` | private static method | `#FromBrowserError(e)` → result | Maps DOMException names: `NotAllowedError` / `AbortError` → `Cancelled`, `InvalidStateError` → `AlreadyOnDevice`, `SecurityError` → `NotSupported` (with `Message`); rethrows others. |
| `#Ids` | private static method | `#Ids(list)` | Converts each `id` of a credential descriptor list from base64 to an ArrayBuffer (`base64ToArray`), in place. |
| `#Transports` | private static method | `#Transports(res)` → array or null | `res.getTransports()` if available, null otherwise or on error. |
| `Get` | static async method | `Get(c, submitName, extra)` → result | Assertion ceremony: decodes `challenge` and `allowCredentials`, calls `navigator.credentials.get({publicKey, ...extra})`, posts `{challengeId, authenticatorAttachment, id, rawId, response: {authenticatorData, clientDataJSON, signature, userHandle}, type}` (base64 via `bufferToBase64`) to `submitName`. Throws on no server response. On `UnknownCredential` calls `PublicKeyCredential.signalUnknownCredential` if available; on success `#SetHint`; always `#SetUser`. Browser errors go through `#FromBrowserError`; null credential → `Cancelled`. |
| `Create` | static async method | `Create(c, submitName)` → result | Registration ceremony: decodes `challenge`, `user.id`, `excludeCredentials`, calls `navigator.credentials.create`, posts `{..., response: {attestationObject, clientDataJSON, transports}}` to `submitName`. Same error, hint and user handling as `Get` (no unknown-credential signal). |
| `SignIn` | static method | `SignIn(userIdentifier)` → `PassKeyChallenge` | With an identifier: `GetUserAuthChallenge(userIdentifier)`; without: `GetAuthChallenge` (any passkey). Submits to `Auth`. |
| `Attach` | static method | `Attach()` → `PassKeyChallenge` | Add a passkey to the signed-in user: `GetCreateChallenge` → `Create`. |
| `AttachUsingToken` | static method | `AttachUsingToken(token)` → `PassKeyChallenge` | Add a passkey to a token's user (QR, reset-password or add-password link) and sign in: `GetResetChallenge(token)` → `New`. Called by `resetPasswordMain` in UserManagerUi.js. |
| `NewAccount` | static method | `NewAccount(token)` → `PassKeyChallenge` | Create an account with a passkey from a sign-up token and sign in: `GetNewChallenge(token)` → `New`. Called by `choosePasswordMain` in UserManagerUi.js. |
| `GetPassKeys` | static async method | `GetPassKeys()` → array | `Call("GetPassKeys")`: passkeys of the signed-in user (`Id, Name, Created, LastUsed, Synced, ThisDevice`). |
| `Rename` | static async method | `Rename(id, name)` → result | `Call("RenamePassKey", {Id, Name})`; truthy when renamed. |
| `Remove` | static async method | `Remove(id, userGuid)` → result | `Call("DeletePassKey", id)`; on `Error == 0` calls `#ForgetOnDevice(id, userGuid)`. `LastSignInMethod` when it is the last way to sign in. |

## CSS reference

### `Passkey.css`
Small add-on stylesheet loaded by a static `<link>` after `theme.css` and `Auth.css` (MicroService.Auth) on this folder's four pages and on `Login.html` (MicroService.Auth), `ChoosePassword.html` and `ResetPassword.html` (MicroService.UserManager). Not templated. Icon classes rely on the mask-based icon base styles (`ColorIcon` / button icons) from common/theme CSS; the icon SVGs are in this service's `icons/` folder (served as `../icons/`). No custom properties, keyframes or media queries.

#### Icons
| Selector(s) | Description |
|---|---|
| `.IconAddPasskey`, `.IconUsePasskey`, `.IconPasskeyKey`, `.IconPasskeyRename` | Mask icons from `../icons/passkey_add.svg`, `passkey_use.svg`, `passkey_key.svg`, `passkey_rename.svg`. Used as `AuthPage` header images and `AuthButton` icons in `Passkey.js` (add / use / rename), in `AuthUi.js` `loginMain` (`IconUsePasskey` button, `IconPasskeyKey` small icon) and in `UserManagerUi.js` (`IconAddPasskey` buttons on choose/reset password pages). |

#### Layout
| Selector(s) | Description |
|---|---|
| `form.SysWeaver-AuthBox.SysWeaver-PassKeyPopUp` | Resets every box styling of the `SysWeaver-AuthBox` form (from `CreateAuthWrapper()`, Auth.css) to `initial` so it renders plainly inside the rename pop-up; the class `SysWeaver-PassKeyPopUp` is added by `rename` in `myPassKeysMain`. |
| `.PasskeyIconButton` | `align-self: center` for the small 32x32 `IconPasskeyKey` `ColorIcon` that `AuthUi.js` `loginMain` shows instead of the "Use passkey" button when `PassKey.HaveHint()` is false. |
