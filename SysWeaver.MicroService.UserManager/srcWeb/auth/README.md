# auth

[⬆ srcWeb](../README.md)

> The self-service account pages of the user manager, plus the API wrappers and UI logic behind them.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.UserManager](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

The pages are served as `auth/...`. That is the same folder as the authentication pages of [SysWeaver.MicroService.Auth](../../../SysWeaver.MicroService.Auth/README.md) and [SysWeaver.MicroService.PassKey](../../../SysWeaver.MicroService.PassKey/README.md). `UserManagerService` and `SignUpService` link to them from *User* menu entries, which are shown only when the action applies, and from the e-mail and text message links (with an action token). Each page has a `*Main()` entry point in `UserManagerUi.js`. `UserManager.js` wraps the service API calls (`../Api/auth/...`).

## Contents

| Item | Description |
|---|---|
| [`SignUp.html`](SignUp.html) | Sign up for a new account |
| [`InviteUser.html`](InviteUser.html) | Invite a new user (privileged) |
| [`ChoosePassword.html`](ChoosePassword.html) | Choose a password when completing an account from a token link |
| [`SetPassword.html`](SetPassword.html) | Change the password of the signed in user |
| [`AddPassword.html`](AddPassword.html) | Add a password to an account |
| [`DeletePassword.html`](DeletePassword.html) | Remove the password from an account |
| [`ForgotPassword.html`](ForgotPassword.html) | Request a password reset |
| [`ResetPassword.html`](ResetPassword.html) | Reset the password from a token link |
| [`AddEmail.html`](AddEmail.html) / [`ChangeEmail.html`](ChangeEmail.html) / [`DeleteEmail.html`](DeleteEmail.html) | Add, change or remove an e-mail address |
| [`AddPhone.html`](AddPhone.html) / [`ChangePhone.html`](ChangePhone.html) / [`DeletePhone.html`](DeletePhone.html) | Add, change or remove a phone number |
| [`DeleteAccount.html`](DeleteAccount.html) | Delete the account |
| [`UserManager.js`](UserManager.js) | API wrappers: password, sign-up, invite, e-mail/phone, nick name, language, short code helpers |
| [`UserManagerUi.js`](UserManagerUi.js) | Page logic, one `*Main()` function per page |
| [`UserManager.css`](UserManager.css) | Styles for the account pages |

## Limitations and considerations

- `UserManager.js` defines `isManagedUser()` twice with identical bodies.

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.MicroService.UserManager](../../README.md)
- **Uses:** `Auth.js` / `AuthUi.js` from [SysWeaver.MicroService.Auth](../../../SysWeaver.MicroService.Auth/README.md), shared web assets of [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/README.md)

## Behavior (web analysis)

### Loading and initialization

- All 15 HTML files are identical shells apart from `<title>` and the `onload` entry point. Scripts are `defer`-loaded in this order: `../common/common.js`, `Auth.js`, `AuthUi.js` (SysWeaver.MicroService.Auth), `UserManager.js`, `UserManagerUi.js`. Styles: `../common/theme.css`, `Auth.css`, `UserManager.css`.
- `ChoosePassword.html` and `ResetPassword.html` also load `Passkey.js` (before `Auth.js`) and `Passkey.css` from SysWeaver.MicroService.PassKey. They offer a "create a passkey" option only when `PassKey` is defined, supported, and the server issues a challenge for the token.
- No page loads the app shell (`application.js`). The pages are reached from the *User* menu (embedded) or from links in e-mails and text messages (stand-alone).
- Every entry point wraps its work in `AuthPage(icon, build)`, which builds the floating auth box, sets `IgnoreUserChanged` and calls `PageLoaded`.
- No `${Variable}` template substitution is used. The server builds the links (`<prefix>auth/<Page>.html?<token>`) in C#.

### Files

**`UserManager.js`** (API wrappers, global async functions; every URL is a hard-coded `../Api/auth/...`):
- **Password proof helpers.** These call `GetUserSalt(user)` (LoginService), compute `Hash = SHA256(SHA256(pw|salt)|oneTimePad)` and send `{Hash, OneTimePad, ...}`. Some also send `NewHash = SHA256(newPw|salt)`.
  - `setPasswordRequest(user, pw, newPw)` -> `SetPassword`
  - `changeEmailRequest` -> `SendChangeEmailRequest {Email}`
  - `addEmailRequest` -> `SendAddEmailRequest {Email}`
  - `deleteEmail` -> `DeleteEmail`
  - `changePhoneRequest` -> `SendChangePhoneRequest {Phone}`
  - `addPhoneRequest` -> `SendAddPhoneRequest {Phone}`
  - `deletePhone` -> `DeletePhone`
- **Token consumers** (argument = token or short code):
  - `deleteUserRequest` -> `DeleteUser`
  - `deletePassword` -> `DeletePassword`
  - `changeEmail` and `addEmail` -> both `AddEmail`
  - `changePhone` and `addPhone` -> both `AddPhone`
- **Token plus new password** (`NewHash = SHA256(pw|salt)`, where the salt comes from the token data):
  - `addUserRequest(token, salt, pw)` -> `AddUser {Token, NewHash}`
  - `resetPasswordRequest(token, pw, salt)` -> `ResetPassword {Token, NewHash}`

  Both write or remove `sessionStorage["SysWeaver.User"]`.
