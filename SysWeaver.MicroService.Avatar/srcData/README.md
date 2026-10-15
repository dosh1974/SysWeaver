# srcData

[⬆ SysWeaver.MicroService.Avatar](../README.md)

> The library of 648 colourful SVG icons (animals, faces, food, vehicles, sports, instruments, ...) from which `AvatarService` picks one per user.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Avatar](../README.md) |
| **Kind** | Embedded data (SVG icons) |

## Purpose

When no `DiscFolder` is configured, [`AvatarService`](../AvatarService.cs) reads every embedded resource of the assembly as an SVG document at start-up. For each user the guid seeds the background shape and colours, and selects one of these icons, which is placed on top. The icons are complete pictures, not parts that are combined.

## How it is built

The `EmbedLocal` target of [`SysWeaver.MicroService.Avatar.csproj`](../SysWeaver.MicroService.Avatar.csproj) (tools in [`_tools`](../../_tools/README.md)):

1. `SvgOpt.exe -Compare .\WebCompare -RemoveSize` optimises `srcData/*.svg` into a temporary `srcData/.opt/` folder (deleted first).
2. `Compress.exe -Methods br` writes Brotli compressed copies to the generated `data/` folder.
3. All `data/**/*.br` files are embedded as resources.

[`RemoveDuplicates.bat`](../RemoveDuplicates.bat) runs the `OnDuplicates` tool on this folder and deletes files with identical content; it is a manual maintenance script, not part of the build.

## Contents

| Item | Description |
|---|---|
| `*-svgrepo-com.svg` (648 files) | Multi-coloured icons, all named after their [SVG Repo](https://www.svgrepo.com) download, e.g. `airplane-svgrepo-com.svg`, `badger-svgrepo-com.svg`, `tiger-face-svgrepo-com.svg`, `zipper-mouth-face-svgrepo-com.svg` |

About 155 of the names are emoji-style faces (`*-face-*`); about 145 carry a ` (1)` / ` (2)` suffix, which are further drawings of the same subject downloaded under the same name (no two files have identical content).

## Limitations and considerations

- The source and licence of each icon are not recorded beyond the file name; check the SVG Repo licence of an icon before reusing it elsewhere.
- File names contain spaces and parentheses; the resource names are only used for loading, not for selection.
- Adding or removing files changes which icon a given user gets, since the icon is picked by index from the loaded list.

## Relationships

- **Part of:** [SysWeaver.MicroService.Avatar](../README.md)
- **Processed with:** [SysWeaver.Media.Svg](../../SysWeaver.Media.Svg/README.md) (`SvgCanvas`)
