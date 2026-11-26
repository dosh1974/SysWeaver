# SysWeaver.MicroService.PassKey

[⬆ SysWeaver overview](../README.md)

> Passwordless login with passkeys (WebAuthn / FIDO2), including adding a passkey from another device via QR code.

| | |
|---|---|
| **Layer** | Security |
| **Kind** | Micro service |

## Purpose

Offer phishing-resistant, passwordless sign-in for the accounts of any authorizer that can store passkeys (the user manager, the simple authorizer, ...), using the platform authenticators of browsers and phones.

## How it fits into SysWeaver

```mermaid
flowchart LR
  B["Browser / authenticator<br/>auth/UsePassKey.html"] -->|"challenge, signed assertion"| P["PassKeyService"]
  P -->|"credentials, sign in"| A["AuthorizerBase (SupportPasskeys)<br/>UserManagerService, SimpleAuthorizer, ..."]
  P -.->|optional| QR["IQrCodeService"]
```

The service issues WebAuthn challenges, verifies the responses with the Fido2 library and stores and resolves the credentials through the passkey methods of [`AuthorizerBase`](../SysWeaver.Auth.Core/AuthorizerBase_PassKey.cs) (plain `PassKeyData` records, no Fido2 types): it uses every authorizer of the auth manager with `SupportPasskeys`. A signed in user's passkeys are handled by the authorizer that owns the user (`Authorization.Auth`), a sign in asks every supporting authorizer for the credential id. The token based flows (QR code, reset / add password links, sign up with a passkey) need an authorizer with `SupportPasskeyTokens` (the user manager).

## Key features

- Discoverable (user-less) passkey sign-in, including synced passkeys and passkeys on a phone, and user-specific sign-in after entering a user id (also works with non-discoverable credentials)
- Adding passkeys to the signed-in account, to an account using a link token (reset or add password), and creating a new account with a passkey (sign-up)
- Cross-device enrolment through a single-use QR code link
- Per-challenge state with server side expiry, so several tabs can sign in at the same time
- `GetUserAuthChallenge` (a challenge for a named user) is rate limited per address, and for a name without passkeys it answers "no passkeys" for seven in eight names and a challenge with a made up credential (fixed per name, keyed with a secret in `KeyValueStore.AllApp`) for the others, so a challenge doesn't tell that a user exists
- Per passkey: signature counter (clone detection), transports, backup state, provider (AAGUID) and a friendly name; pages and APIs to list, rename and remove passkeys (the last sign-in method of a user can't be removed)
- *User* menu entries for signing in, adding and managing passkeys, shown only when applicable

## Key components

- [`PassKeyService`](PassKeyService.cs) — challenge, sign-in, enrolment and management APIs
- [`PassKeyParams`](PassKeyParams.cs) — relying party, origin and verification settings

## Using it

```json
{ "Type": "SysWeaver.MicroService.PassKeyService, SysWeaver.MicroService.PassKey",
  "Params": { "RpName": "Example", "RpId": "example.org", "Prefixes": [ "https://login.example.org" ] } }
```

All parameters are optional.

## Limitations and considerations

- Requires `AuthManagerService` and at least one authorizer with `SupportPasskeys` (`UserManagerService` stores them in its database, `SimpleAuthorizer` in `KeyValueStore.AllApp` when `AllowPasskeys` is true); the QR code enrolment needs an `IQrCodeService` and an authorizer with `SupportPasskeyTokens`
- The origin and relying party id are taken from the request: passkeys work on https (and `http://localhost`), not on IP addresses. Behind a proxy that changes the scheme or host, add the public origins to `Prefixes`
- Passkeys are bound to the host they were created on, unless `RpId` is set to a registrable domain (e.g. `example.org`), then they work on that domain and all its sub domains; changing `RpId` invalidates existing passkeys
- User verification (PIN / biometrics) and discoverable credentials are required by default (`UserVerification`, `ResidentKey`)

## Folder layout

| Folder | Content |
|---|---|
| [`srcWeb/`](srcWeb/README.md) | Embedded passkey pages (`auth/`) and icons (`icons/`) |

## Relationships

- **Project:** [`SysWeaver.MicroService.PassKey.csproj`](SysWeaver.MicroService.PassKey.csproj)
- **Builds on:** [SysWeaver.MicroService.Auth](../SysWeaver.MicroService.Auth/README.md) (no dependency on the user manager)
- **Third-party:** [Fido2](https://www.nuget.org/packages/Fido2) (fido2-net-lib)
