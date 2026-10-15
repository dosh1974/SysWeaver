# linux_arm64

[⬆ runtimes](../README.md)

> Gzip compressed native FFmpeg libraries for Linux arm64 (glibc 2.28 or later).

| | |
|---|---|
| **Project** | [SysWeaver.Media.Video](../../README.md) |
| **Kind** | Native libraries |

## Purpose

The seven FFmpeg shared libraries loaded by [`Ffmpeg`](../../Ffmpeg.cs) on Linux arm64 (glibc 2.28 or later). The build extracts them (see [`runtimes/`](../README.md)) to `runtimes/linux_arm64/` in the output, without the `.gz` extension. Files are named by their SONAME (`libavcodec.so.63`), as the dynamic loader expects.

## Contents

| Item | Description |
|---|---|
| [`libavcodec.so.63.gz`](libavcodec.so.63.gz) | Audio and video codecs (libavcodec), 39 MB compressed |
| [`libavdevice.so.63.gz`](libavdevice.so.63.gz) | Input / output device handling (libavdevice), 2.5 MB compressed |
| [`libavfilter.so.12.gz`](libavfilter.so.12.gz) | Audio and video filters (libavfilter), 14 MB compressed |
| [`libavformat.so.63.gz`](libavformat.so.63.gz) | Container formats and demuxing (libavformat), 10 MB compressed |
| [`libavutil.so.61.gz`](libavutil.so.61.gz) | Common utility functions (libavutil), 2.8 MB compressed |
| [`libswresample.so.7.gz`](libswresample.so.7.gz) | Audio resampling (libswresample), 156 KB compressed |
| [`libswscale.so.10.gz`](libswscale.so.10.gz) | Image scaling and pixel format conversion (libswscale), 688 KB compressed |

About 69 MB in total.

## Relationships

- **Part of:** [runtimes](../README.md), [SysWeaver.Media.Video](../../README.md)
