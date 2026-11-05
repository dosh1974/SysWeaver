# auth

[⬆ srcWeb](../README.md)

> The self-service account pages of the user manager, the UI-less api `Sw.UserManager` (UserManager.js) and the page logic (UserManagerUi.js).

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.UserManager](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Served under `auth/` together with the files of [SysWeaver.MicroService.Auth](../../../SysWeaver.MicroService.Auth/srcWeb/auth/README.md) (`Sw.Auth`, `Sw.AuthUi`, `Auth.css`) and, optionally, [SysWeaver.MicroService.PassKey](../../../SysWeaver.MicroService.PassKey/srcWeb/auth/README.md) (`Passkey.js`).

## Contents

| Page | Entry | Mode |
|---|---|---|
| [`SignUp.html`](SignUp.html) | `signUpMain` | e-mail / phone (from `SignUpMethods`) -> `SignUp` -> short code -> `ChoosePassword.html?<code>` |
| [`ChoosePassword.html`](ChoosePassword.html) | `choosePasswordMain` | `?<token\|code>` or `?user=..&salt=..&token=..` (invite): "Welcome {user}", new password (`AddUser`) or *Create a passkey* (`PassKey.NewAccount`), then signed in |
| [`SetPassword.html`](SetPassword.html) | `setPasswordMain` | current + new password (`SetPassword`), stays on the page |
| [`ForgotPassword.html`](ForgotPassword.html) | `forgotPasswordMain` | user id -> `ForgotPassword` -> "If an account uses {user id}, we sent a code ... Enter the code here, or open the link in the message." (the same answer for every user id) -> short code checked with `ValidateResetCode` (the code belongs to the account: "Wrong or expired code", "Too many wrong codes were tried for this account. Wait a few minutes") -> `ResetPassword.html?<code>`. **I have a code**: user id + a code received before, also one asked for in another browser |
| [`UndoEmail.html`](UndoEmail.html) | `undoEmailMain` | `?<token>` from the changed e-mail notice, no sign in: the facts ("The account {user} gets the email address {old} back."), one button **Undo this change** (`UndoEmailChange`), then "The change is undone" with where the reset link went; a dead link shows "This undo link no longer works" (reset with the phone number, or contact the support) |
| [`ResetPassword.html`](ResetPassword.html) | `resetPasswordMain` | `?<token\|code>`: new password (`ResetPassword`, `Error == 0`) or *Create a passkey* (`PassKey.AttachUsingToken`), then signed in |
| [`AddPassword.html`](AddPassword.html) | `addPasswordMain` | request -> code -> `?<code>`: new password (`ResetPassword`), then signed in |
| [`DeletePassword.html`](DeletePassword.html) | `deletePasswordMain` | request -> code -> `?<code>`: confirm (`DeletePassword`) |
| [`AddEmail.html`](AddEmail.html), [`ChangeEmail.html`](ChangeEmail.html) | `addEmailMain`, `changeEmailMain` | current password + e-mail (`SendAddEmailRequest` / `SendChangeEmailRequest`) -> code -> `?<code>`: confirm (`AddEmail`) |
| [`DeleteEmail.html`](DeleteEmail.html) | `deleteEmailMain` | current password -> `DeleteEmail` -> "Sent restore instructions to ..." |
| [`AddPhone.html`](AddPhone.html), [`ChangePhone.html`](ChangePhone.html) | `addPhoneMain`, `changePhoneMain` | as e-mail with a phone field (flag, prefix lookup) and `AddPhone` |
| [`DeletePhone.html`](DeletePhone.html) | `deletePhoneMain` | current password -> `DeletePhone` |
| [`DeleteAccount.html`](DeleteAccount.html) | `deleteAccountMain` | request -> code -> `?<code>` (or the link `?<token>`; older links `?token=<t>` still work): confirm (`DeleteUser`), "Account deleted" |
| [`InviteUser.html`](InviteUser.html) | `inviteUserMain` | role InviteUser: e-mail, optional user name, security tokens as a picker of pressable chips (the inviter's own tokens; Admin / Debug also the standard roles Admin, Ops, Dev, UserManagement, Debug only for Debug: the tokens the server lets them give) -> `InviteUser`. Enter walks the fields as on the old page (`page.EnterWalks`: e-mail -> user name -> the first token -> *Send invitation*, 4 Enters as the old text field of tokens; Space selects a token; it never sends from a field, Enter on the button sends). No request but `GetUser` on load (the back / pending buttons are always shown: who may invite may see the users). **Unsent invitation**: typing marks the page dirty ("Leave without saving? Your invitation to x@example.com is not sent."), the e-mail and the user name are drafts (`Draft` option of `Sw.TextBox` via `Field`: "invite.email", "invite.name"; restored with "Draft restored" after a reload or a closed tab, cleared when sent); while the invitation is sent leaving asks "The invitation to … may not be sent." (`SetDirty(..., { Work: true })`). |
| [`Users.html`](Users.html) + [`Users.js`](Users.js) | `usersMain` | **The one place for the user accounts**: Administration > Users in the main menu and Management > Users in the user menu (role ViewUser: Ops, Debug or UserManagement). A fill page: header actions *Invite user* (opens InviteUser.html), *Force password reset...* (user id prompt, danger confirmation, `ForcePasswordResetUser`) and *Delete user...* (user id prompt, typed confirmation, `AdminDeleteUser`), each only when `Sw.Access.CanCall` allows it (role UserManagement); then the tabs *Users* (the live `Api/auth/UserAdminTable`, searchable, not cached on the server: after a header or row action the row shows its new state at once and a deleted row leaves; row actions *Force password reset*, *Undo forced reset*, *Disable*, *Enable* by state; a viewer without the right to manage users, ex: Debug or Ops, reads the plain `Api/auth/UserDataTable` instead, as the original "View users": the same columns, no Actions column) and *Pending invitations* (`Api/auth/InvitationTable`, row action *Withdraw invitation*; opened on it by `?tab=invitations`; only for users who may invite; the table is made when its tab is shown and removed when another tab is, so a hidden tab doesn't poll). The chosen tab is kept in the page state (`Sw.History` "UsersTab"): Back and a reload show the tab the user left, a new visit from the menu starts on Users. The users of the server configuration are Administration > Built-in users (SimpleAuthorizer) |
| [`UserManager.js`](UserManager.js) | `Sw.UserManager` | UI-less api (+ legacy globals) |
| [`UserManagerUi.js`](UserManagerUi.js) | `Sw.UserManagerUi` | The 15 entry points |
| [`UserManager.css`](UserManager.css) | | Page specific additions (layout in `Auth.css`, icons in `sw/ui.css`) |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.MicroService.UserManager](../../README.md)
- **Talks to:** `UserManagerService`, `SignUpService` (`Api/auth/...`), `LoginService.GetUserSalt`
- **Uses:** `Auth.js` / `AuthUi.js` / `Auth.css` (MicroService.Auth), optionally `Passkey.js` (MicroService.PassKey), `sw/core.js`, `sw/ui.js` of SysWeaver.HttpServer
- **Linked from:** the User menu (server `Can*` predicates) and the e-mail / text messages (`Page.html?<token>`)

## Behavior

Includes: `../sw/core.js`, `../sw/ui.js`, `Auth.js`, `AuthUi.js`, `UserManager.js`, `UserManagerUi.js`; `../common/theme.css`, `../sw/ui.css`, `Auth.css`, `UserManager.css`. ChoosePassword / ResetPassword link `Passkey.js` / `Passkey.css` statically, as the original pages (a 404 for both when the app has no passkey service; no `Api/HaveService` probe).

- **Token urls**: `Sw.Auth.QueryToken()`: the whole query (url decoded once) is the token or short code; for a keyed link (older `DeleteAccount.html?token=..` links, the invitation `ChoosePassword.html?user=..&salt=..&token=..`) the value of `token` (passing the whole query made these links "Token not found or invalid", also before the rewrite). A missing / unknown token shows "Token not found or invalid".
- **Dead links are said on load** (every token page: ChoosePassword, ResetPassword, AddPassword, DeletePassword, AddEmail / ChangeEmail, AddPhone / ChangePhone, DeleteAccount): before anything is offered the page checks its link with `Sw.AuthUi.CheckLinkToken(kind, { Page })` (`Api/auth/CheckActionToken`, the token is not used); a link that no longer works shows `Sw.AuthUi.LinkGone`: "This link no longer works", the real cause from the server (already used, a newer link was sent, the password / e-mail / phone changed after it was sent, expired, the account was deleted) and what to do (*Request a new link* for a reset link, *Sign in*), never the confirm button or the form. A live link shows its facts above the confirm button ("Account {user}, email address {new address}." / "Account {user}."), and the delete account dialog names the link's account (also signed out, so the account name is the confirm word). An action refused with 410 (the link died while the page was open) switches to the same page. A typed short code is not checked on load (the server refuses a wrong one with the reason). Failures are composed by `Sw.Net.Message`.
- **Request mode** (no query): the message is sent at load ("Sending ...", the loader is removed first), then `Sw.AuthUi.ShortCode` ("Code sent to {targets}.") leads to `<Page>.html?<code>` (the code is accepted only in the same server session).
- **Forms** validate live (newest validation wins, `liveValidation`): empty fields show a neutral hint, invalid values an error (server validation of e-mail / phone at once on every change of the value, one request each and the newest answer wins, as before: `ValidateEmailAddress`, and for a phone `GetPhonePrefixInfo` (the flag, also for a lone "+") then `GetPhoneNumberInfo`; invite uses the client check), new passwords the policy (`GetCreatePasswordPolicy`) and the leak check. **When** (UX guide FRM-3): an error shows once the user left the field with a value, pressed Enter or clicked the main button, never while the first characters are typed; then it follows each input. The error line is the field's `aria-describedby`. New password fields list the rules below the field from the start (`Sw.AuthUi.Rules`, ticked as they are met), so an empty one shows no "may not be blank" text. The main (submit) button is enabled only when valid; while disabled its reason is the first problem (INP-12: "Enter a valid email", "Enter a new password that follows the rules below"; "Fill in the form first" before the first check). Enter moves to the next empty field or submits. Current password fields are checked against the policy of new passwords, as before (`GetCreatePasswordPolicy` on load, `Sw.Auth.CheckPolicy`: "Password must be at least 8 characters!" etc.); the server verifies the proof. Submit: busy (fields read only, button working); failure: one message, in the status line, until a value changes (FRM-3, no toast besides it; `update.Hold()`: the blur of the refocus or a late leak check never replaces it): what failed, the server's reason (a lost connection in the shared words of `Sw.Net.ErrorText`, a refusal without a reason "The server did not accept it.") and what to do ("Check the entries and try again in a moment."); the values stay, re-validation, the field is selected. The same one message for the code request pages, the confirmation link pages and a failed passkey. The request mode pages with a current password (change password, add / change / remove e-mail or phone) load with `GetCreatePasswordPolicy` only and ask the signed in user (`GetUser`) when the form is sent, as the old pages: signed out, the form shows and sending fails with "No user signed in!" (Admin N34). A link of a disabled account answers 423 ("This account is disabled. Contact the administrator of this site."): the failure line has no "try again" advice.
- **Password managers**: `autocomplete` `current-password`, `new-password`, `email`, `tel`, `one-time-code`, plus a hidden `autocomplete=username` input with the account name on password pages.
- **Proof of the current password**: `Sw.Auth.Proof` (`GetUserSalt` + one time pad, the binary salt variant included); new passwords as `NewHash = Sw.Auth.PasswordHash(pw, salt)`.
- **Signed in endings** (ChoosePassword, ResetPassword, AddPassword, passkey creation) call `Sw.Auth.StartPage()` (shell reload when embedded).
- **SignUp**: segments (Email / Phone, the second method is selected first, as before), one field per method. The server's phone coms `Sms` and `WhatsApp` (and the older name `Phone`) share the one Phone tab: the server picks the com from the number.

## JavaScript reference

### `UserManager.js` (`Sw.UserManager`)

`UserName()`, `SetPassword(user, pw, newPw)`, `CreatePolicy()`, `AddUser(token, salt, pw)`, `NewUserData(token)`, `NewPasswordData(token)`, `SendDeleteUserRequest()`, `DeleteUser(token)`, `SignUpMethods()`, `SignUp(target)`, `ForgotPassword(userId)`, `ValidateResetCode(userId, code)`, `UndoEmailChange(token)`, `ResetPassword(token, pw, salt)`, `SendAddPasswordRequest()`, `SendDeletePasswordRequest()`, `DeletePassword(token)`, `Invite(email, name, tokens, domain)`, `SendAddEmailRequest(user, pw, email)`, `SendChangeEmailRequest(user, pw, email)`, `AddEmail(token)`, `DeleteEmail(user, pw)`, `SendAddPhoneRequest(user, pw, phone)`, `SendChangePhoneRequest(user, pw, phone)`, `AddPhone(token)`, `DeletePhone(user, pw)`, `SetNickName(name)`, `SetLanguage(lang)`, `IsManagedUser()`, `Props()`.

Legacy globals (old names, same arguments): `setPasswordRequest`, `getCreatePasswordPolicy`, `addUserRequest`, `sendDeleteUserRequest`, `deleteUserRequest`, `signUpRequest`, `forgotPasswordRequest`, `resetPasswordRequest`, `sendAddPasswordRequest`, `sendDeletePasswordRequest`, `deletePassword`, `inviteUserRequest`, `changeEmailRequest`, `changeEmail`, `deleteEmail`, `addEmailRequest`, `addEmail`, `changePhoneRequest`, `changePhone`, `deletePhone`, `addPhoneRequest`, `addPhone`, `setNickName`, `setLanguage`, `isManagedUser`, `userManagerProps`. `validateShortCode` / `cleanUpShortCode` are defined by `Auth.js` (`Sw.Auth.ValidateShortCode` / `CleanShortCode`).

### `UserManagerUi.js`

The 15 entry points above (globals and `Sw.UserManagerUi.*`).

## CSS reference (`UserManager.css`)

| Selector | Description |
|---|---|
| `.auth-box .auth-buttons + .auth-text.auth-muted` | The warning text under the confirm button of token pages. |
| `.um-link-facts` | The facts of a live link above its confirm button (no own style; a hook for apps). |

Page icon classes (`IconAuthSignUp`, `IconChoosePassword`, `IconAuthSetPassword`, `IconAuthForgotPassword`, `IconAuthResetPassword`, `IconAuthDeleteAccount`, `IconAuthDeleteMail`, `IconAuthInviteUser`, `IconAuthAddPassword`, `IconAuthDeletePassword`, `IconAuth{Add,Change,Delete}{Email,Phone}`) are in `sw/ui.css`.
