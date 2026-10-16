# icons

[⬆ srcWeb](../README.md)

> Monochrome SVG icons for the passkey pages and menu entries.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.PassKey](../../README.md) |
| **Kind** | Icons |

## Purpose

At build time the icons are run through `SvgOpt` (`-RemoveFill -RemoveSize`) and then Brotli compressed into `web/icons` (see [srcWeb](../README.md)). They are used as CSS mask images by the classes in [`../auth/Passkey.css`](../auth/Passkey.css), so the theme decides their colour.

## Contents

| Item | Description |
|---|---|
| [`passkey_add.svg`](passkey_add.svg) | Add a passkey (`IconAddPasskey`) |
| [`passkey_use.svg`](passkey_use.svg) | Sign in with a passkey (`IconUsePasskey`) |
| [`passkey_key.svg`](passkey_key.svg) | A passkey (`IconPasskeyKey`) |
| [`passkey_rename.svg`](passkey_rename.svg) | Rename a passkey (`IconPasskeyRename`) |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.MicroService.PassKey](../../README.md)
- **Used by:** [`../auth/`](../auth/README.md)