- **Message senders** (argument `0`; they return the targets the message was sent to, or null):
  - `sendDeleteUserRequest` -> `SendDeleteUserRequest`
  - `sendAddPasswordRequest` -> `SendAddPasswordRequest`
  - `sendDeletePasswordRequest` -> `SendDeletePasswordRequest`
  - `forgotPasswordRequest(userId)` -> `ForgotPassword`
  - `signUpRequest(target)` -> `SignUp`
- `inviteUserRequest(target, name, tokens, domain)` -> `InviteUser {Email, Name, Tokens, Domain}`. Tokens are trimmed and empty ones dropped; an empty list becomes null.
- `getCreatePasswordPolicy()` -> `GET GetCreatePasswordPolicy`.
- Short codes: `validateShortCode(code)` -> `ValidateShortCode` (returns 1, 0 or -1). `cleanUpShortCode(code, noLengthCheck)` keeps digits only and requires at least 3 of them. **Both are used by `AuthPageValidation` in `AuthUi.js`.**
- Not used by any page in the repository: `setNickName`, `setLanguage`, `isManagedUser` (defined twice), `userManagerProps`. They are wrappers kept for application code.

**`UserManagerUi.js`** (one `*Main()` per page; each builds its form inside `AuthPage`):

| Entry | Page / icon | Behavior |
|---|---|---|
| `setPasswordMain` | SetPassword / `IconAuthSetPassword` | Current password plus new password (`AuthNewPassword` with the create policy). Both are validated against the policy, and the new one is leak-checked. On click: `GetUser` (for the user name), then `setPasswordRequest`. |
| `choosePasswordMain` | ChoosePassword / `IconChoosePassword` | Token = whole `location.search` without `?` (URI-decoded; it must be longer than 3 characters). `GetNewUserData(token)` returns `Salt`, `NickName`, `UserName`. Shows "Welcome {user}". Pre-fetches `PassKey.NewAccount(token)` when possible. *Set password* calls `addUserRequest` and then `AuthStartPage()`. *Create a passkey* runs `newPassKey.Run()` and then `AuthStartPage()`. |
| `deleteAccountMain` | DeleteAccount / `IconAuthDeleteAccount` (token) or `IconAuthDeleteMail` | Without a token: `sendDeleteUserRequest`, then the short-code step leading to `DeleteAccount.html?<code>`. With a token: a *Click to delete account* button calls `deleteUserRequest(token)`. The computed `ps` (`getUrlParams()`) is not used. |
| `signUpMain` | SignUp / `IconAuthSignUp` | `GET SignUpMethods` (`"Email"` / `"Phone"`). Builds an `AuthTab` with one `SysWeaver-AuthTabContent` per supported method: an e-mail input validated by `ValidateEmailAddress`, or a phone input (`AuthInputPhone`) validated by `GetPhoneNumberInfo`. The content elements carry the expandos `SetStatus` / `GetValue`. *Sign up* calls `signUpRequest(value)`, then the short-code step leading to `ChoosePassword.html?<code>`. Initial selection is the hard-coded `tab.SetIndex(1)`. |
| `forgotPasswordMain` | ForgotPassword / `IconAuthForgotPassword` | User id input (must not be empty). `forgotPasswordRequest`, then the short-code step leading to `ResetPassword.html?<code>`. |
| `resetPasswordMain` | ResetPassword / `IconAuthResetPassword` | Token from the query string. `GetNewPasswordData(token)` returns `Salt`, `NickName`, `UserName`. Pre-fetches `PassKey.AttachUsingToken(token)`. *Set password* calls `resetPasswordRequest` and requires `Error == 0`, then `AuthStartPage()`. *Create a passkey* runs the passkey ceremony and then `AuthStartPage()`. |
| `addPasswordMain` | AddPassword / `IconAuthAddPassword` | Without a token: `sendAddPasswordRequest`, then the short-code step leading to `AddPassword.html?<code>`. With a token: `GetNewPasswordData`, a new-password form, `resetPasswordRequest`, then `AuthStartPage()`. |
| `deletePasswordMain` | DeletePassword / `IconAuthDeletePassword` | Without a token: `sendDeletePasswordRequest`, then the short-code step leading to `DeletePassword.html?<code>`. With a token: a confirm button calls `deletePassword(token)`. |
| `inviteUserMain` | InviteUser / `IconAuthInviteUser` | `GetUser` must be `Succeeded`. Fields: e-mail (`isValidEmail`, client-side), optional user name, and comma-separated security tokens (shown only if the inviter has `Tokens`; the tooltip lists them). Calls `inviteUserRequest`; the `domain` argument is never passed. |
| `changeEmailMain` / `addEmailMain` | ChangeEmail / AddEmail | Without a token: current password plus new e-mail (`AuthGetEmailError`), then `GetUser`, then `change/addEmailRequest`, then the short-code step leading to `ChangeEmail.html?` / `AddEmail.html?<code>`. With a token: a button calls `changeEmail` / `addEmail(token)` (both call the `AddEmail` API). |
| `deleteEmailMain` | DeleteEmail | Single phase: current password, then `GetUser`, then `deleteEmail`. Shows "Sent restore instructions to {targets}". |
| `changePhoneMain` / `addPhoneMain` / `deletePhoneMain` | ChangePhone / AddPhone / DeletePhone | The same as the e-mail variants, with `AuthInputPhone` / `AuthGetPhoneError` and the `AddPhone` / `DeletePhone` APIs. |

