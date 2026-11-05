# auth

[⬆ srcWeb](../README.md)

> The browser side of sign in and sign out: the two pages, the UI-less authentication library `Sw.Auth` (Auth.js) and the auth page framework `Sw.AuthUi` (AuthUi.js) that every `auth/` page of every project builds on.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Auth](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Served under `auth/`. Four projects contribute to that url folder (the files are merged at run time):

| Project | Files | Pages |
|---|---|---|
| SysWeaver.MicroService.Auth (this folder) | `Auth.js`, `AuthUi.js`, `Auth.css` | `Login.html`, `Logout.html` |
| [SysWeaver.Auth.Core](../../../SysWeaver.Auth.Core/srcWeb/auth/README.md) | `SimpleAuth.js/.css` | `index.html` (password hash generator), `KeyMan.html` (API keys) |
| [SysWeaver.MicroService.PassKey](../../../SysWeaver.MicroService.PassKey/srcWeb/auth/README.md) | `Passkey.js` (UI-less), `PasskeyUi.js`, `Passkey.css` | `UsePassKey.html`, `AddPassKey.html`, `AddPassKeyOnOtherDevice.html`, `MyPassKeys.html` |
| [SysWeaver.MicroService.UserManager](../../../SysWeaver.MicroService.UserManager/srcWeb/auth/README.md) | `UserManager.js` (UI-less), `UserManagerUi.js`, `UserManager.css` | 15 account pages |

The code is split into **UI-less libraries** (`Auth.js` = `Sw.Auth`, `Passkey.js` = `PassKey`, `UserManager.js` = `Sw.UserManager`; usable by any page or project with only `sw/core.js`) and **UI** (`AuthUi.js` = `Sw.AuthUi` on the Sw component library, and the page files). The old global names stay available as legacy aliases.

Processing (SVG optimisation, Brotli compression, embedding) is described in [`srcWeb/`](../README.md).

## Contents

| Item | Description |
|---|---|
| [`icons/`](icons/README.md) | SVG icons used by the auth services (`user_not_found.svg`) |
| [`Login.html`](Login.html) | Sign in page (`auth/Login.html[?to=..][&parent=1&loc=.]`), calls `loginMain()` |
| [`Logout.html`](Logout.html) | Sign out page (`auth/Logout.html`), calls `logoutMain()` |
| [`Auth.js`](Auth.js) | `Sw.Auth`: login protocol, Basic fallback, logout, password proof / hashes, policy text and checks, password generator, leak check, e-mail / phone validation, phone prefix lookup, short codes, token urls, post sign in navigation; legacy globals |
| [`AuthUi.js`](AuthUi.js) | `Sw.AuthUi`: page frame (`Page`), `Field`, `PhoneField`, `Status`, `Text`, `Heading`, `Buttons`, `Button`, `InfoButton`, `Segments`, `ShortCode`, `UserHtml`, the login / logout pages; legacy `Auth*` globals |
| [`Auth.css`](Auth.css) | Styles of the card, fields, status line, lists, segments (classes `auth-*`) |

## Relationships

- **Part of:** [srcWeb](../README.md), [SysWeaver.MicroService.Auth](../../README.md)
- **Talks to:** [`LoginService`](../../LoginService.cs) and [`AuthManagerService`](../../AuthManagerService.cs) (`Api/auth/...`), the site root endpoints `logout`, `login`, `basic_auth`
- **Requires:** `sw/core.js`, `sw/ui.js`, `sw/ui.css`, `common/theme.css` of [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/srcWeb/README.md) (icon classes `IconAuth*` are in `sw/ui.css`)
- **Used by:** the other three `auth/` contributions (see above); the app shell embeds `auth/Login.html?parent=1&loc=.` (menu User/Login) and the server redirects unauthenticated requests to `auth/Login.html?to=<url>`

## Behavior

### Loading

Every page uses the new format includes:

```html
<script defer src="../sw/core.js"></script>
<script defer src="../sw/ui.js"></script>
<script defer src="Auth.js"></script>
<script defer src="AuthUi.js"></script>
<!-- Passkey.js / Passkey.css are not tags: Sw.AuthUi.LoadPassKeys() loads them only when the passkey service runs -->
<link rel="stylesheet" href="../common/theme.css">
<link rel="stylesheet" href="../sw/ui.css">
<link rel="stylesheet" href="Auth.css">
<body onload="loginMain()">
```

`Sw.AuthUi.Page(icon, build)` calls `Sw.Session.IgnoreUserChanges()` (the page's own sign in / out must not reload it; KeyMan opts out), shows the loader (`Sw.Page.Loading`), builds a centered card (a real `<form>`), awaits the builder and removes the loader exactly once (also on errors, shown as a fail toast). In the shell iframe the page is transparent (core class `Transparent`). Layouts (Design v2): sign in style pages (Login, Logout, SignUp, ForgotPassword, ResetPassword, ChoosePassword, UsePassKey, token links) keep the centered 28rem card with the page icon; outside the phone layout that card is the body of an `Sw.Dialog` (`AuthPage.AsDialog`, class `auth-dialog`: the title bar with the document title, moved and resized like every other dialog, its position remembered per page, "Discard changes?" for typed input; Close and Escape go back: the shell's previous page or its first page, stand alone the previous page of the site or the start page); Login and Logout (`Dialog: false`) and phones keep the plain card. Inside the shell (`html.Embedded`) the page body is the frame height minus the page margin, so a card that fits gets no scroll bar; account and tool pages (`Account: true` inside the shell, or `page.SetHeader({Title, Description, Actions})`: KeyMan, the password hash generator, the UserManager account pages, MyPassKeys, AddPassKey, AddPassKeyOnOtherDevice) become a form page (`main.auth-page.sw-page-form.auth-tool`): a left aligned `Sw.PageHeader` (title = the menu name or the document title) above a start aligned 36rem card (48rem with `auth-wide`), texts start aligned, the big page icon hidden (an image icon such as the QR code stays). The sign up method selector (`.auth-segments`) is a Design v2 segmented control (sunken track, the selected segment raised). The legacy entry `common/common.js` also works (the libraries only need `Sw`).

### Forms, keyboard, touch, password managers

- Fields are `Sw.TextBox` based (`Sw.AuthUi.Field`) with `name`, `autocomplete` (`username`, `username webauthn`, `current-password`, `new-password`, `email`, `tel`, `one-time-code`), `inputmode` and `autocapitalize=none`, so browsers and password managers fill and save them; pages that change a password add a hidden `autocomplete=username` input with the account name.
- Enter moves to the next empty field, else submits the form (a page with `EnterWalks = true`, ex: Invite user, walks every field and then focuses the main button, never submitting from a field, as the old `tabToNextOnEnter`; a field with `EnterWalks = true` does the same for itself: every `PhoneField`, so Enter in a phone number never sends the SMS, as the old phone input, which moved the focus on and sent nothing) (the main button is a submit button, enabled only when the form is valid; the login form uses `CanSubmit` instead, see below). Escape keeps the typed text (the Sw.TextBox default, as the old inputs); the fields do not mark the page dirty (opt in `Dirty`), so the sign in and account pages never ask "Leave site?", as the old pages. The status line is `role=status aria-live=polite`; invalid fields get `aria-invalid`. The initial focus is applied after the loader is removed; on touch (`pointer: coarse`) a text field is not focused then (it would open the on-screen keyboard unasked, UX guide FOC-2): the page `h1` gets the focus instead (`Sw.Page.InitialFocus` is used when the framework has it). The auth pages do not link `app/app.css`, so core/theme.js never hides them; a focus refused while a page is hidden is given again by core/theme.js when it shows (the page has no retry of its own any more).
- Password fields have a show / hide button, new password fields a "generate" button (crypto random, policy conforming, copied to the clipboard) and a "?" button that shows the password policy in a popup (works with touch; the old tooltip was hover only). The "?" is not a tab stop (a mouse / touch helper in the label row: Tab goes from the user id or the current password straight to the password, as on the old forms; the rules are listed under the field and describe the input).
- Touch targets follow `--sw-touch` (44 px on touch / mobile); phones get a full width card and full width buttons; low screens (landscape phones) a small page icon; the page scrolls instead of clipping. Zoom is no longer disabled (inputs are at least 16 px, so mobile browsers don't zoom on focus).

### Login (`loginMain`)

The card starts with the product header (`Brand`: the app banner `../banner.svg`, 9:2, the app icon and name; an app may point `--sw-brand-banner` at another file; OWN4-Auth-1, it was the square `../icon.svg`; the original card showed a user icon only). The page title is "Sign in" (`_TF`; the original title was "Login", see the owner list in `_ai` 64_R3_C). The page does not link `app/app.css` (the original did not either; `core/theme.js` adopts a brand the shell remembered, without a request). The request on an anonymous load is the original one: `POST Api/auth/GetPasswordPolicy ""` (+ the passkey challenges when the plug-in loaded). On load `Sw.Auth.GetUser` decides the view, asked only when `Sw.Auth.MaybeSignedIn()` (the session cookie is HttpOnly; the localStorage hint "SysWeaver.SignedInHint" (json {siteRoot: 1}, per site: a reverse proxy serves several sites on one origin) is set by every sign in and by a `GetUser` that found a user, removed by a sign out and by a `GetUser` that found nobody): **already signed in** ("You are already signed in", "Signed in as <user>.", *Continue* = `Sw.Auth.StartPage(0)`, *Sign out* = `Sw.Auth.Logout`, then the page loads again: the sign in form), else the **sign in form**: user id + password, validated live against the user's policy exactly as the old sign in did: `Sw.Auth.ValidatePassword(user, password, cache, onNewPolicy)` with the form's policy cache sends `POST Api/auth/GetPasswordPolicy(user id)` at once whenever the typed user id differs from the user of the cached policy (on load for the empty user id, then on every change of the user id: each typed or deleted character is one request, no delay, no memo of earlier users; a second check of the same user id before its answer, ex: the autofill re-check while typing, shares that request, never a duplicate), and the validation waits for the answer (newest call wins). The policy belongs to the authorizer that knows the user (else the common policy of all authorizers); the server doesn't tell the client how many authorizers it has and may add some at any time, so the client never skips the request. A failed request gives no policy: only the blank and the default length rules apply (as before). The rules are a friendly reminder (they help people remember their password): a small checklist below the password (`.auth-rules`, `Sw.Auth.PolicyRules`) that ticks each rule as it is met and highlights the unmet ones once a password is typed; the "?" button shows the full policy. While a rule of the user's policy is unmet *Sign in* is blocked. The hint line above *Sign in* always holds the original's status sentence (S6-D4, parity: the reserved cell is never an empty hole; it is sized for these sentences too): "Enter your user id" (neutral at rest, an error once the field was used), "Enter your password", while typing the sentence of the first unmet rule ("Password must be at least 8 characters!", quiet: the checklist marks the rules), then the leak check line; once *Sign in* is activated while blocked all unmet rules are listed in the hint line instead, inside the card, in one phrase until the inputs change (the page catches the activation on the button row before the ui's disabled guard, whose bubble below the button crossed the card's edge, Designer N4) ("To sign in, your password needs: at least 8 characters, an uppercase letter and a digit", `Intl.ListFormat`, `.auth-login-status.auth-warning`), never a silently disabled button: the blocked button is `aria-disabled` (`.auth-blocked`, looks disabled, stays focusable), and activating it (click, Enter) focuses the field to fix, pulses the hint (`.auth-attention`, an outline flash with reduced motion) and announces it (`AuthPage.CanSubmit`); when the hint is outside the window (400 % zoom) it is then scrolled into view (S6-D3, as the original scrolled its message into view). No layout shift while typing (LAY-9): the checklist and the hint line sit in reserved cells (`Sw.Reserve.Stack`) sized for the policies the page has received (all of them merged: the most rules, the widest numbers; the common policy of the load sizes them before the form shows), so the card has no empty band sized for rules no policy of this site has (Designer N4); another user's policy can only grow them, and only when it has more rules. The password input is `aria-describedby` the visible rules list, the hint and the leak check line (the user id input the hint). The hints are announced politely only once typing pauses (1.2 s, `Status` `LiveDelay`, only when the text changed), errors at once. The hints are plain text (no click to copy). Autofilled-but-hidden values count as valid (re-checked at 100..5000 ms after load). The leak check of a valid password works as before: "Checking password ..", then "Password is safe (not leaked)!", "This password is part of N data leaks" (an error colour) or "Password leak check failed!" (a warning, the reason as tool tip) in a line below the password (`.auth-leak`, `Status.Leak(pw)`); one `GET https://api.pwnedpasswords.com/range/<5 hex>` at once on every change of a valid password (the same password as the shown or running check makes no request); it never blocks *Sign in*. Sign in: `Sw.Auth.Login` (`GetUserSalt` -> `[salt, pad]`, hash = b64(sha256(pw|salt)) or the binary salt variant, `Login {OneTimePad, Hash}`; Basic fallback when `GetUserSalt` fails: `basic_auth` must be truthy, then `GET login` with `Authorization: Basic`). Success: "User signed in." and `Sw.Auth.StartPage()`. Failure: the reason inline under the form (`.auth-login-status`, tinted error box, no toast) for 5 s (the time the old sign in showed its message) or until the inputs change, while the leak check runs again at once (one new request, as before) and its result shows in the same place afterwards (`.auth-msg-slot.auth-failing` hides it meanwhile, opacity only): a right password of a disabled account (`AuthInfo.AccountDisabled`): "This account is disabled. Contact the administrator of this site."; else "Wrong user ID or password. Please try again." (or "Sign in failed, please try again." with the error as tooltip when the request failed); when the server refused because a user is signed in (here or in another window) the page switches to the already signed in view (`GetUser` is asked after a failure only when `MaybeSignedIn()`: a wrong password sends no extra request, as the original); the password is selected. Passkeys only when the optional plug-in loaded (`Login.html` links `Passkey.js` / `Passkey.css` statically, as the original page: a 404 for both when the app has no passkey service, and no `Api/HaveService` probe) and it is supported (`PassKey.IsAvailable()`: the browser supports passkeys): a *Use passkey* button (device hint) or a small labelled "Passkey" ghost button (it was an icon only key), a pre-fetched challenge (Safari gesture rule), conditional mediation autofill started only after a working challenge (a failing challenge hides the button, one request), passkey failures shown inline, retry after cancel. Url params `to`, `parent`, `loc` are accepted, not read. Larger text and 44 px (48 px on 1440p+ screens) controls on the login card; the primary button fills the row. Design v2: the 28rem card on the plain canvas (the brand weave only with `?weave=true`), the card and its type grow with the root size step of ui/tokens.css only (no extra 19 px / 32rem step); the "Enter your user id" hint shows from the start, as the original (S6-D4; it is an error only after the field was used).

### Post sign in navigation (`Sw.Auth.StartPage`)

After 3 s: embedded in a same origin window that has `ReloadAll` (the app shell) the top window is reloaded with the hash cleared (the shell never ends up nested in its iframe); otherwise the window goes to the site root (the url without the query and the last two path segments `auth/<page>.html`) with `location.replace` (no history entry; not `replaceState` + reload, which left the sign in page under the root's url for a moment, so its relative urls pointed one folder too high: behind a reverse proxy sub path a 503 for `../icon.svg`). Other windows reload through the server `user.login` message.

### Logout (`logoutMain`)

"Signing out ..", `GetUser` (not signed in: "No user is signed in!"), `Sw.Auth.Logout()` (`GET logout` with `Authorization: Basic logout`, which overwrites cached Basic credentials), "Signed out successfully!" or "Failed to sign out!" with a *Retry* button. The drafts of the user are removed with the `GetUser` reply the page already has (`Sw.Auth.Logout(who)`, no second lookup). In the app shell the whole shell reloads right after (core/session.js: `Sw.Session.SignedOut` and the server's `user.logout` push; the page is blanked at once and the hash cleared, so Back never signs out again) and says "You are signed out" once; the other tabs reload too. Stand-alone this page stays and shows the result (as before).

### Short codes and token urls

`Sw.AuthUi.ShortCode(page, {Html, Link, Again})` replaces the card content with the message, a digits-only code input (`one-time-code`, numeric keyboard, cleaned while typing) and *Continue* (Enter in the code goes to *Continue*, a second Enter continues, as the old code step: `page.EnterWalks`): `ValidateShortCode` -1 disables the input ("Code expired or too many tries"; with `Again: {Text, Title, Url}` "This code no longer works ... Ask for a new code." and a button with that text to `Url`, so the step is no dead end; `Validate` (optional `async code => 1 | 0 | -1 | -2`, default `ValidateShortCode`): checks the code another way, ex: a reset code that belongs to the account (-2: too many wrong codes for the account, the field stays and says to wait; with `Validate` a 0 says "Wrong or expired code"): it becomes the main button (Enter), is described by the status line and gets the focus; the disabled code field and Continue say "This code no longer works. Ask for a new code." instead of "wait"; `Field.SetDisabled` keeps the field read only when a request ends), 0 marks it invalid ("Invalid code, try again"), 1 navigates to `Link + code` in the same window (`Sw.Page.Navigate`). `Sw.Auth.QueryToken()` returns the token of a link (url decoded once): the whole query for the keyless `Page.html?<token>` form, the value of the `token` parameter for the keyed forms (`token=..`, `user=..&salt=..&token=..`; the token apis only know the bare token), theme flags (`usetheme=` / `settheme=`, anywhere, also as the only parameter added by the app shell: `AddEmail.html?useTheme=dark`) are removed.

### Link pages: check the link on load (`CheckLinkToken`)

**Every page opened from a link in an e-mail or text message** (reset / add / delete password, invitation / sign up, add / change / restore e-mail or phone, delete account, add a passkey from a QR code) checks the link first, **without using it**, and a dead link is said at once with its real cause, before the user types or confirms anything (UX guide FBK-1, FBK-6, SAF-2). One call does it:

```js
await Sw.AuthUi.Page("IconAuthDeleteAccount", async page => {
    const link = await Sw.AuthUi.CheckLinkToken("DeleteAccount", { Page: page });
    if (!link.Live)
        return;                     // the page says "This link no longer works", the cause and what to do
    //  live: link.Account, link.Target (the new address) are the facts for the confirmation
    ...
    //  the action itself: a 410 means the link died since the page was opened
    try { await call(); } catch (e) { if (Sw.AuthUi.IsLinkGone(e)) return Sw.AuthUi.LinkGone(page, { Reason: e.Reason }, "DeleteAccount"); ... }
});
```

- `CheckLinkToken(kind, { Token, Page })` asks `Api/auth/CheckActionToken { Token, Kind }` of the user manager (anonymous, a read: the token is not used). Kinds: `ResetPassword` (also add password links), `AddPassword`, `DeletePassword`, `DeleteAccount`, `AddEmail` (add / change / restore), `AddPhone` (add / change / restore), `AddPassKey`, `NewUser` (invitation, sign up), `UndoEmail` (undo an e-mail change, the link of the notice to the old address). `Token` defaults to the url's (`Sw.Auth.QueryToken`). It never rejects; the result: `{ State, Live, Checked, Kind, Reason, Account, Target, Expires, Token }` with the server's `State` (`Live`, `Used`, `Replaced`, `Revoked`, `Expired`, `Unknown`, `WrongKind`) or `None` (no token: request mode, nothing shown), `Code` (a typed short code: the server can't look it up without using it; `Live` true, the action refuses a wrong code with the reason), `Unchecked` (no user manager, a lost connection: `Live` true, the page works as before). With `Page`, a dead link is shown on it (`LinkGone`).
- `LinkGone(page, state, kind)` (state `Disabled`: the title "This account is disabled", the server's reason, no "Request a new link", only *Sign in* and the help line to contact the administrator): replaces the page content with a Design v2 state view (`Sw.StateView`, kind error, `role=alert`, the title is the page heading): "This link no longer works" (invitation / password variants), the server's reason (the real cause: "It was already used." / "A newer link was sent, use the link in the newest message." / "The password of the account was changed after it was sent." ...; a generic sentence without one), what to do for the kind (reset: *Request a new link* to ForgotPassword.html; invitation: ask the inviter; passkey QR code: make a new one on the signed in device; undo e-mail link: "This undo link no longer works", reset with the phone number of the account or contact the support, *Request a new link*; account links: sign in and start again from the page that sent it), *Sign in*, and a help line with the application name. The main button has the focus; Enter activates it.
- `IsLinkGone(e)`: the error of a link action is a 410 (the link was used, replaced, revoked or expired meanwhile): show `LinkGone` instead of a failure line ("try again" can't help).
- Request mode pages (Add password, Delete password, Delete account) send the message with the code when the page opens, as before ("Sending instructions ..", then "Code sent to ..."); only after a failure a "Send the code" button tries again. The UserManager pages do this in `requestCode`.

### Storage

`sessionStorage["SysWeaver.User"]`: JSON of the last successful sign in (AuthInfo; written by `Sw.Auth.Login`, the passkey ceremonies and the UserManager token flows, removed by logout); no reader.

## JavaScript reference

### `Auth.js` (`Sw.Auth`)

| Member | Description |
|---|---|
| `Trim(v)` | Trimmed string (null stays null). |
| `Login(user, password)` | Hashed login (Basic fallback); resolves the AuthInfo. |
| `BasicLogin(user, password)` | The Basic fallback alone. |
| `Logout(who)` | Removes the user's drafts (`Sw.Draft.ClearAll(who)`; `who`: the GetUser reply when the caller has it, else it is looked up), `GET logout` with `Authorization: Basic logout`; on success `Sw.Session.SignedOut()` (every app shell reloads at once; the reloaded shell of this tab says "You are signed out" once). |
| `GetUser()` | AuthInfo of the signed in user or null (updates the signed in hint). |
| `SaltPad(user)`, `PasswordHash(pw, salt)`, `Proof(user, pw)` | `[salt, pad]`; b64 salted hash (binary salt "\|..." supported); `{Hash, OneTimePad, Salt}` proof of the current password. |
| `Limits(policy)`, `PolicyText(policy)`, `CheckPolicy(pw, policy)`, `PolicyRules(pw, policy)` | Length limits (8..64 default, clamped 1..128), translated policy text, first failing rule or null, the rules as a checklist `[{Text, Met}]` (max length only listed when exceeded). |
| `UserPolicy(user, {Throw})`, `ValidatePassword(u, p, cache, onNewPolicy, apiPrefix, policy)`, `CreatePolicy()` | `GetPasswordPolicy(user)`: one request per call, no memo, no time limit (a failure gives null, or throws with `Throw`). `ValidatePassword`: the old validator with the old request pattern (a request when the user id differs from `cache.PasswordPolicyUser`; a check of the same user id while its request is on its way shares it, `cache.PasswordPolicyPending`; `onNewPolicy` when the policy changed). `GetCreatePasswordPolicy` (memoized 60 s). |
| `GenPassword(len, minUpper, minLower, minDigits, minSpecials)`, `GeneratePassword(policy)` | Crypto random passwords (look-alike free alphabets, unbiased). |
| `CheckLeak(pw)`, `LeakText(result)`, `LeakCheckEnabled()`, `LeakCheckIsOff()` | haveibeenpwned k-anonymity check (`{State 0..4, Count, Error}`) and its texts `{Text, Title, Kind}`, the request and texts of the old `CheckPassword`: one `GET https://api.pwnedpasswords.com/range/<first 5 hex of the SHA-1>` per call, at once, no time limit, no retry pause, no memo (the callers remember the last checked password); State 1 "Password is safe (not leaked)!" (`ok`), 2 "This password is part of N data leaks" (`error`), 3 / 4 "Password leak check failed!" (`warning`, the reason in the tool tip), checking "Checking password .." (`info`). The only internet request of the auth pages; it never blocks signing in or saving a password. **Deployment switch:** `:root{--sw-password-leak-check:off}` in the app's `app/app.css` (the shell, its frames, and stand-alone pages once the shell remembered the brand; a stand-alone sign in page of a site never opened in the shell does not read `app/app.css`, as the original page, so the check runs there until then), `<meta name="sw-password-leak-check" content="off">` in a page, or `window.SysWeaverPasswordLeakCheck = false` (this window or the shell, ex: `app/app.js`) turns it off: no request at all, State 0, nothing shown (`LeakText` is empty). Default on. |
| `EmailError(v)`, `PhoneError(v)`, `PhoneCountry(v)`, `CleanPhone(v)` | Server validation (error text or null), `{Iso, Text}` of a number's country, phone character filter. |
| `CleanShortCode(code, noLengthCheck)`, `ValidateShortCode(code)` | Digits of a code (null if < 3); 1 / 0 / -1. |
| `QueryToken(search)`, `StartPage(delayMs)`, `SetUser(info)` | Token of a token url; post sign in navigation; sessionStorage "SysWeaver.User" (a user also sets the signed in hint). |
| `NoteSignedIn(on)`, `MaybeSignedIn()` | The localStorage hint "SysWeaver.SignedInHint" (json {siteRoot: 1}: a user may be signed in on that site): set by every sign in (`SetUser`, `PassKey.SetUser`) and a `GetUser` with a user, removed by `Logout` and a `GetUser` without one. The sign in page asks `GetUser` only when it is set, so an anonymous visit sends the original requests. |
| `Call(name, arg)`, `Get(name)` | `Api/auth/<name>` POST / GET (no cache, a redirect throws). |

Legacy globals (old Auth.js / AuthUi.js / UserManager.js names, same signatures): `loginRequest`, `logoutRequest`, `AuthTrim`, `AuthPolicyText`, `ValidatePassword(u, p, cache, onNewPolicy, apiPrefix, policy)`, `CheckPassword(el, p)` (writes the leak result into an element), `AuthGetEmailError`, `AuthGetPhoneError`, `GenPassword`, `AuthStartPage`, `cleanUpShortCode`, `validateShortCode`.

### `AuthUi.js` (`Sw.AuthUi`)

| Member | Description |
|---|---|
| `Page(icon, build(page), {IconIsFile, KeepUserChanges, Class, Account, Description, Dialog})` | Builds an auth page (see Loading); resolves the `AuthPage`. `Account`: an account page of the signed in user; inside the app shell it is a Design v2 form page (`AuthPage.SetHeader`), stand alone the centered card. `Dialog` (default true): a centered card page without a page header is shown as an `Sw.Dialog` (`AuthPage.AsDialog`, not on phones); `false` keeps the plain card. |
| `AuthPage` | `E`, `Box` (the form, classes `auth-box SysWeaver-AuthBox`), `IconE`, `Fields`, `OnSubmit` (async), `CanSubmit` (optional async check awaited before `OnSubmit`; false stops the submit without the busy state), `MainButton`, `Running`; `SetIcon(icon)`, `Clear()`, `Submit()`, `Run(fn, button)` (busy: fields read only, button working, errors as toasts), `SetBusy(b)`, `Focus(target, select)` (deferred until loaded), `Loaded()`. |
| `Field(target, {Label, Hint, Placeholder, Type, AutoComplete, InputMode, Name, MaxLength, MinLength, Value, Reveal, Copy, Generate, Policy, LabelExtra, OnInput, OnChange, Draft, Dirty})` | `E`, `Input`, `Box` (Sw.TextBox); `Get`, `Set(v, silent)`, `Focus(select)`, `SetInvalid`, `SetReadOnly`, `SetDisabled(d, reason)`. `Copy`: the Sw.TextBox copy button (as the old auth inputs: default on for every field but passwords; `false` = none, a text = what it copies). `Draft` / `Dirty` go to the Sw.TextBox (a draft of the typed text, ex: `{ Context: "invite.email", Dirty: false }`; never for passwords; `Box.ClearDraft()` after the send). A null target creates a detached field (dialogs). |
| `PhoneField(target, o)` | A `tel` field with the country flag (`iso_data/country/<ISO>.svg`, prefixes as tooltip) and the character filter. |
| `Status(target, {Title, Class, Copy, LiveDelay})` | `Set(text, kind, {Title, Html})` (kinds `ok`, `error`, `warning`, `info`), `Clear()`, `Temp(text, ms, kind)`, `Leak(password, onlyLeaked)` (the leak check result, requested at once; the same password as the running or shown check makes no new request, any other text forgets it; onlyLeaked: quiet, only a leaked password is shown, as a warning). `Announce()` (say the current text again now). A click copies an error only (`Copy: "errors"`, default; `"always"` for the legacy `AuthError`, `"never"`); hints are plain text. `LiveDelay` (ms): the line is not live itself, a hidden `role=status` sibling (`Live`) gets the text once stable that long (changed texts only), errors at once. |
| `Text(target, text, {Html, Title, Class, NoCopy})`, `Heading(target, text, title, isHtml)`, `SetContent(el, text, isHtml)` | Texts. |
| `Buttons(target)`, `Button(row, swButtonOptions + {Submit, Hidden}, page)` | Button row; a `Submit` button becomes the page's main (primary, submit) button. |
| `Rules(target, policy, field)` | The password rules as a visible checklist below a new password field (`ul.auth-rules`, the field's `aria-describedby`): `Update(password)` ticks the met rules and highlights the missing ones once a password is typed. Example: `const rules = new U.Rules(page, policy, pwd); ... rules.Update(A.Trim(pwd.Get()));` |
| `InfoButton(text, title)` | A "?" button showing a text in a popup (`InfoText` can be changed); not a tab stop (`tabIndex = -1`). |
| `Segments(target, {Label, OnChange})` | Tab like choice (`Add(text, icon, title)`, `Select(i, silent)`, `SetDisabled`, `Selected`); arrow keys, Home / End. |
| `ShortCode(page, {Html, Link, Label, Placeholder, ButtonText, Icon, Again})` | The short code step. |
| `CheckLinkToken(kind, {Token, Page})`, `LinkGone(page, state, kind)`, `IsLinkGone(e)` | Link pages: check the link on load without using it and show "This link no longer works" with the real cause (see Link pages). |
| `Done(page, {Title, Text, Icon, Next})` | End an account page with its result and where to go next (never a dead end): a state view (a check, the title as the page heading, one sentence) and the `Next` buttons (`{ Text, Title, Icon, Url, Top, OnClick }`; default "Go to the start page" = the site root in the whole window); the focus on the first. Example: `return U.Done(page, { Title: _TF("Your account is deleted", "…"), Text: _TF("… you are signed out …", "…") });` |
| `Hide(result, ...parts)` | Hide used parts of a page (a button row, its note; elements or components) and move a focus that was in them to `result` (`Sw.Focus.Show`, FOC-1 / FOC-2: never to the page body). Ex: `U.Hide(status, row, note)` after a link action succeeded. |
| `UserHtml(list)`, `IsAutoFilled(input)`, `AppName()`, `Brand(page)`, `LoginMain()`, `LogoutMain()` | Helpers and the pages (globals `loginMain`, `logoutMain`). |

Legacy globals (mapped onto the above, for code in other projects): `AuthPage(icon, build(box, img), iconIsFile)` (`box.OnLoaded()`, `img.ChangeImage()`), `CreateAuthWrapper`, `AuthPageValidation`, `AuthUserHtml`, `AuthText`, `AuthLabel`, `AuthError`, `AuthTab` (`Add`, `SetIndex`, `Selected`), `AuthInput`, `AuthPassword`, `AuthNewPassword`, `AuthInputPhone` (return the input; expandos `OnChange`, `Field`), `AuthAddIcon`, `AuthButtonRow`, `AuthButton` (`Element`, `SetEnabled`, `StartWorking`, `StopWorking`), `AuthHr`, `AuthBr`, `AuthSetText`, `AuthSetError`, `IsAutoFilled`.

## CSS reference (`Auth.css`)

| Selector | Description |
|---|---|
| `body.auth-body` (`.Top`) | Centered card (top aligned), safe area padding, the page scrolls. |
| `form.auth-box`, `.auth-wide`, `.auth-inline` | The card (max 30rem, 40rem on wide pages), a box without card styling (legacy dialogs). `html.SwNarrow` = phone padding. |
| `.auth-icon-wrap`, `.auth-icon`, `img.auth-image` | Page icon (accent color, `--auth-icon` size, small on low screens), image icons (QR code on a white plate). |
| `.auth-heading`, `.auth-text`, `.auth-muted`, `.auth-hr` | Texts. |
| `.auth-status` + `.auth-ok/.auth-error/.auth-warning/.auth-info`, `.auth-big` | Status line. |
| `.auth-brand`, `.auth-brand-banner` | Product header (login): the app banner (`banner.svg`, 9:2; was `.auth-brand-icon` with `icon.svg`). |
| `.auth-login form.auth-box`, `.auth-login-status` (`.auth-error`, `.auth-warning`), `.auth-rules` / `.auth-rule` (`.auth-rule-met`, `.auth-rule-missing`, `.auth-rule-mark`), `.auth-leak`, `.auth-blocked`, `.auth-attention` | Login sizing, the inline sign in error, the unmet rules hint next to Sign in, the password rules checklist, the leak check line, the blocked (aria-disabled) Sign in, the hint pulse when the blocked Sign in is activated. |
| `.auth-field`, `.auth-label-row`, `.auth-phone`, `.auth-flag` | Fields, label row with the "?" button, phone flag. |
| `.auth-buttons`, `.auth-passkey-icon` | Button rows (full width buttons on phones). |
| `.auth-segments`, `.auth-segment[aria-selected]`, `.auth-busy` | Segments; blocked while busy. |
| `.auth-list`, `.auth-item`, `.auth-item-name`, `.auth-item-line` | Lists (passkeys, API keys). |
| `.auth-popup-text`, `.auth-dialog` | Popup text (policy), dialog content (rename passkey, create API key). |

Custom property: `--auth-gap` (card spacing), `--auth-icon` (page icon size). Colors come from the Sw tokens (`--sw-*`), so all five themes and custom themes apply; the only media query sets the small icon on low screens.
