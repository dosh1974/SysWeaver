# auth

[⬆ srcWeb](../README.md)

> The passkey pages linked from the *User* menu, the UI-less WebAuthn client `PassKey` (Passkey.js) used by them and by the sign in / account pages of other projects.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.PassKey](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Served under `auth/` together with the files of [SysWeaver.MicroService.Auth](../../../SysWeaver.MicroService.Auth/srcWeb/auth/README.md) (`Sw.Auth`, `Sw.AuthUi`, `Auth.css`). `Passkey.js` is an optional plug-in of `Login.html` (MicroService.Auth) and of `ChoosePassword.html` / `ResetPassword.html` (MicroService.UserManager): those pages test `typeof PassKey`, so they work without this project.

## Contents

| Item | Description |
|---|---|
| [`UsePassKey.html`](UsePassKey.html) | Sign in with a passkey (menu User/UsePassKey), `usePassKeyMain()` |
| [`AddPassKey.html`](AddPassKey.html) | Add a passkey on this device (menu User/AddPassKey, or `?token=` from a QR code / link), `addPassKeyMain()` |
| [`AddPassKeyOnOtherDevice.html`](AddPassKeyOnOtherDevice.html) | A single use QR code for another device (menu User/AddPassKeyOther), `addPassKeyOtherMain()` |
| [`MyPassKeys.html`](MyPassKeys.html) | The user's passkeys: rename, remove, add (menu User/MyPassKeys), `myPassKeysMain()` |
| [`Passkey.js`](Passkey.js) | UI-less: `PassKeyChallenge`, `PassKey` (also `Sw.PassKey`, `Sw.PassKeyChallenge`) |
| [`PasskeyUi.js`](PasskeyUi.js) | The four pages (`Sw.PasskeyUi`) |
| [`Passkey.css`](Passkey.css) | Passkey list detail styles and the passkey icon classes (`IconAddPasskey`, `IconUsePasskey`, `IconPasskeyKey`, `IconPasskeyRename`) |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.MicroService.PassKey](../../README.md)
- **Talks to:** `PassKeyService` (`Api/auth/passkey/*`), `Api/auth/GetUser`, the QR image `Api/auth/passkey/GetQR.svg`
- **Uses:** [`../icons/`](../icons/README.md) (passkey icons, classes in `Passkey.css`; `sw/ui.css` keeps a copy for compatibility), `Auth.js` / `AuthUi.js` / `Auth.css` of SysWeaver.MicroService.Auth, `sw/core.js`, `sw/ui.js` of SysWeaver.HttpServer
- **Used by:** `Login.html` (MicroService.Auth), `ChoosePassword.html`, `ResetPassword.html` (MicroService.UserManager)

## Behavior

Includes: `../sw/core.js`, `../sw/ui.js`, `Auth.js`, `AuthUi.js`, `Passkey.js`, `PasskeyUi.js`; `../common/theme.css`, `../sw/ui.css`, `Auth.css`, `Passkey.css`.

- **Challenges** are fetched before the click (cached for `publicKey.timeout` - 15 s, else 60 s, single use) so `navigator.credentials.get/create` runs directly in the user gesture (Safari). Ceremonies: SignIn (`GetAuthChallenge` / `GetUserAuthChallenge` + `Auth`), Attach (`GetCreateChallenge` + `Create`), AttachUsingToken (`GetResetChallenge(token)` + `New`), NewAccount (`GetNewChallenge(token)` + `New`). `byte[]` are standard base64, ids decoded in place.
- **Errors**: browser `NotAllowedError` / `AbortError` = Cancelled (-1, no toast), `InvalidStateError` = AlreadyOnDevice (-2), `SecurityError` = NotSupported; server codes 0..11 with translated texts (`PassKey.ErrorText`). On UnknownCredential `PublicKeyCredential.signalUnknownCredential` is called when available.
- **Autofill** (`PassKey.Autofill`): conditional mediation loop for `autocomplete="username webauthn"` inputs, restarted before the challenge expires, after a cancel at once, after failures in 1 s; `Stop()` before other WebAuthn requests.
- **UsePassKey**: refuses (warning icon and text) when unsupported, already signed in or the challenge fails; *Sign in with passkey* -> "Signed in!" -> `Sw.Auth.StartPage()`.
- **AddPassKey**: with `?token=` the link (QR code) is checked first without using it (`Sw.AuthUi.CheckLinkToken("AddPassKey", { Page })`): a dead one shows "This link no longer works" with the cause and "make a new QR code on the signed in device"; then no user may be signed in (token ceremony, signs in, then `StartPage`); without, a signed in user is required. Heading "Add a passkey for {user}"; TokenExpired / AlreadyRegistered / AlreadyOnDevice end the page with a warning.
- **AddPassKeyOnOtherDevice**: the page image is the QR code (`GetQR.svg`, encodes `auth/AddPassKey.html?token=..`) on a white plate, the user name and "may only be used once and expires in {N} minutes" (`GetQRLifeTime`, default 15); requires a signed in user.
- **MyPassKeys**: list items with name ("(this device)" when created / used here), live "Created" / "Last used" ("{0} ago"), type (Synced / Device bound, explained), *Rename* (dialog, max 64, enabled when changed and not empty, Enter saves) and *Remove* (danger confirm; LastSignInMethod gets a dedicated message); *Add passkey* (pre-fetched) is disabled with a visible explanation when a passkey of the user is known to be on this device; AlreadyOnDevice / AlreadyRegistered remember that.

