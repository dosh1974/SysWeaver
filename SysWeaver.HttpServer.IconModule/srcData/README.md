# srcData

[⬆ SysWeaver.HttpServer.IconModule](../README.md)

> SVG sources that the icon module loads from its resources: the document template and overlays used to generate file extension and MIME type icons, the folder / virtual / API icons, and the country flags.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer.IconModule](../README.md) |
| **Kind** | Embedded data |

## Purpose

These files are not served by path like [`srcWeb/`](../srcWeb/README.md). The build optimises them with SvgOpt (`-RemoveSize`, compared against the untracked `DataCompare` / `DataCompareFlags` folders) into `srcData\.opt\` and `srcData\.opt\flags\` (git-ignored), then compresses `.opt` with Brotli into `data/`, which is embedded. [`IconHttpServerModule`](../IconHttpServerModule.cs) enumerates the embedded `.svg` resources at start up and handles them by name:

- `base` becomes a dynamic `TextTemplate` (the document icon whose colours, label and overlay are replaced per extension / MIME type).
- `folder`, `virtual` and `api` are registered as static icons at `icons/<name>.svg` (exposed as `FolderName`, `VirtualFolderName`, `ApiFolderName`).
- `flags.*` are registered under the flag prefix, `icons/flags/<code>.svg`.
- Everything else is kept in memory as an overlay; `mime_*` and `ext_*` names are looked up when an icon for `icons/mime/<type>/<subtype>.svg` or `icons/ext/<ext>.svg` is generated.

## Contents

| Item | Description |
|---|---|
| [`flags/`](flags/README.md) | 270 country and region flag icons |
| [`opt/`](opt/README.md) | Tracked copy of optimised flags, not used by the build |
| [`base.svg`](base.svg) | Document template (768×1024) for generated extension and MIME type icons |
| [`folder.svg`](folder.svg), [`virtual.svg`](virtual.svg), [`api.svg`](api.svg) | Folder, virtual folder and API icons, served at `icons/folder.svg`, `icons/virtual.svg`, `icons/api.svg` |
| [`_blank.svg`](_blank.svg) | A blank page shape (192×256); loaded like the other overlays, not referenced by name in the module code |
| [`ext_cpp.svg`](ext_cpp.svg), [`ext_cs.svg`](ext_cs.svg) | Overlay fragments ("C++", "C#") for the `cpp` and `cs` extensions |
| [`mime_application.svg`](mime_application.svg), [`mime_application_javascript.svg`](mime_application_javascript.svg), [`mime_application_zip.svg`](mime_application_zip.svg) | Overlay fragments for application MIME types (generic, JavaScript `{}`, zip) |
| [`mime_audio.svg`](mime_audio.svg), [`mime_font.svg`](mime_font.svg), [`mime_image.svg`](mime_image.svg), [`mime_video.svg`](mime_video.svg) | Overlay fragments for the audio, font, image and video MIME type groups |
| [`mime_text.svg`](mime_text.svg), [`mime_text_xml.svg`](mime_text_xml.svg) | Overlay fragments for text and XML (`<>`) |

The `ext_*` and `mime_*` files are SVG fragments (no `<svg>` root), inserted into `base.svg`.

## Relationships

- **Part of:** [SysWeaver.HttpServer.IconModule](../README.md)
- **Used by:** [`IconHttpServerModule.cs`](../IconHttpServerModule.cs); the flags are used by [SysWeaver.HttpServer.IsoDataModule](../../SysWeaver.HttpServer.IsoDataModule/README.md), the folder and API icons by [SysWeaver.HttpServer.ExploreModule](../../SysWeaver.HttpServer.ExploreModule/README.md)
- **Related:** [`srcWeb/icons/ext/`](../srcWeb/icons/ext/README.md) holds ready-made extension icons that take precedence over generated ones