Each form follows the same shape: inputs, an info line `AuthError(..., policyText)` and a button row. A `setStatus` closure validates on input and enables or disables the main button. The click handler sets the button to working and the inputs to read-only, calls the API, and on success either shows a message, calls `AuthStartPage()` or switches to the short-code step. On failure it shows `Fail(...)`, a temporary text for 5 s (`SetTemporaryInnerText`), re-enables the inputs and focuses one.

**`UserManager.css`**: only icon mask classes (`IconAuthSignUp`, `IconChoosePassword`, `IconAuthSetPassword`, `IconAuthForgotPassword`, `IconAuthResetPassword`, `IconAuthDeleteAccount`, `IconAuthDeleteMail`, `IconAuthInviteUser`, `IconAuth{Add,Delete}Password`, `IconAuth{Add,Change,Delete}Email`, `IconAuth{Add,Change,Delete}Phone`), pointing to `../icons/user_*.svg`. Layout comes entirely from `Auth.css`.

**HTML shells**: see *Contents*. Menu visibility is decided on the server (`UserManagerService` `Can*` predicates; `SignUpService` for `User/SignUp`; `User/Management/InviteUser` requires the role `UserManagerRoles.InviteUser`).

### Flows

The **short-code step** (`AuthPageValidation`, in `AuthUi.js`) is shared by every flow that sends a message:
1. The server sends a message with a link (`auth/<Page>.html?<long token>`) and also stores a short numeric code in the **current HTTP session** (`UserManager.ShortCode`, one slot per session, limited tries, expiry).
2. The page replaces the form with "Code sent to {targets}" and a code input.
3. `ValidateShortCode(code)` returns 1, 0 or -1.
4. On 1 the page navigates to `<Page>.html?<code>`. The short code is then accepted in place of the long token by the server's token lookups (`GetAction`), but only in the same session.

The user can either type the code in the same browser or follow the link (any browser).

1. **Sign up:**
   1. `SignUp.html` calls `SignUpMethods`.
   2. The user enters an e-mail or phone number (validated by `ValidateEmailAddress` / `GetPhoneNumberInfo`, with `GetPhonePrefixInfo` for the flag).
   3. `SignUp(target)` (`SignUpService`; refused if signed in).
   4. Code step, then `ChoosePassword.html?<code|token>`.
   5. `GetNewUserData(token)`.
   6. Either `AddUser {Token, NewHash}`, or the passkey path (`GetNewChallenge`, `credentials.create`, `New`). Both sign the session in.
   7. `AuthStartPage()`.
2. **Invite (privileged):** `InviteUser.html` -> `InviteUser {Email, Name, Tokens}` -> the invitee receives the link `ChoosePassword.html?user=..&salt=..&token=..`, and continues as in sign-up.
3. **Forgot / reset password:**
   1. `ForgotPassword.html` -> `ForgotPassword(userId)` -> code step -> `ResetPassword.html?<code|token>`.
   2. `GetNewPasswordData(token)`.
   3. Either `ResetPassword {Token, NewHash}` (`Error == 0`), or the passkey path (`GetResetChallenge`, `create`, `New`). Both sign the user in.
   4. `AuthStartPage()`.
4. **Change password:** `SetPassword.html` -> `GetUser` -> `GetUserSalt(username)` -> `SetPassword {Hash, OneTimePad, NewHash}`. The user stays on the page ("Password set.").
5. **Add password** (account without a password, e.g. passkey-only): `AddPassword.html` -> `SendAddPasswordRequest` -> code -> `AddPassword.html?<code>` -> `GetNewPasswordData` -> `ResetPassword` -> `AuthStartPage()`.
6. **Delete password:** `DeletePassword.html` -> `SendDeletePasswordRequest` -> code -> `DeletePassword.html?<code>` -> button -> `DeletePassword(token)`.
7. **Add / change e-mail or phone:**
   1. Enter the current password and the new value.
   2. `GetUser`, then `GetUserSalt`.
   3. `SendAdd*Request` / `SendChange*Request {Hash, OneTimePad, Email|Phone}`. The message goes to the *new* address.
   4. Code step, then `<Page>.html?<code>` -> button -> `AddEmail` / `AddPhone(token)`.
8. **Delete e-mail or phone:** current password -> `GetUser` -> `GetUserSalt` -> `DeleteEmail` / `DeletePhone {Hash, OneTimePad}`. The response lists the targets that received restore instructions.
9. **Delete account:** `DeleteAccount.html` -> `SendDeleteUserRequest` -> code -> `DeleteAccount.html?<code>` -> button -> `DeleteUser(token)`. The page shows "Account deleted"; there is no navigation.

### Server interaction

- All APIs are under `../Api/auth/` and are implemented by `UserManagerService` (`[WebApiUrl("auth")]`, partial classes `_PublicApi` / `_PriviledgedApi`) and `SignUpService`. Calls that require a signed-in session (`[WebApiAuth]`):
  - `SendDeleteUserRequest`, `SetPassword`
  - `SendAddPasswordRequest`, `SendDeletePasswordRequest`
  - `SendAddEmailRequest`, `SendChangeEmailRequest`, `DeleteEmail`
  - `SendAddPhoneRequest`, `SendChangePhoneRequest`, `DeletePhone`
  - `SetNickName`, `IsManagedUser`
  - `InviteUser` (role `InviteUser`)
