# upload

[⬆ srcWeb](../README.md)

> A debug page for uploading files manually to any file repository.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.FileUploader](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Served as `upload/Example.html`, the same URL folder as the upload API (`upload/Upload`, `upload/CheckStatus`). [`FileUploaderService`](../../FileUploaderService.cs) adds it to the web menu as *Debug / FileUploader / Upload file* for sessions with the `debug` or `dev` role. It also serves as an example of how to use [`common/fileUploader.js`](../common/README.md).

## Contents

| Item | Description |
|---|---|
| [`Example.html`](Example.html) | Upload example page: loads `common/common.js`, `common/md5.js`, `common/UZIP.js` and `common/fileUploader.js`, has a repository name input (from the `repo` URL parameter, default `UserImage`) and a drop / click area set up with `fileUploaderSetup`, and shows the result of each upload |

## Relationships

- **Part of:** [srcWeb](../README.md), [SysWeaver.MicroService.FileUploader](../../README.md)
- **Uses:** [`common/fileUploader.js`](../common/fileUploader.js)
