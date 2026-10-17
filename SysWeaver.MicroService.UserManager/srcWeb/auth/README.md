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
