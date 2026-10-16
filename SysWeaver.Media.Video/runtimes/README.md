# runtimes

[⬆ SysWeaver.Media.Video](../README.md)

> Gzip compressed native FFmpeg shared libraries, one folder per supported platform.

| | |
|---|---|
| **Project** | [SysWeaver.Media.Video](../README.md) |
| **Kind** | Native libraries |

## Purpose

`FFmpeg.AutoGen.Bindings.DynamicallyLoaded` needs the native FFmpeg libraries at run time. They are kept here gzip compressed, seven libraries per platform: avcodec, avdevice, avfilter, avformat, avutil, swresample and swscale.

## How it is built

The targets in [`SysWeaver.Media.Video.csproj`](../SysWeaver.Media.Video.csproj) pick the folders of the target OS (`RuntimeIdentifier` if set, else the build OS), extract the `.gz` files incrementally into the intermediate (`obj`) folder with an inline `GunzipFiles` task, and add the results as content with `CopyToOutputDirectory=PreserveNewest`. They end up as `runtimes/<os>_<arch>/<library>` in the build and publish output, and flow to referencing projects. At run time [`Ffmpeg`](../Ffmpeg.cs) loads them from `EnvInfo.NativePath`.

## Contents

| Item | Description |
|---|---|
| [`linux_arm64/`](linux_arm64/README.md) | Linux arm64 (glibc) libraries, named by SONAME (`libavcodec.so.63.gz`, ...) |
| [`linux_x64/`](linux_x64/README.md) | Linux x64 (glibc) libraries, named by SONAME |
| [`windows_x64/`](windows_x64/README.md) | Windows x64 DLLs (`avcodec-63.dll.gz`, ...) |

## Limitations and considerations

- The major versions (avcodec / avformat / avdevice 63, avfilter 12, avutil 61, swscale 10, swresample 7) must match what the bindings package expects; update all three folders together.
- Each platform is about 70–80 MB compressed, `avcodec` alone about 40 MB.
- FFmpeg licensing obligations apply to these redistributed binaries.

## Relationships

- **Part of:** [SysWeaver.Media.Video](../README.md)
- **Used by:** [`Ffmpeg.cs`](../Ffmpeg.cs) (native library initialisation)