- Token consumers (`AddUser`, `ResetPassword`, `DeletePassword`, `DeleteUser`, `AddEmail`, `AddPhone`, `GetNewUserData`, `GetNewPasswordData`) work without a signed-in session.
- Also used: `GetUserSalt` (LoginService), `GetUser`, `ValidateEmailAddress`, `GetPhoneNumberInfo`, `GetPhonePrefixInfo` (AuthManagerService), and the PassKey APIs (through `Passkey.js`).
- **URL parameters:** the whole query string (after `?`, URI-decoded) is the token or short code; there are no named parameters. The server builds links as `auth/<Page>.html?<UrlEncoded token>`. Two server link formats differ from that convention: `SendDeleteUserRequest` uses `DeleteAccount.html?token=<t>`, and `InviteUser` uses `ChoosePassword.html?user=..&salt=..&token=..`. With these, the client passes the full string, `token=` included, as the token.
- `AuthPageValidation` builds `<Page>.html?` + code on the client.

### State and persistence

- `sessionStorage["SysWeaver.User"]`: set or removed by `addUserRequest` and `resetPasswordRequest` (no reader).
- No `localStorage`. The PassKey hints are written only through `Passkey.js` when a passkey is created on ChoosePassword or ResetPassword.
- Server-side session state matters to the flows: the short code lives in the session, so typing the code only works in the browser and session that requested it. Sign-in after `AddUser` / `ResetPassword` / passkey `New` is applied to the session cookie on the server.
- Redirects: `AuthStartPage()` (3 s, then reload at the app root) after sign-in flows, and `Open("<Page>.html?<code>", "_self")` after the code step. Other flows end on the page with a status text.

### Dependencies

- **MicroService.Auth `auth/`:**
  - `AuthUi.js`: `AuthPage`, `AuthLabel`, `AuthInput`, `AuthInputPhone`, `AuthPassword`, `AuthNewPassword`, `AuthError`, `AuthText`, `AuthTab`, `AuthButton`, `AuthButtonRow`, `AuthSetText`, `AuthSetError`, `AuthUserHtml`, `AuthPageValidation`, `AuthStartPage`.
  - `Auth.js`: `AuthTrim`, `AuthPolicyText`, `ValidatePassword`, `CheckPassword`, `AuthGetEmailError`, `AuthGetPhoneError`.
  - `Auth.css`: all layout.
- **MicroService.PassKey `auth/`** (optional): `PassKey.IsSupported`, `NewAccount`, `AttachUsingToken`, `Errors`, `ErrorText`; `Passkey.css` (`IconAddPasskey`).
- **common.js:** `sendRequest`, `getRequest`, `hashString`, `getUrlParams`, `isValidEmail`, `_T` / `_TF` / `_TH`, `Fail`, `SetTemporaryInnerText`.
- **Consumers:** the server menus and message links. `AuthUi.js` (MicroService.Auth) depends back on `validateShortCode` / `cleanUpShortCode` from this folder.

### Structural observations

- `UserManagerUi.js` (~81 KB) is mostly near-identical blocks:
  - the add/change e-mail and phone pages differ only in texts, input widget and API;
  - delete e-mail and delete phone are twins;
  - choose, reset and add password share the "new password + optional passkey" form;
  - every "token or request" page uses the same two-mode structure (`if (token) {confirm button} else {send request -> AuthPageValidation}`).
- `UserManager.js` repeats the GetUserSalt / one-time-pad hashing 7 times and does not handle the binary-salt (`|` prefix) variant that `loginRequest` supports.
- Naming carried over from the login form: the "current password" input is called `uname` and the second input `pwd`, even when it holds an e-mail or phone number. The "current password" is validated against the *create* password policy.
- Token parsing (`decodeURIComponent(location.search).substring(1)`) is repeated in each page instead of a shared helper. It differs from PassKey's `?token=` named parameter and from two server link formats.
- `AuthPageValidation` lives in MicroService.Auth but depends on functions in this folder, which makes the cross-project coupling circular.
- `changeEmail` / `addEmail` and `changePhone` / `addPhone` call the same API (`AddEmail` / `AddPhone`); the pairs exist only as names.
- Hard-coded API prefix `../Api/auth/` everywhere (no `apiPrefix` parameter, unlike `Auth.js`).
- All functions are globals in a shared namespace with `Auth.js`, `AuthUi.js` and `Passkey.js`, so load order and unique names are implicit contracts.

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function; "proof hash" means `SHA256(SHA256(password|salt)|oneTimePad)` computed with `hashString` (common.js) after fetching `[salt, oneTimePad]` from `../Api/auth/GetUserSalt`. All requests use `sendRequest` (POST) or `getRequest` (GET) from `../common/common.js`; texts are localized with `_TF` / `_T` / `_TH` (common.js). Cross-folder symbols: `Auth*` UI helpers come from `AuthUi.js`, `AuthTrim` / `AuthPolicyText` / `ValidatePassword` / `CheckPassword` / `AuthGetEmailError` / `AuthGetPhoneError` from `Auth.js` (both in SysWeaver.MicroService.Auth `srcWeb/auth`), `PassKey` from `Passkey.js` (SysWeaver.MicroService.PassKey `srcWeb/auth`).

