# ManagedFile

[⬆ SysWeaver.Common](../README.md)

> A "managed file": a local or http(s) file whose content is kept current, with change notification when it is modified.

| | |
|---|---|
| **Project** | [SysWeaver.Common](../README.md) |
| **Kind** | Source code (namespace `SysWeaver`) |

## Purpose

`ManagedFile` loads a file from the location given in `ManagedFileParams` and calls back when its content changes. Local files are watched for changes (reported after a grace delay, `LocalGraceTime`), web files are polled (`HttpPollFrequency`, with optional credentials). Optionally the content is hashed so that a change notification with identical data is suppressed. It is used for files such as user and certificate files that may be replaced while the application runs.

## Contents

| Item | Description |
|---|---|
| [`ManagedFile.cs`](ManagedFile.cs) | The managed file: current data, `TryGetNow` / `TryGetNowAsync`, change and exception statistics |
| [`ManagedFileData.cs`](ManagedFileData.cs) | The current state of a file: location, content, last write time, MD5 hash, read exception |
| [`ManagedFileParams.cs`](ManagedFileParams.cs) | Location, grace time, poll frequency, hashing and "must exist" options (derives from `CredentialParams`) |
| [`IManagedFileSource.cs`](IManagedFileSource.cs) | The source contract: monitors a file and reports changes |
| [`DiscManagedFile.cs`](DiscManagedFile.cs) | Internal source for local files (change watching) |
| [`HttpManagedFile.cs`](HttpManagedFile.cs) | Internal source for http / https urls (polling) |

## Relationships

- **Part of:** [SysWeaver.Common](../README.md)
- **Used by:** [SysWeaver.Auth.Core](../../SysWeaver.Auth.Core/README.md), [SysWeaver.Security](../../SysWeaver.Security/README.md) (`FileCertificateProvider`)
