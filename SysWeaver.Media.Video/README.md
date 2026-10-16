# SysWeaver.Media.Video

[⬆ SysWeaver overview](../README.md)

> Video decoding and information through FFmpeg, with native FFmpeg libraries shipped per platform.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Library |
| **Platform** | Windows x64, Linux x64 / arm64 (glibc) |

## Purpose

Extract video information (size, fps, duration) and decode frames for thumbnails using FFmpeg, so video files get the same media information and icons as images.

## How it fits into SysWeaver

`MediaInfo` in [SysWeaver.Media](../SysWeaver.Media/README.md) finds `SysWeaver.Media.VideoInfo.GetMediaInfo` in this assembly by reflection; the project only has to be deployed with the application. The native FFmpeg libraries are stored gzip compressed in `runtimes/<os>_<arch>/`, extracted at build time for the target OS and copied to `runtimes/<os>_<arch>/` in the output, where they are loaded from `EnvInfo.NativePath`.

## Key features

- Video information and an icon from a frame at about 3 seconds (or 25% of the duration)
- PNG thumbnails of a frame at a given time (`Ffmpeg.MakeThumbnailPng`)
- Hardware accelerated decoding with automatic selection of the best available device type (`Ffmpeg.HwEncoders`, `Ffmpeg.SetHwEncoder`)

## Key components

- [`VideoInfo`](VideoInfo.cs) — `GetMediaInfo` (the method `MediaInfo` looks for) and `GetMediaInfoAcc`
- [`Ffmpeg`](Ffmpeg.cs) — native library initialization, hardware decoder selection and thumbnails
- [`VideoStreamDecoder`](VideoStreamDecoder.cs) — internal frame decoder

## Limitations and considerations

- The native libraries must match the major versions the `FFmpeg.AutoGen.Bindings.DynamicallyLoaded` package expects (avcodec/avformat/avdevice 63, avfilter 12, avutil 61, swscale 10, swresample 7); keep them in sync when updating either. A mismatch makes the `Ffmpeg` type initializer throw, so `VideoInfo.GetMediaInfo` returns null
- Linux libraries must be named by their SONAME (`libavcodec.so.63`); they are glibc builds (glibc 2.28 or later)
- Alpine and other musl based distributions are not supported, nor is Windows arm64
- `VideoInfo` calls are serialized by a global lock
- FFmpeg licensing obligations apply to redistributed binaries

## Folder layout

| Folder | Content |
|---|---|
| [`runtimes/`](runtimes/README.md) | Gzip compressed native FFmpeg libraries per platform (Windows x64, Linux x64 / arm64) |

## Relationships

- **Project:** [`SysWeaver.Media.Video.csproj`](SysWeaver.Media.Video.csproj)
- **Builds on:** [SysWeaver.Media](../SysWeaver.Media/README.md)
- **Third-party:** FFmpeg (native), `FFmpeg.AutoGen.Bindings.DynamicallyLoaded`