### `UserManager.js`

Global API wrapper functions for the user manager service (`UserManagerService` / `SignUpService`, all under the hard-coded prefix `../Api/auth/`). Loaded with `<script defer>` by all 15 HTML pages of this folder, after `common.js`, `Auth.js` and `AuthUi.js` and before `UserManagerUi.js`. It defines no variables and runs no code at load; only global function declarations. Inputs are passed through `AuthTrim` (Auth.js) before use. **Called from another folder:** `validateShortCode` and `cleanUpShortCode` are called by `AuthPageValidation` in `AuthUi.js` (SysWeaver.MicroService.Auth), which every page with a short-code step relies on. No other folder calls any function in this file.

#### Globals

None (no top-level variables, no immediately executed code).

#### Functions

| Function | Signature | Description |
|---|---|---|
| `setPasswordRequest` | async `(user, password, newPassword)` -> server result (truthy on success) | Change the password of a signed-in user. Trims all three, fetches `GetUserSalt(user)`, sends `SetPassword {Hash: proof hash of password, OneTimePad, NewHash: SHA256(newPassword|salt)}`. Used by `setPasswordMain`. |
| `getCreatePasswordPolicy` | async `()` -> password policy object | `GET ../Api/auth/GetCreatePasswordPolicy`. The policy is passed to `ValidatePassword`, `AuthPolicyText` and `AuthNewPassword` (Auth.js / AuthUi.js). Used by every password form in `UserManagerUi.js`. |
| `addUserRequest` | async `(token, salt, newPassword)` -> `{Error, Username, Tokens}` login response or null | Complete a new account from a sign-up / invite token. Sends `AddUser {Token, NewHash: SHA256(newPassword|salt)}`. On a truthy result stores it as JSON in `sessionStorage["SysWeaver.User"]`, otherwise removes that key. Used by `choosePasswordMain`. |
| `sendDeleteUserRequest` | async `()` -> array of targets (e-mails / phones) or null | `SendDeleteUserRequest` with body `0`: the server sends delete-account instructions and a short code. Used by `deleteAccountMain`. |
| `deleteUserRequest` | async `(token)` -> deleted user name or null | `DeleteUser(token)`; `token` is the long token or short code. Used by `deleteAccountMain`. |
| `signUpRequest` | async `(target)` -> boolean | Trims and sends `SignUp(target)` (e-mail or phone). Used by `signUpMain`. |
| `forgotPasswordRequest` | async `(userId)` -> array of targets or null | Trims and sends `ForgotPassword(userId)` (user name, e-mail or phone). Used by `forgotPasswordMain`. |
| `resetPasswordRequest` | async `(token, newPassword, newSalt)` -> `{Error, Username, Tokens}` or null | Sends `ResetPassword {Token, NewHash: SHA256(newPassword|newSalt)}`. Writes / removes `sessionStorage["SysWeaver.User"]` like `addUserRequest`. Used by `resetPasswordMain` and `addPasswordMain`. |
| `sendAddPasswordRequest` | async `()` -> array of targets or null | `SendAddPasswordRequest` with body `0`. Used by `addPasswordMain`. |
| `sendDeletePasswordRequest` | async `()` -> array of targets or null | `SendDeletePasswordRequest` with body `0`. Used by `deletePasswordMain`. |
| `deletePassword` | async `(token)` -> boolean | `DeletePassword(token)`. Used by `deletePasswordMain`. |
| `inviteUserRequest` | async `(target, userName, tokens, domain)` -> boolean | Trims `target` and `userName`; if `tokens` (array of strings) is given, trims each, drops empty ones and passes null when none remain. Sends `InviteUser {Email: target, Name: userName, Tokens, Domain: domain}`; `domain` is an optional application-specific domain. Used by `inviteUserMain` (without `domain`). |
| `changeEmailRequest` | async `(user, password, newEmail)` -> array of targets (one e-mail) or null | Proof hash of `password`, then `SendChangeEmailRequest {Hash, OneTimePad, Email: newEmail}`. Used by `changeEmailMain`. |
| `changeEmail` | async `(token)` -> boolean | `AddEmail(token)` (same API as `addEmail`). Used by `changeEmailMain`. |
| `deleteEmail` | async `(user, password)` -> array of targets that received restore instructions, or null | Proof hash, then `DeleteEmail {Hash, OneTimePad}`. Used by `deleteEmailMain`. |
| `addEmailRequest` | async `(user, password, newEmail)` -> array of targets or null | Proof hash, then `SendAddEmailRequest {Hash, OneTimePad, Email}`. Used by `addEmailMain`. |
| `addEmail` | async `(token)` -> boolean | `AddEmail(token)`. Used by `addEmailMain`. |
| `changePhoneRequest` | async `(user, password, newPhone)` -> array of targets or null | Proof hash, then `SendChangePhoneRequest {Hash, OneTimePad, Phone}`. Used by `changePhoneMain`. |
| `changePhone` | async `(token)` -> boolean | `AddPhone(token)` (same API as `addPhone`). Used by `changePhoneMain`. |
| `deletePhone` | async `(user, password)` -> array of targets or null | Proof hash, then `DeletePhone {Hash, OneTimePad}`. Used by `deletePhoneMain`. |
| `addPhoneRequest` | async `(user, password, newPhone)` -> array of targets or null | Proof hash, then `SendAddPhoneRequest {Hash, OneTimePad, Phone}`. Used by `addPhoneMain`. |
| `addPhone` | async `(token)` -> boolean | `AddPhone(token)`. Used by `addPhoneMain`. |
| `setNickName` | async `(newNickName)` -> boolean | `SetNickName(newNickName)` for the signed-in user; the server may request a refresh of all the user's pages. Not called in this repository's web code. |
| `setLanguage` | async `(newLanguage)` -> boolean | `SetLanguage(newLanguage)`; `newLanguage` is an ISO 639-1 code with an optional `-` ISO 3166 country (e.g. `"en-US"`). The server may refresh the user's pages. Not called in this repository's web code. |
| `isManagedUser` | async `()` -> boolean | `GET IsManagedUser`: whether the signed-in user is managed by the user manager. Declared twice with identical bodies (the second declaration wins). Not called in this repository's web code. |
| `userManagerProps` | async `()` -> object | `GET UserManagerProps`: misc user manager configuration properties. Not called in this repository's web code. |
| `validateShortCode` | async `(shortCode)` -> `1` valid, `0` invalid, `-1` no code exists | `ValidateShortCode(shortCode)` against the code stored in the current HTTP session. Called from `AuthPageValidation` in `AuthUi.js` (SysWeaver.MicroService.Auth). |
| `cleanUpShortCode` | `(shortCode, noLengthCheck)` -> string or null | Synchronous. Returns null for empty input; otherwise keeps only the characters `0`-`9`. Unless `noLengthCheck` is truthy, returns null when fewer than 3 digits remain. Called from `AuthPageValidation` in `AuthUi.js` (SysWeaver.MicroService.Auth), with `noLengthCheck = true` to normalize the input while typing and without it to decide if the confirm button is enabled / the code is submitted. |

