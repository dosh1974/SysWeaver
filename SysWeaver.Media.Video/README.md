# SysWeaver.Media.Video

[⬆ SysWeaver overview](../README.md)

> Video decoding and information through FFmpeg, with native FFmpeg libraries shipped per platform.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Library |

## Purpose

Extract video information and decode frames (for example for thumbnails) using FFmpeg, loaded dynamically from the framework's `runtimes/<os>_<arch>` native folder convention.

## Limitations and considerations

- Large native binaries per platform/architecture; they must match the deployment target.
- FFmpeg licensing obligations apply to redistributed binaries.
- Not included in the main solution file.

## Relationships

- **Project:** [`SysWeaver.Media.Video.csproj`](SysWeaver.Media.Video.csproj)
- **Builds on:** [SysWeaver.Media](../SysWeaver.Media/README.md)
