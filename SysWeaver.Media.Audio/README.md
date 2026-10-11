# SysWeaver.Media.Audio

[⬆ SysWeaver overview](../README.md)

> Audio file information (duration, format) using NAudio.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Library |

## Key features

- `AudioInfo.GetAudioInfo` / `GetMediaInfo` (the name `MediaInfo` looks for): duration and a description (ex: "Stereo @ 44 100 Hz"), and optionally a waveform icon (`baseName + "_Icon.png"`).
  An image next to the audio file (`file.wav.png`, `file.wav.jpg`, `file.jpg` or `file.png`) is used as the icon background.

## Limitations and considerations

- NAudio's format support is strongest on Windows.
- The whole file is decoded when an icon is requested, in blocks. Up to 32M samples (128 MB of 32-bit floats) are kept in memory; longer files are decoded twice
  (first to count the samples, then to draw the waveform) and use little memory. The waveform is accumulated per column (the cost per sample doesn't depend on the icon height), sample values outside [-1, 1] are clamped to the icon.
- Exceptions (unreadable files) are not caught (`MediaInfo` catches them).
- Not included in the main solution file.

## Relationships

- **Project:** [`SysWeaver.Media.Audio.csproj`](SysWeaver.Media.Audio.csproj)
- **Builds on:** [SysWeaver.Media](../SysWeaver.Media/README.md)