### Storage (localStorage)

`SysWeaver.PassKey.Hint` ("1": a platform passkey was created / used in this browser; the login page then shows the *Use passkey* button instead of the small key icon), `SysWeaver.PassKey.Local` (JSON array, max 64, base64url credential ids created / used here), `SysWeaver.PassKey.UserOnDevice` (JSON array of user guids the browser holds a passkey for). Removing a passkey forgets its id and the user flag. sessionStorage `SysWeaver.User` gets the AuthInfo after a ceremony that signed in.

## JavaScript reference

### `Passkey.js`

| Member | Description |
|---|---|
| `new PassKeyChallenge(challengeName, arg, submitName, isCreate)` | `Fetch()`, `Run()` -> `{Error, User?, Message?}`, `UserName` (after Fetch), `Options`, `Fetched`. |
| `PassKey.Errors` | None 0, NotSupported 1, AlreadySignedIn 2, NotSignedIn 3, ChallengeExpired 4, UnknownCredential 5, NoPassKeys 6, VerificationFailed 7, AlreadyRegistered 8, TokenExpired 9, AccountExists 10, LastSignInMethod 11, Cancelled -1, AlreadyOnDevice -2. |
| `PassKey.IsSupported()`, `IsAutofillSupported()`, `ErrorText(result)` | Support checks, texts. |
| `PassKey.IsAvailable(refresh)` | Promise: `IsSupported()` (no request: this script is only loaded where the passkey service runs, `Sw.AuthUi.LoadPassKeys` asks `Api/HaveService` first; a failing challenge hides the passkey button, as before). `refresh` is ignored. Used by the Login, ChoosePassword and ResetPassword pages. |
| `PassKey.SignIn(user?)`, `Attach()`, `AttachUsingToken(token)`, `NewAccount(token)` | Ceremony factories. |
| `PassKey.Autofill(onResult)` | `{Start, Stop}`. |
| `PassKey.Get(c, submit, extra)`, `Create(c, submit)`, `Call(name, arg)`, `GetLoggedInUser()`, `GetPassKeys()`, `Rename(id, name)`, `Remove(id, userGuid)` | Wrappers. |
| `PassKey.HaveHint()`, `SetHint(attachment, id)`, `IsOnDevice(id)`, `IsUserOnDevice(guid)`, `SetUserOnDevice(guid)`, `ForgetOnDevice(id, guid)`, `SetUser(result)`, `MaxAge(c)`, `ApiPrefix` | Device hints and helpers. |

### `PasskeyUi.js`

Globals `usePassKeyMain`, `addPassKeyMain`, `addPassKeyOtherMain`, `myPassKeysMain` (also `Sw.PasskeyUi.*`).

## CSS reference (`Passkey.css`)

| Selector | Description |
|---|---|
| `.auth-item-value` | The live times in the list (plain text instead of the table cell look). |
| `.IconAddPasskey`, `.IconUsePasskey`, `.IconPasskeyKey`, `.IconPasskeyRename` | The passkey icon classes (mask images `../icons/passkey_*.svg` of this module). |

The layout, list, dialog and button styles are in `Auth.css`. `IconAddPasskeyOther` (qr_code.svg of the icon module) stays in `sw/ui.css`; `sw/ui.css` also still defines the four passkey classes (compatibility; not removed until every app is checked). The PassKey menu items use the svg urls (`icons/passkey_add.svg`, `icons/passkey_use.svg`) so the shell needs no passkey class.
