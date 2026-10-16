# Db

[⬆ SysWeaver.MicroService.UserManager](../README.md)

> Database row types (SimpleStack ORM) for the user manager's MySQL tables: users, sign-in methods, communication methods, action tokens, session tokens and per-user data.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.UserManager](../README.md) |
| **Kind** | Database rows |

## Purpose

`UserManagerService` creates these tables (`InitTable<T>`) and works with them through the classes in the `SysWeaver.MicroService.Db` namespace. Attributes set the table names (`[Alias]`), keys, indices and partitioning. A user owns one or more sign-in methods (passwords, passkeys), which derive from `DbAuthMethod`, and one or more communication methods (e-mail addresses, phone numbers). Pending account actions are stored as tokens with an expiry until the user confirms them.

## Contents

| Item | Description |
|---|---|
| [`DbUser.cs`](DbUser.cs) | Table `Users`: id, creation time, user name, domain, language and nick name |
| [`DbAuthMethod.cs`](DbAuthMethod.cs) | Abstract base of sign-in methods: owning user id, creation and last use time |
| [`DbAuthPassword.cs`](DbAuthPassword.cs) | Table `AuthPasswords`: salted password hash and the "must reset password" flag |
| [`DbAuthPassKey.cs`](DbAuthPassKey.cs) | Table `AuthPasskeys`: passkey credential id, device, public key, user handle, relying party id, signature counter, transports, backup state and AAGUID |
| [`DbEmailAddress.cs`](DbEmailAddress.cs) | Table `Emails`: e-mail address to user mapping |
| [`DbPhoneNumber.cs`](DbPhoneNumber.cs) | Table `PhoneNumbers`: phone number to user mapping |
| [`DbAction.cs`](DbAction.cs) | Table `Actions`: pending action tokens (type, expiration, serialized data) sent by e-mail or SMS |
| [`DbToken.cs`](DbToken.cs) | Table `Tokens`: tokens linked to a user id |
| [`DbUserData.cs`](DbUserData.cs) | Per-user key/value data: `DbUserDataBase` with the tables `UserDataT` (binary) and `UserDataString` (text) |

## Relationships

- **Part of:** [SysWeaver.MicroService.UserManager](../README.md)
- **Uses:** [SysWeaver.DbSimpleStack.MySql](../../SysWeaver.DbSimpleStack.MySql/README.md)
- **Related:** passkey rows are used by [SysWeaver.MicroService.PassKey](../../SysWeaver.MicroService.PassKey/README.md) through the user manager
