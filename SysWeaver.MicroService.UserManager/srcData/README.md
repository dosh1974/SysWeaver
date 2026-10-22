# srcData

[⬆ SysWeaver.MicroService.UserManager](../README.md)

> Default e-mail, text message, WhatsApp and web notification templates (English) for every account action the user manager sends.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.UserManager](../README.md) |
| **Kind** | Embedded data (message templates) |

## Purpose

The `EmbedLocal` target in [`SysWeaver.MicroService.UserManager.csproj`](../SysWeaver.MicroService.UserManager.csproj) runs `Compress.exe -OptimizeWebFiles false -Methods br .\srcData\*+ .\data` before each build. The HTML is not minified. The generated `data/**/*.br` files (not in git) are embedded as resources and referenced by name, e.g. `data.InviteMail.html`.

[`EmailComs`](../EmailComs.cs) uses the `*Mail.txt` (subject) and `*Mail.html` (body) pairs. The defaults are set in [`EmailComsParams.cs`](../EmailComsParams.cs). [`PhoneComs`](../PhoneComs.cs) uses the `*Text.txt` files. Each default can be replaced through the service parameters. The templates contain placeholders such as `[Site]`, `[Link]`, `[Header]`, `[Footer]`, `[Background]` and `[Color]`. The header and footer templates are inserted where `[Header]` and `[Footer]` appear.

## Contents

Each account action has three files: `<Action>Mail.txt` (e-mail subject), `<Action>Mail.html` (e-mail body) and `<Action>Text.txt` (text message).
Actions sent to a phone also have `<Action>WhatsApp.md` (WhatsApp formatting only), and actions on an existing account have
a web notification: `<Action>Notification.txt` (headline) and `<Action>Notification.md` (markdown body). E-mail only actions
(`AddEmail*`, `ChangeEmail*`, `DeletedEmail*`) have an empty `Text.txt`, phone only actions (`AddPhone*`, `ChangePhone*`,
`DeletedPhone*`) have an empty `Mail.*`.

| Item | Description |
|---|---|
| `Invite*` | Invitation to join the site |
| `SignUp*` | Sign-up confirmation |
| `DeleteUser*` | Confirm deletion of the account |
| `ResetPassword*` | Forgotten / forced password reset |
| `AddPassword*`, `DeletePassword*` | Add or remove a password |
| `AddEmail*`, `ChangeEmail*` | Add or change an e-mail address (with a short code) |
| `AddEmailNoCode*`, `ChangeEmailNoCode*` | Add or change an e-mail address (link only, no short code) |
| `DeletedEmail*` | Notification that an e-mail address was removed |
| `AddPhone*`, `ChangePhone*`, `DeletedPhone*` | Add, change or removal of a phone number |
| [`HeaderMail.html`](HeaderMail.html), [`FooterMail.html`](FooterMail.html) | Header and footer inserted into HTML e-mails |
| [`HeaderText.txt`](HeaderText.txt), [`FooterText.txt`](FooterText.txt) | Header and footer inserted into text messages (empty, kept as hooks) |
| `*WhatsApp.md` | WhatsApp versions of the phone actions |
| `*Notification.txt`, `*Notification.md` | Web notification headline and body (no code or link) |

That is 14 actions × 3 files plus 4 header/footer files, 9 WhatsApp files and 12 notification pairs, 79 files in total.
Example: [`ResetPasswordMail.txt`](ResetPasswordMail.txt), [`ResetPasswordMail.html`](ResetPasswordMail.html),
[`ResetPasswordText.txt`](ResetPasswordText.txt), [`ResetPasswordWhatsApp.md`](ResetPasswordWhatsApp.md),
[`ResetPasswordNotification.md`](ResetPasswordNotification.md).

## Writing rules

The templates are generic (no site, product or market specifics) and follow one design:

- E-mail: a 600px column (with an Outlook `<!--[if mso]>` table) on a `bgcolor="[Background]"` wrapper, `[Header]`,
  a headline, "We got a request to ...", a box with the code (`[ShortCode]`, 32px monospace on `[Color]`), "It works once,
  for 15 minutes.", "Enter it only on the [Site] website. We will never ask you for it.", a button for `[Link]` (24 hours)
  and the raw link below it, a "Not you?" line that fits the action, and `[Footer]` ("Requested from: [Origin]" and the
  "Powered by" logo). Invite has no footer, so the invitee never sees the inviter's ip address.
- All text uses `[Color]` (at least 15px), `[Acc1]` only for borders: the accent can have a very low contrast.
- Text message: `Your code to ...:` (only sign up and invite, the first message in the conversation, start with
  `[Site]: `), the code alone on the next line, validity / safety / "Not you?". Messages
  with a code don't include `[Link]` (it alone is ~120 characters), only messages without a code do.
- E-mail, WhatsApp and notification: every sentence starts on a new line ("Not you?" / "Ignore this e-mail." /
  "Nothing will change."). Text messages stay compact.
- WhatsApp: only WhatsApp formatting, the code in ```` ```monospace``` ```` on its own line.
  One emoji at the start of a line, one meaning each: 🔑 code, ⏱️ validity, 🔒 safety, 🌐 the label above a link,
  ⚠️ danger / possible attack, 💡 tip.
- Notification: a log of account events (📨 we sent a code, 🗑️ removed, ⚠️ / 💡 "Not you?"), never used to verify
  anything: never include `[ShortCode]` or `[Link]` (the code proves the owner of the e-mail / phone), say what
  happened and link to `auth/SetPassword.html` in the "Not you?" line.
- Short, plain sentences, each kept whole (the templates are machine translated).

## Relationships

- **Part of:** [SysWeaver.MicroService.UserManager](../README.md)
- **Used by:** [`EmailComs`](../EmailComs.cs), [`PhoneComs`](../PhoneComs.cs)
- **Uses:** `ManagedMailMessage` / `ManagedMessages` from [SysWeaver.Auth.Core](../../SysWeaver.Auth.Core/README.md)