### `UserManagerUi.js`

Page logic for the account pages: one global async `*Main()` entry point per HTML page, called from `<body onload="...">`. Loaded with `<script defer>` last on every page of this folder. It defines no variables and runs nothing at load. Every entry point calls `AuthPage(icon, build)` (AuthUi.js), which creates the auth box, shows the page icon (an `Icon*` class from `UserManager.css`, rendered as a `ColorIcon` with `IconColorThemeMain`) and calls `build(box, img)`; `box.OnLoaded()` calls `PageLoaded()` once, and exceptions thrown by `build` are passed to `Fail`. Pages that take a token read it as `decodeURIComponent(window.location.search)` without the leading `?`. None of these functions is called from another folder; the server links to the HTML pages.

Common pattern of the form pages (referenced below as "standard form"): labels/inputs built with `AuthLabel`, `AuthInput` / `AuthInputPhone` / `AuthPassword` / `AuthNewPassword`; an info line `info = AuthError(target, "", policyText)`; a button row `AuthButtonRow` with a main `AuthButton` created disabled. An inner `setStatus` validates the inputs on every input event (Enter clicks the button when valid), calls `AuthSetError` / `AuthSetText` and enables the button. The click handler calls `button.StartWorking()`, sets inputs `readOnly`, runs the request, and on success shows a message, calls `AuthStartPage()` (sign-in flows) or `AuthPageValidation(target, html, "<Page>.html?")` (short-code step). On failure it calls `Fail(...)` (with the exception text appended when thrown), `button.StopWorking()`, re-runs `setStatus`, shows a 5 s `SetTemporaryInnerText` retry message, restores the inputs and focuses one. Pages that need the signed-in user call `sendRequest("../Api/auth/GetUser")` and throw "No user signed in!" unless `Succeeded` and `Username` are set.

"Token mode / request mode" pages: with a query string they show a single confirm `AuthButton` whose click hides the button row, shows `AuthText` progress, calls the token API and shows the result (the row is shown again on failure); without a query string they show progress via `AuthError`, call `target.OnLoaded()`, call the send-request API and on success hand over to `AuthPageValidation` with "Code sent to {targets}" (`_TH` + `AuthUserHtml`), or show and `Fail` an error.

#### Globals

None (no top-level variables, no immediately executed code).

#### Functions

