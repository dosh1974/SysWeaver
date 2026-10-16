# windows_x64

[⬆ runtimes](../README.md)

> Gzip compressed native FFmpeg libraries for Windows x64.

| | |
|---|---|
| **Project** | [SysWeaver.Media.Video](../../README.md) |
| **Kind** | Native libraries |

## Purpose

The seven FFmpeg shared libraries loaded by [`Ffmpeg`](../../Ffmpeg.cs) on Windows x64. The build extracts them (see [`runtimes/`](../README.md)) to `runtimes/windows_x64/` in the output, without the `.gz` extension. Files use the usual FFmpeg Windows DLL names (`avcodec-63.dll`).

## Contents

| Item | Description |
|---|---|
| [`avcodec-63.dll.gz`](avcodec-63.dll.gz) | Audio and video codecs (libavcodec), 42 MB compressed |
| [`avdevice-63.dll.gz`](avdevice-63.dll.gz) | Input / output device handling (libavdevice), 1.8 MB compressed |
| [`avfilter-12.dll.gz`](avfilter-12.dll.gz) | Audio and video filters (libavfilter), 16 MB compressed |
| [`avformat-63.dll.gz`](avformat-63.dll.gz) | Container formats and demuxing (libavformat), 9.9 MB compressed |
| [`avutil-61.dll.gz`](avutil-61.dll.gz) | Common utility functions (libavutil), 1.2 MB compressed |
| [`swresample-7.dll.gz`](swresample-7.dll.gz) | Audio resampling (libswresample), 328 KB compressed |
| [`swscale-10.dll.gz`](swscale-10.dll.gz) | Image scaling and pixel format conversion (libswscale), 780 KB compressed |

About 72 MB in total.

## Relationships

- **Part of:** [runtimes](../README.md), [SysWeaver.Media.Video](../../README.md)
