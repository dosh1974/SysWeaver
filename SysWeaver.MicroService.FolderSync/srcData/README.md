# srcData

[⬆ SysWeaver.MicroService.FolderSync](../README.md)

> The HTML template of the iframe "shim" page that keeps a remote web folder on a stable URL while its versions change.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.FolderSync](../README.md) |
| **Kind** | Embedded data (HTML template) |

## Purpose

For remote folders served on the web with the `IFramePage` swap method, [`FolderSyncService`](../FolderSyncService.cs) reads this template (resource `data.redirect.html`) into a `TextTemplate` with `${...}` placeholders. For each page listed in the folder's `HtmlShims` (default `index.html`), `AddIFrame` in [`FolderSyncService.Remote.cs`](../FolderSyncService.Remote.cs) fills in the variables and registers the result as a static page at the original path. The page shows the real, versioned page (`<folder>_<version>/<file>`) in a full-window iframe and copies its title. When version checking is enabled for the folder, it compares the folder version (`FolderSync/GetFolderVersion`) either on an interval (`AutoReload`) or when the framed page posts a `CheckVersion` message, and reloads on a change.

## How it is built

The `EmbedLocal` target of [`SysWeaver.MicroService.FolderSync.csproj`](../SysWeaver.MicroService.FolderSync.csproj) runs `Compress.exe -Methods br .\srcData\*+ .\data` (see [`_tools`](../../_tools/README.md)); the generated `data/**/*.br` files are embedded as resources.

## Contents

| Item | Description |
|---|---|
| [`redirect.html`](redirect.html) | Template with the placeholders `ToRoot` (relative path to the site root, for icons and manifest), `Url` (the versioned page), `GetVersion` (version API URL, empty to disable checks) and `AutoReload` (check interval in milliseconds, `0` to check only on request) |

## Limitations and considerations

- In the interval branch the template calls `setInterval(autoReload, async () => ...)`, which passes the arguments in the reverse order of `setInterval(callback, delay)`; periodic reloading may therefore not work as intended.

## Relationships

- **Part of:** [SysWeaver.MicroService.FolderSync](../README.md)
- **Used by:** [`FolderSyncService.Remote.cs`](../FolderSyncService.Remote.cs) (remote folders with `IFramePage`)