| Function | Signature | Description |
|---|---|---|
| `setPasswordMain` | async `()` | `SetPassword.html`, icon `IconAuthSetPassword`. Standard form: current password (`AuthPassword`, variable `uname`) and new password (`AuthNewPassword` with the create policy, variable `pwd`). Button "Set password" (icon `IconAuthPassword`): `GetUser`, then `setPasswordRequest(username, current, new)`; on success shows "Password set." and disables the button. |
| `choosePasswordMain` | async `()` | `ChoosePassword.html`, icon `IconChoosePassword`. Requires a token (more than 3 chars incl. `?`), else `Fail("Token not found or invalid")`. `sendRequest("GetNewUserData", token)` -> `{Salt, NickName, UserName}`; shows "Welcome {user}". If `PassKey` (Passkey.js, SysWeaver.MicroService.PassKey) is defined and `PassKey.IsSupported()`, calls `PassKey.NewAccount(token)` and awaits `.Fetch()` before rendering so the later click opens the browser prompt directly; passkey support is dropped if that throws or returns `error`. Label text depends on passkey availability. "Set password" calls `addUserRequest(token, salt, pw)` then `AuthStartPage()`. Optional "Create a passkey" button (icon `IconAddPasskey`) calls `newPassKey.Run()`; on `Error == 0` calls `AuthStartPage()`, otherwise `Fail(PassKey.ErrorText(res) ...)` unless `PassKey.Errors.Cancelled`, then fetches a fresh challenge (`Fetch()`, errors ignored). |
| `deleteAccountMain` | async `()` | `DeleteAccount.html`. Token mode (icon `IconAuthDeleteAccount`): confirm button "Click to delete account" with a warning tooltip, calls `deleteUserRequest(token)`, shows "Account deleted". Request mode (icon `IconAuthDeleteMail`): `sendDeleteUserRequest()` then `AuthPageValidation(..., "DeleteAccount.html?")`. Computes `getUrlParams()` into an unused local `ps`. |
| `signUpMain` | async `()` | `SignUp.html`, icon `IconAuthSignUp`. `GET ../Api/auth/SignUpMethods` (array of `"Email"` / `"Phone"`); fails with "No sign up methods found!" when empty. Creates an `AuthTab` and, per supported method, a `SysWeaver-AuthTabContent` element `g` appended to the box, with a tab header (`tab.Add`, icons `IconAuthEmail` / `IconAuthPhone`) and an `AuthInput` (`type="email"`) or `AuthInputPhone`. Expandos `g.SetStatus` / `g.GetValue` are set on each content element (see inner functions). The tab change callback shows only the selected content, re-validates it and focuses its input. Fails with "No supported sign up methods found!" when no content was created. "Sign up" (icon `IconAuthPost`) adds class `Disabled` to the tab and contents, calls `signUpRequest(value)` and on success `AuthPageValidation(..., "ChoosePassword.html?")`; on failure removes `Disabled`. Ends with `tab.SetIndex(1)` and validates/focuses `elements[tab.Selected]`. |
| `forgotPasswordMain` | async `()` | `ForgotPassword.html`, icon `IconAuthForgotPassword`. Standard form with one "User ID" `AuthInput` (autocomplete `userid`); synchronous inner `setStatus` only requires non-empty text. Button (icon `IconAuthPost`) calls `forgotPasswordRequest(user)`; on success `AuthPageValidation(..., "ResetPassword.html?")`. |
| `resetPasswordMain` | async `()` | `ResetPassword.html`, icon `IconAuthResetPassword`. Same structure as `choosePasswordMain`, but uses `GetNewPasswordData(token)`, `PassKey.AttachUsingToken(token)` for the optional passkey button, and `resetPasswordRequest(token, pw, salt)`, which must return `Error == 0` before `AuthStartPage()`. No welcome text. |
| `addPasswordMain` | async `()` | `AddPassword.html`, icon `IconAuthAddPassword`. Token mode: `GetNewPasswordData(token)`, then a new-password standard form (inner `setStatus` passes the user name to `ValidatePassword`); button "Add password" calls `resetPasswordRequest` and on `Error == 0` `AuthStartPage()`. Request mode: `sendAddPasswordRequest()` then `AuthPageValidation(..., "AddPassword.html?")`. Computes an unused `getUrlParams()` local. |
| `deletePasswordMain` | async `()` | `DeletePassword.html`, icon `IconAuthDeletePassword`. Token mode: confirm button calls `deletePassword(token)`, shows "Password deleted". Request mode: `sendDeletePasswordRequest()` then `AuthPageValidation(..., "DeletePassword.html?")`. |
| `inviteUserMain` | async `()` | `InviteUser.html`, icon `IconAuthInviteUser`. Requires `GetUser` -> `Succeeded` (throws otherwise, caught by `AuthPage`). Inputs: e-mail (validated with `isValidEmail` from common.js), optional user name, and, only when the inviter has `Tokens`, a comma-separated "Security tokens" input whose tooltip lists the inviter's tokens. "Send invitation" (icon `IconAuthPost`) calls `inviteUserRequest(email, name, tokens.split(','))` and shows "Invitation sent to {email}." |
| `changeEmailMain` | async `()` | `ChangeEmail.html`, icon `IconAuthChangeEmail`. Token mode: confirm button calls `changeEmail(token)`, shows "Email address changed". Request mode: standard form with current password (`uname`) and new e-mail (`AuthInput`, variable `pwd`, validated with `AuthGetEmailError`); button "Verify email address" (icon `IconAuthEmail`): `GetUser`, `changeEmailRequest(username, password, email)`, then `AuthPageValidation(..., "ChangeEmail.html?")`. |
| `deleteEmailMain` | async `()` | `DeleteEmail.html`, icon `IconAuthDeleteEmail`. Single-mode standard form with only the current password; the button calls `GetUser` then `deleteEmail(username, password)` and shows "Sent restore instructions to {targets}." |
| `addEmailMain` | async `()` | `AddEmail.html`, icon `IconAuthAddEmail`. Same as `changeEmailMain` but with `addEmail(token)` / `addEmailRequest` and link `AddEmail.html?`. |
| `changePhoneMain` | async `()` | `ChangePhone.html`, icon `IconAuthChangePhone`. Same as `changeEmailMain` but the new value uses `AuthInputPhone` (with a "+country code" tooltip) validated by `AuthGetPhoneError`; button "Verify phone number" (icon `IconAuthPhone`); APIs `changePhone(token)` / `changePhoneRequest`; link `ChangePhone.html?`. |
| `deletePhoneMain` | async `()` | `DeletePhone.html`, icon `IconAuthDeletePhone`. Same as `deleteEmailMain` with `deletePhone`. |
| `addPhoneMain` | async `()` | `AddPhone.html`, icon `IconAuthAddPhone`. Same as `changePhoneMain` with `addPhone(token)` / `addPhoneRequest`; link `AddPhone.html?`. |

