# srcData

[⬆ SysWeaver.MicroService.UserManager](../README.md)

> Default e-mail and text message templates (English) for every account action the user manager sends.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.UserManager](../README.md) |
| **Kind** | Embedded data (message templates) |

## Purpose

The `EmbedLocal` target in [`SysWeaver.MicroService.UserManager.csproj`](../SysWeaver.MicroService.UserManager.csproj) runs `Compress.exe -OptimizeWebFiles false -Methods br .\srcData\*+ .\data` before each build. The HTML is not minified. The generated `data/**/*.br` files (not in git) are embedded as resources and referenced by name, e.g. `data.InviteMail.html`.

[`EmailComs`](../EmailComs.cs) uses the `*Mail.txt` (subject) and `*Mail.html` (body) pairs. The defaults are set in [`EmailComsParams.cs`](../EmailComsParams.cs). [`PhoneComs`](../PhoneComs.cs) uses the `*Text.txt` files. Each default can be replaced through the service parameters. The templates contain placeholders such as `[Site]`, `[Link]`, `[Header]`, `[Footer]`, `[Background]` and `[Color]`. The header and footer templates are inserted where `[Header]` and `[Footer]` appear.

## Contents

Each account action has three files: `<Action>Mail.txt` (e-mail subject), `<Action>Mail.html` (e-mail body) and `<Action>Text.txt` (text message).

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
| [`HeaderText.txt`](HeaderText.txt), [`FooterText.txt`](FooterText.txt) | Header and footer inserted into text messages |

That is 14 actions × 3 files plus 4 header/footer files, 46 files in total. Example: [`InviteMail.txt`](InviteMail.txt), [`InviteMail.html`](InviteMail.html), [`InviteText.txt`](InviteText.txt).

## Relationships

- **Part of:** [SysWeaver.MicroService.UserManager](../README.md)
- **Used by:** [`EmailComs`](../EmailComs.cs), [`PhoneComs`](../PhoneComs.cs)
- **Uses:** `ManagedMailMessage` / `ManagedMessages` from [SysWeaver.Auth.Core](../../SysWeaver.Auth.Core/README.md)
