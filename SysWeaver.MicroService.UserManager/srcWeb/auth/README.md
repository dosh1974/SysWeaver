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