#### Inner functions

| Function | Signature | Description |
|---|---|---|
| `setStatus` (in `setPasswordMain`, `changeEmailMain`, `addEmailMain`, `changePhoneMain`, `addPhoneMain`) | async `(uText, pwText)` -> boolean | Validates the current password `uText` with `ValidatePassword(null, uText, ..., policy)`; on error marks `uname`, clears the error on `pwd`, shows the message, disables the button and returns false. Otherwise validates the second field (new password via `ValidatePassword` plus a background `CheckPassword` leak check in `setPasswordMain`; e-mail via `AuthGetEmailError`; phone via `AuthGetPhoneError`; empty input yields an "Enter ..." prompt), sets error/info, enables the button when valid and returns true. |
| `setStatus` (in `choosePasswordMain`, `resetPasswordMain`, `addPasswordMain`) | async `(text)` -> boolean | Validates the new password with `ValidatePassword` (user name passed only in `addPasswordMain`), shows the error or starts `CheckPassword(info, text)` without awaiting, enables the button and marks `pwd`. Returns true when valid. |
| `setStatus` (in `deleteEmailMain`, `deletePhoneMain`) | async `(uText)` -> boolean | Validates the current password only; enables the button when valid. |
| `setStatus` (in `forgotPasswordMain`, `inviteUserMain`) | `(uText)` -> boolean | Synchronous. Forgot password: non-empty user id. Invite: `isValidEmail(uText)`. Sets error/info text and the button state. |
| `setEmailStatus` (in `signUpMain`) | async `(el)` -> boolean | Validates the e-mail input `el` with `AuthGetEmailError` (or "Enter a valid email address" when empty), updates `info` and the "Sign up" button. |
| `setPhoneStatus` (in `signUpMain`) | async `(el)` -> boolean | Same for phone numbers with `AuthGetPhoneError`. |
| `g.SetStatus` / `g.GetValue` (expandos in `signUpMain`) | `()` | Set on each `SysWeaver-AuthTabContent` element: `SetStatus` re-validates its input (`setEmailStatus` / `setPhoneStatus`), `GetValue` returns the input value. `SetStatus` is called by the tab change callback, after a failed sign-up and at start-up; `GetValue` is not read by the page code. |

## CSS reference

### `UserManager.css`

Icon definitions for the account pages. Loaded by all 15 HTML pages of this folder after `../common/theme.css` and `Auth.css` (SysWeaver.MicroService.Auth). Not templated. It contains only `mask-image` rules; the mask-icon base styling (size, color from the theme) comes from the shared theme/icon CSS and the `ColorIcon` class from common.js. The classes are passed by name to `AuthPage(icon, ...)` in `UserManagerUi.js` (rendered as a `ColorIcon` with `IconColorThemeMain`) and some are reused as `AuthButton` icons. The SVGs resolve to `../icons/` (the shared icon folder, SysWeaver.HttpServer.IconModule). Layout and all other auth styling come from `Auth.css`; passkey icons from `Passkey.css`.

#### Page and button icons

| Selector(s) | Description |
|---|---|
| `.IconAuthSignUp`, `.IconChoosePassword`, `.IconAuthInviteUser` | `../icons/user_add.svg`. Page icons of `signUpMain`, `choosePasswordMain`, `inviteUserMain`. |
| `.IconAuthSetPassword` | `../icons/user_password.svg`. Page icon of `setPasswordMain`. |
| `.IconAuthForgotPassword`, `.IconAuthResetPassword` | `../icons/user_check.svg`. Page icons of `forgotPasswordMain`, `resetPasswordMain`. |
| `.IconAuthDeleteAccount`, `.IconAuthDeleteMail` | `../icons/user_delete.svg`. `deleteAccountMain` page icon in token mode / request mode; `IconAuthDeleteAccount` is also the confirm button icon. |
| `.IconAuthAddPassword` | `../icons/user_password_add.svg`. Page icon of `addPasswordMain`. |
| `.IconAuthDeletePassword` | `../icons/user_password_delete.svg`. Page and confirm button icon of `deletePasswordMain`. |
| `.IconAuthChangeEmail`, `.IconAuthAddEmail`, `.IconAuthDeleteEmail` | `../icons/user_email_change.svg`, `user_email_add.svg`, `user_email_delete.svg`. Page and button icons of `changeEmailMain`, `addEmailMain`, `deleteEmailMain`. |
| `.IconAuthChangePhone`, `.IconAuthAddPhone`, `.IconAuthDeletePhone` | `../icons/user_phone_change.svg`, `user_phone_add.svg`, `user_phone_delete.svg`. Page and button icons of `changePhoneMain`, `addPhoneMain`, `deletePhoneMain`. |

#### Custom properties

None defined or used.

#### Animations, media queries and theme variants

None.
