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
- The native libraries must match the major versions the `FFmpeg.AutoGen.Bindings.DynamicallyLoaded` package expects (9.0.1.1: avcodec / avformat / avdevice 63, avfilter 12, avutil 61, swscale 10, swresample 7; the shipped ones are FFmpeg n9.0.2).
  When updating either, keep them in sync: a mismatch makes the `Ffmpeg` type initializer throw, so `VideoInfo.GetMediaInfo` returns null and `Ffmpeg.MakeThumbnailPng` returns false (media-cov-17).
- File names: the bindings load `avcodec-63.dll` (Windows) and `libavcodec.so.63` (Linux, `lib<name>.so.<major>`), so the Linux files must be named by their SONAME, not the full version (`libavcodec.so.63.1.102` isn't found).
  The Linux libraries have no RPATH: their dependencies (ex: libavcodec needs libavutil.so.61) resolve because the bindings load each library's dependencies first (by full path), and the loader matches them by SONAME.
- The Linux libraries are glibc builds and need glibc 2.28 or later (Debian 10, Ubuntu 20.04, RHEL 8). Besides glibc they only use libgcc_s, which .NET needs too,
  and .NET 10 itself needs glibc 2.27: on any glibc distribution that runs .NET 10 the build output can be copied and run, nothing has to be installed.
- **Alpine (and other musl based distributions) are not supported**: glibc can't be bundled (a process has one C library, the .NET runtime already uses the system's musl),
  and no musl builds of the FFmpeg libraries are shipped. `EnvInfo.NativePath` points to `runtimes/linux_x64` (or `linux_arm64`) there too, the glibc libraries fail to load,
  so the `Ffmpeg` type initializer throws: `VideoInfo.GetMediaInfo` returns null, `Ffmpeg.MakeThumbnailPng` returns false and every other `Ffmpeg` / `VideoStreamDecoder` use throws.
  Supporting it would need musl builds of the same FFmpeg version (same major versions) in their own folder, selected when running on musl.
- `Ffmpeg.MakeThumbnailPng` uses the first frame at or after `atTime`; if the video ends before, it uses the last decoded frame (a reference to it is kept while decoding the next one).
- `VideoStreamDecoder` frees all its native objects (frames with `av_frame_free`, the packet with `av_packet_free`, codec and format contexts) on `Dispose`, and when its constructor throws.
- FFmpeg licensing obligations apply to redistributed binaries.
- Not included in the main solution file.

## Relationships

- **Project:** [`SysWeaver.Media.Video.csproj`](SysWeaver.Media.Video.csproj)
- **Builds on:** [SysWeaver.Media](../SysWeaver.Media/README.md)
