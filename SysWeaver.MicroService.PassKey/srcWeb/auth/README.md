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
