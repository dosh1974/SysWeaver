# SysWeaver.Media.Audio

[⬆ SysWeaver overview](../README.md)

> Audio file information (duration, format) and waveform icons using NAudio.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Library |

## Purpose

Supply the audio part of `MediaInfo` in [SysWeaver.Media](../SysWeaver.Media/README.md): duration, a short format description and a rendered waveform icon, so audio files get meaningful thumbnails.

## How it fits into SysWeaver

`MediaInfo` finds `SysWeaver.Media.AudioInfo.GetMediaInfo` in this assembly by reflection; nothing needs to reference this project directly, it only has to be deployed with the application.

## Key features

- Duration and a description such as "Stereo @ 44 100 Hz"
- Optional waveform icon (`baseName + "_Icon.png"`, 8-bit palette PNG)
- An image next to the audio file (`file.wav.png`, `file.wav.jpg`, `file.jpg` or `file.png`) is used as the icon background

## Key components

- [`AudioInfo`](AudioInfo.cs) — `GetAudioInfo` / `GetMediaInfo` (the method `MediaInfo` looks for)

## Limitations and considerations

- NAudio's format support is strongest on Windows
- The whole file is decoded when an icon is requested; up to 32M samples are buffered in memory, longer files are decoded twice instead
- Exceptions (unreadable files) are not caught here (`MediaInfo` catches them)

## Relationships

- **Project:** [`SysWeaver.Media.Audio.csproj`](SysWeaver.Media.Audio.csproj)
- **Builds on:** [SysWeaver.Media](../SysWeaver.Media/README.md)
- **Third-party:** NAudio
